namespace Tempo.Core.Runtime
{
    using System;

    /// <summary>Diagnostics surfaced by artifact-backed runners for step-run history.</summary>
    public interface IArtifactRuntimeDiagnostics
    {
        /// <summary>Identifier of the artifact used for execution, or null when not applicable.</summary>
        string? ArtifactId { get; }
        /// <summary>Identifier of the resolved artifact version, or null when not applicable.</summary>
        string? ArtifactVersionId { get; }
        /// <summary>Version string of the resolved artifact, or null when not applicable.</summary>
        string? ArtifactVersion { get; }
        /// <summary>SHA-256 hash of the resolved artifact version, or null when not applicable.</summary>
        string? ArtifactSha256 { get; }
        /// <summary>Name of the manifest entrypoint used for execution, or null when not applicable.</summary>
        string? ManifestEntrypoint { get; }
        /// <summary>UTC time at which the runtime was queued awaiting capacity, or null when not applicable.</summary>
        DateTime? CapacityQueuedUtc { get; }
        /// <summary>UTC time at which the runtime acquired capacity, or null when not applicable.</summary>
        DateTime? CapacityAcquiredUtc { get; }
        /// <summary>Time in milliseconds spent waiting for capacity, or null when not applicable.</summary>
        long? CapacityWaitMs { get; }
    }
}
