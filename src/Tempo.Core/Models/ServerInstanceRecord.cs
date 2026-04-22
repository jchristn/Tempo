namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Scheduler server instance heartbeat row.
    /// </summary>
    public class ServerInstanceRecord
    {
        /// <summary>Server instance identifier (prefix "srv_").</summary>
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

        /// <summary>Host name.</summary>
        public string? HostName { get; set; } = null;

        /// <summary>UTC time the instance started.</summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC time of the last heartbeat.</summary>
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Version string.</summary>
        public string? Version { get; set; } = null;

        private string _Id = IdGenerator.GenerateServerInstanceId();
    }
}
