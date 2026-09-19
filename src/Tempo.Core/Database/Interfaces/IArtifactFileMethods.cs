namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;

    /// <summary>Editable artifact file persistence methods.</summary>
    public interface IArtifactFileMethods
    {
        /// <summary>
        /// Inserts a new artifact file record or updates the existing one.
        /// </summary>
        /// <param name="record">The artifact file record to persist.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>The persisted artifact file record.</returns>
        Task<ArtifactFileRecord> UpsertAsync(ArtifactFileRecord record, CancellationToken token = default);

        /// <summary>
        /// Reads a single artifact file record by tenant, artifact, and path.
        /// </summary>
        /// <param name="tenantId">Identifier of the owning tenant.</param>
        /// <param name="artifactId">Identifier of the artifact.</param>
        /// <param name="path">Relative path of the file within the artifact.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>The matching artifact file record, or null when none exists.</returns>
        Task<ArtifactFileRecord?> ReadAsync(string tenantId, string artifactId, string path, CancellationToken token = default);

        /// <summary>
        /// Retrieves all file records belonging to an artifact.
        /// </summary>
        /// <param name="tenantId">Identifier of the owning tenant.</param>
        /// <param name="artifactId">Identifier of the artifact.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>The list of artifact file records; empty when none exist.</returns>
        Task<List<ArtifactFileRecord>> AllAsync(string tenantId, string artifactId, CancellationToken token = default);

        /// <summary>
        /// Deletes a single artifact file record identified by tenant, artifact, and path.
        /// </summary>
        /// <param name="tenantId">Identifier of the owning tenant.</param>
        /// <param name="artifactId">Identifier of the artifact.</param>
        /// <param name="path">Relative path of the file within the artifact.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>True when a record was deleted; otherwise false.</returns>
        Task<bool> DeleteAsync(string tenantId, string artifactId, string path, CancellationToken token = default);

        /// <summary>
        /// Deletes all file records belonging to an artifact.
        /// </summary>
        /// <param name="tenantId">Identifier of the owning tenant.</param>
        /// <param name="artifactId">Identifier of the artifact.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>True when at least one record was deleted; otherwise false.</returns>
        Task<bool> DeleteByArtifactAsync(string tenantId, string artifactId, CancellationToken token = default);

        /// <summary>
        /// Replaces all file records for an artifact with the supplied set.
        /// </summary>
        /// <param name="tenantId">Identifier of the owning tenant.</param>
        /// <param name="artifactId">Identifier of the artifact.</param>
        /// <param name="files">The complete set of file records to store for the artifact.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>A task that completes when the replacement finishes.</returns>
        Task ReplaceAllAsync(string tenantId, string artifactId, IEnumerable<ArtifactFileRecord> files, CancellationToken token = default);
    }
}
