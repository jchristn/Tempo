namespace Tempo.Sdk
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>stdin/stdout host and result helpers for Tempo step handlers.</summary>
    public static class TempoStepHost
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Deserialize a request envelope from JSON.</summary>
        public static StepRequest DeserializeRequest(string json)
        {
            StepRequest? request = JsonSerializer.Deserialize<StepRequest>(json, _Json);
            return request ?? throw new InvalidOperationException("JSON did not contain a StepRequest.");
        }

        /// <summary>Serialize a result envelope to JSON.</summary>
        public static string SerializeResult(StepResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return JsonSerializer.Serialize(result, _Json);
        }

        /// <summary>Copy host-owned correlation fields from the request to the result.</summary>
        public static StepResult Correlate(StepResult result, StepRequest request)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (request == null) throw new ArgumentNullException(nameof(request));
            result.ProtocolVersion = request.ProtocolVersion;
            result.TenantId = request.TenantId;
            result.DataFlowId = request.DataFlowId;
            result.FlowRunId = request.FlowRunId;
            result.StepRunId = request.StepRunId;
            result.RequestId = request.RequestId;
            return result;
        }

        /// <summary>Create a successful result correlated to the request.</summary>
        public static StepResult Success(StepRequest request, object? data, object? metadata = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Correlate(new StepResult
            {
                Result = StepResultType.Success,
                Data = data,
                Metadata = metadata ?? request.Metadata
            }, request);
        }

        /// <summary>Create an error result correlated to the request.</summary>
        public static StepResult Error(StepRequest request, object? data, object? metadata = null)
        {
            StepResult result = Success(request, data, metadata);
            result.Result = StepResultType.Error;
            return result;
        }

        /// <summary>Create an exception result correlated to the request when available.</summary>
        public static StepResult Exception(StepRequest? request, Exception exception, object? metadata = null)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            StepResult result = new StepResult
            {
                ProtocolVersion = request?.ProtocolVersion ?? ProtocolVersions.Current,
                TenantId = request?.TenantId,
                DataFlowId = request?.DataFlowId ?? "unknown",
                FlowRunId = request?.FlowRunId,
                StepRunId = request?.StepRunId,
                RequestId = request?.RequestId ?? Ids.RequestId(),
                Result = StepResultType.Exception,
                ExceptionMessage = exception.Message,
                Metadata = metadata ?? request?.Metadata
            };
            return result;
        }

        /// <summary>Run a handler by reading one request from stdin and writing one result to stdout.</summary>
        public static async Task<int> RunAsync(ITempoStepHandler handler, TextReader? input = null, TextWriter? output = null, CancellationToken token = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            input ??= Console.In;
            output ??= Console.Out;
            StepRequest? request = null;
            IDisposable? executionScope = null;
            ITempoStepLogger? logger = null;
            TextWriter? originalOut = null;
            TextWriter? originalErr = null;

            try
            {
                string text = await input.ReadToEndAsync(token).ConfigureAwait(false);
                request = DeserializeRequest(text);
                logger = CreateLoggerFromEnvironment();
                executionScope = TempoExecutionContext.Push(new TempoExecutionContext
                {
                    TenantId = request.TenantId,
                    DataFlowId = request.DataFlowId,
                    FlowRunId = request.FlowRunId,
                    RunAssignmentId = Environment.GetEnvironmentVariable("TEMPO_RUN_ASSIGNMENT_ID"),
                    StepId = Environment.GetEnvironmentVariable("TEMPO_STEP_ID"),
                    StepRunId = request.StepRunId,
                    RequestId = request.RequestId,
                    WorkerId = Environment.GetEnvironmentVariable("TEMPO_WORKER_ID"),
                    Logger = logger
                });
                RedirectConsole(logger, out originalOut, out originalErr);

                StepResult result = await handler.RunAsync(request, token).ConfigureAwait(false);
                if (result == null) throw new InvalidOperationException("Handler returned null StepResult.");
                RestoreConsole(originalOut, originalErr);
                await output.WriteAsync(SerializeResult(Correlate(result, request))).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                RestoreConsole(originalOut, originalErr);
                await output.WriteAsync(SerializeResult(Exception(request, ex))).ConfigureAwait(false);
                return 0;
            }
            finally
            {
                RestoreConsole(originalOut, originalErr);
                executionScope?.Dispose();
                if (logger is IDisposable disposable) disposable.Dispose();
            }
        }

        private static ITempoStepLogger CreateLoggerFromEnvironment()
        {
            string? path = Environment.GetEnvironmentVariable("TEMPO_RUN_LOG_FILE");
            if (string.IsNullOrWhiteSpace(path)) return ConsoleTempoStepLogger.Instance;
            return new FileTempoStepLogger(path!);
        }

        private static void RedirectConsole(ITempoStepLogger logger, out TextWriter? originalOut, out TextWriter? originalErr)
        {
            originalOut = Console.Out;
            Console.SetOut(new TempoLogTextWriter(logger, "Info"));
            if (logger is FileTempoStepLogger)
            {
                originalErr = Console.Error;
                Console.SetError(new TempoLogTextWriter(logger, "Error"));
                return;
            }

            originalErr = null;
        }

        private static void RestoreConsole(TextWriter? originalOut, TextWriter? originalErr)
        {
            if (originalOut != null) Console.SetOut(originalOut);
            if (originalErr != null) Console.SetError(originalErr);
        }

        private sealed class FileTempoStepLogger : ITempoStepLogger, IDisposable
        {
            private readonly object _Sync = new object();
            private readonly StreamWriter _Writer;
            private bool _Disposed;

            public FileTempoStepLogger(string path)
            {
                string directory = Path.GetDirectoryName(path) ?? ".";
                Directory.CreateDirectory(directory);
                FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                _Writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            }

            public void Debug(string message) => Write("Debug", message);
            public void Info(string message) => Write("Info", message);
            public void Warn(string message) => Write("Warn", message);
            public void Error(string message) => Write("Error", message);

            public void Dispose()
            {
                lock (_Sync)
                {
                    if (_Disposed) return;
                    _Writer.Dispose();
                    _Disposed = true;
                }
            }

            private void Write(string severity, string? message)
            {
                if (string.IsNullOrWhiteSpace(message)) return;
                string[] lines = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                lock (_Sync)
                {
                    if (_Disposed) return;
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        _Writer.WriteLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " [" + severity + "] " + line);
                    }
                    _Writer.Flush();
                }
            }
        }

        private sealed class TempoLogTextWriter : TextWriter
        {
            private readonly ITempoStepLogger _Logger;
            private readonly string _Severity;
            private readonly StringBuilder _Buffer = new StringBuilder();
            private readonly object _Sync = new object();

            public TempoLogTextWriter(ITempoStepLogger logger, string severity)
            {
                _Logger = logger;
                _Severity = severity;
            }

            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

            public override void Write(char value)
            {
                lock (_Sync)
                {
                    if (value == '\r') return;
                    if (value == '\n')
                    {
                        FlushBuffer();
                        return;
                    }

                    _Buffer.Append(value);
                }
            }

            public override void Write(string? value)
            {
                if (string.IsNullOrEmpty(value)) return;
                lock (_Sync)
                {
                    WriteLocked(value);
                }
            }

            public override void WriteLine(string? value)
            {
                lock (_Sync)
                {
                    WriteLocked(value);
                    FlushBuffer();
                }
            }

            public override void Flush()
            {
                lock (_Sync)
                {
                    FlushBuffer();
                }
            }

            private void WriteLocked(string? value)
            {
                if (string.IsNullOrEmpty(value)) return;
                foreach (char c in value)
                {
                    if (c == '\r') continue;
                    if (c == '\n')
                    {
                        FlushBuffer();
                        continue;
                    }

                    _Buffer.Append(c);
                }
            }

            private void FlushBuffer()
            {
                if (_Buffer.Length < 1) return;
                string line = _Buffer.ToString();
                _Buffer.Clear();
                if (string.Equals(_Severity, "Error", StringComparison.Ordinal))
                    _Logger.Error(line);
                else
                    _Logger.Info(line);
            }
        }
    }
}
