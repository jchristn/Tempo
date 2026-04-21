namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Models;

    /// <summary>Driver-agnostic implementation of <see cref="IArtifactFileMethods"/>.</summary>
    public class ArtifactFileMethods : IArtifactFileMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public ArtifactFileMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<ArtifactFileRecord> UpsertAsync(ArtifactFileRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            NormalizeForStorage(record);
            ArtifactFileRecord? existing = await ReadAsync(record.TenantId, record.ArtifactId, record.Path, token).ConfigureAwait(false);
            if (existing == null)
            {
                record.CreatedUtc = DateTime.UtcNow;
                record.LastUpdateUtc = record.CreatedUtc;
                await _Driver.ExecuteQueryAsync(Insert(record), false, token).ConfigureAwait(false);
                return record;
            }

            record.CreatedUtc = existing.CreatedUtc;
            record.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE artifact_files SET content = " + _D.Quote(record.Content) +
                ", content_type = " + _D.Quote(record.ContentType) +
                ", is_binary = " + _D.Bit(record.IsBinary) +
                ", sha256 = " + _D.Quote(record.Sha256) +
                ", byte_length = " + record.ByteLength +
                ", last_update_utc = " + _D.Quote(record.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(record.TenantId) +
                " AND artifact_id = " + _D.Quote(record.ArtifactId) +
                " AND path = " + _D.Quote(record.Path) + ";",
                false, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc/>
        public async Task<ArtifactFileRecord?> ReadAsync(string tenantId, string artifactId, string path, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            string normalizedPath = ArtifactFilePath.Normalize(path);
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifact_files WHERE tenant_id = " + _D.Quote(tenantId) +
                " AND artifact_id = " + _D.Quote(artifactId) +
                " AND path = " + _D.Quote(normalizedPath) + ";",
                false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<List<ArtifactFileRecord>> AllAsync(string tenantId, string artifactId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifact_files WHERE tenant_id = " + _D.Quote(tenantId) +
                " AND artifact_id = " + _D.Quote(artifactId) +
                " ORDER BY path ASC;",
                false, token).ConfigureAwait(false);
            List<ArtifactFileRecord> list = new List<ArtifactFileRecord>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string artifactId, string path, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            string normalizedPath = ArtifactFilePath.Normalize(path);
            await _Driver.ExecuteQueryAsync(
                "DELETE FROM artifact_files WHERE tenant_id = " + _D.Quote(tenantId) +
                " AND artifact_id = " + _D.Quote(artifactId) +
                " AND path = " + _D.Quote(normalizedPath) + ";",
                false, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteByArtifactAsync(string tenantId, string artifactId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            await _Driver.ExecuteQueryAsync(
                "DELETE FROM artifact_files WHERE tenant_id = " + _D.Quote(tenantId) +
                " AND artifact_id = " + _D.Quote(artifactId) + ";",
                false, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public async Task ReplaceAllAsync(string tenantId, string artifactId, IEnumerable<ArtifactFileRecord> files, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            if (files == null) throw new ArgumentNullException(nameof(files));
            DateTime now = DateTime.UtcNow;
            List<string> statements = new List<string>
            {
                "DELETE FROM artifact_files WHERE tenant_id = " + _D.Quote(tenantId) + " AND artifact_id = " + _D.Quote(artifactId) + ";"
            };

            foreach (ArtifactFileRecord file in files)
            {
                file.TenantId = tenantId;
                file.ArtifactId = artifactId;
                NormalizeForStorage(file);
                file.CreatedUtc = now;
                file.LastUpdateUtc = now;
                statements.Add(Insert(file));
            }

            await _Driver.ExecuteQueriesAsync(statements, true, token).ConfigureAwait(false);
        }

        private string Insert(ArtifactFileRecord record)
        {
            return "INSERT INTO artifact_files(tenant_id, artifact_id, path, content, content_type, is_binary, sha256, byte_length, created_utc, last_update_utc) VALUES (" +
                _D.Quote(record.TenantId) + ", " +
                _D.Quote(record.ArtifactId) + ", " +
                _D.Quote(record.Path) + ", " +
                _D.Quote(record.Content) + ", " +
                _D.Quote(record.ContentType) + ", " +
                _D.Bit(record.IsBinary) + ", " +
                _D.Quote(record.Sha256) + ", " +
                record.ByteLength + ", " +
                _D.Quote(record.CreatedUtc) + ", " +
                _D.Quote(record.LastUpdateUtc) + ");";
        }

        private static void NormalizeForStorage(ArtifactFileRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.TenantId)) throw new ArgumentNullException(nameof(record.TenantId));
            if (string.IsNullOrWhiteSpace(record.ArtifactId)) throw new ArgumentNullException(nameof(record.ArtifactId));
            record.Path = ArtifactFilePath.Normalize(record.Path);
            record.Content ??= string.Empty;
            record.ContentType = string.IsNullOrWhiteSpace(record.ContentType) ? null : record.ContentType.Trim();
            if (record.ByteLength < 0) throw new ArgumentOutOfRangeException(nameof(record.ByteLength));
            if (string.IsNullOrWhiteSpace(record.Sha256)) throw new ArgumentNullException(nameof(record.Sha256));
            string sha = record.Sha256.Trim().ToLowerInvariant();
            if (sha.Length != 64) throw new ArgumentException("Artifact file SHA-256 must be 64 hexadecimal characters.", nameof(record.Sha256));
            foreach (char c in sha)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!ok) throw new ArgumentException("Artifact file SHA-256 must be 64 hexadecimal characters.", nameof(record.Sha256));
            }
            record.Sha256 = sha;
        }

        private static ArtifactFileRecord Map(DataRow row)
        {
            return new ArtifactFileRecord
            {
                TenantId = Converters.String(row, "tenant_id"),
                ArtifactId = Converters.String(row, "artifact_id"),
                Path = Converters.String(row, "path"),
                Content = Converters.String(row, "content"),
                ContentType = Converters.StringOrNull(row, "content_type"),
                IsBinary = Converters.Bool(row, "is_binary"),
                Sha256 = Converters.String(row, "sha256"),
                ByteLength = Converters.Long(row, "byte_length"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
