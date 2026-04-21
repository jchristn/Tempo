namespace Tempo.Server.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using Tempo.Core.Settings;

    /// <summary>
    /// Thread-safe wrapper around the running <see cref="Settings"/> that supports
    /// reading and persisting the JSON file on disk, and replacing the in-memory copy.
    /// Sections annotated as reboot-required are not applied live.
    /// </summary>
    public class SettingsStore
    {
        private readonly object _Lock = new object();
        private Settings _Current;
        private readonly string _Path;

        /// <summary>Path to the settings JSON file on disk.</summary>
        public string Path => _Path;

        /// <summary>Current snapshot of the settings. Treat as read-only.</summary>
        public Settings Current
        {
            get { lock (_Lock) { return _Current; } }
        }

        /// <summary>Instantiate.</summary>
        /// <param name="initial">Settings loaded during startup.</param>
        /// <param name="path">On-disk path to the settings JSON file.</param>
        public SettingsStore(Settings initial, string path)
        {
            _Current = initial ?? throw new ArgumentNullException(nameof(initial));
            _Path = string.IsNullOrWhiteSpace(path) ? "./tempo.json" : path;
        }

        /// <summary>
        /// Write a new settings snapshot to disk and replace the in-memory copy.
        /// Returns the section names whose changes require a reboot to take effect.
        /// </summary>
        /// <param name="updated">Replacement settings.</param>
        /// <returns>Comma-separated list of sections requiring reboot, or empty string.</returns>
        public string[] Save(Settings updated)
        {
            if (updated == null) throw new ArgumentNullException(nameof(updated));
            lock (_Lock)
            {
                string[] rebootRequired = ComputeRebootRequired(_Current, updated);
                string json = JsonSerializer.Serialize(updated, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_Path));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_Path, json);
                _Current = updated;
                return rebootRequired;
            }
        }

        /// <summary>Sections whose changes do not take effect until the server is restarted.</summary>
        public static string[] RebootRequiredSections => new[] { "rest", "database", "runtimes" };

        private static string[] ComputeRebootRequired(Settings oldS, Settings newS)
        {
            System.Collections.Generic.List<string> changes = new System.Collections.Generic.List<string>();
            if (oldS.Rest.Hostname != newS.Rest.Hostname || oldS.Rest.Port != newS.Rest.Port || oldS.Rest.Ssl != newS.Rest.Ssl)
                changes.Add("rest");
            if (oldS.Database.Type != newS.Database.Type ||
                oldS.Database.Filename != newS.Database.Filename ||
                oldS.Database.Server != newS.Database.Server ||
                oldS.Database.Port != newS.Database.Port ||
                oldS.Database.DatabaseName != newS.Database.DatabaseName ||
                oldS.Database.Username != newS.Database.Username ||
                oldS.Database.Password != newS.Database.Password)
                changes.Add("database");
            if (ExternalExecutionChanged(oldS.Runtimes.ExternalExecution, newS.Runtimes.ExternalExecution) ||
                HostExecutablesChanged(oldS.Runtimes.HostExecutables, newS.Runtimes.HostExecutables))
                changes.Add("runtimes");
            return changes.ToArray();
        }

        private static bool ExternalExecutionChanged(ExternalExecutionSettings oldSettings, ExternalExecutionSettings newSettings)
        {
            string oldJson = JsonSerializer.Serialize(oldSettings);
            string newJson = JsonSerializer.Serialize(newSettings);
            return !string.Equals(oldJson, newJson, StringComparison.Ordinal);
        }

        private static bool HostExecutablesChanged(HostExecutableSettings oldSettings, HostExecutableSettings newSettings)
        {
            string oldJson = JsonSerializer.Serialize(oldSettings);
            string newJson = JsonSerializer.Serialize(newSettings);
            return !string.Equals(oldJson, newJson, StringComparison.Ordinal);
        }
    }
}
