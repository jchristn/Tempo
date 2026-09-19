namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>Artifact metadata persistence methods.</summary>
    public interface IArtifactMethods
    {
        /// <summary>Creates a new artifact record.</summary>
        /// <param name="record">The artifact record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created artifact record.</returns>
        Task<ArtifactRecord> CreateAsync(ArtifactRecord record, CancellationToken token = default);
        /// <summary>Updates an existing artifact record.</summary>
        /// <param name="record">The artifact record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated artifact record.</returns>
        Task<ArtifactRecord> UpdateAsync(ArtifactRecord record, CancellationToken token = default);
        /// <summary>Reads an artifact record by its identifier.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="id">The artifact identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The matching artifact record, or null if not found.</returns>
        Task<ArtifactRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);
        /// <summary>Reads an artifact record by its name.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="name">The artifact name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The matching artifact record, or null if not found.</returns>
        Task<ArtifactRecord?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);
        /// <summary>Enumerates artifact records for a tenant using the specified filter.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="filter">The enumeration filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The enumeration result of artifact records.</returns>
        Task<EnumerationResult<ArtifactRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);
        /// <summary>Retrieves all artifact records for a tenant.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of all artifact records for the tenant.</returns>
        Task<List<ArtifactRecord>> AllAsync(string tenantId, CancellationToken token = default);
        /// <summary>Deletes an artifact record by its identifier.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="id">The artifact identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the record was deleted; otherwise false.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
