namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;

    /// <summary>
    /// Role-permission mapping data access methods.
    /// </summary>
    public interface IRolePermissionMapMethods
    {
        /// <summary>Create a role-permission mapping.</summary>
        Task<RolePermissionMap> CreateAsync(RolePermissionMap map, CancellationToken token = default);

        /// <summary>Read a mapping by identifier (tenant-scoped).</summary>
        Task<RolePermissionMap?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Enumerate mappings for a role.</summary>
        Task<List<RolePermissionMap>> EnumerateByRoleAsync(string tenantId, string roleId, CancellationToken token = default);

        /// <summary>Enumerate mappings for a permission.</summary>
        Task<List<RolePermissionMap>> EnumerateByPermissionAsync(string tenantId, string permissionId, CancellationToken token = default);

        /// <summary>Delete a mapping by identifier.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
