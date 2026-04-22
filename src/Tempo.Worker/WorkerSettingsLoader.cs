namespace Tempo.Worker
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Linq;

    /// <summary>
    /// Loads worker settings from JSON and environment variables.
    /// </summary>
    public static class WorkerSettingsLoader
    {
        public const string DefaultSettingsFile = "./tempo.worker.json";
        public const string EnvSettingsFile = "TEMPO_WORKER_SETTINGS_FILE";
        public const string EnvServerEndpoint = "TEMPO_WORKER_SERVER_ENDPOINT";
        public const string EnvWorkerId = "TEMPO_WORKER_ID";
        public const string EnvWorkerToken = "TEMPO_WORKER_TOKEN";
        public const string EnvWorkerName = "TEMPO_WORKER_NAME";
        public const string EnvWorkerKind = "TEMPO_WORKER_KIND";
        public const string EnvWorkerLabels = "TEMPO_WORKER_LABELS";
        public const string EnvMaxConcurrentRuns = "TEMPO_WORKER_MAX_CONCURRENT_RUNS";
        public const string EnvMaxTaskTimeoutMs = "TEMPO_WORKER_MAX_TASK_TIMEOUT_MS";
        public const string EnvRequestTimeoutMs = "TEMPO_WORKER_REQUEST_TIMEOUT_MS";
        public const string EnvLogDirectory = "TEMPO_WORKER_LOG_DIRECTORY";
        public const string EnvLogFilename = "TEMPO_WORKER_LOG_FILENAME";
        public const string EnvRunLogEnabled = "TEMPO_RUN_LOG_ENABLED";
        public const string EnvRunLogRoot = "TEMPO_RUN_LOG_ROOT";

        /// <summary>Load worker settings from disk and environment overrides.</summary>
        public static WorkerSettings Load(string? path = null)
        {
            string resolved = path ?? Environment.GetEnvironmentVariable(EnvSettingsFile) ?? DefaultSettingsFile;
            WorkerSettings settings;

            if (File.Exists(resolved))
            {
                string json = File.ReadAllText(resolved);
                settings = JsonSerializer.Deserialize<WorkerSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new WorkerSettings();
            }
            else
            {
                settings = new WorkerSettings();
            }

            ApplyEnvironmentOverrides(settings);
            return settings;
        }

        /// <summary>Persist worker settings to disk.</summary>
        public static void Save(WorkerSettings settings, string path)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(path, json);
        }

        private static void ApplyEnvironmentOverrides(WorkerSettings settings)
        {
            string? value = Environment.GetEnvironmentVariable(EnvServerEndpoint);
            if (!string.IsNullOrWhiteSpace(value)) settings.ServerEndpoint = value;

            value = Environment.GetEnvironmentVariable(EnvWorkerId);
            if (!string.IsNullOrWhiteSpace(value)) settings.WorkerId = value;

            value = Environment.GetEnvironmentVariable(EnvWorkerToken);
            if (!string.IsNullOrWhiteSpace(value)) settings.WorkerToken = value;

            value = Environment.GetEnvironmentVariable(EnvWorkerName);
            if (!string.IsNullOrWhiteSpace(value)) settings.Name = value;

            value = Environment.GetEnvironmentVariable(EnvWorkerKind);
            if (!string.IsNullOrWhiteSpace(value)) settings.Kind = value;

            value = Environment.GetEnvironmentVariable(EnvWorkerLabels);
            if (!string.IsNullOrWhiteSpace(value))
            {
                settings.Labels = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }

            value = Environment.GetEnvironmentVariable(EnvMaxConcurrentRuns);
            if (int.TryParse(value, out int maxConcurrentRuns))
            {
                settings.MaxConcurrentRuns = maxConcurrentRuns;
            }

            value = Environment.GetEnvironmentVariable(EnvMaxTaskTimeoutMs);
            if (int.TryParse(value, out int maxTaskTimeoutMs))
            {
                settings.MaxTaskTimeoutMs = maxTaskTimeoutMs;
            }

            value = Environment.GetEnvironmentVariable(EnvRequestTimeoutMs);
            if (int.TryParse(value, out int requestTimeoutMs))
            {
                settings.RequestTimeoutMs = requestTimeoutMs;
            }

            value = Environment.GetEnvironmentVariable(EnvLogDirectory);
            if (!string.IsNullOrWhiteSpace(value))
            {
                settings.Logging.LogDirectory = value;
            }

            value = Environment.GetEnvironmentVariable(EnvLogFilename);
            if (!string.IsNullOrWhiteSpace(value))
            {
                settings.Logging.LogFilename = value;
            }

            value = Environment.GetEnvironmentVariable(EnvRunLogEnabled);
            if (!string.IsNullOrWhiteSpace(value) && bool.TryParse(value, out bool runLogEnabled))
            {
                settings.RunLogs.Enabled = runLogEnabled;
            }

            value = Environment.GetEnvironmentVariable(EnvRunLogRoot);
            if (!string.IsNullOrWhiteSpace(value))
            {
                settings.RunLogs.RootPath = value;
            }
        }
    }
}
