namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Permission data access methods.
    /// </summary>
    public interface IPermissionMethods
    {
        /// <summary>Create a permission.</summary>
        Task<Permission> CreateAsync(Permission permission, CancellationToken token = default);

        /// <summary>Update a permission.</summary>
        Task<Permission> UpdateAsync(Permission permission, CancellationToken token = default);

        /// <summary>Read a permission by identifier (tenant-scoped).</summary>
        Task<Permission?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Enumerate permissions within a tenant.</summary>
        Task<EnumerationResult<Permission>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all permissions in a tenant.</summary>
        Task<List<Permission>> AllAsync(string tenantId, CancellationToken token = default);

        /// <summary>Delete a permission. Cascades to role permission maps.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Resolve every active permission that applies to a user for a given resource+operation.
        /// The server evaluates the results with Deny-first semantics.
        /// </summary>
        Task<List<Permission>> ResolveForUserAsync(
            string tenantId,
            string userId,
            ResourceTypeEnum resource,
            OperationTypeEnum operation,
            CancellationToken token = default);
    }
}
