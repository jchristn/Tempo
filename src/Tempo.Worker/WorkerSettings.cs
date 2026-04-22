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
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public string ServerEndpoint
        {
            get => _ServerEndpoint;
            set => _ServerEndpoint = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:8901" : value.Trim().TrimEnd('/');
        }

        public string WorkerId
        {
            get => _WorkerId;
            set => _WorkerId = string.IsNullOrWhiteSpace(value) ? "wrk_worker_1" : value.Trim();
        }

        public string WorkerToken { get; set; } = string.Empty;

        public string Name
        {
            get => _Name;
            set => _Name = string.IsNullOrWhiteSpace(value) ? Environment.MachineName : value.Trim();
        }

        public string Kind
        {
            get => _Kind;
            set => _Kind = string.IsNullOrWhiteSpace(value) ? "Worker" : value.Trim();
        }

        public int MaxConcurrentRuns
        {
            get => _MaxConcurrentRuns;
            set => _MaxConcurrentRuns = Math.Clamp(value, 1, 1024);
        }

        public int MaxTaskTimeoutMs
        {
            get => _MaxTaskTimeoutMs;
            set => _MaxTaskTimeoutMs = Math.Clamp(value, 0, 86400000);
        }

        public int ReconnectDelayMs
        {
            get => _ReconnectDelayMs;
            set => _ReconnectDelayMs = Math.Clamp(value, 1000, 60000);
        }

        public int RequestTimeoutMs
        {
            get => _RequestTimeoutMs;
            set => _RequestTimeoutMs = Math.Clamp(value, 1000, 600000);
        }

        public List<string> Labels { get; set; } = new List<string>();

        public LoggingSettings Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        public RuntimeSettings Runtimes
        {
            get => _Runtimes;
            set => _Runtimes = value ?? throw new ArgumentNullException(nameof(Runtimes));
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
    }
}
