namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Append-only worker activity log row.
    /// </summary>
    public class WorkerActivityRecord
    {
        /// <summary>Activity identifier (prefix "wac_").</summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
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
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(WorkerId));
                _WorkerId = value;
            }
        }

        /// <summary>Worker session identifier.</summary>
        public string? WorkerSessionId { get; set; } = null;

        /// <summary>Flow-run identifier.</summary>
        public string? FlowRunId { get; set; } = null;

        /// <summary>Run-assignment identifier.</summary>
        public string? RunAssignmentId { get; set; } = null;

        /// <summary>Event type.</summary>
        public string EventType { get; set; } = "info";

        /// <summary>Severity label.</summary>
        public string? Severity { get; set; } = null;

        /// <summary>Human-readable message.</summary>
        public string? Message { get; set; } = null;

        /// <summary>Optional structured payload JSON.</summary>
        public string? PayloadJson { get; set; } = null;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateWorkerActivityId();
        private string _WorkerId = string.Empty;
    }
}
