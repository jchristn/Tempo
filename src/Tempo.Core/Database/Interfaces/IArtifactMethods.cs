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
        Task<ArtifactRecord> CreateAsync(ArtifactRecord record, CancellationToken token = default);
        Task<ArtifactRecord> UpdateAsync(ArtifactRecord record, CancellationToken token = default);
        Task<ArtifactRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);
        Task<ArtifactRecord?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default);
        Task<EnumerationResult<ArtifactRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);
        Task<List<ArtifactRecord>> AllAsync(string tenantId, CancellationToken token = default);
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
