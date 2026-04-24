namespace Tempo.Core.Settings
{
    using System;

    /// <summary>
    /// Workflow execution engine settings.
    /// </summary>
    public class EngineSettings
    {
        /// <summary>Whether the flow queue worker is enabled. Default: true.</summary>
        public bool QueueEnabled { get; set; } = true;

        /// <summary>Whether the server can participate in workload execution as a pseudo-worker. Default: true.</summary>
        public bool ServerCanExecuteWorkload { get; set; } = true;

        /// <summary>Load-balancing strategy. Supported values: LeastLoaded, LabelPinned. Default: LeastLoaded.</summary>
        public string LoadBalancingStrategy
        {
            get
            {
                return _LoadBalancingStrategy;
            }
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? "LeastLoaded" : value.Trim();
                _LoadBalancingStrategy = string.Equals(normalized, "LabelPinned", StringComparison.OrdinalIgnoreCase)
                    ? "LabelPinned"
                    : "LeastLoaded";
            }
        }

        /// <summary>Maximum concurrent flow runs. Default: 4. Range: 1 to 1024.</summary>
        public int MaxConcurrentRuns
        {
            get
            {
                return _MaxConcurrentRuns;
            }
            set
            {
                _MaxConcurrentRuns = Math.Clamp(value, 1, 1024);
            }
        }

        /// <summary>Milliseconds between queue poll cycles. Default: 1000. Range: 100 to 60000.</summary>
        public int PollIntervalMs
        {
            get
            {
                return _PollIntervalMs;
            }
            set
            {
                _PollIntervalMs = Math.Clamp(value, 100, 60000);
            }
        }

        /// <summary>Assignment lease duration in milliseconds. Default: 300000. Range: 1000 to 86400000.</summary>
        public int LeaseDurationMs
        {
            get
            {
                return _LeaseDurationMs;
            }
            set
            {
                _LeaseDurationMs = Math.Clamp(value, 1000, 86400000);
            }
        }

        /// <summary>Worker heartbeat timeout in milliseconds. Default: 30000. Range: 1000 to 86400000.</summary>
        public int WorkerHeartbeatTimeoutMs
        {
            get
            {
                return _WorkerHeartbeatTimeoutMs;
            }
            set
            {
                _WorkerHeartbeatTimeoutMs = Math.Clamp(value, 1000, 86400000);
            }
        }

        /// <summary>Maximum assignment attempts before a run is failed. Default: 3. Range: 1 to 1024.</summary>
        public int MaxAssignmentAttempts
        {
            get
            {
                return _MaxAssignmentAttempts;
            }
            set
            {
                _MaxAssignmentAttempts = Math.Clamp(value, 1, 1024);
            }
        }

        /// <summary>Whether multiple active schedulers are allowed to dispatch simultaneously. Default: false.</summary>
        public bool AllowDuplicateScheduler { get; set; } = false;

        /// <summary>Comma-separated list of assembly paths to scan for <c>[StepMethod]</c> attributes at startup.</summary>
        public string? StepAssemblyPaths { get; set; } = null;

        private string _LoadBalancingStrategy = "LeastLoaded";
        private int _MaxConcurrentRuns = 4;
        private int _PollIntervalMs = 1000;
        private int _LeaseDurationMs = 300000;
        private int _WorkerHeartbeatTimeoutMs = 30000;
        private int _MaxAssignmentAttempts = 3;
    }
}
