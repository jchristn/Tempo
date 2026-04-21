namespace Tempo.Core.Runtime
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Models;
    using Tempo.Core.Settings;

    /// <summary>Builds and reuses Artifact.Python virtual environments when dependencies are declared.</summary>
    public class PythonEnvironmentCache
    {
        private readonly ExternalExecutionSettings _Settings;
        private readonly string _CacheRoot;

        public PythonEnvironmentCache(ExternalExecutionSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _CacheRoot = Path.GetFullPath(Path.Combine(_Settings.CacheRoot, "python"));
            Directory.CreateDirectory(_CacheRoot);
        }

        public async Task<string> PrepareAsync(ArtifactRuntimePlan plan, string? pythonVersion, CancellationToken token = default)
        {
            string basePython = ResolvePythonExecutable(pythonVersion);
            string? requirements = RequirementsFile(plan);
            if (string.IsNullOrWhiteSpace(requirements)) return basePython;
            if (!_Settings.AllowPythonDependencyInstall)
                throw new InvalidOperationException("Artifact.Python dependency installation is disabled by settings.");

            string venv = VenvPath(plan.Artifact.Sha256, pythonVersion);
            string python = VenvPython(venv);
            string marker = Path.Combine(venv, ".tempo-venv-ready");
            if (File.Exists(marker) && File.Exists(python)) return python;

            Directory.CreateDirectory(Path.GetDirectoryName(venv)!);
            if (Directory.Exists(venv)) Directory.Delete(venv, recursive: true);
            await RunAsync(basePython, new[] { "-m", "venv", venv }, plan.ArtifactRoot, token).ConfigureAwait(false);
            await RunAsync(python, new[] { "-m", "pip", "install", "-r", requirements }, plan.ArtifactRoot, token).ConfigureAwait(false);
            File.WriteAllText(marker, DateTime.UtcNow.ToString("o"));
            return python;
        }

        public void DeleteCache(string sha256)
        {
            string path = Path.GetFullPath(Path.Combine(_CacheRoot, SafeSegment(sha256)));
            TryDelete(path);
        }

        private string ResolvePythonExecutable(string? pythonVersion)
        {
            if (string.IsNullOrWhiteSpace(pythonVersion)) return _Settings.PythonExecutable;
            string trimmed = pythonVersion.Trim();
            if (trimmed.Contains(Path.DirectorySeparatorChar) || trimmed.Contains(Path.AltDirectorySeparatorChar) || trimmed.Contains(":"))
                throw new InvalidOperationException("pythonVersion must be an executable name/version, not a path.");
            if (trimmed.StartsWith("python", StringComparison.OrdinalIgnoreCase)) return trimmed;
            return "python" + trimmed;
        }

        private string VenvPath(string sha256, string? pythonVersion)
        {
            string path = Path.GetFullPath(Path.Combine(_CacheRoot, SafeSegment(sha256), SafeSegment(string.IsNullOrWhiteSpace(pythonVersion) ? _Settings.PythonExecutable : pythonVersion!)));
            string prefix = _CacheRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? _CacheRoot : _CacheRoot + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Python cache path escaped cache root.");
            return path;
        }

        private static string VenvPython(string venv)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(venv, "Scripts", "python.exe")
                : Path.Combine(venv, "bin", "python");
        }

        private static string? RequirementsFile(ArtifactRuntimePlan plan)
        {
            string? name = RuntimeSetting(plan.Entrypoint.RuntimeSettings, "requirementsFile") ?? RuntimeSetting(plan.Manifest.RuntimeSettings, "requirementsFile");
            if (string.IsNullOrWhiteSpace(name)) name = "requirements.txt";
            if (ArtifactManifestService.IsUnsafePathReference(name)) throw new InvalidOperationException("requirementsFile must be a relative artifact path.");
            string path = Path.GetFullPath(Path.Combine(plan.ArtifactRoot, name.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = plan.ArtifactRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? plan.ArtifactRoot : plan.ArtifactRoot + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("requirementsFile escaped artifact root.");
            return File.Exists(path) ? path : null;
        }

        private static string? RuntimeSetting(System.Collections.Generic.Dictionary<string, JsonElement> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out JsonElement element)) return null;
            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        }

        private async Task RunAsync(string fileName, string[] args, string workingDirectory, CancellationToken token)
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string arg in args) process.StartInfo.ArgumentList.Add(arg);
            if (!process.Start()) throw new InvalidOperationException("Could not start Python environment command.");
            string stderr = await process.StandardError.ReadToEndAsync(token).ConfigureAwait(false);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException("Python environment command failed: " + stderr.Trim());
        }

        private static string SafeSegment(string value)
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
    }
}
