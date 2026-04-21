namespace Tempo.Core.Runtime
{
    using Tempo.Core.Enums;

    /// <summary>One built-in step reconciliation outcome.</summary>
    public class BuiltinStepReconciliationEntry
    {
        public string StepId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ExecutionKey { get; set; } = string.Empty;
        public RuntimeKey RuntimeKey { get; set; }
        public StepRuntimeBindingStateEnum State { get; set; }
        public int CandidateCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
