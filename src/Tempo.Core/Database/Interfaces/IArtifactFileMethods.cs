namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;

    /// <summary>Editable artifact file persistence methods.</summary>
    public interface IArtifactFileMethods
    {
        Task<ArtifactFileRecord> UpsertAsync(ArtifactFileRecord record, CancellationToken token = default);
        Task<ArtifactFileRecord?> ReadAsync(string tenantId, string artifactId, string path, CancellationToken token = default);
        Task<List<ArtifactFileRecord>> AllAsync(string tenantId, string artifactId, CancellationToken token = default);
        Task<bool> DeleteAsync(string tenantId, string artifactId, string path, CancellationToken token = default);
        Task<bool> DeleteByArtifactAsync(string tenantId, string artifactId, CancellationToken token = default);
        Task ReplaceAllAsync(string tenantId, string artifactId, IEnumerable<ArtifactFileRecord> files, CancellationToken token = default);
    }
}
