namespace Tempo.Hosting
{
    using System;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Radiant;
    using SyslogLogging;
    using Tempo.Core.Settings;
    using Tempo.Telemetry;

    /// <summary>
    /// The single telemetry host for a Tempo process. Wraps one Radiant host at the composition root that subscribes
    /// to the <c>Tempo</c> and <c>Watson</c> meters and activity sources, exports over OTLP and optionally an in-process
    /// Prometheus endpoint, adds .NET runtime metrics, and (for processes with background work) forwards log messages
    /// as trace-correlated OpenTelemetry logs.
    /// <para>
    /// Telemetry is best-effort: a start failure is logged as a warning and the process runs without export.
    /// Instances are thread-safe; dispose once at shutdown to flush exporters and release the scrape port.
    /// </para>
    /// </summary>
    public sealed class TelemetryHost : IDisposable
    {
        #region Public-Members

        /// <summary>Whether a Radiant host is running and exporting. False when disabled by settings or when start failed.</summary>
        public bool IsEnabled => _Host != null;

        /// <summary>The resolved service name stamped as <c>service.name</c>. Never null.</summary>
        public string ServiceName { get; }

        #endregion

        #region Private-Members

        private readonly RadiantHost? _Host;
        private readonly bool _LogsEnabled;
        private readonly object _Lock = new object();
        private LoggingModule? _BridgedLogging = null;
        private Action<LogEntry>? _BridgeHandler = null;
        private int _Disposed = 0;

        private const string WatsonRequestDuration = "http.server.request.duration";
        private static readonly double[] HttpDurationBuckets = new double[] { 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30 };

        #endregion

        #region Constructors-and-Factories

        private TelemetryHost(string serviceName, RadiantHost? host, bool logsEnabled)
        {
            ServiceName = serviceName;
            _Host = host;
            _LogsEnabled = logsEnabled;
        }

