namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core.Database;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Settings;
    using Tempo.Telemetry;

    /// <summary>
    /// Captures HTTP request/response pairs asynchronously (fire-and-forget).
    /// Redacts secrets from headers and truncates bodies beyond configured thresholds.
    /// </summary>
    public class RequestHistoryCaptureService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly RequestHistorySettings _Settings;
        private readonly LoggingModule? _Logging;
        private readonly string _Header = "[RequestHistoryCapture] ";
        private const string CaptureTask = "request_history_capture";

        private static readonly HashSet<string> _RedactHeaderSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "authorization",
            "proxy-authorization",
            "cookie",
            "set-cookie"
        };

        /// <summary>Instantiate.</summary>
        public RequestHistoryCaptureService(DatabaseDriverBase database, RequestHistorySettings settings, LoggingModule? logging = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging;
        }

        /// <summary>
        /// Persist a captured entry in the background. Errors are logged and swallowed.
        /// </summary>
        public void Capture(RequestHistoryEntry entry)
        {
            if (!_Settings.Enabled) return;
            if (entry == null) return;

            RedactHeaders(entry.RequestHeaders);
            RedactHeaders(entry.ResponseHeaders);
            TruncateBody(entry);

            TempoTelemetry.AddTaskPending(CaptureTask, 1);
            _ = Task.Run(async () =>
            {
                long start = Stopwatch.GetTimestamp();
                try
                {
                    await _Database.RequestHistory.CreateAsync(entry).ConfigureAwait(false);
                    TempoTelemetry.RecordTask(CaptureTask, TelemetryConstants.OutcomeSuccess, TempoTelemetry.SecondsSince(start));
                }
                catch (Exception ex)
                {
                    TempoTelemetry.RecordTask(CaptureTask, TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(start));
                    TempoTelemetry.RecordError(CaptureTask, ex);
                    _Logging?.Warn(LogMessages.WithoutTerminalPeriod(_Header + "capture failed: " + ex.Message));
                }
                finally
                {
                    TempoTelemetry.AddTaskPending(CaptureTask, -1);
                }
            });
        }

        private static void RedactHeaders(Dictionary<string, string> headers)
        {
            if (headers == null) return;
            List<string> keys = headers.Keys.ToList();
            foreach (string k in keys)
            {
                string lower = k.ToLowerInvariant();
                if (_RedactHeaderSuffixes.Contains(lower)
                    || lower.Contains("api-key")
                    || lower.Contains("token")
                    || lower.Contains("secret")
                    || lower.EndsWith("password"))
                {
                    headers[k] = Constants.RedactedValue;
                }
            }
        }

        private void TruncateBody(RequestHistoryEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.RequestBody))
            {
                long byteCount = Encoding.UTF8.GetByteCount(entry.RequestBody);
                entry.RequestBodyBytes = byteCount;
                if (byteCount > _Settings.MaxRequestBodyBytes && _Settings.MaxRequestBodyBytes > 0)
                {
                    entry.RequestBody = TakeUtf8Prefix(entry.RequestBody, _Settings.MaxRequestBodyBytes);
                    entry.RequestBodyTruncated = true;
                }
            }

            if (!string.IsNullOrEmpty(entry.ResponseBody))
            {
                long byteCount = Encoding.UTF8.GetByteCount(entry.ResponseBody);
                entry.ResponseBodyBytes = byteCount;
                if (byteCount > _Settings.MaxResponseBodyBytes && _Settings.MaxResponseBodyBytes > 0)
                {
                    entry.ResponseBody = TakeUtf8Prefix(entry.ResponseBody, _Settings.MaxResponseBodyBytes);
                    entry.ResponseBodyTruncated = true;
                }
            }
        }

        private static string TakeUtf8Prefix(string value, int maxBytes)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length <= maxBytes) return value;
            byte[] truncated = new byte[maxBytes];
            Array.Copy(bytes, 0, truncated, 0, maxBytes);
            return Encoding.UTF8.GetString(truncated);
        }
    }
}
