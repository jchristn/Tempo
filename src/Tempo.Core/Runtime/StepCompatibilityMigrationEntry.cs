namespace Tempo.Core.Runtime
{
    /// <summary>One compatibility migration outcome.</summary>
    public class StepCompatibilityMigrationEntry
    {
        public string TenantId { get; set; } = string.Empty;
        public string FlowId { get; set; } = string.Empty;
        public string OriginalExecutionKey { get; set; } = string.Empty;
        public string ExecutionKey { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public bool StepCreated { get; set; }
        public bool FlowUpdated { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
