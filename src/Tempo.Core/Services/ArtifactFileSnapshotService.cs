namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Models;
    using Tempo.Core.Settings;

    /// <summary>Manages mutable artifact files and regenerates the executable current snapshot.</summary>
    public class ArtifactFileSnapshotService
    {
        private static readonly UTF8Encoding _StrictUtf8 = new UTF8Encoding(false, true);

        private readonly DatabaseDriverBase _Database;
        private readonly IArtifactBlobStore _BlobStore;
        private readonly ExternalExecutionSettings? _RuntimeSettings;

        /// <summary>Instantiate.</summary>
        public ArtifactFileSnapshotService(DatabaseDriverBase database, IArtifactBlobStore blobStore, ExternalExecutionSettings? runtimeSettings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _BlobStore = blobStore ?? throw new ArgumentNullException(nameof(blobStore));
            _RuntimeSettings = runtimeSettings;
        }

        /// <summary>Create a file record from request text or base64 content.</summary>
        public static ArtifactFileRecord CreateFileRecord(string tenantId, string artifactId, string path, string? content, bool isBinary, string? contentType = null)
        {
            string normalizedPath = ArtifactFilePath.Normalize(path);
            byte[] bytes;
            string storedContent = content ?? string.Empty;
            if (isBinary)
            {
                try { bytes = Convert.FromBase64String(storedContent); }
                catch (FormatException ex) { throw new ArgumentException("binary artifact file content must be base64.", nameof(content), ex); }
            }
            else
            {
                bytes = Encoding.UTF8.GetBytes(storedContent);
            }

            return new ArtifactFileRecord
            {
                TenantId = tenantId,
                ArtifactId = artifactId,
                Path = normalizedPath,
                Content = storedContent,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? ContentTypeForPath(normalizedPath) : contentType.Trim(),
                IsBinary = isBinary,
                Sha256 = Sha256Hex(bytes),
                ByteLength = bytes.LongLength
            };
        }

        /// <summary>Create a file record from decoded bytes, storing UTF-8 text where possible.</summary>
        public static ArtifactFileRecord CreateFileRecordFromBytes(string tenantId, string artifactId, string path, byte[] bytes, string? contentType = null)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            string normalizedPath = ArtifactFilePath.Normalize(path);
            bool isText = TryDecodeUtf8(bytes, out string? text);
            return new ArtifactFileRecord
            {
                TenantId = tenantId,
                ArtifactId = artifactId,
                Path = normalizedPath,
                Content = isText ? text! : Convert.ToBase64String(bytes),
                ContentType = string.IsNullOrWhiteSpace(contentType) ? ContentTypeForPath(normalizedPath) : contentType.Trim(),
                IsBinary = !isText,
                Sha256 = Sha256Hex(bytes),
                ByteLength = bytes.LongLength
            };
        }

        /// <summary>Replace all editable files from decoded entries and regenerate the current snapshot.</summary>
        public async Task<ArtifactVersionRecord> ReplaceFilesAndSnapshotAsync(string tenantId, string artifactId, IDictionary<string, byte[]> files, CancellationToken token = default)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            List<ArtifactFileRecord> records = files
                .OrderBy(f => ArtifactFilePath.Normalize(f.Key), StringComparer.Ordinal)
                .Select(f => CreateFileRecordFromBytes(tenantId, artifactId, f.Key, f.Value))
                .ToList();
            await _Database.ArtifactFiles.ReplaceAllAsync(tenantId, artifactId, records, token).ConfigureAwait(false);
            await TouchArtifactAsync(tenantId, artifactId, token).ConfigureAwait(false);
            return await SnapshotCurrentAsync(tenantId, artifactId, token).ConfigureAwait(false);
        }

        /// <summary>Import ZIP package entries into editable files, replacing the working tree, then regenerate the current snapshot.</summary>
        public async Task<ArtifactVersionRecord> ImportZipAndSnapshotAsync(string tenantId, string artifactId, byte[] packageBytes, CancellationToken token = default)
        {
            if (packageBytes == null) throw new ArgumentNullException(nameof(packageBytes));
            Dictionary<string, byte[]> files = ReadZipEntries(tenantId, artifactId, packageBytes, token);
            return await ReplaceFilesAndSnapshotAsync(tenantId, artifactId, files, token).ConfigureAwait(false);
        }

        /// <summary>Regenerate the executable current version from editable files.</summary>
        public async Task<ArtifactVersionRecord> SnapshotCurrentAsync(string tenantId, string artifactId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            List<ArtifactFileRecord> files = await _Database.ArtifactFiles.AllAsync(tenantId, artifactId, token).ConfigureAwait(false);
            if (files.Count == 0) throw new InvalidOperationException("Artifact has no editable files to snapshot.");

            ArtifactFileRecord? manifestFile = files.FirstOrDefault(f => string.Equals(f.Path, ArtifactManifestService.ManifestFileName, StringComparison.OrdinalIgnoreCase));
            if (manifestFile == null) throw new InvalidOperationException("Artifact file '" + ArtifactManifestService.ManifestFileName + "' is required.");
            if (manifestFile.IsBinary) throw new InvalidOperationException("Artifact manifest must be a UTF-8 JSON text file.");

            string manifestJson = Encoding.UTF8.GetString(DecodeFile(manifestFile));
            ArtifactManifest manifest = ArtifactManifestService.Parse(manifestJson)
                ?? throw new InvalidOperationException("Artifact manifest is required.");
            IReadOnlyList<string> manifestErrors = ArtifactManifestService.Validate(manifest);
            if (manifestErrors.Count > 0) throw new InvalidOperationException("Artifact manifest is invalid: " + string.Join("; ", manifestErrors));

            byte[] package = ZipWithFiles(files);
            string sha = Sha256Hex(package);
            ArtifactVersionRecord? existing = await _Database.ArtifactVersions.ReadByVersionAsync(tenantId, artifactId, Constants.MutableArtifactVersion, token).ConfigureAwait(false);
            string? oldSha = existing?.Sha256;

            using MemoryStream ms = new MemoryStream(package, writable: false);
            ArtifactBlobWriteResult write = await _BlobStore.PutAsync(tenantId, sha, ms, package.LongLength, token).ConfigureAwait(false);
            ArtifactVersionRecord version;
            if (existing == null)
            {
                version = await _Database.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                {
                    TenantId = tenantId,
                    ArtifactId = artifactId,
                    Version = Constants.MutableArtifactVersion,
                    Sha256 = write.Sha256,
                    ByteLength = write.ByteLength,
                    ContentType = "application/zip",
                    OriginalFileName = artifactId + "-" + Constants.MutableArtifactVersion + ".zip",
                    ManifestJson = ArtifactManifestService.Serialize(manifest),
                    StorageKey = write.StorageKey,
                    Active = true
                }, token).ConfigureAwait(false);
            }
            else
            {
                existing.Sha256 = write.Sha256;
                existing.ByteLength = write.ByteLength;
                existing.ContentType = "application/zip";
                existing.OriginalFileName = artifactId + "-" + Constants.MutableArtifactVersion + ".zip";
                existing.ManifestJson = ArtifactManifestService.Serialize(manifest);
                existing.StorageKey = write.StorageKey;
                existing.Active = true;
                existing.DeletedUtc = null;
                existing.GcEligibleUtc = null;
                version = await _Database.ArtifactVersions.UpdateAsync(existing, token).ConfigureAwait(false);
            }

            await DeleteOldSnapshotBlobIfUnreferencedAsync(tenantId, oldSha, write.Sha256, token).ConfigureAwait(false);
            return version;
        }

        /// <summary>Decode stored file content into bytes.</summary>
        public static byte[] DecodeFile(ArtifactFileRecord file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return file.IsBinary ? Convert.FromBase64String(file.Content ?? string.Empty) : Encoding.UTF8.GetBytes(file.Content ?? string.Empty);
        }

        private static Dictionary<string, byte[]> ReadZipEntries(string tenantId, string artifactId, byte[] packageBytes, CancellationToken token)
        {
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using MemoryStream ms = new MemoryStream(packageBytes, writable: false);
            using ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                string entryName = entry.FullName.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(entryName) || entryName.EndsWith("/", StringComparison.Ordinal)) continue;
                if (IsUnixSymlink(entry)) throw new InvalidOperationException("Artifact archives may not contain symlinks: " + entry.FullName);
                string path = ArtifactFilePath.Normalize(entryName);
                using Stream input = entry.Open();
                using MemoryStream content = new MemoryStream();
                input.CopyTo(content);
                files[path] = content.ToArray();
            }

            return files;
        }

        private static byte[] ZipWithFiles(List<ArtifactFileRecord> files)
        {
            using MemoryStream ms = new MemoryStream();
            using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (ArtifactFileRecord file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
                {
                    ZipArchiveEntry entry = zip.CreateEntry(file.Path);
                    using Stream output = entry.Open();
                    byte[] bytes = DecodeFile(file);
                    output.Write(bytes, 0, bytes.Length);
                }
            }

            return ms.ToArray();
        }

        private async Task TouchArtifactAsync(string tenantId, string artifactId, CancellationToken token)
        {
            ArtifactRecord? artifact = await _Database.Artifacts.ReadAsync(tenantId, artifactId, token).ConfigureAwait(false);
            if (artifact != null) await _Database.Artifacts.UpdateAsync(artifact, token).ConfigureAwait(false);
        }

        private async Task DeleteOldSnapshotBlobIfUnreferencedAsync(string tenantId, string? oldSha, string newSha, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(oldSha) || string.Equals(oldSha, newSha, StringComparison.OrdinalIgnoreCase)) return;
            List<ArtifactVersionRecord> remaining = await _Database.ArtifactVersions.FindBySha256Async(tenantId, oldSha, token).ConfigureAwait(false);
            if (remaining.Count != 0) return;
            try { await _BlobStore.DeleteAsync(tenantId, oldSha, token).ConfigureAwait(false); } catch { }
            if (_RuntimeSettings != null)
            {
                try { new ArtifactPackageCache(_BlobStore, _RuntimeSettings).DeleteCache(tenantId, oldSha); } catch { }
            }
        }

        private static bool IsUnixSymlink(ZipArchiveEntry entry)
        {
            int mode = (entry.ExternalAttributes >> 16) & 0xF000;
            return mode == 0xA000;
        }

        private static bool TryDecodeUtf8(byte[] bytes, out string? text)
        {
            if (bytes.Any(b => b == 0))
            {
                text = null;
                return false;
            }

            try
            {
                text = _StrictUtf8.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                return false;
            }
        }

        private static string ContentTypeForPath(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".json": return "application/json";
                case ".js": return "text/javascript";
                case ".py": return "text/x-python";
                case ".cs": return "text/x-csharp";
                case ".csproj": return "application/xml";
                case ".xml": return "application/xml";
                case ".md": return "text/markdown";
                case ".txt": return "text/plain";
                case ".sh": return "text/x-shellscript";
                case ".cmd":
                case ".bat": return "text/plain";
                case ".dll":
                case ".exe": return "application/octet-stream";
                default: return "text/plain";
            }
        }

        private static string Sha256Hex(byte[] data)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            char[] chars = new char[hash.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < hash.Length; i++)
            {
                chars[i * 2] = hex[hash[i] >> 4];
                chars[i * 2 + 1] = hex[hash[i] & 0xF];
            }
            return new string(chars);
        }
    }

}
