namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Persisted worker metadata and current scheduler-visible state.
    /// </summary>
    public class WorkerRecord
    {
        /// <summary>Worker identifier (prefix "wrk_").</summary>
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

        /// <summary>Human-readable worker name.</summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>Worker kind, for example Server or Worker.</summary>
        public string Kind { get; set; } = "Worker";

        /// <summary>Worker state, for example Online or Offline.</summary>
        public string State { get; set; } = "Offline";

        /// <summary>Whether the worker is enabled for placement.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Whether the worker is draining.</summary>
        public bool DrainMode { get; set; } = false;

        /// <summary>Runtime version string.</summary>
        public string? Version { get; set; } = null;

        /// <summary>Host name.</summary>
        public string? HostName { get; set; } = null;

        /// <summary>Serialized JSON labels.</summary>
        public string LabelsJson { get; set; } = "[]";

        /// <summary>Serialized JSON capabilities.</summary>
        public string CapabilitiesJson { get; set; } = "[]";

        /// <summary>Maximum concurrent runs.</summary>
        public int MaxConcurrentRuns { get; set; } = 1;

        /// <summary>Maximum worker-enforced task timeout in milliseconds. Zero means no explicit worker timeout.</summary>
        public int MaxTaskTimeoutMs { get; set; } = 0;

        /// <summary>SHA-256 hash of the current worker token.</summary>
        public string? TokenHash { get; set; } = null;

        /// <summary>UTC time the worker token was last rotated.</summary>
        public DateTime? TokenLastRotatedUtc { get; set; } = null;

        /// <summary>UTC time of the last heartbeat.</summary>
        public DateTime? LastHeartbeatUtc { get; set; } = null;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateWorkerId();
        private string _Name = "worker";
    }
}
