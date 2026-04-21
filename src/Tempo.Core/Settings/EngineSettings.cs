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

        /// <summary>Comma-separated list of assembly paths to scan for <c>[StepMethod]</c> attributes at startup.</summary>
        public string? StepAssemblyPaths { get; set; } = null;

        private int _MaxConcurrentRuns = 4;
        private int _PollIntervalMs = 1000;
    }
}
