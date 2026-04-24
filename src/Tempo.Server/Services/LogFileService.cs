namespace Tempo.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Responses;
    using Tempo.Core.Settings;

    /// <summary>
    /// Resolves server and worker log roots into a safe, file-backed admin surface.
    /// </summary>
    public class LogFileService
    {
        private readonly SettingsStore _SettingsStore;
        private readonly RunDispatchCoordinator _Coordinator;

        /// <summary>Instantiate.</summary>
        public LogFileService(SettingsStore settingsStore, RunDispatchCoordinator coordinator)
        {
            _SettingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        /// <summary>List every currently discoverable log source.</summary>
        public async Task<List<LogSourceSummaryResponse>> ListSourcesAsync(CancellationToken token = default)
        {
            Settings settings = _SettingsStore.Current;
            List<LogSourceSummaryResponse> results = new List<LogSourceSummaryResponse>
            {
                BuildServerSource(settings)
            };

            string workerRoot = Path.GetFullPath(settings.LogViewer.WorkerRootPath);
            HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, WorkerRecord> workers = (await _Coordinator.ListWorkersAsync(token).ConfigureAwait(false))
                .ToDictionary(worker => worker.Id, worker => worker, StringComparer.Ordinal);

            foreach (string workerId in workers.Keys)
            {
                sourceIds.Add(workerId);
            }

            if (Directory.Exists(workerRoot))
            {
                foreach (string directory in Directory.EnumerateDirectories(workerRoot, "*", SearchOption.TopDirectoryOnly))
                {
                    string? name = Path.GetFileName(directory);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        sourceIds.Add(name);
                    }
                }
            }

            foreach (string sourceId in sourceIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                workers.TryGetValue(sourceId, out WorkerRecord? worker);
                results.Add(BuildWorkerSource(settings, sourceId, worker));
            }

            return results;
        }

        /// <summary>List files visible within one log source.</summary>
        public async Task<List<LogFileSummaryResponse>> ListFilesAsync(string sourceKind, string sourceId, CancellationToken token = default)
        {
            ResolvedLogSource source = await ResolveSourceAsync(sourceKind, sourceId, token).ConfigureAwait(false);
            if (!Directory.Exists(source.RootPath))
            {
                return new List<LogFileSummaryResponse>();
            }

            List<LogFileSummaryResponse> files = new List<LogFileSummaryResponse>();
            foreach (string file in Directory.EnumerateFiles(source.RootPath, "*", SearchOption.TopDirectoryOnly))
            {
                files.Add(BuildFileSummary(source, file));
            }

            return files
                .OrderByDescending(file => file.IsCurrent)
                .ThenByDescending(file => file.LastModifiedUtc)
                .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Read a bounded tail of one log file.</summary>
        public async Task<LogFileReadResponse> ReadAsync(string sourceKind, string sourceId, string relativePath, int? tailLines, long? maxBytes, CancellationToken token = default)
        {
            ResolvedLogSource source = await ResolveSourceAsync(sourceKind, sourceId, token).ConfigureAwait(false);
            string filePath = ResolveRelativeFilePath(source.RootPath, relativePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Log file not found.", relativePath);

            Settings settings = _SettingsStore.Current;
            int effectiveTailLines = Math.Clamp(
                tailLines ?? settings.LogViewer.DefaultTailLines,
                1,
                Math.Max(1, settings.LogViewer.MaxTailLines));
            long effectiveMaxBytes = Math.Clamp(
                maxBytes ?? settings.LogViewer.DefaultMaxBytes,
                1,
                Math.Max(1L, settings.LogViewer.MaxReadBytes));

            (string content, bool truncated, long returnedByteLength, long byteLength) =
                await ReadTailAsync(filePath, effectiveTailLines, effectiveMaxBytes, token).ConfigureAwait(false);

            LogFileSummaryResponse summary = BuildFileSummary(source, filePath);
            return new LogFileReadResponse
            {
                SourceKind = summary.SourceKind,
                SourceId = summary.SourceId,
                Path = summary.Path,
                FileName = summary.FileName,
                ByteLength = byteLength,
                LastModifiedUtc = summary.LastModifiedUtc,
                IsCurrent = summary.IsCurrent,
                SourceActive = summary.SourceActive,
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

        /// <summary>Download one complete log file.</summary>
        public async Task<(byte[] Bytes, string ContentType, string DownloadFileName)> DownloadAsync(string sourceKind, string sourceId, string relativePath, CancellationToken token = default)
        {
            ResolvedLogSource source = await ResolveSourceAsync(sourceKind, sourceId, token).ConfigureAwait(false);
            string filePath = ResolveRelativeFilePath(source.RootPath, relativePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Log file not found.", relativePath);

            using FileStream stream = OpenSharedRead(filePath);
            using MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms, 81920, token).ConfigureAwait(false);

            string safeSource = SafeFileName(source.SourceId);
            string safeName = SafeFileName(Path.GetFileName(filePath));
            return (ms.ToArray(), "text/plain", safeSource + "-" + safeName);
        }

        /// <summary>Delete or clear one log file.</summary>
        public async Task<LogFileDeleteResponse> DeleteAsync(string sourceKind, string sourceId, string relativePath, CancellationToken token = default)
        {
            ResolvedLogSource source = await ResolveSourceAsync(sourceKind, sourceId, token).ConfigureAwait(false);
            string filePath = ResolveRelativeFilePath(source.RootPath, relativePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Log file not found.", relativePath);

            LogFileSummaryResponse summary = BuildFileSummary(source, filePath);
            if (summary.IsCurrent)
            {
                await using FileStream stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.SetLength(0);
                return new LogFileDeleteResponse
                {
                    SourceKind = source.SourceKind,
                    SourceId = source.SourceId,
                    Path = summary.Path,
                    Action = "Truncated",
                    Success = true
                };
            }

            File.Delete(filePath);
            return new LogFileDeleteResponse
            {
                SourceKind = source.SourceKind,
                SourceId = source.SourceId,
                Path = summary.Path,
                Action = "Deleted",
                Success = true
            };
        }

        private LogSourceSummaryResponse BuildServerSource(Settings settings)
        {
            string rootPath = Path.GetFullPath(settings.Logging.LogDirectory);
            return BuildSourceSummary(
                sourceKind: "server",
                sourceId: "server",
                displayName: "Tempo Server",
                rootPath: rootPath,
                state: "Online",
                enabled: true,
                active: true,
                hostName: Environment.MachineName);
        }

        private LogSourceSummaryResponse BuildWorkerSource(Settings settings, string sourceId, WorkerRecord? worker)
        {
            string rootPath = ResolveWorkerRootPath(settings, sourceId);
            string state = worker?.State ?? "Offline";
            bool enabled = worker?.Enabled ?? false;
            bool active = worker != null && !string.Equals(state, "Offline", StringComparison.OrdinalIgnoreCase);
            string displayName = !string.IsNullOrWhiteSpace(worker?.Name) ? worker!.Name : sourceId;

            return BuildSourceSummary(
                sourceKind: "worker",
                sourceId: sourceId,
                displayName: displayName,
                rootPath: rootPath,
                state: state,
                enabled: enabled,
                active: active,
                hostName: worker?.HostName);
        }

        private static LogSourceSummaryResponse BuildSourceSummary(
            string sourceKind,
            string sourceId,
            string displayName,
            string rootPath,
            string? state,
            bool enabled,
            bool active,
            string? hostName)
        {
            List<FileInfo> files = Directory.Exists(rootPath)
                ? Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly).Select(path => new FileInfo(path)).ToList()
                : new List<FileInfo>();

            DateTime? lastModifiedUtc = files.Count > 0
                ? files.Max(file => file.LastWriteTimeUtc)
                : null;

            return new LogSourceSummaryResponse
            {
                SourceKind = sourceKind,
                SourceId = sourceId,
                DisplayName = displayName,
                Available = Directory.Exists(rootPath),
                HasFiles = files.Count > 0,
                FileCount = files.Count,
                Enabled = enabled,
                Active = active,
                State = state,
                HostName = hostName,
                LastModifiedUtc = lastModifiedUtc
            };
        }

        private async Task<ResolvedLogSource> ResolveSourceAsync(string sourceKind, string sourceId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(sourceKind)) throw new ArgumentException("sourceKind required.", nameof(sourceKind));
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("sourceId required.", nameof(sourceId));

            Settings settings = _SettingsStore.Current;
            string normalizedKind = sourceKind.Trim().ToLowerInvariant();
            if (normalizedKind == "server")
            {
                if (!string.Equals(sourceId.Trim(), "server", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Unknown server log source.", nameof(sourceId));

                return new ResolvedLogSource(
                    "server",
                    "server",
                    "Tempo Server",
                    Path.GetFullPath(settings.Logging.LogDirectory),
                    settings.Logging.LogFilename,
                    isActive: true,
                    state: "Online");
            }

            if (normalizedKind == "worker")
            {
                WorkerRecord? worker = await _Coordinator.ReadWorkerAsync(sourceId.Trim(), token).ConfigureAwait(false);
                string rootPath = ResolveWorkerRootPath(settings, sourceId.Trim());
                bool directoryExists = Directory.Exists(rootPath);
                if (worker == null && !directoryExists)
                    throw new ArgumentException("Unknown worker log source.", nameof(sourceId));

                string displayName = !string.IsNullOrWhiteSpace(worker?.Name) ? worker!.Name : sourceId.Trim();
                string state = worker?.State ?? "Offline";
                bool active = worker != null && !string.Equals(state, "Offline", StringComparison.OrdinalIgnoreCase);

                return new ResolvedLogSource(
                    "worker",
                    sourceId.Trim(),
                    displayName,
                    rootPath,
                    settings.LogViewer.WorkerLogFilename,
                    active,
                    state);
            }

            throw new ArgumentException("sourceKind must be 'server' or 'worker'.", nameof(sourceKind));
        }

        private static string ResolveWorkerRootPath(Settings settings, string sourceId)
        {
            string root = Path.GetFullPath(settings.LogViewer.WorkerRootPath);
            return ResolveRelativeDirectory(root, sourceId);
        }

        private static string ResolveRelativeDirectory(string rootPath, string relativeName)
        {
            if (string.IsNullOrWhiteSpace(relativeName)) throw new ArgumentException("Relative name required.", nameof(relativeName));
            if (Path.IsPathRooted(relativeName)) throw new ArgumentException("Relative name must not be absolute.", nameof(relativeName));
            if (relativeName.Contains('\\', StringComparison.Ordinal) || relativeName.Contains('/', StringComparison.Ordinal))
                throw new ArgumentException("Relative name must not contain directory separators.", nameof(relativeName));
            if (relativeName.Contains("..", StringComparison.Ordinal))
                throw new ArgumentException("Relative name may not traverse parent directories.", nameof(relativeName));

            string full = Path.GetFullPath(Path.Combine(rootPath, relativeName));
            EnsureUnderRoot(full, rootPath);
            return full;
        }

        private static string ResolveRelativeFilePath(string rootPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("path required.", nameof(relativePath));
            if (Path.IsPathRooted(relativePath)) throw new ArgumentException("path must be relative.", nameof(relativePath));

            string decoded = Uri.UnescapeDataString(relativePath);
            string normalized = decoded.Replace('\\', '/');
            foreach (string segment in normalized.Split('/'))
            {
                if (segment == "..") throw new ArgumentException("path may not traverse parent directories.", nameof(relativePath));
            }

            string full = Path.GetFullPath(Path.Combine(rootPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
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
                throw new InvalidOperationException("Resolved path escaped the configured log root.");
            }
        }

        private static LogFileSummaryResponse BuildFileSummary(ResolvedLogSource source, string filePath)
        {
            FileInfo info = new FileInfo(filePath);
            string relative = Path.GetRelativePath(source.RootPath, info.FullName).Replace('\\', '/');
            bool isCurrent = string.Equals(relative, source.CurrentFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(info.Name, source.CurrentFileName, StringComparison.OrdinalIgnoreCase);

            return new LogFileSummaryResponse
            {
                SourceKind = source.SourceKind,
                SourceId = source.SourceId,
                Path = relative,
                FileName = info.Name,
                ByteLength = info.Exists ? info.Length : 0,
                LastModifiedUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
                IsCurrent = isCurrent,
                SourceActive = source.IsActive,
                DeleteAllowed = true,
                DownloadAllowed = true,
                DeleteMode = isCurrent ? "Truncate" : "Delete"
            };
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

        private static string SafeFileName(string value)
        {
            string name = Path.GetFileName(value);
            char[] chars = name.ToCharArray();
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

        private sealed class ResolvedLogSource
        {
            public ResolvedLogSource(string sourceKind, string sourceId, string displayName, string rootPath, string currentFileName, bool isActive, string state)
            {
                SourceKind = sourceKind;
                SourceId = sourceId;
                DisplayName = displayName;
                RootPath = rootPath;
                CurrentFileName = currentFileName;
                IsActive = isActive;
                State = state;
            }

            public string SourceKind { get; }
            public string SourceId { get; }
            public string DisplayName { get; }
            public string RootPath { get; }
            public string CurrentFileName { get; }
            public bool IsActive { get; }
            public string State { get; }
        }
    }
}
