namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Persisted assignment attempt for a flow run.
    /// </summary>
    public class RunAssignmentRecord
    {
        /// <summary>Assignment identifier (prefix "ras_").</summary>
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

        /// <summary>Flow-run identifier.</summary>
        public string FlowRunId
        {
            get
            {
                return _FlowRunId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FlowRunId));
                _FlowRunId = value;
            }
        }

        /// <summary>Worker identifier.</summary>
        public string WorkerId
        {
            get
            {
                return _WorkerId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(WorkerId));
                _WorkerId = value;
            }
        }

        /// <summary>Worker-session identifier. Null for local server execution.</summary>
        public string? WorkerSessionId { get; set; } = null;

        /// <summary>Assignment attempt number for the parent run.</summary>
        public int AttemptNumber { get; set; } = 1;

        /// <summary>Assignment state.</summary>
        public RunAssignmentStateEnum State { get; set; } = RunAssignmentStateEnum.Assigned;

        /// <summary>Opaque lease token tied to this assignment attempt.</summary>
        public string LeaseToken
        {
            get
            {
                return _LeaseToken;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LeaseToken));
                _LeaseToken = value;
            }
        }

        /// <summary>Lease-expiry timestamp in UTC.</summary>
        public DateTime LeaseExpiresUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Assignment timestamp in UTC.</summary>
        public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Completion timestamp in UTC.</summary>
        public DateTime? CompletedUtc { get; set; } = null;

        private string _Id = IdGenerator.GenerateRunAssignmentId();
        private string _FlowRunId = String.Empty;
        private string _WorkerId = String.Empty;
        private string _LeaseToken = IdGenerator.GenerateNonceId();
    }
}
