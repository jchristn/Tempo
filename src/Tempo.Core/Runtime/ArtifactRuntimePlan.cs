namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Models;
    using Tempo.Core.Settings;

    /// <summary>Resolved artifact, manifest, entrypoint, and extracted root for execution.</summary>
    public class ArtifactRuntimePlan
    {
        public ArtifactVersionSnapshot Artifact { get; set; } = new ArtifactVersionSnapshot();
        public ArtifactVersionRecord Version { get; set; } = new ArtifactVersionRecord();
        public ArtifactManifest Manifest { get; set; } = new ArtifactManifest();
        public string EntrypointName { get; set; } = string.Empty;
        public ArtifactManifestEntrypoint Entrypoint { get; set; } = new ArtifactManifestEntrypoint();
        public string ArtifactRoot { get; set; } = string.Empty;

        internal static async Task AddArtifactReferenceValidationErrorsAsync(
            DatabaseDriverBase? database,
            string tenantId,
            string? artifactId,
            IList<string> errors,
            CancellationToken token)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (database == null || string.IsNullOrWhiteSpace(artifactId)) return;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                errors.Add("tenantId is required for artifact validation.");
                return;
            }

            ArtifactRecord? artifact = await database.Artifacts.ReadAsync(tenantId, artifactId.Trim(), token).ConfigureAwait(false);
            if (artifact == null || !artifact.Active) errors.Add("artifactId was not found for this tenant.");
        }

        public static async Task<ArtifactRuntimePlan> ResolveAsync(
            DatabaseDriverBase database,
            IArtifactBlobStore blobStore,
            ExternalExecutionSettings settings,
            StepExecutionContext context,
            StepRecord step,
            ArtifactProcessRuntimeConfig config,
            RuntimeKey expectedRuntimeKey,
            CancellationToken token)
        {
            string artifactId = config.ArtifactId ?? step.ArtifactId ?? throw new InvalidOperationException("artifactId is required.");
            string? requestedVersion = config.ArtifactVersion ?? step.ArtifactVersion;
            ArtifactVersionResolver resolver = new ArtifactVersionResolver(database);
            ArtifactVersionSnapshot artifact = await resolver.ResolveAsync(context.TenantId, artifactId, requestedVersion, context.Snapshot, config.Entrypoint, token).ConfigureAwait(false);
            ArtifactVersionRecord version = await database.ArtifactVersions.ReadAsync(context.TenantId, artifact.VersionId, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Resolved artifact version was not found: " + artifact.VersionId);
            ArtifactManifest manifest = ArtifactManifestService.Parse(version.ManifestJson)
                ?? throw new InvalidOperationException("Artifact version '" + version.Version + "' does not have a runtime manifest.");
            IReadOnlyList<string> errors = ArtifactManifestService.Validate(manifest);
            if (errors.Count > 0) throw new InvalidOperationException("Artifact manifest is invalid: " + string.Join("; ", errors));
            if (!string.Equals(manifest.RuntimeKey, expectedRuntimeKey.ToString(), StringComparison.Ordinal))
                throw new InvalidOperationException("Artifact manifest runtimeKey '" + manifest.RuntimeKey + "' does not match step runtime '" + expectedRuntimeKey + "'.");

            string entrypointName = string.IsNullOrWhiteSpace(config.Entrypoint) ? manifest.DefaultEntrypoint : config.Entrypoint!.Trim();
            if (!manifest.Entrypoints.TryGetValue(entrypointName, out ArtifactManifestEntrypoint? entrypoint))
                throw new InvalidOperationException("Manifest entrypoint '" + entrypointName + "' was not found.");
            artifact.ManifestEntrypoint = entrypointName;

            ArtifactPackageCache cache = new ArtifactPackageCache(blobStore, settings);
            string root = await cache.PrepareAsync(version, token).ConfigureAwait(false);
            return new ArtifactRuntimePlan
            {
                Artifact = artifact,
                Version = version,
                Manifest = manifest,
                EntrypointName = entrypointName,
                Entrypoint = entrypoint,
                ArtifactRoot = root
            };
        }
    }
}
