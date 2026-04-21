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

    /// <summary>Driver-agnostic implementation of <see cref="ITenantMethods"/>.</summary>
    public class TenantMethods : ITenantMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public TenantMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<Tenant> CreateAsync(Tenant t, CancellationToken token = default)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (string.IsNullOrWhiteSpace(t.Id)) t.Id = IdGenerator.GenerateTenantId();
            t.CreatedUtc = DateTime.UtcNow;
            t.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO tenants(id, account_id, name, region, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(t.Id) + ", " + _D.Quote(t.AccountId) + ", " + _D.Quote(t.Name) + ", " + _D.Quote(t.Region) + ", " +
                _D.Bit(t.Active) + ", " + _D.Bit(t.IsProtected) + ", " + _D.Quote(t.CreatedUtc) + ", " + _D.Quote(t.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return t;
        }

        /// <inheritdoc/>
        public async Task<Tenant> UpdateAsync(Tenant t, CancellationToken token = default)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            t.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE tenants SET account_id = " + _D.Quote(t.AccountId) + ", name = " + _D.Quote(t.Name) + ", region = " + _D.Quote(t.Region) + ", " +
                "active = " + _D.Bit(t.Active) + ", is_protected = " + _D.Bit(t.IsProtected) + ", last_update_utc = " + _D.Quote(t.LastUpdateUtc) +
                " WHERE id = " + _D.Quote(t.Id) + ";", false, token).ConfigureAwait(false);
            return t;
        }

        /// <inheritdoc/>
        public async Task<Tenant?> ReadAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM tenants WHERE id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<Tenant>> EnumerateAsync(EnumerationFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string where = filter.IncludeInactive ? "" : " WHERE active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM tenants" + where + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM tenants" + where + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<Tenant> r = new EnumerationResult<Tenant> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<Tenant>> AllAsync(CancellationToken token = default)
        {
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM tenants ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<Tenant> list = new List<Tenant>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string q = _D.Quote(id);
            List<string> batch = new List<string>
            {
                "DELETE FROM credentials WHERE tenant_id = " + q + ";",
                "DELETE FROM user_role_maps WHERE tenant_id = " + q + ";",
                "DELETE FROM role_permission_maps WHERE tenant_id = " + q + ";",
                "DELETE FROM permissions WHERE tenant_id = " + q + ";",
                "DELETE FROM roles WHERE tenant_id = " + q + ";",
                "DELETE FROM users WHERE tenant_id = " + q + ";",
                "DELETE FROM step_runs WHERE tenant_id = " + q + ";",
                "DELETE FROM flow_runs WHERE tenant_id = " + q + ";",
                "DELETE FROM artifact_files WHERE tenant_id = " + q + ";",
                "DELETE FROM artifact_versions WHERE tenant_id = " + q + ";",
                "DELETE FROM artifacts WHERE tenant_id = " + q + ";",
                "DELETE FROM triggers WHERE tenant_id = " + q + ";",
                "DELETE FROM data_flows WHERE tenant_id = " + q + ";",
                "DELETE FROM steps WHERE tenant_id = " + q + ";",
                "DELETE FROM request_history WHERE tenant_id = " + q + ";",
                "DELETE FROM tenants WHERE id = " + q + ";"
            };
            await _Driver.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            return true;
        }

        private static Tenant Map(DataRow row)
        {
            return new Tenant
            {
                Id = Converters.String(row, "id"),
                AccountId = Converters.StringOrNull(row, "account_id"),
                Name = Converters.String(row, "name"),
                Region = Converters.StringOrNull(row, "region"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
