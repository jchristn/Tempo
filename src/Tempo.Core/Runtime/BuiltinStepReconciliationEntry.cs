namespace Tempo.Core.Runtime
{
    using Tempo.Core.Enums;

    /// <summary>One built-in step reconciliation outcome.</summary>
    public class BuiltinStepReconciliationEntry
    {
        /// <summary>Identifier of the built-in step being reconciled. Defaults to an empty string.</summary>
        public string StepId { get; set; } = string.Empty;
        /// <summary>Identifier of the tenant that owns the step. Defaults to an empty string.</summary>
        public string TenantId { get; set; } = string.Empty;
        /// <summary>Execution key used to match the step against runtime candidates. Defaults to an empty string.</summary>
        public string ExecutionKey { get; set; } = string.Empty;
        /// <summary>Runtime key resolved for the step.</summary>
        public RuntimeKey RuntimeKey { get; set; }
        /// <summary>Resulting binding state for the step (for example resolved, ambiguous, or orphaned).</summary>
        public StepRuntimeBindingStateEnum State { get; set; }
        /// <summary>Number of runtime candidates that matched the step.</summary>
        public int CandidateCount { get; set; }
        /// <summary>Human-readable message describing the reconciliation outcome. Defaults to an empty string.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
