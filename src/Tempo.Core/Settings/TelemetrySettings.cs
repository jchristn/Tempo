namespace Tempo.Core.Settings
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Telemetry export settings for a Tempo process (server, worker, or MCP server). Tempo always emits on the
    /// <c>Tempo</c> and <c>Watson</c> meters and activity sources; these settings control whether and where the
    /// hosting process exports them (OTLP push to a collector, an in-process Prometheus scrape endpoint, and logs).
    /// Loopback defaults use <c>127.0.0.1</c> rather than <c>localhost</c>. Instances are not thread-safe; populate
    /// them before the telemetry host starts.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>Environment variable toggling telemetry export.</summary>
        public const string EnvEnabled = "TEMPO_TELEMETRY_ENABLED";

        /// <summary>Environment variable overriding the reported service name.</summary>
        public const string EnvServiceName = "TEMPO_TELEMETRY_SERVICE_NAME";

        /// <summary>Environment variable toggling OTLP push.</summary>
        public const string EnvOtlpEnabled = "TEMPO_TELEMETRY_OTLP_ENABLED";

        /// <summary>Environment variable overriding the OTLP endpoint.</summary>
        public const string EnvOtlpEndpoint = "TEMPO_TELEMETRY_OTLP_ENDPOINT";

        /// <summary>Environment variable overriding the OTLP protocol (grpc or httpprotobuf).</summary>
        public const string EnvOtlpProtocol = "TEMPO_TELEMETRY_OTLP_PROTOCOL";

        /// <summary>Environment variable toggling the in-process Prometheus endpoint.</summary>
        public const string EnvPrometheusEnabled = "TEMPO_TELEMETRY_PROMETHEUS_ENABLED";

        /// <summary>Environment variable overriding the Prometheus listener hostname.</summary>
        public const string EnvPrometheusHostname = "TEMPO_TELEMETRY_PROMETHEUS_HOSTNAME";

        /// <summary>Environment variable overriding the Prometheus listener port.</summary>
        public const string EnvPrometheusPort = "TEMPO_TELEMETRY_PROMETHEUS_PORT";

        /// <summary>Environment variable toggling log export.</summary>
        public const string EnvLogsEnabled = "TEMPO_TELEMETRY_LOGS_ENABLED";

        /// <summary>Environment variable toggling direct-to-Loki log export.</summary>
        public const string EnvLokiEnabled = "TEMPO_TELEMETRY_LOKI_ENABLED";

        /// <summary>Environment variable overriding the Loki OTLP endpoint.</summary>
        public const string EnvLokiEndpoint = "TEMPO_TELEMETRY_LOKI_ENDPOINT";

        /// <summary>Environment variable overriding the trace sampling ratio.</summary>
        public const string EnvSamplingRatio = "TEMPO_TELEMETRY_SAMPLING_RATIO";

        /// <summary>Whether this process exports telemetry at all. Default: true. When false nothing is subscribed or exported; emission stays a no-op.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Service name stamped as the OpenTelemetry <c>service.name</c> resource attribute.
        /// Default: null, meaning the process default (<c>tempo-server</c>, <c>tempo-worker</c>, or <c>tempo-mcp</c>).
        /// </summary>
        public string? ServiceName { get; set; } = null;

        /// <summary>Whether to push metrics, traces, and logs over OTLP. Default: true.</summary>
        public bool OtlpEnabled { get; set; } = true;

        /// <summary>
        /// OTLP endpoint (an OpenTelemetry Collector, or any OTLP backend). Default: <c>http://127.0.0.1:4317</c>.
        /// Use the gRPC port (4317) with protocol <c>grpc</c>, or the HTTP port (4318) with <c>httpprotobuf</c>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the value is not an absolute URI.</exception>
        public string OtlpEndpoint
        {
            get => _OtlpEndpoint;
            set
            {
                if (String.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out _))
                    throw new ArgumentException("OtlpEndpoint must be an absolute URI.", nameof(OtlpEndpoint));
                _OtlpEndpoint = value.Trim();
            }
        }

        /// <summary>OTLP protocol: <c>grpc</c> or <c>httpprotobuf</c>. Default: <c>grpc</c>. Unrecognized values are rejected.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is not grpc or httpprotobuf.</exception>
        public string OtlpProtocol
        {
            get => _OtlpProtocol;
            set
            {
                string normalized = (value ?? String.Empty).Trim().ToLowerInvariant();
                if (normalized != "grpc" && normalized != "httpprotobuf")
                    throw new ArgumentException("OtlpProtocol must be 'grpc' or 'httpprotobuf'.", nameof(OtlpProtocol));
                _OtlpProtocol = normalized;
            }
        }

        /// <summary>
        /// Whether to serve an in-process Prometheus scrape endpoint covering every subscribed meter. Default: false
        /// (metrics reach Prometheus through the collector). Enable when no collector is deployed.
        /// </summary>
        public bool PrometheusEnabled { get; set; } = false;

        /// <summary>
        /// Hostname the Prometheus listener binds. Default: <c>127.0.0.1</c>. Use <c>*</c> inside a container so a
        /// scraper on another container can reach it. Never expose it on a public interface.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null or whitespace.</exception>
        public string PrometheusHostname
        {
            get => _PrometheusHostname;
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PrometheusHostname));
                _PrometheusHostname = value.Trim();
            }
        }

        /// <summary>Port the Prometheus listener binds. Default: 9464. Range: 1 to 65535. Each process on one host needs its own port.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 1 to 65535.</exception>
        public int PrometheusPort
        {
            get => _PrometheusPort;
            set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException(nameof(PrometheusPort), "PrometheusPort must be between 1 and 65535.");
                _PrometheusPort = value;
            }
        }

        /// <summary>
        /// Whether to export this process's log messages as OpenTelemetry logs (correlated with the active trace).
        /// Default: true for the server and worker, which run background work. The MCP server forces this off.
        /// </summary>
        public bool LogsEnabled { get; set; } = true;

        /// <summary>
        /// Minimum log severity exported, on the OpenTelemetry scale used by Radiant: 0 verbose, 1 debug, 2 information,
        /// 3 warning, 4 error, 5 critical, 7 none. Default: 2 (information). Range: 0 to 7.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 0 to 7.</exception>
        public int LogsMinimumSeverity
        {
            get => _LogsMinimumSeverity;
            set
            {
                if (value < 0 || value > 7) throw new ArgumentOutOfRangeException(nameof(LogsMinimumSeverity), "LogsMinimumSeverity must be between 0 and 7.");
                _LogsMinimumSeverity = value;
            }
        }

        /// <summary>Whether to push logs directly to Loki's OTLP endpoint (no collector in the path). Default: false.</summary>
        public bool LokiEnabled { get; set; } = false;

        /// <summary>Loki OTLP base endpoint (the exporter appends <c>/v1/logs</c>). Default: <c>http://127.0.0.1:3100/otlp</c>.</summary>
        /// <exception cref="ArgumentException">Thrown when the value is not an absolute URI.</exception>
        public string LokiEndpoint
        {
            get => _LokiEndpoint;
            set
            {
                if (String.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out _))
                    throw new ArgumentException("LokiEndpoint must be an absolute URI.", nameof(LokiEndpoint));
                _LokiEndpoint = value.Trim();
            }
        }

        /// <summary>Parent-based trace sampling ratio. Default: 1.0 (sample everything). Range: 0.0 to 1.0.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 0.0 to 1.0.</exception>
        public double SamplingRatio
        {
            get => _SamplingRatio;
            set
            {
                if (Double.IsNaN(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(SamplingRatio), "SamplingRatio must be between 0.0 and 1.0.");
                _SamplingRatio = value;
            }
        }

        /// <summary>OTLP metric export interval in milliseconds. Default: 15000. Range: 1000 to 300000.</summary>
        public int MetricsExportIntervalMs
        {
            get => _MetricsExportIntervalMs;
            set => _MetricsExportIntervalMs = Math.Clamp(value, 1000, 300000);
        }

        /// <summary>Whether to include .NET runtime and process metrics (GC, thread pool, working set). Default: true.</summary>
        public bool IncludeRuntimeMetrics { get; set; } = true;

        #endregion

        #region Private-Members

        private string _OtlpEndpoint = "http://127.0.0.1:4317";
        private string _OtlpProtocol = "grpc";
        private string _PrometheusHostname = "127.0.0.1";
        private int _PrometheusPort = 9464;
        private int _LogsMinimumSeverity = 2;
        private string _LokiEndpoint = "http://127.0.0.1:3100/otlp";
        private double _SamplingRatio = 1.0;
        private int _MetricsExportIntervalMs = 15000;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply <c>TEMPO_TELEMETRY_*</c> environment variable overrides. Unset or unparseable values are ignored.
        /// </summary>
        public void ApplyEnvironmentOverrides()
        {
            string? v;

            v = Environment.GetEnvironmentVariable(EnvEnabled);
            if (!String.IsNullOrEmpty(v) && Boolean.TryParse(v, out bool enabled)) Enabled = enabled;

            v = Environment.GetEnvironmentVariable(EnvServiceName);
            if (!String.IsNullOrWhiteSpace(v)) ServiceName = v.Trim();

            v = Environment.GetEnvironmentVariable(EnvOtlpEnabled);
            if (!String.IsNullOrEmpty(v) && Boolean.TryParse(v, out bool otlpEnabled)) OtlpEnabled = otlpEnabled;

            v = Environment.GetEnvironmentVariable(EnvOtlpEndpoint);
            if (!String.IsNullOrWhiteSpace(v) && Uri.TryCreate(v, UriKind.Absolute, out _)) OtlpEndpoint = v;

            v = Environment.GetEnvironmentVariable(EnvOtlpProtocol);
            if (!String.IsNullOrWhiteSpace(v) && (v.Trim().Equals("grpc", StringComparison.OrdinalIgnoreCase) || v.Trim().Equals("httpprotobuf", StringComparison.OrdinalIgnoreCase))) OtlpProtocol = v;

            v = Environment.GetEnvironmentVariable(EnvPrometheusEnabled);
            if (!String.IsNullOrEmpty(v) && Boolean.TryParse(v, out bool prometheusEnabled)) PrometheusEnabled = prometheusEnabled;

            v = Environment.GetEnvironmentVariable(EnvPrometheusHostname);
            if (!String.IsNullOrWhiteSpace(v)) PrometheusHostname = v;

            v = Environment.GetEnvironmentVariable(EnvPrometheusPort);
            if (!String.IsNullOrEmpty(v) && Int32.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) && port >= 1 && port <= 65535) PrometheusPort = port;

            v = Environment.GetEnvironmentVariable(EnvLogsEnabled);
            if (!String.IsNullOrEmpty(v) && Boolean.TryParse(v, out bool logsEnabled)) LogsEnabled = logsEnabled;

            v = Environment.GetEnvironmentVariable(EnvLokiEnabled);
            if (!String.IsNullOrEmpty(v) && Boolean.TryParse(v, out bool lokiEnabled)) LokiEnabled = lokiEnabled;

            v = Environment.GetEnvironmentVariable(EnvLokiEndpoint);
            if (!String.IsNullOrWhiteSpace(v) && Uri.TryCreate(v, UriKind.Absolute, out _)) LokiEndpoint = v;

            v = Environment.GetEnvironmentVariable(EnvSamplingRatio);
            if (!String.IsNullOrEmpty(v) && Double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio) && ratio >= 0 && ratio <= 1) SamplingRatio = ratio;
        }

        #endregion
    }
}
