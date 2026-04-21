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

    /// <summary>Driver-agnostic implementation of <see cref="IRolePermissionMapMethods"/>.</summary>
    public class RolePermissionMapMethods : IRolePermissionMapMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public RolePermissionMapMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<RolePermissionMap> CreateAsync(RolePermissionMap m, CancellationToken token = default)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (string.IsNullOrWhiteSpace(m.Id)) m.Id = IdGenerator.GenerateRolePermissionMapId();
            m.CreatedUtc = DateTime.UtcNow;
            m.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO role_permission_maps(id, tenant_id, role_id, permission_id, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(m.Id) + ", " + _D.Quote(m.TenantId) + ", " + _D.Quote(m.RoleId) + ", " + _D.Quote(m.PermissionId) + ", " +
                _D.Bit(m.Active) + ", " + _D.Bit(m.IsProtected) + ", " + _D.Quote(m.CreatedUtc) + ", " + _D.Quote(m.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return m;
        }

        /// <inheritdoc/>
        public async Task<RolePermissionMap?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM role_permission_maps WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<List<RolePermissionMap>> EnumerateByRoleAsync(string tenantId, string roleId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(roleId)) throw new ArgumentNullException(nameof(roleId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM role_permission_maps WHERE tenant_id = " + _D.Quote(tenantId) + " AND role_id = " + _D.Quote(roleId) + ";", false, token).ConfigureAwait(false);
            List<RolePermissionMap> list = new List<RolePermissionMap>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<List<RolePermissionMap>> EnumerateByPermissionAsync(string tenantId, string permissionId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(permissionId)) throw new ArgumentNullException(nameof(permissionId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM role_permission_maps WHERE tenant_id = " + _D.Quote(tenantId) + " AND permission_id = " + _D.Quote(permissionId) + ";", false, token).ConfigureAwait(false);
            List<RolePermissionMap> list = new List<RolePermissionMap>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            await _Driver.ExecuteQueryAsync("DELETE FROM role_permission_maps WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return true;
        }

        private static RolePermissionMap Map(DataRow row)
        {
            return new RolePermissionMap
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                RoleId = Converters.String(row, "role_id"),
                PermissionId = Converters.String(row, "permission_id"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