        /// <summary>
        /// Start the telemetry host for this process. Never throws: on failure a warning is written to
        /// <paramref name="logging"/> (when supplied) and an inert host is returned.
        /// </summary>
        /// <param name="settings">Telemetry settings. Null is treated as defaults.</param>
        /// <param name="defaultServiceName">Service name used when <see cref="TelemetrySettings.ServiceName"/> is not set (for example tempo-server).</param>
        /// <param name="component">Bounded process role reported on <c>tempo.build.info</c> (server, worker, or mcp).</param>
        /// <param name="logging">Optional logger for start-up and exporter diagnostics. May be null.</param>
        /// <param name="allowLogs">Whether this process may export logs. Pass false for processes without background work. Default: true.</param>
        /// <returns>A started (or inert) telemetry host. Never null.</returns>
        public static TelemetryHost Start(TelemetrySettings? settings, string defaultServiceName, string component, LoggingModule? logging = null, bool allowLogs = true)
        {
            settings ??= new TelemetrySettings();
            string serviceName = String.IsNullOrWhiteSpace(settings.ServiceName)
                ? (String.IsNullOrWhiteSpace(defaultServiceName) ? "tempo" : defaultServiceName.Trim())
                : settings.ServiceName.Trim();
            TempoTelemetry.Component = component;

            if (!settings.Enabled)
            {
                logging?.Info("[TelemetryHost] telemetry export disabled by settings");
                return new TelemetryHost(serviceName, null, false);
            }

            bool logsEnabled = allowLogs && settings.LogsEnabled;

            try
            {
                RadiantSettings radiant = new RadiantSettings(serviceName);
                radiant.Sources.AddMeter(TelemetryConstants.MeterName);
                radiant.Sources.AddActivitySource(TelemetryConstants.ActivitySourceName);
                radiant.Sources.AddMeter(TelemetryConstants.WatsonSourceName);
                radiant.Sources.AddActivitySource(TelemetryConstants.WatsonSourceName);
                radiant.Sources.AddActivitySource(TelemetryConstants.HttpClientSourceName);

                radiant.Otlp.Enable = settings.OtlpEnabled;
                radiant.Otlp.Endpoint = settings.OtlpEndpoint;
                radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                    ? OtlpProtocolEnum.HttpProtobuf
                    : OtlpProtocolEnum.Grpc;

                radiant.Prometheus.Enable = settings.PrometheusEnabled;
                radiant.Prometheus.Hostname = settings.PrometheusHostname;
                radiant.Prometheus.Port = settings.PrometheusPort;

                // Watson leaves histogram buckets to the collector; without a view its seconds-unit request
                // duration would use the SDK's millisecond-style defaults and every request would land in one bucket.
                radiant.Metrics.Define(Convention.Histogram(WatsonRequestDuration, "s", HttpDurationBuckets));
                radiant.Metrics.LabelPolicy = LabelPolicyEnum.Lenient;
                radiant.Metrics.ExportIntervalMs = settings.MetricsExportIntervalMs;
                radiant.Metrics.IncludeRuntime = settings.IncludeRuntimeMetrics;
                radiant.Metrics.IncludeProcess = settings.IncludeRuntimeMetrics;

                radiant.Traces.SamplingRatio = settings.SamplingRatio;
                radiant.Traces.PropagateContext = true;

                radiant.Logs.Enable = logsEnabled;
                radiant.Logs.MinimumSeverity = settings.LogsMinimumSeverity;
                radiant.Logs.IncludeTraceCorrelation = true;
                radiant.Loki.Enable = logsEnabled && settings.LokiEnabled;
                radiant.Loki.Endpoint = settings.LokiEndpoint;
                radiant.Loki.MinimumSeverity = settings.LogsMinimumSeverity;

                if (logging != null)
                {
                    radiant.DiagnosticCallback = message => logging.Debug("[TelemetryHost] " + message);
                }

                RadiantHost host = RadiantHost.Start(radiant);
                logging?.Info(
                    "[TelemetryHost] telemetry enabled for " + serviceName +
                    (settings.OtlpEnabled ? ", OTLP " + settings.OtlpProtocol + " to " + settings.OtlpEndpoint : ", OTLP off") +
                    (settings.PrometheusEnabled ? ", Prometheus at " + radiant.Prometheus.ToScrapeUrl() : string.Empty) +
                    (logsEnabled ? ", logs exported" : string.Empty));
                return new TelemetryHost(serviceName, host, logsEnabled);
            }
            catch (Exception ex)
            {
                logging?.Warn("[TelemetryHost] telemetry disabled, start failed: " + ex.Message);
                return new TelemetryHost(serviceName, null, false);
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Forward every message written to <paramref name="logging"/> from now on as an OpenTelemetry log record,
        /// stamped with the active trace and span. Call after any start-up messages that may contain secrets have
        /// been written. No-op when log export is disabled or a module is already bridged. Thread-safe.
        /// </summary>
        /// <param name="logging">The logging module to bridge. Null is ignored.</param>
        public void BridgeLogs(LoggingModule? logging)
        {
            if (logging == null || _Host == null || !_LogsEnabled) return;

            lock (_Lock)
            {
                if (_BridgedLogging != null || Volatile.Read(ref _Disposed) != 0) return;

                try
                {
                    ILogger logger = _Host.CreateLogger("Tempo." + TempoTelemetry.Component);
                    Action<LogEntry> handler = entry => Forward(logger, entry);
                    logging.MessageLogged += handler;
                    _BridgedLogging = logging;
                    _BridgeHandler = handler;
                }
                catch (Exception ex)
                {
                    logging.Warn("[TelemetryHost] log export unavailable: " + ex.Message);
                }
            }
        }

        /// <summary>Flush pending telemetry. Best-effort; never throws.</summary>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds. Default: 5000.</param>
        public void Flush(int timeoutMs = 5000)
        {
            try { _Host?.ForceFlush(Math.Max(0, timeoutMs)); } catch (Exception) { }
        }

        /// <summary>Detach the log bridge, flush, and stop exporting. Safe to call more than once.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) != 0) return;

            lock (_Lock)
            {
                if (_BridgedLogging != null && _BridgeHandler != null)
                {
                    try { _BridgedLogging.MessageLogged -= _BridgeHandler; } catch (Exception) { }
                }

                _BridgedLogging = null;
                _BridgeHandler = null;
            }

            try { _Host?.Dispose(); } catch (Exception) { }
        }

        #endregion

        #region Private-Methods

        private static void Forward(ILogger logger, LogEntry entry)
        {
            if (entry == null) return;
            LogLevel level = entry.Severity switch
            {
                Severity.Debug => LogLevel.Debug,
                Severity.Info => LogLevel.Information,
                Severity.Warn => LogLevel.Warning,
                Severity.Error => LogLevel.Error,
                Severity.Alert => LogLevel.Error,
                Severity.Critical => LogLevel.Critical,
                Severity.Emergency => LogLevel.Critical,
                _ => LogLevel.Information
            };

            if (!logger.IsEnabled(level)) return;
            logger.Log(level, 0, entry.Message, entry.Exception, (message, _) => message ?? String.Empty);
        }

        #endregion
    }
}
