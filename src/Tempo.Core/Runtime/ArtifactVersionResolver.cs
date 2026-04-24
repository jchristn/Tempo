namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core;
    using Tempo.Core.Database;
    using Tempo.Core.Models;

    /// <summary>Resolves artifact version labels into immutable run snapshot entries.</summary>
    public class ArtifactVersionResolver
    {
        private readonly DatabaseDriverBase _Database;

        public ArtifactVersionResolver(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public async Task<ArtifactVersionSnapshot> ResolveAsync(string tenantId, string artifactId, string? requestedVersion, FlowRunExecutionSnapshot snapshot, string? entrypoint = null, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string requested = string.IsNullOrWhiteSpace(requestedVersion) ? "latest" : requestedVersion.Trim();
            string key = FlowRunExecutionSnapshot.ArtifactKey(artifactId, requested);
            if (snapshot.ArtifactVersions.TryGetValue(key, out ArtifactVersionSnapshot? existing)) return existing;

            ArtifactVersionRecord? version;
            if (string.Equals(requested, "latest", StringComparison.OrdinalIgnoreCase))
            {
                version = await _Database.ArtifactVersions.ReadByVersionAsync(tenantId, artifactId, Constants.MutableArtifactVersion, token).ConfigureAwait(false);
                if (version != null && !version.Active) version = null;
                if (version == null)
                {
                    List<ArtifactVersionRecord> versions = await _Database.ArtifactVersions.AllAsync(tenantId, artifactId, token).ConfigureAwait(false);
                    version = versions.Where(v => v.Active).OrderByDescending(v => v.LastUpdateUtc).FirstOrDefault();
                }
            }
            else
            {
                version = await _Database.ArtifactVersions.ReadByVersionAsync(tenantId, artifactId, requested, token).ConfigureAwait(false);
                if (version != null && !version.Active) version = null;
            }

            if (version == null) throw new InvalidOperationException("Artifact version '" + requested + "' was not found for artifact '" + artifactId + "'.");

            ArtifactVersionSnapshot resolved = new ArtifactVersionSnapshot
            {
                ArtifactId = artifactId,
                RequestedVersion = requested,
                VersionId = version.Id,
                Version = version.Version,
                Sha256 = version.Sha256,
                ManifestJson = version.ManifestJson,
                ManifestEntrypoint = entrypoint
            };
            snapshot.ArtifactVersions[key] = resolved;
            return resolved;
        }
    }
}
