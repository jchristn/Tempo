namespace Tempo.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>Artifact version metadata persistence methods.</summary>
    public interface IArtifactVersionMethods
    {
        /// <summary>Creates a new artifact version record.</summary>
        /// <param name="record">The artifact version record to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created artifact version record.</returns>
        Task<ArtifactVersionRecord> CreateAsync(ArtifactVersionRecord record, CancellationToken token = default);
        /// <summary>Updates an existing artifact version record.</summary>
        /// <param name="record">The artifact version record to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated artifact version record.</returns>
        Task<ArtifactVersionRecord> UpdateAsync(ArtifactVersionRecord record, CancellationToken token = default);
        /// <summary>Reads an artifact version record by its identifier.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="id">The artifact version identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The matching artifact version record, or null if not found.</returns>
        Task<ArtifactVersionRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);
        /// <summary>Reads an artifact version record by artifact identifier and version string.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="artifactId">The artifact identifier.</param>
        /// <param name="version">The version string.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The matching artifact version record, or null if not found.</returns>
        Task<ArtifactVersionRecord?> ReadByVersionAsync(string tenantId, string artifactId, string version, CancellationToken token = default);
        /// <summary>Enumerates artifact version records for an artifact using the specified filter.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="artifactId">The artifact identifier.</param>
        /// <param name="filter">The enumeration filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The enumeration result of artifact version records.</returns>
        Task<EnumerationResult<ArtifactVersionRecord>> EnumerateAsync(string tenantId, string artifactId, EnumerationFilter filter, CancellationToken token = default);
        /// <summary>Retrieves all artifact version records for an artifact.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="artifactId">The artifact identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of all artifact version records for the artifact.</returns>
        Task<List<ArtifactVersionRecord>> AllAsync(string tenantId, string artifactId, CancellationToken token = default);
        /// <summary>Finds artifact version records matching a SHA-256 hash.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="sha256">The SHA-256 hash to match.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of artifact version records matching the hash.</returns>
        Task<List<ArtifactVersionRecord>> FindBySha256Async(string tenantId, string sha256, CancellationToken token = default);
        /// <summary>Retrieves artifact version records eligible for garbage collection.</summary>
        /// <param name="utcNow">The current UTC time used to evaluate eligibility.</param>
        /// <param name="maxResults">The maximum number of results to return. Default: 100.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of garbage-collection-eligible artifact version records.</returns>
        Task<List<ArtifactVersionRecord>> GcEligibleAsync(DateTime utcNow, int maxResults = 100, CancellationToken token = default);
        /// <summary>Deletes an artifact version record by its identifier.</summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="id">The artifact version identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the record was deleted; otherwise false.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
