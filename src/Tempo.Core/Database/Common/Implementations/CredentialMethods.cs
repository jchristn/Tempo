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

    /// <summary>Driver-agnostic implementation of <see cref="ICredentialMethods"/>.</summary>
    public class CredentialMethods : ICredentialMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public CredentialMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<Credential> CreateAsync(Credential c, CancellationToken token = default)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (string.IsNullOrWhiteSpace(c.Id)) c.Id = IdGenerator.GenerateCredentialId();
            c.CreatedUtc = DateTime.UtcNow;
            c.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO credentials(id, tenant_id, user_id, name, access_key, secret_key, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(c.Id) + ", " + _D.Quote(c.TenantId) + ", " + _D.Quote(c.UserId) + ", " + _D.Quote(c.Name) + ", " +
                _D.Quote(c.AccessKey) + ", " + _D.Quote(c.SecretKey) + ", " +
                _D.Bit(c.Active) + ", " + _D.Bit(c.IsProtected) + ", " + _D.Quote(c.CreatedUtc) + ", " + _D.Quote(c.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return c;
        }

        /// <inheritdoc/>
        public async Task<Credential> UpdateAsync(Credential c, CancellationToken token = default)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            c.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE credentials SET name = " + _D.Quote(c.Name) + ", active = " + _D.Bit(c.Active) + ", " +
                "is_protected = " + _D.Bit(c.IsProtected) + ", last_update_utc = " + _D.Quote(c.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(c.TenantId) + " AND id = " + _D.Quote(c.Id) + ";",
                false, token).ConfigureAwait(false);
            return c;
        }

        /// <inheritdoc/>
        public async Task<Credential?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM credentials WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<Credential?> ReadByAccessKeyAsync(string accessKey, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM credentials WHERE access_key = " + _D.Quote(accessKey) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM credentials WHERE tenant_id = " + _D.Quote(tenantId) + active + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM credentials WHERE tenant_id = " + _D.Quote(tenantId) + active + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<Credential> r = new EnumerationResult<Credential> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<Credential>> AllAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM credentials WHERE tenant_id = " + _D.Quote(tenantId) + " ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<Credential> list = new List<Credential>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            await _Driver.ExecuteQueryAsync("DELETE FROM credentials WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return true;
        }

        private static Credential Map(DataRow row)
        {
            return new Credential
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                UserId = Converters.String(row, "user_id"),
                Name = Converters.String(row, "name"),
                AccessKey = Converters.String(row, "access_key"),
                SecretKey = Converters.String(row, "secret_key"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
