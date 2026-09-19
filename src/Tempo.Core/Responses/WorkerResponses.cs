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
        /// <summary>
        /// Unique identifier of the worker session.
        /// Default: empty string.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the worker associated with this session.
        /// Default: empty string.
        /// </summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp, in UTC, when the session connected.
        /// </summary>
        public DateTime ConnectedUtc { get; set; }

        /// <summary>
        /// Timestamp, in UTC, when the session disconnected, or null if still connected.
        /// Default: null.
        /// </summary>
        public DateTime? DisconnectedUtc { get; set; } = null;

        /// <summary>
        /// Reason the session disconnected, or null if not applicable.
        /// Default: null.
        /// </summary>
        public string? DisconnectReason { get; set; } = null;

        /// <summary>
        /// Protocol version negotiated for the session, or null if unknown.
        /// Default: null.
        /// </summary>
        public string? ProtocolVersion { get; set; } = null;
    }

    /// <summary>
    /// Sanitized worker summary returned from the REST API.
    /// </summary>
    public class WorkerSummaryResponse
    {
        /// <summary>
        /// Unique identifier of the worker.
        /// Default: empty string.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the worker.
        /// Default: empty string.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Kind of the worker.
        /// Default: empty string.
        /// </summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// Current state of the worker.
        /// Default: empty string.
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the worker is enabled.
        /// Default: true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Indicates whether the worker is in drain mode.
        /// Default: false.
        /// </summary>
        public bool DrainMode { get; set; } = false;

        /// <summary>
        /// Version reported by the worker, or null if unknown.
        /// Default: null.
        /// </summary>
        public string? Version { get; set; } = null;

        /// <summary>
        /// Host name on which the worker is running, or null if unknown.
        /// Default: null.
        /// </summary>
        public string? HostName { get; set; } = null;

        /// <summary>
        /// Labels associated with the worker.
        /// Default: empty list.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Capabilities advertised by the worker.
        /// Default: empty list.
        /// </summary>
        public List<WorkerCapabilityDescriptor> Capabilities { get; set; } = new List<WorkerCapabilityDescriptor>();

        /// <summary>
        /// Maximum number of runs the worker can execute concurrently.
        /// Default: 1.
        /// </summary>
        public int MaxConcurrentRuns { get; set; } = 1;

        /// <summary>
        /// Maximum task timeout in milliseconds (0 for no timeout).
        /// Default: 0.
        /// </summary>
        public int MaxTaskTimeoutMs { get; set; } = 0;

        /// <summary>
        /// Number of assignments currently active on the worker.
        /// Default: 0.
        /// </summary>
        public int ActiveAssignmentCount { get; set; } = 0;

        /// <summary>
        /// Timestamp, in UTC, when the worker token was last rotated, or null if never.
        /// Default: null.
        /// </summary>
        public DateTime? TokenLastRotatedUtc { get; set; } = null;

        /// <summary>
        /// Timestamp, in UTC, of the worker's last heartbeat, or null if none received.
        /// Default: null.
        /// </summary>
        public DateTime? LastHeartbeatUtc { get; set; } = null;

        /// <summary>
        /// Timestamp, in UTC, when the worker was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Most recent session for the worker, or null if none exists.
        /// Default: null.
        /// </summary>
        public WorkerSessionResponse? LatestSession { get; set; } = null;
    }
}
