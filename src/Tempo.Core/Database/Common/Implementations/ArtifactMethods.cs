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

    /// <summary>Driver-agnostic implementation of <see cref="IArtifactMethods"/>.</summary>
    public class ArtifactMethods : IArtifactMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public ArtifactMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<ArtifactRecord> CreateAsync(ArtifactRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (string.IsNullOrWhiteSpace(record.Id)) record.Id = IdGenerator.GenerateArtifactId();
            record.CreatedUtc = DateTime.UtcNow;
            record.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO artifacts(id, tenant_id, name, description, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(record.Id) + ", " + _D.Quote(record.TenantId) + ", " + _D.Quote(record.Name) + ", " +
                _D.Quote(record.Description) + ", " + _D.Bit(record.Active) + ", " + _D.Bit(record.IsProtected) + ", " +
                _D.Quote(record.CreatedUtc) + ", " + _D.Quote(record.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc/>
        public async Task<ArtifactRecord> UpdateAsync(ArtifactRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            record.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE artifacts SET name = " + _D.Quote(record.Name) + ", description = " + _D.Quote(record.Description) + ", " +
                "active = " + _D.Bit(record.Active) + ", is_protected = " + _D.Bit(record.IsProtected) + ", " +
                "last_update_utc = " + _D.Quote(record.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(record.TenantId) + " AND id = " + _D.Quote(record.Id) + ";",
                false, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc/>
        public async Task<ArtifactRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM artifacts WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<ArtifactRecord?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM artifacts WHERE tenant_id = " + _D.Quote(tenantId) + " AND name = " + _D.Quote(name.Trim()) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<ArtifactRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM artifacts WHERE tenant_id = " + _D.Quote(tenantId) + active + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM artifacts WHERE tenant_id = " + _D.Quote(tenantId) + active + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<ArtifactRecord> result = new EnumerationResult<ArtifactRecord> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) result.Items.Add(Map(row));
            return result;
        }

        /// <inheritdoc/>
        public async Task<List<ArtifactRecord>> AllAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM artifacts WHERE tenant_id = " + _D.Quote(tenantId) + " ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<ArtifactRecord> list = new List<ArtifactRecord>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string t = _D.Quote(tenantId);
            string artifact = _D.Quote(id);
            await _Driver.ExecuteQueriesAsync(new[]
            {
                "DELETE FROM artifact_files WHERE tenant_id = " + t + " AND artifact_id = " + artifact + ";",
                "DELETE FROM artifact_versions WHERE tenant_id = " + t + " AND artifact_id = " + artifact + ";",
                "DELETE FROM artifacts WHERE tenant_id = " + t + " AND id = " + artifact + ";"
            }, true, token).ConfigureAwait(false);
            return true;
        }

        private static ArtifactRecord Map(DataRow row)
        {
            return new ArtifactRecord
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                Name = Converters.String(row, "name"),
                Description = Converters.StringOrNull(row, "description"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
