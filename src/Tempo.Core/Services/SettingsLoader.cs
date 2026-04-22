namespace Tempo.Core.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Tempo.Core.Settings;

    /// <summary>
    /// Loads <see cref="Settings"/> from a JSON file and applies environment variable overrides.
    /// </summary>
    public static class SettingsLoader
    {
        /// <summary>Environment variable naming the settings file path.</summary>
        public const string EnvSettingsFile = "TEMPO_SETTINGS_FILE";
        /// <summary>Environment variable for database type.</summary>
        public const string EnvDbType = "TEMPO_DB_TYPE";
        /// <summary>Environment variable for database server.</summary>
        public const string EnvDbServer = "TEMPO_DB_SERVER";
        /// <summary>Environment variable for database port.</summary>
        public const string EnvDbPort = "TEMPO_DB_PORT";
        /// <summary>Environment variable for database name.</summary>
        public const string EnvDbDatabase = "TEMPO_DB_DATABASE";
        /// <summary>Environment variable for database username.</summary>
        public const string EnvDbUsername = "TEMPO_DB_USERNAME";
        /// <summary>Environment variable for database password.</summary>
        public const string EnvDbPassword = "TEMPO_DB_PASSWORD";
        /// <summary>Environment variable for the token signing key.</summary>
        public const string EnvAuthSigningKey = "TEMPO_AUTH_SIGNING_KEY";
        /// <summary>Environment variable for the admin API key.</summary>
        public const string EnvAuthAdminKey = "TEMPO_AUTH_ADMIN_API_KEY";
        /// <summary>Environment variable for the artifact root path.</summary>
        public const string EnvArtifactRootPath = "TEMPO_ARTIFACT_ROOT";
        /// <summary>Environment variable for external execution scratch root.</summary>
        public const string EnvExternalExecutionScratchRoot = "TEMPO_EXTERNAL_EXECUTION_SCRATCH_ROOT";
        /// <summary>Environment variable for external execution cache root.</summary>
        public const string EnvExternalExecutionCacheRoot = "TEMPO_EXTERNAL_EXECUTION_CACHE_ROOT";
        /// <summary>Environment variable for the worker log root exposed to the server log viewer.</summary>
        public const string EnvLogViewerWorkerRoot = "TEMPO_LOG_VIEWER_WORKER_ROOT";
        /// <summary>Environment variable for the current worker log filename exposed to the server log viewer.</summary>
        public const string EnvLogViewerWorkerLogFilename = "TEMPO_LOG_VIEWER_WORKER_LOG_FILENAME";
        /// <summary>Environment variable toggling run-log capture.</summary>
        public const string EnvRunLogEnabled = "TEMPO_RUN_LOG_ENABLED";
        /// <summary>Environment variable for the shared run-log root.</summary>
        public const string EnvRunLogRoot = "TEMPO_RUN_LOG_ROOT";
        /// <summary>Environment variable for the run-log retention in days.</summary>
        public const string EnvRunLogRetentionDays = "TEMPO_RUN_LOG_RETENTION_DAYS";
        /// <summary>Environment variable for the run-log prune cadence in minutes.</summary>
        public const string EnvRunLogPruneIntervalMinutes = "TEMPO_RUN_LOG_PRUNE_INTERVAL_MINUTES";
        /// <summary>Environment variable for the Python executable used by Artifact.Python.</summary>
        public const string EnvExternalExecutionPythonExecutable = "TEMPO_EXTERNAL_EXECUTION_PYTHON_EXECUTABLE";
        /// <summary>Environment variable for the Node.js executable used by Artifact.JavaScript.</summary>
        public const string EnvExternalExecutionNodeExecutable = "TEMPO_EXTERNAL_EXECUTION_NODE_EXECUTABLE";
        /// <summary>Environment variable for the .NET executable used by Artifact.DotnetProcess and C# source packaging.</summary>
        public const string EnvExternalExecutionDotnetExecutable = "TEMPO_EXTERNAL_EXECUTION_DOTNET_EXECUTABLE";

        /// <summary>
        /// Load settings from the supplied path (or the default), then apply environment overrides.
        /// Returns a fresh <see cref="Settings"/> when the file does not exist.
        /// </summary>
        /// <param name="path">Path to the settings file. When null, <c>./tempo.json</c> is used.</param>
        /// <returns>Loaded settings object.</returns>
        public static Settings Load(string? path = null)
        {
            string resolved = path ?? Environment.GetEnvironmentVariable(EnvSettingsFile) ?? Constants.DefaultSettingsFile;
            Settings settings;

            if (File.Exists(resolved))
            {
                string json = File.ReadAllText(resolved);
                settings = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Settings();
            }
            else
            {
                settings = new Settings();
            }

            ApplyEnvironmentOverrides(settings);
            return settings;
        }

        /// <summary>Persist a settings object to a file.</summary>
        public static void Save(Settings settings, string path)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(path, json);
        }

        private static void ApplyEnvironmentOverrides(Settings settings)
        {
            string? v;

            v = Environment.GetEnvironmentVariable(EnvDbType);
            if (!string.IsNullOrEmpty(v) && Enum.TryParse(v, true, out Tempo.Core.Enums.DatabaseTypeEnum dbType))
                settings.Database.Type = dbType;

            v = Environment.GetEnvironmentVariable(EnvDbServer);
            if (!string.IsNullOrEmpty(v)) settings.Database.Server = v;

            v = Environment.GetEnvironmentVariable(EnvDbPort);
            if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int port)) settings.Database.Port = port;

            v = Environment.GetEnvironmentVariable(EnvDbDatabase);
            if (!string.IsNullOrEmpty(v)) settings.Database.DatabaseName = v;

            v = Environment.GetEnvironmentVariable(EnvDbUsername);
            if (!string.IsNullOrEmpty(v)) settings.Database.Username = v;

            v = Environment.GetEnvironmentVariable(EnvDbPassword);
            if (!string.IsNullOrEmpty(v)) settings.Database.Password = v;

            v = Environment.GetEnvironmentVariable(EnvAuthSigningKey);
            if (!string.IsNullOrEmpty(v)) settings.Auth.SigningKey = v;

            v = Environment.GetEnvironmentVariable(EnvAuthAdminKey);
            if (!string.IsNullOrEmpty(v)) settings.Auth.AdminApiKey = v;

            v = Environment.GetEnvironmentVariable(EnvArtifactRootPath);
            if (!string.IsNullOrEmpty(v)) settings.Artifacts.RootPath = v;

            v = Environment.GetEnvironmentVariable(EnvExternalExecutionScratchRoot);
            if (!string.IsNullOrEmpty(v)) settings.Runtimes.ExternalExecution.ScratchRoot = v;

            v = Environment.GetEnvironmentVariable(EnvExternalExecutionCacheRoot);
            if (!string.IsNullOrEmpty(v)) settings.Runtimes.ExternalExecution.CacheRoot = v;

            v = Environment.GetEnvironmentVariable(EnvLogViewerWorkerRoot);
            if (!string.IsNullOrEmpty(v)) settings.LogViewer.WorkerRootPath = v;

            v = Environment.GetEnvironmentVariable(EnvLogViewerWorkerLogFilename);
            if (!string.IsNullOrEmpty(v)) settings.LogViewer.WorkerLogFilename = v;

            v = Environment.GetEnvironmentVariable(EnvRunLogEnabled);
            if (!string.IsNullOrEmpty(v) && bool.TryParse(v, out bool runLogEnabled)) settings.RunLogs.Enabled = runLogEnabled;

            v = Environment.GetEnvironmentVariable(EnvRunLogRoot);
            if (!string.IsNullOrEmpty(v)) settings.RunLogs.RootPath = v;

            v = Environment.GetEnvironmentVariable(EnvRunLogRetentionDays);
            if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int runLogRetentionDays)) settings.RunLogs.RetentionDays = runLogRetentionDays;

            v = Environment.GetEnvironmentVariable(EnvRunLogPruneIntervalMinutes);
            if (!string.IsNullOrEmpty(v) && int.TryParse(v, out int runLogPruneIntervalMinutes)) settings.RunLogs.PruneIntervalMinutes = runLogPruneIntervalMinutes;

            v = Environment.GetEnvironmentVariable(EnvExternalExecutionPythonExecutable);
            if (!string.IsNullOrEmpty(v)) settings.Runtimes.ExternalExecution.PythonExecutable = v;

            v = Environment.GetEnvironmentVariable(EnvExternalExecutionNodeExecutable);
            if (!string.IsNullOrEmpty(v)) settings.Runtimes.ExternalExecution.NodeExecutable = v;

            v = Environment.GetEnvironmentVariable(EnvExternalExecutionDotnetExecutable);
            if (!string.IsNullOrEmpty(v)) settings.Runtimes.ExternalExecution.DotnetExecutable = v;
        }
    }
}
