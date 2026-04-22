namespace Tempo.Sdk.TestApp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Sdk;

    internal static class Program
    {
        private static async Task<int> Main()
        {
            TestPublicApiCoverage();
            TestProtocolVersions();
            TestRequestModel();
            TestResultModelAndHelpers();
            await TestRunnerSuccessAsync();
            await TestRunnerFailuresAsync();
            await TestRunLoggingAsync();
            Console.WriteLine("Tempo.Sdk C# test app PASS");
            return 0;
        }

        private static void TestPublicApiCoverage()
        {
            SortedSet<string> expected = new SortedSet<string>
            {
                "T:ITempoStepHandler", "M:ITempoStepHandler.RunAsync",
                "T:ITempoStepLogger", "M:ITempoStepLogger.Debug", "M:ITempoStepLogger.Info", "M:ITempoStepLogger.Warn", "M:ITempoStepLogger.Error",
                "T:ProtocolVersions", "F:ProtocolVersions.V1", "F:ProtocolVersions.Current", "F:ProtocolVersions.ProtocolVersionEnvironmentVariable", "F:ProtocolVersions.SupportedProtocolVersionsEnvironmentVariable", "P:ProtocolVersions.Supported", "M:ProtocolVersions.IsSupported", "M:ProtocolVersions.Normalize",
                "T:StepRequest", "C:StepRequest", "P:StepRequest.ProtocolVersion", "P:StepRequest.TenantId", "P:StepRequest.DataFlowId", "P:StepRequest.FlowRunId", "P:StepRequest.StepRunId", "P:StepRequest.RequestId", "P:StepRequest.Data", "P:StepRequest.Metadata", "P:StepRequest.PreviousResult",
                "T:StepResult", "C:StepResult", "P:StepResult.ProtocolVersion", "P:StepResult.TenantId", "P:StepResult.DataFlowId", "P:StepResult.FlowRunId", "P:StepResult.StepRunId", "P:StepResult.RequestId", "P:StepResult.Result", "P:StepResult.Data", "P:StepResult.Exception", "P:StepResult.ExceptionMessage", "P:StepResult.Metadata",
                "T:StepResultType", "E:StepResultType.Success", "E:StepResultType.Timeout", "E:StepResultType.Error", "E:StepResultType.Exception", "E:StepResultType.MaxIterationsExceeded",
                "T:TempoExecutionContext", "C:TempoExecutionContext", "P:TempoExecutionContext.Current", "P:TempoExecutionContext.TenantId", "P:TempoExecutionContext.DataFlowId", "P:TempoExecutionContext.FlowRunId", "P:TempoExecutionContext.RunAssignmentId", "P:TempoExecutionContext.StepId", "P:TempoExecutionContext.StepRunId", "P:TempoExecutionContext.WorkerId", "P:TempoExecutionContext.Logger",
                "T:TempoStepHost", "M:TempoStepHost.DeserializeRequest", "M:TempoStepHost.SerializeResult", "M:TempoStepHost.Correlate", "M:TempoStepHost.Success", "M:TempoStepHost.Error", "M:TempoStepHost.Exception", "M:TempoStepHost.RunAsync"
            };

            SortedSet<string> actual = new SortedSet<string>();
            foreach (Type type in typeof(ProtocolVersions).Assembly.GetTypes().Where(t => t.IsPublic && t.Namespace == "Tempo.Sdk"))
            {
                actual.Add("T:" + type.Name);
                foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) actual.Add("C:" + type.Name);
                if (!type.IsEnum)
                {
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)) actual.Add("F:" + type.Name + "." + field.Name);
                }
                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)) actual.Add("P:" + type.Name + "." + prop.Name);
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName)) actual.Add("M:" + type.Name + "." + method.Name);
                if (type.IsEnum)
                {
                    foreach (string name in Enum.GetNames(type)) actual.Add("E:" + type.Name + "." + name);
                }
            }

            Equal(string.Join("\n", expected), string.Join("\n", actual), "public API coverage inventory");
        }

        private static void TestProtocolVersions()
        {
            Equal("1.0", ProtocolVersions.V1, "v1");
            Equal("1.0", ProtocolVersions.Current, "current");
            Equal("TEMPO_PROTOCOL_VERSION", ProtocolVersions.ProtocolVersionEnvironmentVariable, "protocol env");
            Equal("TEMPO_SUPPORTED_PROTOCOL_VERSIONS", ProtocolVersions.SupportedProtocolVersionsEnvironmentVariable, "supported env");
            True(ProtocolVersions.IsSupported(null), "null defaults to current");
            True(ProtocolVersions.IsSupported(" 1.0 "), "trim supported");
            Equal("1.0", ProtocolVersions.Normalize("1.0"), "normalize");
            Throws<NotSupportedException>(() => ProtocolVersions.Normalize("9.9"), "unsupported normalize");
        }

        private static void TestRequestModel()
        {
            StepRequest defaults = new StepRequest();
            Equal("1.0", defaults.ProtocolVersion, "default protocol");
            AssertKSortableId(defaults.DataFlowId, "flow_", "default dataflow id");
            AssertKSortableId(defaults.RequestId, "req_", "default request id");
            Throws<ArgumentNullException>(() => defaults.DataFlowId = "", "dataflow empty");
            Throws<ArgumentNullException>(() => defaults.RequestId = "", "request empty");
            Throws<NotSupportedException>(() => defaults.ProtocolVersion = "9.9", "bad protocol");

            string json = "{\"protocolVersion\":\"1.0\",\"tenantId\":\"ten_1\",\"dataFlowId\":\"flow_1\",\"flowRunId\":\"run_1\",\"stepRunId\":\"sru_1\",\"requestId\":\"req_1\",\"data\":{\"x\":2},\"metadata\":{\"m\":3},\"previousResult\":\"Error\"}";
            StepRequest req = TempoStepHost.DeserializeRequest(json);
            Equal("ten_1", req.TenantId!, "tenant");
            Equal("flow_1", req.DataFlowId, "flow");
            Equal("run_1", req.FlowRunId!, "run");
            Equal("sru_1", req.StepRunId!, "step run");
            Equal("req_1", req.RequestId, "request");
            Equal(StepResultType.Error, req.PreviousResult!.Value, "previous result");
            Equal(2, ((JsonElement)req.Data!).GetProperty("x").GetInt32(), "data json");
            Equal(3, ((JsonElement)req.Metadata!).GetProperty("m").GetInt32(), "metadata json");
        }

        private static void TestResultModelAndHelpers()
        {
            StepRequest req = Request();
            StepResult success = TempoStepHost.Success(req, new { ok = true });
            Equal(StepResultType.Success, success.Result, "success result");
            Equal(req.RequestId, success.RequestId, "success correlation");
            Equal(req.Metadata, success.Metadata, "metadata fallback");

            StepResult error = TempoStepHost.Error(req, new { valid = false }, new { reason = "bad" });
            Equal(StepResultType.Error, error.Result, "error result");
            NotNull(error.Metadata, "error metadata");

            StepResult exception = TempoStepHost.Exception(req, new InvalidOperationException("boom"));
            Equal(StepResultType.Exception, exception.Result, "exception result");
            Equal("boom", exception.ExceptionMessage!, "exception message");
            Equal(req.DataFlowId, exception.DataFlowId, "exception correlation");

            StepResult noRequest = TempoStepHost.Exception(null, new Exception("no request"));
            Equal("unknown", noRequest.DataFlowId, "exception no request dataflow");
            AssertKSortableId(noRequest.RequestId, "req_", "exception no request request id");

            StepResult localException = new StepResult { Exception = new ApplicationException("local") };
            Equal("local", localException.ExceptionMessage!, "local exception projection");

            string serialized = TempoStepHost.SerializeResult(success);
            True(serialized.Contains("\"result\":\"Success\"", StringComparison.Ordinal), "enum serializes as string");
            Throws<ArgumentNullException>(() => TempoStepHost.SerializeResult(null!), "serialize null");
            Throws<ArgumentNullException>(() => TempoStepHost.Success(null!, null), "success null request");
            Throws<ArgumentNullException>(() => TempoStepHost.Exception(req, null!), "exception null");
        }

        private static async Task TestRunnerSuccessAsync()
        {
            StepRequest req = Request();
            string input = RequestJson(req);
            StringWriter output = new StringWriter();
            int code = await TempoStepHost.RunAsync(new SuccessHandler(), new StringReader(input), output);
            Equal(0, code, "run exit code");
            StepResult result = JsonSerializer.Deserialize<StepResult>(output.ToString(), JsonOptions())!;
            Equal(StepResultType.Success, result.Result, "run success");
            Equal(req.DataFlowId, result.DataFlowId, "run correlates dataflow");
            Equal(req.RequestId, result.RequestId, "run correlates request");
            Equal("ten_1", result.TenantId!, "run tenant");
            Equal(true, ((JsonElement)result.Data!).GetProperty("handled").GetBoolean(), "run data");
        }

        private static async Task TestRunnerFailuresAsync()
        {
            StringWriter invalidOutput = new StringWriter();
            int invalidCode = await TempoStepHost.RunAsync(new SuccessHandler(), new StringReader("not-json"), invalidOutput);
            Equal(0, invalidCode, "invalid run code");
            StepResult invalid = JsonSerializer.Deserialize<StepResult>(invalidOutput.ToString(), JsonOptions())!;
            Equal(StepResultType.Exception, invalid.Result, "invalid stdin exception");
            Equal("unknown", invalid.DataFlowId, "invalid dataflow");

            StringWriter throwingOutput = new StringWriter();
            int throwingCode = await TempoStepHost.RunAsync(new ThrowingHandler(), new StringReader(RequestJson(Request())), throwingOutput);
            Equal(0, throwingCode, "throw run code");
            StepResult throwing = JsonSerializer.Deserialize<StepResult>(throwingOutput.ToString(), JsonOptions())!;
            Equal(StepResultType.Exception, throwing.Result, "throw exception");
            Equal("handler boom", throwing.ExceptionMessage!, "throw message");

            StringWriter nullOutput = new StringWriter();
            int nullCode = await TempoStepHost.RunAsync(new NullHandler(), new StringReader(RequestJson(Request())), nullOutput);
            Equal(0, nullCode, "null handler code");
            StepResult nullResult = JsonSerializer.Deserialize<StepResult>(nullOutput.ToString(), JsonOptions())!;
            Equal(StepResultType.Exception, nullResult.Result, "null handler exception");
            Throws<ArgumentNullException>(() => TempoStepHost.RunAsync(null!).GetAwaiter().GetResult(), "null handler");
        }

        private static async Task TestRunLoggingAsync()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "tempo-sdk-log-" + Guid.NewGuid().ToString("N") + ".log");
            string? previousRunLog = Environment.GetEnvironmentVariable("TEMPO_RUN_LOG_FILE");
            string? previousAssignment = Environment.GetEnvironmentVariable("TEMPO_RUN_ASSIGNMENT_ID");
            string? previousStep = Environment.GetEnvironmentVariable("TEMPO_STEP_ID");
            string? previousWorker = Environment.GetEnvironmentVariable("TEMPO_WORKER_ID");

            Environment.SetEnvironmentVariable("TEMPO_RUN_LOG_FILE", logPath);
            Environment.SetEnvironmentVariable("TEMPO_RUN_ASSIGNMENT_ID", "ras_1");
            Environment.SetEnvironmentVariable("TEMPO_STEP_ID", "step_1");
            Environment.SetEnvironmentVariable("TEMPO_WORKER_ID", "wrk_1");

            try
            {
                StringWriter output = new StringWriter();
                int code = await TempoStepHost.RunAsync(new LoggingHandler(), new StringReader(RequestJson(Request())), output);
                Equal(0, code, "logging run exit code");

                StepResult result = JsonSerializer.Deserialize<StepResult>(output.ToString(), JsonOptions())!;
                Equal(StepResultType.Success, result.Result, "logging result");
                JsonElement data = (JsonElement)result.Data!;
                True(data.GetProperty("hasContext").GetBoolean(), "execution context available");
                Equal("ras_1", data.GetProperty("runAssignmentId").GetString(), "execution context assignment id");
                Equal("step_1", data.GetProperty("stepId").GetString(), "execution context step id");
                Equal("wrk_1", data.GetProperty("workerId").GetString(), "execution context worker id");

                True(File.Exists(logPath), "run log file created");
                string logText = await File.ReadAllTextAsync(logPath);
                True(logText.Contains("logger-info", StringComparison.Ordinal), "logger writes captured");
                True(logText.Contains("console-info", StringComparison.Ordinal), "console out redirected to log");
                True(logText.Contains("console-error", StringComparison.Ordinal), "console error redirected to log");
                True(!output.ToString().Contains("console-info", StringComparison.Ordinal), "protocol stdout remains clean");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TEMPO_RUN_LOG_FILE", previousRunLog);
                Environment.SetEnvironmentVariable("TEMPO_RUN_ASSIGNMENT_ID", previousAssignment);
                Environment.SetEnvironmentVariable("TEMPO_STEP_ID", previousStep);
                Environment.SetEnvironmentVariable("TEMPO_WORKER_ID", previousWorker);
                try { if (File.Exists(logPath)) File.Delete(logPath); } catch { /* ignore */ }
            }
        }

        private static StepRequest Request() => new StepRequest
        {
            ProtocolVersion = "1.0",
            TenantId = "ten_1",
            DataFlowId = "flow_1",
            FlowRunId = "run_1",
            StepRunId = "sru_1",
            RequestId = "req_1",
            Data = new { value = 5 },
            Metadata = new { trace = "abc" },
            PreviousResult = StepResultType.Success
        };

        private static string RequestJson(StepRequest request) => JsonSerializer.Serialize(request, JsonOptions());

        private static JsonSerializerOptions JsonOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            return options;
        }

        private sealed class SuccessHandler : ITempoStepHandler
        {
            public Task<StepResult> RunAsync(StepRequest request, CancellationToken token)
            {
                StepResult result = new StepResult
                {
                    ProtocolVersion = "1.0",
                    TenantId = "wrong",
                    DataFlowId = "wrong",
                    FlowRunId = "wrong",
                    StepRunId = "wrong",
                    RequestId = "wrong",
                    Result = StepResultType.Success,
                    Data = new { handled = true }
                };
                return Task.FromResult(result);
            }
        }

        private sealed class ThrowingHandler : ITempoStepHandler
        {
            public Task<StepResult> RunAsync(StepRequest request, CancellationToken token) => throw new InvalidOperationException("handler boom");
        }

        private sealed class NullHandler : ITempoStepHandler
        {
            public Task<StepResult> RunAsync(StepRequest request, CancellationToken token) => Task.FromResult<StepResult>(null!);
        }

        private sealed class LoggingHandler : ITempoStepHandler
        {
            public Task<StepResult> RunAsync(StepRequest request, CancellationToken token)
            {
                TempoExecutionContext? context = TempoExecutionContext.Current;
                context?.Logger.Info("logger-info");
                Console.WriteLine("console-info");
                Console.Error.WriteLine("console-error");
                return Task.FromResult(new StepResult
                {
                    Result = StepResultType.Success,
                    Data = new
                    {
                        hasContext = context != null,
                        runAssignmentId = context?.RunAssignmentId,
                        stepId = context?.StepId,
                        workerId = context?.WorkerId
                    }
                });
            }
        }

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(name + ": expected " + expected + " got " + actual);
        }

        private static void True(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name + ": expected true");
        }

        private static void NotNull(object? value, string name)
        {
            if (value == null) throw new InvalidOperationException(name + ": expected non-null");
        }

        private static void AssertKSortableId(string value, string prefix, string name)
        {
            True(value.StartsWith(prefix, StringComparison.Ordinal), name + " prefix");
            Equal(32, value.Length, name + " length");
            string[] parts = value.Substring(prefix.Length).Split('_');
            Equal(2, parts.Length, name + " segments");
            True(parts[0].Length > 0, name + " timestamp segment");
            True(parts[1].Length > 0, name + " random segment");
        }

        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception ex) { throw new InvalidOperationException(name + ": expected " + typeof(T).Name + " got " + ex.GetType().Name); }
            throw new InvalidOperationException(name + ": expected " + typeof(T).Name);
        }
    }
}
