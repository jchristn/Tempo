namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;
    using Tempo.Core.Models;

    /// <summary>Context for validating runtime configuration.</summary>
    public class StepRuntimeValidationContext
    {
        public string TenantId { get; set; } = string.Empty;
        public RuntimeKey RuntimeKey { get; set; }
        public StepRuntimeConfig? Config { get; set; }
    }

    /// <summary>Context for creating a step runner.</summary>
    public class StepExecutionContext
    {
        public string TenantId { get; set; } = string.Empty;
        public string ExecutionKey { get; set; } = string.Empty;
        public string FlowRunId { get; set; } = string.Empty;
        public string StepRunId { get; set; } = string.Empty;
        public string? RunAssignmentId { get; set; } = null;
        public string? WorkerId { get; set; } = null;
        public int AttemptNumber { get; set; } = 0;
        public int StepSequence { get; set; } = 0;
        public FlowRunExecutionSnapshot Snapshot { get; set; } = new FlowRunExecutionSnapshot();
        public RunLogSession? RunLogSession { get; set; } = null;
        public RunLogStepScope? RunLogStep { get; set; } = null;
    }

    /// <summary>Resolved step execution metadata.</summary>
    public class ResolvedStepExecution
    {
        public StepRecord Step { get; set; } = new StepRecord();
        public StepRuntimeConfig? Config { get; set; }
    }

    /// <summary>Run-start execution snapshot, including resolved artifact versions.</summary>
    public class FlowRunExecutionSnapshot
    {
        public string FlowRunId { get; set; } = string.Empty;
        public Dictionary<string, ArtifactVersionSnapshot> ArtifactVersions { get; set; } = new Dictionary<string, ArtifactVersionSnapshot>();

        public static string ArtifactKey(string artifactId, string? requestedVersion)
        {
            return artifactId + "|" + (string.IsNullOrWhiteSpace(requestedVersion) ? "latest" : requestedVersion.Trim());
        }
    }

    /// <summary>Resolved artifact version captured at flow-run start.</summary>
    public class ArtifactVersionSnapshot
    {
        public string ArtifactId { get; set; } = string.Empty;
        public string RequestedVersion { get; set; } = "latest";
        public string VersionId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string? ManifestJson { get; set; } = null;
        public string? ManifestEntrypoint { get; set; } = null;
    }
}
