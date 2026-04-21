namespace Tempo.Core.Runtime
{
    using System;

    /// <summary>Diagnostics surfaced by artifact-backed runners for step-run history.</summary>
    public interface IArtifactRuntimeDiagnostics
    {
        string? ArtifactId { get; }
        string? ArtifactVersionId { get; }
        string? ArtifactVersion { get; }
        string? ArtifactSha256 { get; }
        string? ManifestEntrypoint { get; }
        DateTime? CapacityQueuedUtc { get; }
        DateTime? CapacityAcquiredUtc { get; }
        long? CapacityWaitMs { get; }
    }
}
