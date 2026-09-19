namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;
    using Tempo.Core.Models;

    /// <summary>Context for validating runtime configuration.</summary>
    public class StepRuntimeValidationContext
    {
        /// <summary>Identifier of the tenant that owns the runtime configuration. Default: empty string.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Runtime key identifying the runtime being validated.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>Runtime configuration to validate. Default: null.</summary>
        public StepRuntimeConfig? Config { get; set; }
    }

    /// <summary>Context for creating a step runner.</summary>
    public class StepExecutionContext
    {
        /// <summary>Identifier of the tenant that owns the execution. Default: empty string.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Execution key identifying the step being executed. Default: empty string.</summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>Identifier of the flow run this execution belongs to. Default: empty string.</summary>
        public string FlowRunId { get; set; } = string.Empty;

        /// <summary>Identifier of the step run for this execution. Default: empty string.</summary>
        public string StepRunId { get; set; } = string.Empty;

        /// <summary>Optional identifier of the run assignment. Default: null.</summary>
        public string? RunAssignmentId { get; set; } = null;

        /// <summary>Optional identifier of the worker executing the step. Default: null.</summary>
        public string? WorkerId { get; set; } = null;

        /// <summary>Zero-based attempt number for this execution. Default: 0.</summary>
        public int AttemptNumber { get; set; } = 0;

        /// <summary>Zero-based sequence position of the step within the flow run. Default: 0.</summary>
        public int StepSequence { get; set; } = 0;

        /// <summary>Execution snapshot captured at flow-run start. Default: a new empty snapshot.</summary>
        public FlowRunExecutionSnapshot Snapshot { get; set; } = new FlowRunExecutionSnapshot();

        /// <summary>Optional run-log session for this execution. Default: null.</summary>
        public RunLogSession? RunLogSession { get; set; } = null;

        /// <summary>Optional run-log step scope for this execution. Default: null.</summary>
        public RunLogStepScope? RunLogStep { get; set; } = null;
    }

    /// <summary>Resolved step execution metadata.</summary>
    public class ResolvedStepExecution
    {
        /// <summary>The resolved step record. Default: a new empty step record.</summary>
        public StepRecord Step { get; set; } = new StepRecord();

        /// <summary>Resolved runtime configuration for the step. Default: null.</summary>
        public StepRuntimeConfig? Config { get; set; }
    }

    /// <summary>Run-start execution snapshot, including resolved artifact versions.</summary>
    public class FlowRunExecutionSnapshot
    {
        /// <summary>Identifier of the flow run this snapshot describes. Default: empty string.</summary>
        public string FlowRunId { get; set; } = string.Empty;

        /// <summary>Resolved artifact versions keyed by artifact key. Default: an empty dictionary.</summary>
        public Dictionary<string, ArtifactVersionSnapshot> ArtifactVersions { get; set; } = new Dictionary<string, ArtifactVersionSnapshot>();

        /// <summary>Builds the composite key used to look up an artifact version in the snapshot.</summary>
        /// <param name="artifactId">Identifier of the artifact.</param>
        /// <param name="requestedVersion">Requested version, or null/whitespace to indicate "latest".</param>
        /// <returns>The composite artifact key combining the artifact id and requested version.</returns>
        public static string ArtifactKey(string artifactId, string? requestedVersion)
        {
            return artifactId + "|" + (string.IsNullOrWhiteSpace(requestedVersion) ? "latest" : requestedVersion.Trim());
        }
    }

    /// <summary>Resolved artifact version captured at flow-run start.</summary>
    public class ArtifactVersionSnapshot
    {
        /// <summary>Identifier of the artifact. Default: empty string.</summary>
        public string ArtifactId { get; set; } = string.Empty;

        /// <summary>Version requested when the snapshot was captured. Default: "latest".</summary>
        public string RequestedVersion { get; set; } = "latest";

        /// <summary>Identifier of the resolved artifact version. Default: empty string.</summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>Resolved version string of the artifact. Default: empty string.</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>SHA-256 hash of the resolved artifact version. Default: empty string.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Optional raw manifest JSON for the artifact version. Default: null.</summary>
        public string? ManifestJson { get; set; } = null;

        /// <summary>Optional entrypoint declared in the artifact manifest. Default: null.</summary>
        public string? ManifestEntrypoint { get; set; } = null;
    }
}
