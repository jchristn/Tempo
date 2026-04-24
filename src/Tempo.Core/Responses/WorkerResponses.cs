namespace Tempo.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Workers;

    /// <summary>
    /// Sanitized worker session details returned from the REST API.
    /// </summary>
    public class WorkerSessionResponse
    {
        public string Id { get; set; } = string.Empty;
        public string WorkerId { get; set; } = string.Empty;
        public DateTime ConnectedUtc { get; set; }
        public DateTime? DisconnectedUtc { get; set; } = null;
        public string? DisconnectReason { get; set; } = null;
        public string? ProtocolVersion { get; set; } = null;
    }

    /// <summary>
    /// Sanitized worker summary returned from the REST API.
    /// </summary>
    public class WorkerSummaryResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool DrainMode { get; set; } = false;
        public string? Version { get; set; } = null;
        public string? HostName { get; set; } = null;
        public List<string> Labels { get; set; } = new List<string>();
        public List<WorkerCapabilityDescriptor> Capabilities { get; set; } = new List<WorkerCapabilityDescriptor>();
        public int MaxConcurrentRuns { get; set; } = 1;
        public int MaxTaskTimeoutMs { get; set; } = 0;
        public int ActiveAssignmentCount { get; set; } = 0;
        public DateTime? TokenLastRotatedUtc { get; set; } = null;
        public DateTime? LastHeartbeatUtc { get; set; } = null;
        public DateTime CreatedUtc { get; set; }
        public WorkerSessionResponse? LatestSession { get; set; } = null;
    }
}
