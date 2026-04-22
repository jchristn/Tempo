namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;

    /// <summary>
    /// A single execution of a data flow.
    /// </summary>
    public class FlowRun
    {
        /// <summary>Flow run identifier (prefix "run_").</summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>Tenant identifier.</summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>Data flow identifier.</summary>
        public string DataFlowId
        {
            get
            {
                return _DataFlowId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(DataFlowId));
                _DataFlowId = value;
            }
        }

        /// <summary>User identifier of the user who triggered the run (optional).</summary>
        public string? TriggeredByUserId { get; set; } = null;

        /// <summary>Trigger identifier that caused this run (optional).</summary>
        public string? TriggerId { get; set; } = null;

        /// <summary>Client source IP observed by the server when the run was enqueued (optional).</summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>Current state.</summary>
        public FlowRunStateEnum State { get; set; } = FlowRunStateEnum.Queued;

        /// <summary>Initial request data serialized as JSON.</summary>
        public string? InputData { get; set; } = null;

        /// <summary>Final result data serialized as JSON.</summary>
        public string? OutputData { get; set; } = null;

        /// <summary>Exception text if the run terminated in exception state.</summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>Serialized run-start execution snapshot, including resolved artifact versions.</summary>
        public string? ExecutionSnapshotJson { get; set; } = null;

        /// <summary>Current fine-grained dispatch state.</summary>
        public FlowRunDispatchStateEnum DispatchState { get; set; } = FlowRunDispatchStateEnum.Pending;

        /// <summary>Number of dispatch attempts made for this run.</summary>
        public int DispatchAttempt { get; set; } = 0;

        /// <summary>Worker identifier currently assigned to this run.</summary>
        public string? AssignedWorkerId { get; set; } = null;

        /// <summary>Current assignment identifier, if any.</summary>
        public string? RunAssignmentId { get; set; } = null;

        /// <summary>Milliseconds spent waiting in queue before assignment.</summary>
        public long? QueueWaitMs { get; set; } = null;

        /// <summary>UTC time at which the current assignment was created.</summary>
        public DateTime? AssignedUtc { get; set; } = null;

        /// <summary>UTC time at which the current assignment lease expires.</summary>
        public DateTime? LeaseExpiresUtc { get; set; } = null;

        /// <summary>Execution node kind handling the run.</summary>
        public ExecutionNodeKindEnum? ExecutionNodeKind { get; set; } = null;

        /// <summary>UTC time the run was enqueued.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC time the run started (null while queued).</summary>
        public DateTime? StartedUtc { get; set; } = null;

        /// <summary>UTC time the run completed (null while queued or running).</summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateFlowRunId();
        private string _TenantId = String.Empty;
        private string _DataFlowId = String.Empty;
    }
}
