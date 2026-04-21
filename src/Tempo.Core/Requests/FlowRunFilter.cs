namespace Tempo.Core.Requests
{
    using System;
    using Tempo.Core.Enums;

    /// <summary>
    /// Filter for flow run enumeration.
    /// </summary>
    public class FlowRunFilter : EnumerationFilter
    {
        /// <summary>Tenant identifier.</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>Filter to runs of a specific flow.</summary>
        public string? DataFlowId { get; set; } = null;

        /// <summary>Filter by current state.</summary>
        public FlowRunStateEnum? State { get; set; } = null;

        /// <summary>Inclusive lower UTC bound on CreatedUtc.</summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>Exclusive upper UTC bound on CreatedUtc.</summary>
        public DateTime? ToUtc { get; set; } = null;
    }
}
