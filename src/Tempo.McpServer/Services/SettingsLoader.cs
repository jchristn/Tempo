namespace Tempo.McpServer.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Tempo.McpServer.Settings;

    /// <summary>
    /// Loads and saves Tempo MCP settings.
    /// </summary>
    public static class SettingsLoader
    {
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        /// <summary>Load settings from disk.</summary>
        /// <param name="path">Settings path.</param>
        /// <returns>Settings.</returns>
        public static TempoMcpServerSettings Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) path = Constants.DefaultSettingsFile;
            if (!File.Exists(path)) return new TempoMcpServerSettings();

            string json = File.ReadAllText(path);
            TempoMcpServerSettings? settings = JsonSerializer.Deserialize<TempoMcpServerSettings>(json, _JsonOptions);
            return settings ?? new TempoMcpServerSettings();
        }

        /// <summary>Save settings to disk.</summary>
        /// <param name="settings">Settings.</param>
        /// <param name="path">Settings path.</param>
        public static void Save(TempoMcpServerSettings settings, string path)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(path)) path = Constants.DefaultSettingsFile;

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(settings, _JsonOptions);
            File.WriteAllText(path, json);
        }

        /// <summary>Serialize settings for display.</summary>
        /// <param name="settings">Settings.</param>
        /// <returns>JSON.</returns>
        public static string Serialize(TempoMcpServerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return JsonSerializer.Serialize(settings, _JsonOptions);
        }

        /// <summary>Apply environment variable overrides.</summary>
        /// <param name="settings">Settings.</param>
        public static void ApplyEnvironment(TempoMcpServerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ApplyString(Constants.TempoEndpointEnvironmentVariable, value => settings.Tempo.Endpoint = value);
            ApplyString(Constants.TempoTokenEnvironmentVariable, value => settings.Tempo.Token = value);
            ApplyString(Constants.TempoApiKeyEnvironmentVariable, value => settings.Tempo.ApiKey = value);
            ApplyString(Constants.TempoAccessKeyEnvironmentVariable, value => settings.Tempo.AccessKey = value);
            ApplyString(Constants.TempoTenantIdEnvironmentVariable, value => settings.Tempo.DefaultTenantId = value);
            ApplyString(Constants.McpHttpHostnameEnvironmentVariable, value => settings.Http.Hostname = value);
            ApplyInt(Constants.McpHttpPortEnvironmentVariable, value => settings.Http.Port = value);
            ApplyString(Constants.McpTcpAddressEnvironmentVariable, value => settings.Tcp.Address = value);
            ApplyInt(Constants.McpTcpPortEnvironmentVariable, value => settings.Tcp.Port = value);
            ApplyString(Constants.McpWebSocketHostnameEnvironmentVariable, value => settings.WebSocket.Hostname = value);
            ApplyInt(Constants.McpWebSocketPortEnvironmentVariable, value => settings.WebSocket.Port = value);
        }

        private static void ApplyString(string environmentVariable, Action<string> setter)
        {
            string? value = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(value)) setter(value);
        }

        private static void ApplyInt(string environmentVariable, Action<int> setter)
        {
            string? value = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!int.TryParse(value, out int parsed)) return;
            if (parsed < 1 || parsed > 65535) return;
            setter(parsed);
        }
    }
}
