namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Helpers;
    using Tempo.Core.Settings;
    using Tempo.Enums;
    using Tempo.Protocol;
    using Tempo.Runners;

    /// <summary>Executes an artifact-rooted process using JSON-over-stdin/stdout.</summary>
    public class ArtifactProcessStepRunner : StepRunner, IArtifactRuntimeDiagnostics
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        protected readonly string _TenantId;
        protected readonly string _ArtifactRoot;
        protected readonly string _ScratchRoot;
        protected readonly ExternalExecutionSettings _Settings;
        protected readonly ExternalRuntimeCapacityManager _Capacity;
        protected readonly string _Command;
        protected readonly List<string> _Arguments;
        protected readonly List<string> _EnvironmentReferences;
        protected readonly RunLogSession? _RunLogs;
        protected readonly RunLogStepScope? _RunLogStep;
        private readonly int _MaxRuntimeMs;
        private bool _UseLinuxProcessGroupKill;

        private const int SigKill = 9;

        public ArtifactProcessStepRunner(
            string tenantId,
            ArtifactVersionSnapshot artifact,
            string artifactRoot,
            string entrypoint,
            string command,
            IEnumerable<string> arguments,
            IEnumerable<string> environmentReferences,
            ExternalExecutionSettings settings,
            ExternalRuntimeCapacityManager capacity,
            RunLogSession? runLogs = null,
            RunLogStepScope? runLogStep = null,
            int maxRuntimeMs = 0)
        {
            _TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
            ArtifactId = artifact?.ArtifactId ?? throw new ArgumentNullException(nameof(artifact));
            ArtifactVersionId = artifact.VersionId;
            ArtifactVersion = artifact.Version;
            ArtifactSha256 = artifact.Sha256;
            ManifestEntrypoint = entrypoint;
            _ArtifactRoot = Path.GetFullPath(artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot)));
            _ScratchRoot = Path.GetFullPath(settings?.ScratchRoot ?? throw new ArgumentNullException(nameof(settings)));
            _Settings = settings;
            _Capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
            _Command = command ?? throw new ArgumentNullException(nameof(command));
            _Arguments = arguments?.ToList() ?? new List<string>();
            _EnvironmentReferences = environmentReferences?.ToList() ?? new List<string>();
            _RunLogs = runLogs;
            _RunLogStep = runLogStep;
            _MaxRuntimeMs = maxRuntimeMs > 0 ? maxRuntimeMs : _Settings.DefaultMaxRuntimeMs;
        }

        public string? ArtifactId { get; }
        public string? ArtifactVersionId { get; }
        public string? ArtifactVersion { get; }
        public string? ArtifactSha256 { get; }
        public string? ManifestEntrypoint { get; }
        public DateTime? CapacityQueuedUtc { get; private set; }
        public DateTime? CapacityAcquiredUtc { get; private set; }
        public long? CapacityWaitMs { get; private set; }

        protected override async Task<StepResult> ExecuteInternal(StepRequest req, CancellationToken token)
        {
            string scratch = ScratchPath(req.StepRunId);
            Directory.CreateDirectory(scratch);
            try
            {
                string requestJson = JsonSerializer.Serialize(req, _Json);
                if (Encoding.UTF8.GetByteCount(requestJson) > _Settings.MaxInputBytes)
                    return ExceptionResult(req, "Step request exceeds external execution maxInputBytes.");

                CapacityQueuedUtc = DateTime.UtcNow;
                using ExternalRuntimeCapacityLease lease = await _Capacity.AcquireAsync(_TenantId, req.StepRunId, token).ConfigureAwait(false);
                CapacityAcquiredUtc = lease.AcquiredUtc;
                CapacityWaitMs = lease.CapacityWaitMs;

                return await RunProcessAsync(req, requestJson, scratch, token).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(scratch);
            }
        }

        protected virtual ProcessStartInfo BuildStartInfo(string scratch)
        {
            string commandPath = ResolveArtifactPath(_Command);
            ProcessStartInfo psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _ArtifactRoot
            };

            AddCommand(psi, commandPath);
            foreach (string arg in _Arguments) psi.ArgumentList.Add(arg);

            psi.Environment["TEMPO_ARTIFACT_ROOT"] = _ArtifactRoot;
            psi.Environment["TEMPO_SCRATCH_DIR"] = scratch;
            foreach (string name in _EnvironmentReferences.Distinct(StringComparer.Ordinal))
            {
                string? value = Environment.GetEnvironmentVariable(name);
                if (value != null) psi.Environment[name] = value;
            }

            WrapWithLinuxProcessGroup(psi);
            return psi;
        }

        protected void WrapWithLinuxProcessGroup(ProcessStartInfo psi)
        {
            _UseLinuxProcessGroupKill = false;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;

            string? setsid = File.Exists("/usr/bin/setsid") ? "/usr/bin/setsid" :
                File.Exists("/bin/setsid") ? "/bin/setsid" : null;
            if (setsid == null) return;

            string originalFileName = psi.FileName;
            List<string> originalArguments = psi.ArgumentList.ToList();
            psi.FileName = setsid;
            psi.ArgumentList.Clear();
            psi.ArgumentList.Add(originalFileName);
            foreach (string arg in originalArguments) psi.ArgumentList.Add(arg);
            _UseLinuxProcessGroupKill = true;
        }

        private async Task<StepResult> RunProcessAsync(StepRequest req, string requestJson, string scratch, CancellationToken token)
        {
            using Process process = new Process();
            process.StartInfo = BuildStartInfo(scratch);
            process.StartInfo.Environment[ProtocolVersions.ProtocolVersionEnvironmentVariable] = req.ProtocolVersion;
            process.StartInfo.Environment[ProtocolVersions.SupportedProtocolVersionsEnvironmentVariable] = string.Join(",", ProtocolVersions.Supported);
            ApplyRunLogEnvironment(process.StartInfo, req);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(_MaxRuntimeMs);
            using CancellationTokenSource timeoutCts = new CancellationTokenSource(_MaxRuntimeMs);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            bool timedOut = false;

            try
            {
                if (!process.Start())
                {
                    await WriteHostAsync("External process failed to start", token).ConfigureAwait(false);
                    return ExceptionResult(req, "External process failed to start");
                }
                long stdoutLimit = Math.Min(_Settings.MaxStdoutBytes, _Settings.MaxOutputBytes);
                Task<string> stdoutTask = ReadLimitedAsync(process.StandardOutput, stdoutLimit, "stdout", token);
                Task<string> stderrTask = ReadLimitedAsync(process.StandardError, _Settings.MaxStderrBytes, "stderr", token);

                try
                {
                    await process.StandardInput.WriteAsync(requestJson.AsMemory(), linked.Token).ConfigureAwait(false);
                    await process.StandardInput.FlushAsync(linked.Token).ConfigureAwait(false);
                    process.StandardInput.Close();
                    await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    timedOut = true;
                    Kill(process);
                }
                catch (OperationCanceledException)
                {
                    Kill(process);
                    throw;
                }

                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = Redact(await stderrTask.ConfigureAwait(false), process.StartInfo.Environment);
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    await WriteStepStdErrAsync(stderr, token).ConfigureAwait(false);
                }
                if (timedOut)
                {
                    await WriteHostAsync("External process exceeded maximum runtime of " + _MaxRuntimeMs + "ms", token).ConfigureAwait(false);
                    return TimeoutResult(req, "External process exceeded maximum runtime of " + _MaxRuntimeMs + "ms");
                }
                if (process.ExitCode != 0)
                {
                    await WriteHostAsync("External process exited with code " + process.ExitCode + StderrSuffix(stderr), token).ConfigureAwait(false);
                    return ExceptionResult(req, "External process exited with code " + process.ExitCode + StderrSuffix(stderr));
                }
                if (string.IsNullOrWhiteSpace(stdout))
                {
                    await WriteHostAsync("External process produced empty stdout", token).ConfigureAwait(false);
                    return ExceptionResult(req, "External process produced empty stdout");
                }

                try
                {
                    StepResult? result = JsonSerializer.Deserialize<StepResult>(stdout, _Json);
                    return result ?? ExceptionResult(req, "External process stdout did not contain a StepResult");
                }
                catch (JsonException ex)
                {
                    await WriteHostAsync("External process stdout was not valid StepResult JSON: " + ex.Message + StderrSuffix(stderr), token).ConfigureAwait(false);
                    return ExceptionResult(req, "External process stdout was not valid StepResult JSON: " + ex.Message + StderrSuffix(stderr));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    await WriteHostAsync("External process exceeded maximum runtime of " + _MaxRuntimeMs + "ms", token).ConfigureAwait(false);
                    return TimeoutResult(req, "External process exceeded maximum runtime of " + _MaxRuntimeMs + "ms");
                }
                await WriteHostAsync(ex.Message, token).ConfigureAwait(false);
                return ExceptionResult(req, ex.Message);
            }
        }

        protected void AddCommand(ProcessStartInfo psi, string commandPath)
        {
            string ext = Path.GetExtension(commandPath).ToLowerInvariant();
            if (ext == ".dll")
            {
                psi.FileName = string.IsNullOrWhiteSpace(_Settings.DotnetExecutable) ? "dotnet" : _Settings.DotnetExecutable;
                psi.ArgumentList.Add(commandPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (ext == ".cmd" || ext == ".bat"))
            {
                psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                psi.ArgumentList.Add("/d");
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(commandPath);
            }
            else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && ext == ".sh")
            {
                psi.FileName = "/bin/sh";
                psi.ArgumentList.Add(commandPath);
            }
            else
            {
                psi.FileName = commandPath;
            }
        }

        protected string ResolveArtifactPath(string relativePath)
        {
            if (ArtifactManifestService.IsUnsafePathReference(relativePath))
                throw new InvalidOperationException("Artifact command must be a relative artifact path.");
            string full = Path.GetFullPath(Path.Combine(_ArtifactRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = _ArtifactRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? _ArtifactRoot : _ArtifactRoot + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Artifact command escaped artifact root.");
            if (!File.Exists(full)) throw new FileNotFoundException("Artifact command was not found.", full);
            return full;
        }

        private string ScratchPath(string? stepRunId)
        {
            string tenant = SafeSegment(_TenantId);
            string step = SafeSegment(string.IsNullOrWhiteSpace(stepRunId) ? IdGenerator.GenerateStepRunId() : stepRunId!);
            string path = Path.GetFullPath(Path.Combine(_ScratchRoot, tenant, step + "-" + IdGenerator.GenerateNonceId()));
            string prefix = _ScratchRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? _ScratchRoot : _ScratchRoot + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Scratch path escaped scratch root.");
            return path;
        }

        private async Task<string> ReadLimitedAsync(StreamReader reader, long limit, string streamName, CancellationToken token)
        {
            char[] buffer = new char[8192];
            StringBuilder sb = new StringBuilder();
            long bytes = 0;
            while (true)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                if (read == 0) break;
                bytes += Encoding.UTF8.GetByteCount(buffer, 0, read);
                if (bytes > limit) throw new InvalidOperationException("External process " + streamName + " exceeded configured byte limit.");
                sb.Append(buffer, 0, read);
            }
            return sb.ToString();
        }

        private void Kill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    if (_UseLinuxProcessGroupKill) TryKillLinuxProcessGroup(process.Id);
                    process.Kill(_Settings.KillProcessTreeOnCancel);
                    _Capacity.RecordProcessKilled();
                }
            }
            catch { }
        }

        private static void TryKillLinuxProcessGroup(int processId)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
            try { kill(-processId, SigKill); } catch { }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);

        private string Redact(string value, IDictionary<string, string?> environment)
        {
            string redacted = value ?? string.Empty;
            foreach (string name in _EnvironmentReferences)
            {
                if (!environment.TryGetValue(name, out string? secret)) continue;
                if (string.IsNullOrEmpty(secret) || secret.Length < 4) continue;
                redacted = redacted.Replace(secret, "[redacted]", StringComparison.Ordinal);
            }
            return redacted;
        }

        private static string StderrSuffix(string stderr)
        {
            return string.IsNullOrWhiteSpace(stderr) ? string.Empty : ": " + stderr.Trim();
        }

        private static StepResult ExceptionResult(StepRequest req, string message)
        {
            return new StepResult
            {
                ProtocolVersion = req.ProtocolVersion,
                TenantId = req.TenantId,
                DataFlowId = req.DataFlowId,
                FlowRunId = req.FlowRunId,
                StepRunId = req.StepRunId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Exception,
                Exception = new InvalidOperationException(message),
                Metadata = req.Metadata
            };
        }

        private static StepResult TimeoutResult(StepRequest req, string message)
        {
            return new StepResult
            {
                ProtocolVersion = req.ProtocolVersion,
                TenantId = req.TenantId,
                DataFlowId = req.DataFlowId,
                FlowRunId = req.FlowRunId,
                StepRunId = req.StepRunId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Timeout,
                Exception = new TimeoutException(message),
                Metadata = req.Metadata
            };
        }

        protected static string SafeSegment(string value)
        {
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok) chars[i] = '_';
            }
            return new string(chars);
        }

        private static void TryDelete(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
        }

        private void ApplyRunLogEnvironment(ProcessStartInfo psi, StepRequest req)
        {
            if (_RunLogStep == null) return;

            string logDir = Path.GetDirectoryName(_RunLogStep.LogPath) ?? _ArtifactRoot;
            psi.Environment["TEMPO_RUN_LOG_DIR"] = logDir;
            psi.Environment["TEMPO_RUN_LOG_FILE"] = _RunLogStep.LogPath;
            psi.Environment["TEMPO_RUN_LOG_KIND"] = "Step";
            psi.Environment["TEMPO_FLOW_RUN_ID"] = req.FlowRunId ?? string.Empty;
            psi.Environment["TEMPO_RUN_ASSIGNMENT_ID"] = _RunLogs?.Context.RunAssignmentId ?? string.Empty;
            psi.Environment["TEMPO_STEP_RUN_ID"] = req.StepRunId ?? string.Empty;
            psi.Environment["TEMPO_STEP_ID"] = _RunLogStep.StepId;
            psi.Environment["TEMPO_WORKER_ID"] = _RunLogs?.Context.WorkerId ?? string.Empty;
        }

        private Task WriteHostAsync(string message, CancellationToken token)
        {
            if (_RunLogs == null) return Task.CompletedTask;
            return _RunLogs.AppendHostAsync("Info", message, token);
        }

        private Task WriteStepStdErrAsync(string stderr, CancellationToken token)
        {
            if (_RunLogs == null || _RunLogStep == null) return Task.CompletedTask;
            return _RunLogs.AppendStepStdErrAsync(_RunLogStep, stderr, token);
        }
    }
}
