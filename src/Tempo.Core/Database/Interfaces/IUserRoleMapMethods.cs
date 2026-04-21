namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;

    /// <summary>
    /// User-role mapping data access methods.
    /// </summary>
    public interface IUserRoleMapMethods
    {
        /// <summary>Create a user-role mapping.</summary>
        Task<UserRoleMap> CreateAsync(UserRoleMap map, CancellationToken token = default);

        /// <summary>Read a mapping by identifier (tenant-scoped).</summary>
        Task<UserRoleMap?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Enumerate mappings for a user.</summary>
        Task<List<UserRoleMap>> EnumerateByUserAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>Enumerate mappings for a role.</summary>
        Task<List<UserRoleMap>> EnumerateByRoleAsync(string tenantId, string roleId, CancellationToken token = default);

        /// <summary>Delete a mapping by identifier.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
