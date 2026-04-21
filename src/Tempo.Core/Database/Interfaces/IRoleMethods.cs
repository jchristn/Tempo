namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Role data access methods.
    /// </summary>
    public interface IRoleMethods
    {
        /// <summary>Create a role.</summary>
        Task<Role> CreateAsync(Role role, CancellationToken token = default);

        /// <summary>Update a role.</summary>
        Task<Role> UpdateAsync(Role role, CancellationToken token = default);

        /// <summary>Read a role by identifier (tenant-scoped).</summary>
        Task<Role?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Enumerate roles within a tenant.</summary>
        Task<EnumerationResult<Role>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all roles in a tenant.</summary>
        Task<List<Role>> AllAsync(string tenantId, CancellationToken token = default);

        /// <summary>Delete a role. Cascades to user role maps and role permission maps.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
