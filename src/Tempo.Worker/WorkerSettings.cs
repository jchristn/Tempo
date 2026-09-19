namespace Tempo.Worker
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Settings;

    /// <summary>
    /// Root worker settings loaded from <c>tempo.worker.json</c>.
    /// </summary>
    public class WorkerSettings
    {
        /// <summary>Timestamp, in UTC, when these settings were created. Default: current UTC time.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Base endpoint of the Tempo server the worker connects to.
        /// Trailing slashes are trimmed. Default: <c>http://127.0.0.1:8901</c>.
        /// </summary>
        public string ServerEndpoint
        {
            get => _ServerEndpoint;
            set => _ServerEndpoint = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:8901" : value.Trim().TrimEnd('/');
        }

        /// <summary>Unique identifier for this worker. Default: <c>wrk_worker_1</c>.</summary>
        public string WorkerId
        {
            get => _WorkerId;
            set => _WorkerId = string.IsNullOrWhiteSpace(value) ? "wrk_worker_1" : value.Trim();
        }

        /// <summary>Authentication token used when registering with the server. Default: empty.</summary>
        public string WorkerToken { get; set; } = string.Empty;

        /// <summary>Display name for this worker. Default: the machine name.</summary>
        public string Name
        {
            get => _Name;
            set => _Name = string.IsNullOrWhiteSpace(value) ? Environment.MachineName : value.Trim();
        }

        /// <summary>Classification of this worker. Default: <c>Worker</c>.</summary>
        public string Kind
        {
            get => _Kind;
            set => _Kind = string.IsNullOrWhiteSpace(value) ? "Worker" : value.Trim();
        }

        /// <summary>Maximum number of runs executed concurrently. Default: 1. Range: 1 to 1024.</summary>
        public int MaxConcurrentRuns
        {
            get => _MaxConcurrentRuns;
            set => _MaxConcurrentRuns = Math.Clamp(value, 1, 1024);
        }

        /// <summary>Maximum runtime for a single task in milliseconds (0 for no limit). Default: 0. Range: 0 to 86400000.</summary>
        public int MaxTaskTimeoutMs
        {
            get => _MaxTaskTimeoutMs;
            set => _MaxTaskTimeoutMs = Math.Clamp(value, 0, 86400000);
        }

        /// <summary>Delay before reconnecting to the server in milliseconds. Default: 5000. Range: 1000 to 60000.</summary>
        public int ReconnectDelayMs
        {
            get => _ReconnectDelayMs;
            set => _ReconnectDelayMs = Math.Clamp(value, 1000, 60000);
        }

        /// <summary>Timeout for individual server requests in milliseconds. Default: 30000. Range: 1000 to 600000.</summary>
        public int RequestTimeoutMs
        {
            get => _RequestTimeoutMs;
            set => _RequestTimeoutMs = Math.Clamp(value, 1000, 600000);
        }

        /// <summary>Labels advertised by this worker for task routing. Default: empty list.</summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>Logging configuration for the worker. Never null.</summary>
        public LoggingSettings Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        /// <summary>Runtime configuration for the worker. Never null.</summary>
        public RuntimeSettings Runtimes
        {
            get => _Runtimes;
            set => _Runtimes = value ?? throw new ArgumentNullException(nameof(Runtimes));
        }

        /// <summary>Run-log configuration for the worker. Never null.</summary>
        public RunLogSettings RunLogs
        {
            get => _RunLogs;
            set => _RunLogs = value ?? throw new ArgumentNullException(nameof(RunLogs));
        }

        private string _ServerEndpoint = "http://127.0.0.1:8901";
        private string _WorkerId = "wrk_worker_1";
        private string _Name = Environment.MachineName;
        private string _Kind = "Worker";
        private int _MaxConcurrentRuns = 1;
        private int _MaxTaskTimeoutMs = 0;
        private int _ReconnectDelayMs = 5000;
        private int _RequestTimeoutMs = 30000;
        private LoggingSettings _Logging = new LoggingSettings();
        private RuntimeSettings _Runtimes = new RuntimeSettings();
        private RunLogSettings _RunLogs = new RunLogSettings();
    }
}
