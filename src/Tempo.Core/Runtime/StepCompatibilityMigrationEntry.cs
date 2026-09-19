namespace Tempo.Core.Runtime
{
    /// <summary>One compatibility migration outcome.</summary>
    public class StepCompatibilityMigrationEntry
    {
        /// <summary>Tenant identifier associated with the migrated step. Default: empty string.</summary>
        public string TenantId { get; set; } = string.Empty;
        /// <summary>Flow identifier associated with the migrated step. Default: empty string.</summary>
        public string FlowId { get; set; } = string.Empty;
        /// <summary>Execution key of the step before migration. Default: empty string.</summary>
        public string OriginalExecutionKey { get; set; } = string.Empty;
        /// <summary>Execution key of the step after migration. Default: empty string.</summary>
        public string ExecutionKey { get; set; } = string.Empty;
        /// <summary>Identifier of the step involved in the migration. Default: empty string.</summary>
        public string StepId { get; set; } = string.Empty;
        /// <summary>True when a new step was created for this entry; false when an existing step was reused.</summary>
        public bool StepCreated { get; set; }
        /// <summary>True when the flow was updated as part of this entry.</summary>
        public bool FlowUpdated { get; set; }
        /// <summary>Human-readable message describing the migration outcome. Default: empty string.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
