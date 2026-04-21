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
        Task<ArtifactVersionRecord> CreateAsync(ArtifactVersionRecord record, CancellationToken token = default);
        Task<ArtifactVersionRecord> UpdateAsync(ArtifactVersionRecord record, CancellationToken token = default);
        Task<ArtifactVersionRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);
        Task<ArtifactVersionRecord?> ReadByVersionAsync(string tenantId, string artifactId, string version, CancellationToken token = default);
        Task<EnumerationResult<ArtifactVersionRecord>> EnumerateAsync(string tenantId, string artifactId, EnumerationFilter filter, CancellationToken token = default);
        Task<List<ArtifactVersionRecord>> AllAsync(string tenantId, string artifactId, CancellationToken token = default);
        Task<List<ArtifactVersionRecord>> FindBySha256Async(string tenantId, string sha256, CancellationToken token = default);
        Task<List<ArtifactVersionRecord>> GcEligibleAsync(DateTime utcNow, int maxResults = 100, CancellationToken token = default);
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
