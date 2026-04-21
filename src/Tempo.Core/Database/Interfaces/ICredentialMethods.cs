namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Credential data access methods.
    /// </summary>
    public interface ICredentialMethods
    {
        /// <summary>Create a credential.</summary>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>Update a credential.</summary>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>Read a credential by identifier (tenant-scoped).</summary>
        Task<Credential?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Read a credential by access key.</summary>
        Task<Credential?> ReadByAccessKeyAsync(string accessKey, CancellationToken token = default);

        /// <summary>Enumerate credentials within a tenant.</summary>
        Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all credentials in a tenant.</summary>
        Task<List<Credential>> AllAsync(string tenantId, CancellationToken token = default);

        /// <summary>Delete a credential.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
