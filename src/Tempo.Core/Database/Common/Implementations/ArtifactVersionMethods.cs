namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>Driver-agnostic implementation of <see cref="IArtifactVersionMethods"/>.</summary>
    public class ArtifactVersionMethods : IArtifactVersionMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public ArtifactVersionMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<ArtifactVersionRecord> CreateAsync(ArtifactVersionRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(record.Id)) record.Id = IdGenerator.GenerateArtifactVersionId();
            record.CreatedUtc = DateTime.UtcNow;
            record.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO artifact_versions(id, tenant_id, artifact_id, version, sha256, byte_length, content_type, original_file_name, manifest_json, storage_key, active, is_protected, created_utc, last_update_utc, deleted_utc, gc_eligible_utc) VALUES (" +
                _D.Quote(record.Id) + ", " + _D.Quote(record.TenantId) + ", " + _D.Quote(record.ArtifactId) + ", " +
                _D.Quote(record.Version) + ", " + _D.Quote(record.Sha256) + ", " + record.ByteLength + ", " +
                _D.Quote(record.ContentType) + ", " + _D.Quote(record.OriginalFileName) + ", " + _D.Quote(record.ManifestJson) + ", " +
                _D.Quote(record.StorageKey) + ", " + _D.Bit(record.Active) + ", " + _D.Bit(record.IsProtected) + ", " +
                _D.Quote(record.CreatedUtc) + ", " + _D.Quote(record.LastUpdateUtc) + ", " + _D.Quote(record.DeletedUtc) + ", " + _D.Quote(record.GcEligibleUtc) + ");",
                false, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc/>
        public async Task<ArtifactVersionRecord> UpdateAsync(ArtifactVersionRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            record.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE artifact_versions SET artifact_id = " + _D.Quote(record.ArtifactId) + ", version = " + _D.Quote(record.Version) + ", " +
                "sha256 = " + _D.Quote(record.Sha256) + ", byte_length = " + record.ByteLength + ", content_type = " + _D.Quote(record.ContentType) + ", " +
                "original_file_name = " + _D.Quote(record.OriginalFileName) + ", manifest_json = " + _D.Quote(record.ManifestJson) + ", " +
                "storage_key = " + _D.Quote(record.StorageKey) + ", active = " + _D.Bit(record.Active) + ", is_protected = " + _D.Bit(record.IsProtected) + ", " +
                "last_update_utc = " + _D.Quote(record.LastUpdateUtc) + ", deleted_utc = " + _D.Quote(record.DeletedUtc) + ", " +
                "gc_eligible_utc = " + _D.Quote(record.GcEligibleUtc) +
                " WHERE tenant_id = " + _D.Quote(record.TenantId) + " AND id = " + _D.Quote(record.Id) + ";",
                false, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc/>
        public async Task<ArtifactVersionRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM artifact_versions WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<ArtifactVersionRecord?> ReadByVersionAsync(string tenantId, string artifactId, string version, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifact_versions WHERE tenant_id = " + _D.Quote(tenantId) + " AND artifact_id = " + _D.Quote(artifactId) + " AND version = " + _D.Quote(version.Trim()) + ";",
                false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<ArtifactVersionRecord>> EnumerateAsync(string tenantId, string artifactId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            string scope = " WHERE tenant_id = " + _D.Quote(tenantId) + " AND artifact_id = " + _D.Quote(artifactId) + active;
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM artifact_versions" + scope + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifact_versions" + scope + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<ArtifactVersionRecord> result = new EnumerationResult<ArtifactVersionRecord> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) result.Items.Add(Map(row));
            return result;
        }

        /// <inheritdoc/>
        public async Task<List<ArtifactVersionRecord>> AllAsync(string tenantId, string artifactId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifact_versions WHERE tenant_id = " + _D.Quote(tenantId) + " AND artifact_id = " + _D.Quote(artifactId) + " ORDER BY created_utc DESC;",
                false, token).ConfigureAwait(false);
            return MapList(dt);
        }

        /// <inheritdoc/>
        public async Task<List<ArtifactVersionRecord>> FindBySha256Async(string tenantId, string sha256, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(sha256)) throw new ArgumentNullException(nameof(sha256));
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifact_versions WHERE tenant_id = " + _D.Quote(tenantId) + " AND sha256 = " + _D.Quote(sha256.Trim().ToLowerInvariant()) + " ORDER BY created_utc DESC;",
                false, token).ConfigureAwait(false);
            return MapList(dt);
        }

        /// <inheritdoc/>
        public async Task<List<ArtifactVersionRecord>> GcEligibleAsync(DateTime utcNow, int maxResults = 100, CancellationToken token = default)
        {
            if (maxResults < 1) throw new ArgumentOutOfRangeException(nameof(maxResults));
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifact_versions WHERE gc_eligible_utc IS NOT NULL AND gc_eligible_utc <= " + _D.Quote(utcNow) +
                " ORDER BY gc_eligible_utc ASC " + _D.Paging(maxResults, 0) + ";",
                false, token).ConfigureAwait(false);
            return MapList(dt);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            await _Driver.ExecuteQueryAsync("DELETE FROM artifact_versions WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return true;
        }

        private static List<ArtifactVersionRecord> MapList(DataTable table)
        {
            List<ArtifactVersionRecord> list = new List<ArtifactVersionRecord>();
            foreach (DataRow row in table.Rows) list.Add(Map(row));
            return list;
        }

        private static ArtifactVersionRecord Map(DataRow row)
        {
            return new ArtifactVersionRecord
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                ArtifactId = Converters.String(row, "artifact_id"),
                Version = Converters.String(row, "version"),
                Sha256 = Converters.String(row, "sha256"),
                ByteLength = Converters.Long(row, "byte_length"),
                ContentType = Converters.StringOrNull(row, "content_type"),
                OriginalFileName = Converters.StringOrNull(row, "original_file_name"),
                ManifestJson = Converters.StringOrNull(row, "manifest_json"),
                StorageKey = Converters.StringOrNull(row, "storage_key"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc"),
                DeletedUtc = Converters.DateTimeOrNull(row, "deleted_utc"),
                GcEligibleUtc = Converters.DateTimeOrNull(row, "gc_eligible_utc")
            };
        }
    }
}
