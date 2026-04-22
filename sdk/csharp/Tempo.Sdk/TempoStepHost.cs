namespace Tempo.Sdk
{
    using System;
    using System.Globalization;
    using System.IO;
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
            TextWriter? originalOut = null;
            TextWriter? originalErr = null;

            try
            {
                string text = await input.ReadToEndAsync(token).ConfigureAwait(false);
                request = DeserializeRequest(text);
                ITempoStepLogger logger = CreateLoggerFromEnvironment();
                executionScope = TempoExecutionContext.Push(new TempoExecutionContext
                {
                    TenantId = request.TenantId,
                    DataFlowId = request.DataFlowId,
                    FlowRunId = request.FlowRunId,
                    RunAssignmentId = Environment.GetEnvironmentVariable("TEMPO_RUN_ASSIGNMENT_ID"),
                    StepId = Environment.GetEnvironmentVariable("TEMPO_STEP_ID"),
                    StepRunId = request.StepRunId,
                    WorkerId = Environment.GetEnvironmentVariable("TEMPO_WORKER_ID"),
                    Logger = logger
                });
                if (logger is not NullTempoStepLogger)
                {
                    originalOut = Console.Out;
                    originalErr = Console.Error;
                    Console.SetOut(new TempoLogTextWriter(logger, "Info"));
                    Console.SetError(new TempoLogTextWriter(logger, "Error"));
                }

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
            }
        }

        private static ITempoStepLogger CreateLoggerFromEnvironment()
        {
            string? path = Environment.GetEnvironmentVariable("TEMPO_RUN_LOG_FILE");
            if (string.IsNullOrWhiteSpace(path)) return NullTempoStepLogger.Instance;
            return new FileTempoStepLogger(path!);
        }

        private static void RestoreConsole(TextWriter? originalOut, TextWriter? originalErr)
        {
            if (originalOut != null) Console.SetOut(originalOut);
            if (originalErr != null) Console.SetError(originalErr);
        }

        private sealed class FileTempoStepLogger : ITempoStepLogger
        {
            private readonly string _Path;
            private readonly object _Sync = new object();

            public FileTempoStepLogger(string path)
            {
                _Path = path;
            }

            public void Debug(string message) => Write("Debug", message);
            public void Info(string message) => Write("Info", message);
            public void Warn(string message) => Write("Warn", message);
            public void Error(string message) => Write("Error", message);

            private void Write(string severity, string? message)
            {
                if (string.IsNullOrWhiteSpace(message)) return;
                string directory = Path.GetDirectoryName(_Path) ?? ".";
                Directory.CreateDirectory(directory);

                lock (_Sync)
                {
                    using StreamWriter writer = new StreamWriter(new FileStream(_Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete));
                    foreach (string line in message.Replace("\r\n", "\n").Split('\n'))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        writer.WriteLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " [" + severity + "] " + line);
                    }
                    writer.Flush();
                }
            }
        }

        private sealed class TempoLogTextWriter : TextWriter
        {
            private readonly ITempoStepLogger _Logger;
            private readonly string _Severity;
            private readonly System.Text.StringBuilder _Buffer = new System.Text.StringBuilder();

            public TempoLogTextWriter(ITempoStepLogger logger, string severity)
            {
                _Logger = logger;
                _Severity = severity;
            }

            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

            public override void Write(char value)
            {
                if (value == '\r') return;
                if (value == '\n')
                {
                    FlushBuffer();
                    return;
                }

                _Buffer.Append(value);
            }

            public override void Write(string? value)
            {
                if (string.IsNullOrEmpty(value)) return;
                foreach (char c in value) Write(c);
            }

            public override void WriteLine(string? value)
            {
                Write(value);
                FlushBuffer();
            }

            public override void Flush()
            {
                FlushBuffer();
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
