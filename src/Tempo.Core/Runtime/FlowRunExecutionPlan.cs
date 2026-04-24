namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Models;

    /// <summary>
    /// Serializable execution plan consumed by local and remote executors.
    /// </summary>
    public class FlowRunExecutionPlan
    {
        /// <summary>Flow-run identifier.</summary>
        public string FlowRunId { get; set; } = string.Empty;

        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Data-flow identifier.</summary>
        public string DataFlowId { get; set; } = string.Empty;

        /// <summary>Trigger context for the run.</summary>
        public FlowRunTriggerContext TriggerContext { get; set; } = new FlowRunTriggerContext();

        /// <summary>Resolved flow graph.</summary>
        public Tempo.DataFlow Flow { get; set; } = new Tempo.DataFlow();

        /// <summary>Resolved step map keyed by execution key.</summary>
        public Dictionary<string, FlowRunResolvedStep> Steps { get; set; } = new Dictionary<string, FlowRunResolvedStep>();

        /// <summary>Capability set required to execute the run.</summary>
        public List<FlowRunCapabilityRequirement> RequiredCapabilities { get; set; } = new List<FlowRunCapabilityRequirement>();

        /// <summary>Initial input payload serialized as JSON.</summary>
        public string? InitialInputData { get; set; } = null;

        /// <summary>Execution budget and lease metadata.</summary>
        public FlowRunExecutionBudget Budget { get; set; } = new FlowRunExecutionBudget();

        /// <summary>Optional placement label requested by the flow definition.</summary>
        public string? PlacementLabel { get; set; } = null;

        /// <summary>Resolved artifact-version snapshot pinned at run start.</summary>
        public FlowRunExecutionSnapshot ExecutionSnapshot { get; set; } = new FlowRunExecutionSnapshot();
    }

    /// <summary>
    /// Trigger/user context captured when a run is enqueued.
    /// </summary>
    public class FlowRunTriggerContext
    {
        /// <summary>Trigger identifier, if any.</summary>
        public string? TriggerId { get; set; } = null;

        /// <summary>User identifier that enqueued the run, if any.</summary>
        public string? TriggeredByUserId { get; set; } = null;
    }

    /// <summary>
    /// Resolved step snapshot carried in an execution plan.
    /// </summary>
    public class FlowRunResolvedStep
    {
        /// <summary>Stable execution key.</summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>Tenant scope used for resolution.</summary>
        public string TenantScope { get; set; } = string.Empty;

        /// <summary>Source kind used for placement matching.</summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>Stable capability signature hash.</summary>
        public string SignatureHash { get; set; } = string.Empty;

        /// <summary>Runtime provider key used to execute the step.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>Resolved step record.</summary>
        public StepRecord Step { get; set; } = new StepRecord();

        /// <summary>Inline step configuration when the flow embeds a step directly.</summary>
        public Tempo.RestStepConfiguration? InlineRestConfiguration { get; set; } = null;

        /// <summary>Artifact reference when the step is registry-backed.</summary>
        public ArtifactReference? ArtifactReference { get; set; } = null;
    }

    /// <summary>
    /// Capability requirement for one resolved step.
    /// </summary>
    public class FlowRunCapabilityRequirement
    {
        /// <summary>Stable execution key.</summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>Tenant scope used for resolution.</summary>
        public string TenantScope { get; set; } = string.Empty;

        /// <summary>Source kind used for placement matching.</summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>Stable capability signature hash.</summary>
        public string SignatureHash { get; set; } = string.Empty;

        /// <summary>Runtime provider key used to execute the step.</summary>
        public RuntimeKey RuntimeKey { get; set; }
    }

    /// <summary>
    /// Budget and lease metadata for an execution attempt.
    /// </summary>
    public class FlowRunExecutionBudget
    {
        /// <summary>Maximum runtime in milliseconds for the flow.</summary>
        public int MaxRuntimeMs { get; set; } = 0;

        /// <summary>Dispatch attempt number.</summary>
        public int DispatchAttempt { get; set; } = 0;

        /// <summary>Assignment identifier tied to the current attempt.</summary>
        public string? RunAssignmentId { get; set; } = null;

        /// <summary>Lease token tied to the current attempt.</summary>
        public string? LeaseToken { get; set; } = null;

        /// <summary>Assignment timestamp in UTC.</summary>
        public DateTime? AssignedUtc { get; set; } = null;

        /// <summary>Lease-expiry timestamp in UTC.</summary>
        public DateTime? LeaseExpiresUtc { get; set; } = null;

        /// <summary>Configured lease duration in milliseconds.</summary>
        public int LeaseDurationMs { get; set; } = 0;
    }
}
