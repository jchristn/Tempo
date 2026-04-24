namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Persisted worker connection session.
    /// </summary>
    public class WorkerSessionRecord
    {
        /// <summary>Session identifier (prefix "wse_").</summary>
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

        /// <summary>UTC time the session connected.</summary>
        public DateTime ConnectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC time the session disconnected, when applicable.</summary>
        public DateTime? DisconnectedUtc { get; set; } = null;

        /// <summary>Disconnect reason, when known.</summary>
        public string? DisconnectReason { get; set; } = null;

        /// <summary>Negotiated protocol version.</summary>
        public string? ProtocolVersion { get; set; } = null;

        private string _Id = IdGenerator.GenerateWorkerSessionId();
        private string _WorkerId = string.Empty;
    }
}
