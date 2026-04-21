namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>Driver-agnostic implementation of <see cref="IPermissionMethods"/>.</summary>
    public class PermissionMethods : IPermissionMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public PermissionMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<Permission> CreateAsync(Permission p, CancellationToken token = default)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (string.IsNullOrWhiteSpace(p.Id)) p.Id = IdGenerator.GeneratePermissionId();
            p.CreatedUtc = DateTime.UtcNow;
            p.LastUpdateUtc = DateTime.UtcNow;
            string resources = Converters.JsonSerialize(p.ResourceTypes.Select(x => x.ToString()).ToList());
            string operations = Converters.JsonSerialize(p.OperationTypes.Select(x => x.ToString()).ToList());
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO permissions(id, tenant_id, name, resource_types, operation_types, permission_type, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(p.Id) + ", " + _D.Quote(p.TenantId) + ", " + _D.Quote(p.Name) + ", " +
                _D.Quote(resources) + ", " + _D.Quote(operations) + ", " + _D.Quote(p.PermissionType.ToString()) + ", " +
                _D.Bit(p.Active) + ", " + _D.Bit(p.IsProtected) + ", " + _D.Quote(p.CreatedUtc) + ", " + _D.Quote(p.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return p;
        }

        /// <inheritdoc/>
        public async Task<Permission> UpdateAsync(Permission p, CancellationToken token = default)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            p.LastUpdateUtc = DateTime.UtcNow;
            string resources = Converters.JsonSerialize(p.ResourceTypes.Select(x => x.ToString()).ToList());
            string operations = Converters.JsonSerialize(p.OperationTypes.Select(x => x.ToString()).ToList());
            await _Driver.ExecuteQueryAsync(
                "UPDATE permissions SET name = " + _D.Quote(p.Name) + ", resource_types = " + _D.Quote(resources) + ", " +
                "operation_types = " + _D.Quote(operations) + ", permission_type = " + _D.Quote(p.PermissionType.ToString()) + ", " +
                "active = " + _D.Bit(p.Active) + ", is_protected = " + _D.Bit(p.IsProtected) + ", last_update_utc = " + _D.Quote(p.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(p.TenantId) + " AND id = " + _D.Quote(p.Id) + ";",
                false, token).ConfigureAwait(false);
            return p;
        }

        /// <inheritdoc/>
        public async Task<Permission?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM permissions WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<Permission>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM permissions WHERE tenant_id = " + _D.Quote(tenantId) + active + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM permissions WHERE tenant_id = " + _D.Quote(tenantId) + active + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<Permission> r = new EnumerationResult<Permission> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<Permission>> AllAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM permissions WHERE tenant_id = " + _D.Quote(tenantId) + " ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<Permission> list = new List<Permission>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string tq = _D.Quote(tenantId);
            string pq = _D.Quote(id);
            List<string> batch = new List<string>
            {
                "DELETE FROM role_permission_maps WHERE tenant_id = " + tq + " AND permission_id = " + pq + ";",
                "DELETE FROM permissions WHERE tenant_id = " + tq + " AND id = " + pq + ";"
            };
            await _Driver.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public async Task<List<Permission>> ResolveForUserAsync(string tenantId, string userId, ResourceTypeEnum resource, OperationTypeEnum operation, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            string sql =
                "SELECT p.* FROM permissions p " +
                "INNER JOIN role_permission_maps rpm ON p.id = rpm.permission_id AND rpm.active = " + _D.BoolLiteral(true) + " " +
                "INNER JOIN roles r ON rpm.role_id = r.id AND r.active = " + _D.BoolLiteral(true) + " " +
                "INNER JOIN user_role_maps urm ON r.id = urm.role_id AND urm.active = " + _D.BoolLiteral(true) + " " +
                "WHERE p.tenant_id = " + _D.Quote(tenantId) + " AND urm.user_id = " + _D.Quote(userId) + " AND p.active = " + _D.BoolLiteral(true) + ";";
            DataTable dt = await _Driver.ExecuteQueryAsync(sql, false, token).ConfigureAwait(false);
            List<Permission> all = new List<Permission>();
            foreach (DataRow row in dt.Rows) all.Add(Map(row));
            List<Permission> matching = new List<Permission>();
            foreach (Permission p in all)
            {
                bool resourceMatch = p.ResourceTypes.Contains(ResourceTypeEnum.All) || p.ResourceTypes.Contains(resource);
                bool operationMatch = p.OperationTypes.Contains(OperationTypeEnum.All) || p.OperationTypes.Contains(operation);
                if (resourceMatch && operationMatch) matching.Add(p);
            }
            return matching;
        }

        private static Permission Map(DataRow row)
        {
            return new Permission
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                Name = Converters.String(row, "name"),
                ResourceTypes = Converters.ResourceTypes(row, "resource_types"),
                OperationTypes = Converters.OperationTypes(row, "operation_types"),
                PermissionType = Converters.EnumValue<PermissionTypeEnum>(row, "permission_type", PermissionTypeEnum.Permit),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
