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

    /// <summary>Driver-agnostic implementation of <see cref="IRoleMethods"/>.</summary>
    public class RoleMethods : IRoleMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public RoleMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<Role> CreateAsync(Role r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrWhiteSpace(r.Id)) r.Id = IdGenerator.GenerateRoleId();
            r.CreatedUtc = DateTime.UtcNow;
            r.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO roles(id, tenant_id, name, description, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(r.Id) + ", " + _D.Quote(r.TenantId) + ", " + _D.Quote(r.Name) + ", " + _D.Quote(r.Description) + ", " +
                _D.Bit(r.Active) + ", " + _D.Bit(r.IsProtected) + ", " + _D.Quote(r.CreatedUtc) + ", " + _D.Quote(r.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<Role> UpdateAsync(Role r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            r.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE roles SET name = " + _D.Quote(r.Name) + ", description = " + _D.Quote(r.Description) + ", " +
                "active = " + _D.Bit(r.Active) + ", is_protected = " + _D.Bit(r.IsProtected) + ", last_update_utc = " + _D.Quote(r.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(r.TenantId) + " AND id = " + _D.Quote(r.Id) + ";",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<Role?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM roles WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<Role>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM roles WHERE tenant_id = " + _D.Quote(tenantId) + active + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM roles WHERE tenant_id = " + _D.Quote(tenantId) + active + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<Role> r = new EnumerationResult<Role> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<Role>> AllAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM roles WHERE tenant_id = " + _D.Quote(tenantId) + " ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<Role> list = new List<Role>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string tq = _D.Quote(tenantId);
            string rq = _D.Quote(id);
            List<string> batch = new List<string>
            {
                "DELETE FROM user_role_maps WHERE tenant_id = " + tq + " AND role_id = " + rq + ";",
                "DELETE FROM role_permission_maps WHERE tenant_id = " + tq + " AND role_id = " + rq + ";",
                "DELETE FROM roles WHERE tenant_id = " + tq + " AND id = " + rq + ";"
            };
            await _Driver.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            return true;
        }

        private static Role Map(DataRow row)
        {
            return new Role
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
