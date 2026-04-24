namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Responses;
    using Tempo.Core.Settings;

    /// <summary>
    /// Shared file-backed service for writing, indexing, reading, and deleting per-run logs.
    /// </summary>
    public sealed class RunLogService
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _ManifestLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly RunLogSettings _Settings;

        /// <summary>Instantiate.</summary>
        public RunLogService(RunLogSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>Whether run-log capture is enabled.</summary>
        public bool Enabled => _Settings.Enabled;

        /// <summary>Absolute root path for run logs.</summary>
        public string RootPath => Path.GetFullPath(_Settings.RootPath);

        /// <summary>Create a new session object for one run assignment attempt.</summary>
        public async Task<RunLogSession?> CreateSessionAsync(RunLogSessionContext context, CancellationToken token = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!Enabled) return null;

            RunLogSession session = new RunLogSession(this, context);
            await session.InitializeAsync(token).ConfigureAwait(false);
            return session;
        }

        /// <summary>List files within one run-log directory.</summary>
        public async Task<List<RunLogFileSummaryResponse>> ListFilesAsync(string flowRunId, bool activeRun, CancellationToken token = default)
        {
            string runRoot = ResolveRunRoot(flowRunId);
            if (!Directory.Exists(runRoot))
            {
                return new List<RunLogFileSummaryResponse>();
            }

            RunLogManifest manifest = await LoadManifestAsync(runRoot, token).ConfigureAwait(false);
            int currentAttempt = manifest.Attempts.Count > 0 ? manifest.Attempts.Max(x => x.AttemptNumber) : 0;
            Dictionary<string, RunLogManifestFile> index = manifest.Files.ToDictionary(x => x.Path, x => x, StringComparer.OrdinalIgnoreCase);
            List<RunLogFileSummaryResponse> results = new List<RunLogFileSummaryResponse>();

            foreach (string file in Directory.EnumerateFiles(runRoot, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFileName(file), RunLogManifest.ManifestFileName, StringComparison.OrdinalIgnoreCase)) continue;

                RunLogFileSummaryResponse summary = BuildFileSummary(flowRunId, runRoot, file, activeRun, currentAttempt, index);
                results.Add(summary);
            }

            return results
                .OrderBy(file => file.AttemptNumber ?? 0)
                .ThenBy(file => file.StepRunId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Read a bounded tail from one run-log file.</summary>
        public async Task<RunLogFileReadResponse> ReadAsync(string flowRunId, string relativePath, bool activeRun, int? tailLines, long? maxBytes, CancellationToken token = default)
        {
            string runRoot = ResolveRunRoot(flowRunId);
            string filePath = ResolveRelativeFilePath(runRoot, relativePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Run log file not found.", relativePath);

            List<RunLogFileSummaryResponse> files = await ListFilesAsync(flowRunId, activeRun, token).ConfigureAwait(false);
            RunLogFileSummaryResponse summary = files.FirstOrDefault(file => string.Equals(file.Path, NormalizeRelativePath(relativePath), StringComparison.OrdinalIgnoreCase))
                ?? BuildFileSummary(flowRunId, runRoot, filePath, activeRun, 0, new Dictionary<string, RunLogManifestFile>(StringComparer.OrdinalIgnoreCase));

            int effectiveTailLines = Math.Clamp(
                tailLines ?? _Settings.DefaultTailLines,
                1,
                Math.Max(1, _Settings.MaxTailLines));
            long effectiveMaxBytes = Math.Clamp(
                maxBytes ?? _Settings.DefaultMaxBytes,
                1,
                Math.Max(1L, _Settings.MaxReadBytes));

            (string content, bool truncated, long returnedByteLength, long byteLength) =
                await ReadTailAsync(filePath, effectiveTailLines, effectiveMaxBytes, token).ConfigureAwait(false);

            return new RunLogFileReadResponse
            {
                FlowRunId = summary.FlowRunId,
                Path = summary.Path,
                FileName = summary.FileName,
                Kind = summary.Kind,
                AttemptNumber = summary.AttemptNumber,
                RunAssignmentId = summary.RunAssignmentId,
                WorkerId = summary.WorkerId,
                StepId = summary.StepId,
                StepRunId = summary.StepRunId,
                ByteLength = byteLength,
                LastModifiedUtc = summary.LastModifiedUtc,
                Active = summary.Active,
                DeleteAllowed = summary.DeleteAllowed,
                DownloadAllowed = summary.DownloadAllowed,
                DeleteMode = summary.DeleteMode,
                Content = content,
                Truncated = truncated,
                TailLines = effectiveTailLines,
                MaxBytes = effectiveMaxBytes,
                ReturnedByteLength = returnedByteLength
            };
        }

        /// <summary>Download one complete run-log file.</summary>
        public async Task<(byte[] Bytes, string ContentType, string DownloadFileName)> DownloadAsync(string flowRunId, string relativePath, CancellationToken token = default)
        {
            string runRoot = ResolveRunRoot(flowRunId);
            string filePath = ResolveRelativeFilePath(runRoot, relativePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Run log file not found.", relativePath);

            using FileStream stream = OpenSharedRead(filePath);
            using MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms, 81920, token).ConfigureAwait(false);

            string safeRun = SafeSegment(flowRunId);
            string safeName = SafeSegment(Path.GetFileName(filePath));
            return (ms.ToArray(), "text/plain", safeRun + "-" + safeName);
        }

        /// <summary>Delete or truncate one run-log file.</summary>
        public async Task<RunLogDeleteResponse> DeleteFileAsync(string flowRunId, string relativePath, bool activeRun, CancellationToken token = default)
        {
            string runRoot = ResolveRunRoot(flowRunId);
            string filePath = ResolveRelativeFilePath(runRoot, relativePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Run log file not found.", relativePath);

            List<RunLogFileSummaryResponse> files = await ListFilesAsync(flowRunId, activeRun, token).ConfigureAwait(false);
            RunLogFileSummaryResponse summary = files.FirstOrDefault(file => string.Equals(file.Path, NormalizeRelativePath(relativePath), StringComparison.OrdinalIgnoreCase))
                ?? BuildFileSummary(flowRunId, runRoot, filePath, activeRun, 0, new Dictionary<string, RunLogManifestFile>(StringComparer.OrdinalIgnoreCase));

            if (!summary.DeleteAllowed)
                throw new InvalidOperationException("Cannot delete the active log file for a running run.");

            if (string.Equals(summary.DeleteMode, "Truncate", StringComparison.OrdinalIgnoreCase))
            {
                await using FileStream stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.SetLength(0);
                return new RunLogDeleteResponse
                {
                    FlowRunId = flowRunId,
                    Path = summary.Path,
                    Action = "Truncated",
                    Success = true
                };
            }

            File.Delete(filePath);
            return new RunLogDeleteResponse
            {
                FlowRunId = flowRunId,
                Path = summary.Path,
                Action = "Deleted",
                Success = true
            };
        }

        /// <summary>Delete one run directory and every file beneath it.</summary>
        public Task DeleteRunDirectoryAsync(string flowRunId, CancellationToken token = default)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(flowRunId)) return Task.CompletedTask;

            string runRoot = ResolveRunRoot(flowRunId);
            if (Directory.Exists(runRoot))
            {
                Directory.Delete(runRoot, true);
            }

            return Task.CompletedTask;
        }

        /// <summary>Enumerate visible run ids from the filesystem.</summary>
        public IEnumerable<string> EnumerateRunIds()
        {
            string root = RootPath;
            if (!Directory.Exists(root)) yield break;

            foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                string? name = Path.GetFileName(directory);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return name;
                }
            }
        }

        /// <summary>Resolve one run directory path.</summary>
        public string ResolveRunRoot(string flowRunId)
        {
            if (string.IsNullOrWhiteSpace(flowRunId)) throw new ArgumentNullException(nameof(flowRunId));
            string root = RootPath;
            string full = Path.GetFullPath(Path.Combine(root, SafeSegment(flowRunId)));
            EnsureUnderRoot(full, root);
            return full;
        }

        internal static string SafeSegment(string value)
        {
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == '_' ||
                    c == '-';
                if (!ok) chars[i] = '_';
            }

            return new string(chars);
        }

        internal static string SafeStepLabel(string? value)
        {
            string safe = SafeSegment(string.IsNullOrWhiteSpace(value) ? "step" : value!);
            return string.IsNullOrWhiteSpace(safe) ? "step" : safe;
        }

        internal static string NormalizeRelativePath(string relativePath)
        {
            return Uri.UnescapeDataString(relativePath ?? string.Empty).Replace('\\', '/');
        }

        internal string ResolveAttemptDirectoryPath(RunLogSessionContext context)
        {
            string runRoot = ResolveRunRoot(context.FlowRunId);
            string attemptName = "attempt-" + context.AttemptNumber.ToString("D3") + "-" + SafeSegment(context.RunAssignmentId ?? "local");
            string full = Path.GetFullPath(Path.Combine(runRoot, attemptName));
            EnsureUnderRoot(full, runRoot);
            return full;
        }

        internal async Task RegisterAttemptAsync(RunLogSessionContext context, CancellationToken token)
        {
            if (!Enabled) return;

            string runRoot = ResolveRunRoot(context.FlowRunId);
            string attemptRoot = ResolveAttemptDirectoryPath(context);
            Directory.CreateDirectory(runRoot);
            Directory.CreateDirectory(attemptRoot);

            RunLogManifest manifest = await LoadManifestAsync(runRoot, token).ConfigureAwait(false);
            manifest.FlowRunId = context.FlowRunId;
            manifest.TenantId = context.TenantId;
            manifest.DataFlowId = context.DataFlowId;
            manifest.LastUpdatedUtc = DateTime.UtcNow;

            RunLogManifestAttempt? attempt = manifest.Attempts.FirstOrDefault(x => x.AttemptNumber == context.AttemptNumber);
            if (attempt == null)
            {
                attempt = new RunLogManifestAttempt
                {
                    AttemptNumber = context.AttemptNumber,
                    RunAssignmentId = context.RunAssignmentId,
                    WorkerId = context.WorkerId,
                    NodeKind = context.NodeKind,
                    Directory = Path.GetFileName(attemptRoot),
                    CreatedUtc = DateTime.UtcNow
                };
                manifest.Attempts.Add(attempt);
            }
            else
            {
                attempt.RunAssignmentId = context.RunAssignmentId;
                attempt.WorkerId = context.WorkerId;
                attempt.NodeKind = context.NodeKind;
                attempt.Directory = Path.GetFileName(attemptRoot);
            }

            RegisterFile(manifest, new RunLogManifestFile
            {
                Path = "run.log",
                FileName = "run.log",
                Kind = "Run",
                CreatedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });

            RegisterFile(manifest, new RunLogManifestFile
            {
                Path = RelativePath(runRoot, Path.Combine(attemptRoot, "worker.log")),
                FileName = "worker.log",
                Kind = "Worker",
                AttemptNumber = context.AttemptNumber,
                RunAssignmentId = context.RunAssignmentId,
                WorkerId = context.WorkerId,
                CreatedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });

            RegisterFile(manifest, new RunLogManifestFile
            {
                Path = RelativePath(runRoot, Path.Combine(attemptRoot, "host.log")),
                FileName = "host.log",
                Kind = "Host",
                AttemptNumber = context.AttemptNumber,
                RunAssignmentId = context.RunAssignmentId,
                WorkerId = context.WorkerId,
                CreatedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });

            await SaveManifestAsync(runRoot, manifest, token).ConfigureAwait(false);
        }

        internal async Task RegisterStepAsync(RunLogSessionContext context, RunLogStepScope stepScope, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (stepScope == null) throw new ArgumentNullException(nameof(stepScope));

            string runRoot = ResolveRunRoot(context.FlowRunId);
            RunLogManifest manifest = await LoadManifestAsync(runRoot, token).ConfigureAwait(false);
            RegisterFile(manifest, new RunLogManifestFile
            {
                Path = stepScope.RelativeLogPath,
                FileName = Path.GetFileName(stepScope.LogPath),
                Kind = "Step",
                AttemptNumber = context.AttemptNumber,
                RunAssignmentId = context.RunAssignmentId,
                WorkerId = context.WorkerId,
                StepId = stepScope.StepId,
                StepRunId = stepScope.StepRunId,
                CreatedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });
            RegisterFile(manifest, new RunLogManifestFile
            {
                Path = stepScope.RelativeStderrPath,
                FileName = Path.GetFileName(stepScope.StderrPath),
                Kind = "StepStderr",
                AttemptNumber = context.AttemptNumber,
                RunAssignmentId = context.RunAssignmentId,
                WorkerId = context.WorkerId,
                StepId = stepScope.StepId,
                StepRunId = stepScope.StepRunId,
                CreatedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await SaveManifestAsync(runRoot, manifest, token).ConfigureAwait(false);
        }

        internal Task AppendRunAsync(RunLogSessionContext context, string severity, string message, CancellationToken token = default)
        {
            return AppendLineAsync(Path.Combine(ResolveRunRoot(context.FlowRunId), "run.log"), FormatLine(severity, message), token);
        }

        internal Task AppendWorkerAsync(RunLogSessionContext context, string severity, string message, CancellationToken token = default)
        {
            return AppendLineAsync(Path.Combine(ResolveAttemptDirectoryPath(context), "worker.log"), FormatLine(severity, message), token);
        }

        internal Task AppendHostAsync(RunLogSessionContext context, string severity, string message, CancellationToken token = default)
        {
            return AppendLineAsync(Path.Combine(ResolveAttemptDirectoryPath(context), "host.log"), FormatLine(severity, message), token);
        }

        internal Task AppendStepAsync(RunLogStepScope stepScope, string severity, string message, CancellationToken token = default)
        {
            return AppendLineAsync(stepScope.LogPath, FormatLine(severity, message), token);
        }

        internal Task AppendStepStdErrAsync(RunLogStepScope stepScope, string content, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(content)) return Task.CompletedTask;
            return AppendTextAsync(stepScope.StderrPath, content, token);
        }

        internal async Task TouchFileMetadataAsync(string flowRunId, string relativePath, CancellationToken token = default)
        {
            string runRoot = ResolveRunRoot(flowRunId);
            RunLogManifest manifest = await LoadManifestAsync(runRoot, token).ConfigureAwait(false);
            RunLogManifestFile? file = manifest.Files.FirstOrDefault(x => string.Equals(x.Path, relativePath, StringComparison.OrdinalIgnoreCase));
            if (file == null) return;
            file.LastUpdatedUtc = DateTime.UtcNow;
            manifest.LastUpdatedUtc = DateTime.UtcNow;
            await SaveManifestAsync(runRoot, manifest, token).ConfigureAwait(false);
        }

        private static void RegisterFile(RunLogManifest manifest, RunLogManifestFile file)
        {
            RunLogManifestFile? existing = manifest.Files.FirstOrDefault(x => string.Equals(x.Path, file.Path, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                manifest.Files.Add(file);
            }
            else
            {
                existing.FileName = file.FileName;
                existing.Kind = file.Kind;
                existing.AttemptNumber = file.AttemptNumber;
                existing.RunAssignmentId = file.RunAssignmentId;
                existing.WorkerId = file.WorkerId;
                existing.StepId = file.StepId;
                existing.StepRunId = file.StepRunId;
                existing.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        private async Task AppendLineAsync(string filePath, string line, CancellationToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
            await using FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
            await stream.WriteAsync(bytes.AsMemory(0, bytes.Length), token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private async Task AppendTextAsync(string filePath, string content, CancellationToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            await using FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
            await stream.WriteAsync(bytes.AsMemory(0, bytes.Length), token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private async Task<RunLogManifest> LoadManifestAsync(string runRoot, CancellationToken token)
        {
            string manifestPath = Path.Combine(runRoot, RunLogManifest.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return new RunLogManifest();
            }

            using FileStream stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
            RunLogManifest? manifest = await JsonSerializer.DeserializeAsync<RunLogManifest>(stream, _Json, token).ConfigureAwait(false);
            return manifest ?? new RunLogManifest();
        }

        private async Task SaveManifestAsync(string runRoot, RunLogManifest manifest, CancellationToken token)
        {
            string manifestPath = Path.Combine(runRoot, RunLogManifest.ManifestFileName);
            SemaphoreSlim gate = _ManifestLocks.GetOrAdd(manifestPath, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(runRoot);
                manifest.LastUpdatedUtc = DateTime.UtcNow;
                await using FileStream stream = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
                await JsonSerializer.SerializeAsync(stream, manifest, _Json, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private static RunLogFileSummaryResponse BuildFileSummary(
            string flowRunId,
            string runRoot,
            string filePath,
            bool activeRun,
            int currentAttempt,
            IDictionary<string, RunLogManifestFile> index)
        {
            FileInfo info = new FileInfo(filePath);
            string relative = RelativePath(runRoot, filePath);
            index.TryGetValue(relative, out RunLogManifestFile? metadata);

            int? attemptNumber = metadata?.AttemptNumber;
            bool active = activeRun && (!attemptNumber.HasValue || attemptNumber.Value == currentAttempt);
            bool deleteAllowed = !active;

            return new RunLogFileSummaryResponse
            {
                FlowRunId = flowRunId,
                Path = relative,
                FileName = info.Name,
                Kind = metadata?.Kind ?? InferKind(info.Name),
                AttemptNumber = attemptNumber,
                RunAssignmentId = metadata?.RunAssignmentId,
                WorkerId = metadata?.WorkerId,
                StepId = metadata?.StepId,
                StepRunId = metadata?.StepRunId,
                ByteLength = info.Exists ? info.Length : 0,
                LastModifiedUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
                Active = active,
                DeleteAllowed = deleteAllowed,
                DownloadAllowed = true,
                DeleteMode = active ? "Truncate" : "Delete"
            };
        }

        private static string RelativePath(string root, string fullPath)
        {
            return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        }

        private static string InferKind(string fileName)
        {
            if (string.Equals(fileName, "run.log", StringComparison.OrdinalIgnoreCase)) return "Run";
            if (string.Equals(fileName, "worker.log", StringComparison.OrdinalIgnoreCase)) return "Worker";
            if (string.Equals(fileName, "host.log", StringComparison.OrdinalIgnoreCase)) return "Host";
            if (fileName.EndsWith(".stderr.log", StringComparison.OrdinalIgnoreCase)) return "StepStderr";
            return "Step";
        }

        private static string ResolveRelativeFilePath(string rootPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("path required.", nameof(relativePath));
            if (Path.IsPathRooted(relativePath)) throw new ArgumentException("path must be relative.", nameof(relativePath));

            string decoded = NormalizeRelativePath(relativePath);
            foreach (string segment in decoded.Split('/'))
            {
                if (segment == "..") throw new ArgumentException("path may not traverse parent directories.", nameof(relativePath));
            }

            string full = Path.GetFullPath(Path.Combine(rootPath, decoded.Replace('/', Path.DirectorySeparatorChar)));
            EnsureUnderRoot(full, rootPath);
            return full;
        }

        private static void EnsureUnderRoot(string path, string rootPath)
        {
            string normalizedRoot = Path.GetFullPath(rootPath);
            string prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;

            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Resolved path escaped the configured run-log root.");
            }
        }

        private static string FormatLine(string severity, string message)
        {
            return DateTime.UtcNow.ToString("O") + " [" + (string.IsNullOrWhiteSpace(severity) ? "Info" : severity.Trim()) + "] " + message;
        }

        private static FileStream OpenSharedRead(string filePath)
        {
            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
        }

        private static async Task<(string Content, bool Truncated, long ReturnedByteLength, long ByteLength)> ReadTailAsync(string filePath, int tailLines, long maxBytes, CancellationToken token)
        {
            using FileStream stream = OpenSharedRead(filePath);
            long fileLength = stream.Length;
            if (fileLength <= 0)
            {
                return (string.Empty, false, 0, 0);
            }

            const int chunkSize = 8192;
            long scanLimit = Math.Min(Math.Max(maxBytes * 8L, 65536L), 4L * 1024L * 1024L);
            List<byte[]> chunks = new List<byte[]>();
            long position = fileLength;
            long collected = 0;
            int newlineCount = 0;

            while (position > 0 && collected < scanLimit && newlineCount <= tailLines)
            {
                int toRead = (int)Math.Min(chunkSize, position);
                position -= toRead;
                stream.Position = position;

                byte[] chunk = new byte[toRead];
                int read = await stream.ReadAsync(chunk.AsMemory(0, toRead), token).ConfigureAwait(false);
                if (read <= 0) break;
                if (read != toRead) Array.Resize(ref chunk, read);

                chunks.Add(chunk);
                collected += read;
                newlineCount += CountNewlines(chunk);
            }

            chunks.Reverse();
            int combinedLength = chunks.Sum(chunk => chunk.Length);
            byte[] combined = new byte[combinedLength];
            int offset = 0;
            foreach (byte[] chunk in chunks)
            {
                Buffer.BlockCopy(chunk, 0, combined, offset, chunk.Length);
                offset += chunk.Length;
            }

            string text = Encoding.UTF8.GetString(combined);
            string normalized = text.Replace("\r\n", "\n");
            string[] split = normalized.Split('\n');
            bool hasTrailingNewline = normalized.EndsWith('\n');
            int logicalLineCount = split.Length;
            if (hasTrailingNewline && logicalLineCount > 0)
            {
                logicalLineCount--;
            }

            bool truncated = collected < fileLength;
            if (tailLines > 0 && logicalLineCount > tailLines)
            {
                int startIndex = Math.Max(0, logicalLineCount - tailLines);
                int takeCount = split.Length - startIndex;
                normalized = string.Join("\n", split.Skip(startIndex).Take(takeCount));
                truncated = true;
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(normalized);
            if (utf8.LongLength > maxBytes)
            {
                byte[] tail = new byte[maxBytes];
                Buffer.BlockCopy(utf8, utf8.Length - (int)maxBytes, tail, 0, (int)maxBytes);
                normalized = Encoding.UTF8.GetString(tail);
                utf8 = tail;
                truncated = true;
            }

            return (normalized, truncated, utf8.LongLength, fileLength);
        }

        private static int CountNewlines(byte[] buffer)
        {
            int count = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == (byte)'\n') count++;
            }
            return count;
        }

        private sealed class RunLogManifest
        {
            public const string ManifestFileName = "manifest.json";

            public string FlowRunId { get; set; } = string.Empty;
            public string TenantId { get; set; } = string.Empty;
            public string DataFlowId { get; set; } = string.Empty;
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
            public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
            public List<RunLogManifestAttempt> Attempts { get; set; } = new List<RunLogManifestAttempt>();
            public List<RunLogManifestFile> Files { get; set; } = new List<RunLogManifestFile>();
        }

        private sealed class RunLogManifestAttempt
        {
            public int AttemptNumber { get; set; } = 0;
            public string? RunAssignmentId { get; set; } = null;
            public string? WorkerId { get; set; } = null;
            public string? NodeKind { get; set; } = null;
            public string Directory { get; set; } = string.Empty;
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        }

        private sealed class RunLogManifestFile
        {
            public string Path { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string Kind { get; set; } = string.Empty;
            public int? AttemptNumber { get; set; } = null;
            public string? RunAssignmentId { get; set; } = null;
            public string? WorkerId { get; set; } = null;
            public string? StepId { get; set; } = null;
            public string? StepRunId { get; set; } = null;
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
            public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// One run-log session scoped to a single run assignment attempt.
    /// </summary>
    public sealed class RunLogSession
    {
        private readonly RunLogService _Service;

        internal RunLogSession(RunLogService service, RunLogSessionContext context)
        {
            _Service = service ?? throw new ArgumentNullException(nameof(service));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>Session context.</summary>
        public RunLogSessionContext Context { get; }

        /// <summary>Absolute run directory path.</summary>
        public string RunDirectoryPath => _Service.ResolveRunRoot(Context.FlowRunId);

        /// <summary>Absolute attempt directory path.</summary>
        public string AttemptDirectoryPath => _Service.ResolveAttemptDirectoryPath(Context);

        internal async Task InitializeAsync(CancellationToken token)
        {
            await _Service.RegisterAttemptAsync(Context, token).ConfigureAwait(false);
        }

        /// <summary>Create file paths for one step within the current attempt.</summary>
        public async Task<RunLogStepScope> CreateStepScopeAsync(int sequence, string stepId, string stepRunId, CancellationToken token = default)
        {
            string safeStep = RunLogService.SafeStepLabel(stepId);
            string prefix = "step-" + sequence.ToString("D3") + "-" + RunLogService.SafeSegment(stepRunId) + "-" + safeStep;
            RunLogStepScope scope = new RunLogStepScope
            {
                Sequence = sequence,
                StepId = stepId,
                StepRunId = stepRunId,
                LogPath = Path.Combine(AttemptDirectoryPath, prefix + ".log"),
                StderrPath = Path.Combine(AttemptDirectoryPath, prefix + ".stderr.log")
            };
            scope.RelativeLogPath = Path.GetRelativePath(RunDirectoryPath, scope.LogPath).Replace('\\', '/');
            scope.RelativeStderrPath = Path.GetRelativePath(RunDirectoryPath, scope.StderrPath).Replace('\\', '/');

            await _Service.RegisterStepAsync(Context, scope, token).ConfigureAwait(false);
            return scope;
        }

        /// <summary>Append a top-level run log line.</summary>
        public Task AppendRunAsync(string severity, string message, CancellationToken token = default)
        {
            return _Service.AppendRunAsync(Context, severity, message, token);
        }

        /// <summary>Append a worker/assignment log line.</summary>
        public Task AppendWorkerAsync(string severity, string message, CancellationToken token = default)
        {
            return _Service.AppendWorkerAsync(Context, severity, message, token);
        }

        /// <summary>Append a host/runtime log line.</summary>
        public Task AppendHostAsync(string severity, string message, CancellationToken token = default)
        {
            return _Service.AppendHostAsync(Context, severity, message, token);
        }

        /// <summary>Append a step log line.</summary>
        public Task AppendStepAsync(RunLogStepScope stepScope, string severity, string message, CancellationToken token = default)
        {
            return _Service.AppendStepAsync(stepScope, severity, message, token);
        }

        /// <summary>Append captured stderr text for one step.</summary>
        public Task AppendStepStdErrAsync(RunLogStepScope stepScope, string content, CancellationToken token = default)
        {
            return _Service.AppendStepStdErrAsync(stepScope, content, token);
        }
    }

    /// <summary>
    /// Session-scoped run-log metadata.
    /// </summary>
    public sealed class RunLogSessionContext
    {
        public string FlowRunId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string DataFlowId { get; set; } = string.Empty;
        public int AttemptNumber { get; set; } = 0;
        public string? RunAssignmentId { get; set; } = null;
        public string? WorkerId { get; set; } = null;
        public string? NodeKind { get; set; } = null;
    }

    /// <summary>
    /// File paths and correlation fields for one step execution.
    /// </summary>
    public sealed class RunLogStepScope
    {
        public int Sequence { get; set; } = 0;
        public string StepId { get; set; } = string.Empty;
        public string StepRunId { get; set; } = string.Empty;
        public string LogPath { get; set; } = string.Empty;
        public string RelativeLogPath { get; set; } = string.Empty;
        public string StderrPath { get; set; } = string.Empty;
        public string RelativeStderrPath { get; set; } = string.Empty;
    }
}
