namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Tenant data access methods.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>Create a tenant.</summary>
        Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>Update a tenant.</summary>
        Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>Read a tenant by identifier.</summary>
        Task<Tenant?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate tenants.</summary>
        Task<EnumerationResult<Tenant>> EnumerateAsync(EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all tenants.</summary>
        Task<List<Tenant>> AllAsync(CancellationToken token = default);

        /// <summary>Delete a tenant. Cascades to its users, credentials, flows, etc.</summary>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);
    }
}
