namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Tempo.Enums;
    using Tempo.Hosting;
    using Tempo.McpServer.Services;
    using Tempo.McpServer.Settings;
    using Tempo.McpServer.Tools;
    using Tempo.Runners;
    using Tempo.Server;
    using Tempo.Telemetry;
    using Touchstone.Core;
    using CoreTenant = Tempo.Core.Models.Tenant;

    /// <summary>
    /// Proves Tempo emits the metrics and spans documented in TELEMETRY.md: engine flows and steps, failure paths,
    /// outbound integrations with W3C propagation (HTTP, subprocess, worker websocket, MCP to server), the dispatch
    /// pipeline, limiters, caches, background tasks, security, gauges, export through the telemetry host, and bounded labels.
    /// </summary>
    public static class TelemetrySuite
    {
        private static readonly HashSet<string> _AllowedMetricLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            TelemetryConstants.AttrOutcome, TelemetryConstants.AttrRunner, TelemetryConstants.AttrPipeline, TelemetryConstants.AttrStage,
            TelemetryConstants.AttrTask, TelemetryConstants.AttrAction, TelemetryConstants.AttrService, TelemetryConstants.AttrOperation,
            TelemetryConstants.AttrPath, TelemetryConstants.AttrSource, TelemetryConstants.AttrNodeKind, TelemetryConstants.AttrState,
            TelemetryConstants.AttrReason, TelemetryConstants.AttrEvent, TelemetryConstants.AttrDirection, TelemetryConstants.AttrFrame,
            TelemetryConstants.AttrCache, TelemetryConstants.AttrLimiter, TelemetryConstants.AttrMethod, TelemetryConstants.AttrTool,
            TelemetryConstants.AttrComponent, TelemetryConstants.AttrErrorType, TelemetryConstants.AttrVersion, TelemetryConstants.AttrSetting
        };

        /// <summary>Build the suite descriptor.</summary>
        /// <returns>The suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Telemetry",
                displayName: "Telemetry (metrics and traces)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Telemetry", "NoListenerIsSafe", "Emission without any listener never throws, including null and empty inputs", NoListenerIsSafeAsync),
                    new TestCaseDescriptor("Telemetry", "EngineFlowEmitsFlowAndStepTelemetry", "An engine data flow emits a flow span with child step spans, flow/step counters and histograms, and transitions", EngineFlowEmitsFlowAndStepTelemetryAsync),
                    new TestCaseDescriptor("Telemetry", "StepFailurePathsAreRecorded", "Error, exception, and timeout step results are recorded with outcomes, error spans, and exception events", StepFailurePathsAreRecordedAsync),
                    new TestCaseDescriptor("Telemetry", "RestStepPropagatesTraceContext", "REST steps emit an HTTP client span and integration metrics and send a W3C traceparent; unreachable targets record an exception", RestStepPropagatesTraceContextAsync),
                    new TestCaseDescriptor("Telemetry", "DatabaseCallsEmitIntegrationTelemetry", "Database calls emit integration metrics and child client spans without SQL text; failures are counted as errors", DatabaseCallsEmitIntegrationTelemetryAsync),
                    new TestCaseDescriptor("Telemetry", "CapacityLimiterTelemetry", "The external runtime limiter records waits and balanced in-use and queued counts", CapacityLimiterTelemetryAsync),
                    new TestCaseDescriptor("Telemetry", "ProcessStepPropagatesTraceContext", "Artifact process steps emit a process client span with exit code, cache telemetry, and pass TRACEPARENT to the child", ProcessStepPropagatesTraceContextAsync),
                    new TestCaseDescriptor("Telemetry", "LocalDispatchPipelineIsOneTrace", "Enqueue, schedule, assign, execute, flow, and completion share one trace on the server-local path, with per-stage metrics and gauges", LocalDispatchPipelineIsOneTraceAsync),
                    new TestCaseDescriptor("Telemetry", "RemoteWorkerJoinsDispatchTrace", "A remote worker's execution and the server's completion join the dispatch trace across the websocket", RemoteWorkerJoinsDispatchTraceAsync),
                    new TestCaseDescriptor("Telemetry", "EnqueueFailureIsRecorded", "A rejected enqueue is counted and its span is marked failed", EnqueueFailureIsRecordedAsync),
                    new TestCaseDescriptor("Telemetry", "McpToolCallsPropagateToServer", "MCP tool calls emit tool and client spans and metrics, and the server's request span joins the MCP trace; failures are recorded", McpToolCallsPropagateToServerAsync),
                    new TestCaseDescriptor("Telemetry", "AuthenticationOutcomesAreCounted", "Authentication attempts are counted by method and outcome, and authorization decisions by outcome", AuthenticationOutcomesAreCountedAsync),
                    new TestCaseDescriptor("Telemetry", "BackgroundTasksRecordRunsAndLastSuccess", "Background request-history capture records runs, balanced pending items, and a last-success timestamp", BackgroundTasksRecordRunsAndLastSuccessAsync),
                    new TestCaseDescriptor("Telemetry", "BuildInfoAndConfigGauges", "Build-info and configuration gauges are observable", BuildInfoAndConfigGaugesAsync),
                    new TestCaseDescriptor("Telemetry", "TelemetryHostExportsMetricsTracesAndLogs", "The telemetry host serves Prometheus metrics and pushes OTLP traces and trace-correlated logs", TelemetryHostExportsMetricsTracesAndLogsAsync),
                    new TestCaseDescriptor("Telemetry", "TelemetryHostDisabledOrFailingIsInert", "A disabled or failing telemetry host never throws and reports itself inert", TelemetryHostDisabledOrFailingIsInertAsync),
                    new TestCaseDescriptor("Telemetry", "TelemetrySettingsValidateAndApplyEnvironment", "Telemetry settings validate their ranges and apply TEMPO_TELEMETRY_* overrides", TelemetrySettingsValidateAndApplyEnvironmentAsync),
                    new TestCaseDescriptor("Telemetry", "MetricLabelsStayBounded", "Metric labels use only documented keys and never carry identifiers or free-form text", MetricLabelsStayBoundedAsync)
                });
        }

        private static async Task NoListenerIsSafeAsync(CancellationToken ct)
        {
            StepResult result = await RunEngineFlowAsync(new[] { "succeed" }, ct).ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "flow ran without listeners");

            TempoTelemetry.RecordFlowRun(null!, null!, -1);
            TempoTelemetry.RecordStep(string.Empty, string.Empty, double.NaN);
            TempoTelemetry.RecordIntegration(null!, null!, null!, 0);
            TempoTelemetry.RecordStage(null!, null!, null!, 0);
            TempoTelemetry.RecordTask(null!, null!, 0);
            TempoTelemetry.RecordTaskItems("x", "y", 0);
            TempoTelemetry.RecordError(null!, (Exception?)null);
            TempoTelemetry.SetConfig(null!, 1);
            TempoTelemetry.MarkSuccess(null!);
            TempoTelemetry.SetOk(null);
            TempoTelemetry.SetError(null, "x");
            TempoTelemetry.RecordException(null, new InvalidOperationException());
            TempoTelemetry.StopActivityAt(null, DateTimeOffset.UtcNow);
            Assert2.IsNull(TempoTelemetry.StartActivity(string.Empty), "empty span name yields null");
            using (Activity? orphan = TempoTelemetry.StartIntegration("sqlite", "select", requireParent: true)) { }
            using (Activity? bad = TempoTelemetry.StartActivity("tempo.test", ActivityKind.Internal, "not-a-traceparent")) { }
        }

        private static async Task EngineFlowEmitsFlowAndStepTelemetryAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            StepResult result = await RunEngineFlowAsync(new[] { "succeed", "succeed" }, ct).ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "flow succeeded");

            Activity flow = capture.Spans(TelemetryConstants.SpanFlowRun).Last(a => (a.GetTagItem(TelemetryConstants.AttrFlowRunId) as string) == result.RequestId);
            Assert2.Equal(ActivityStatusCode.Ok, flow.Status, "flow span ok");
            Assert2.Equal("engine", flow.GetTagItem(TelemetryConstants.AttrRunner) as string, "flow runner tag");

            List<Activity> steps = capture.Activities.Where(a => a.OperationName.StartsWith(TelemetryConstants.SpanStepPrefix, StringComparison.Ordinal) && a.TraceId == flow.TraceId).ToList();
            Assert2.Equal(2, steps.Count, "two step spans in the flow trace");
            Assert2.True(steps.All(s => s.ParentSpanId == flow.SpanId), "step spans are children of the flow span");
            Assert2.True(steps.All(s => (s.GetTagItem(TelemetryConstants.AttrRunner) as string) == "code_attribute"), "step runner label");

            Assert2.True(capture.Sum(TelemetryConstants.FlowRuns, "runner=engine", "outcome=success") >= 1, "flow run counted");
            Assert2.True(capture.Count(TelemetryConstants.FlowDuration, "runner=engine", "outcome=success") >= 1, "flow duration recorded");
            Assert2.True(capture.Sum(TelemetryConstants.StepExecutions, "runner=code_attribute", "outcome=success") >= 2, "step executions counted");
            Assert2.True(capture.Count(TelemetryConstants.StepDuration, "runner=code_attribute", "outcome=success") >= 2, "step duration recorded");
            Assert2.True(capture.Sum(TelemetryConstants.StepTransitions, "path=on_success") >= 1, "on_success transition");
            Assert2.True(capture.Sum(TelemetryConstants.StepTransitions, "path=terminal") >= 1, "terminal transition");
            Assert2.Equal(0.0, capture.Sum(TelemetryConstants.FlowActive, "runner=engine"), "active flows return to zero");
        }

        private static async Task StepFailurePathsAreRecordedAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();

            StepResult error = await RunEngineFlowAsync(new[] { "fail" }, ct).ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Error, error.Result, "error result");
            StepResult exception = await RunEngineFlowAsync(new[] { "throw" }, ct).ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Exception, exception.Result, "exception result");
            StepResult timeout = await RunEngineFlowAsync(new[] { "slow" }, ct, stepTimeoutMs: 100).ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Timeout, timeout.Result, "timeout result");

            Assert2.True(capture.Sum(TelemetryConstants.StepExecutions, "outcome=error") >= 1, "error step counted");
            Assert2.True(capture.Sum(TelemetryConstants.StepExecutions, "outcome=exception") >= 1, "exception step counted");
            Assert2.True(capture.Sum(TelemetryConstants.StepExecutions, "outcome=timeout") >= 1, "timeout step counted");
            Assert2.True(capture.Sum(TelemetryConstants.FlowRuns, "runner=engine", "outcome=exception") >= 1, "exception flow counted");
            Assert2.True(capture.Sum(TelemetryConstants.FlowRuns, "runner=engine", "outcome=timeout") >= 1, "timeout flow counted");

            Activity thrown = capture.Activities.Last(a => a.OperationName == TelemetryConstants.SpanStepPrefix + "throw");
            Assert2.Equal(ActivityStatusCode.Error, thrown.Status, "exception step span is an error");
            Assert2.True(thrown.Events.Any(e => e.Name == "exception"), "exception event recorded");
            Assert2.Equal(nameof(InvalidOperationException), thrown.GetTagItem(TelemetryConstants.AttrErrorType) as string, "error.type is the exception type");

            Activity slow = capture.Activities.Last(a => a.OperationName == TelemetryConstants.SpanStepPrefix + "slow");
            Assert2.Equal(ActivityStatusCode.Error, slow.Status, "timeout step span is an error");

            Activity failed = capture.Activities.Last(a => a.OperationName == TelemetryConstants.SpanStepPrefix + "fail");
            Assert2.Equal(ActivityStatusCode.Error, failed.Status, "error step span is an error");
            Assert2.Equal("error", failed.GetTagItem(TelemetryConstants.AttrStepResult) as string, "step result tag");
        }

        private static async Task RestStepPropagatesTraceContextAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using DistributedExecutionSuite.OneShotHttpServer target = new DistributedExecutionSuite.OneShotHttpServer(DistributedExecutionSuite.FreePort(), "{\"ok\":true}");
            Task serve = target.ServeOnceAsync(ct);

            StepResult ok = await RunRestFlowAsync(target.Url, ct).ConfigureAwait(false);
            await serve.ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Success, ok.Result, "rest step succeeded");

            Activity client = capture.Spans("http GET").Last();
            Assert2.Equal(ActivityKind.Client, client.Kind, "client span kind");
            Assert2.Equal("127.0.0.1", client.GetTagItem(TelemetryConstants.AttrServerAddress) as string, "server.address tag");
            Assert2.Equal(200, Convert.ToInt32(client.GetTagItem(TelemetryConstants.AttrHttpStatusCode)), "status code tag");
            string? traceparent = target.RequestHeaderLines.FirstOrDefault(h => h.StartsWith("traceparent:", StringComparison.OrdinalIgnoreCase));
            Assert2.NotNull(traceparent, "traceparent header sent");
            Assert2.True(traceparent!.Contains(client.TraceId.ToHexString(), StringComparison.Ordinal), "traceparent carries the step trace id");
            Assert2.True(capture.Sum(TelemetryConstants.IntegrationRequests, "service=http", "operation=GET", "outcome=success") >= 1, "integration success counted");
            Assert2.True(capture.Count(TelemetryConstants.IntegrationDuration, "service=http", "operation=GET") >= 1, "integration latency recorded");

            StepResult unreachable = await RunRestFlowAsync("http://127.0.0.1:" + DistributedExecutionSuite.FreePort() + "/", ct).ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Exception, unreachable.Result, "unreachable target is an exception");
            Assert2.True(capture.Sum(TelemetryConstants.IntegrationRequests, "service=http", "operation=GET", "outcome=exception") >= 1, "integration exception counted");
            Assert2.True(capture.Spans("http GET").Any(a => a.Status == ActivityStatusCode.Error), "failed client span is an error");
        }

        private static async Task DatabaseCallsEmitIntegrationTelemetryAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            try
            {
                using (Activity? parent = TempoTelemetry.StartActivity("tempo.test.parent"))
                {
                    Assert2.NotNull(parent, "parent span sampled");
                    await driver.ExecuteQueryAsync("SELECT 1;", false, ct).ConfigureAwait(false);
                    try
                    {
                        await driver.ExecuteQueryAsync("SELECT * FROM telemetry_missing_table;", false, ct).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // Expected: the failure must be recorded, not swallowed.
                    }

                    Activity select = capture.Spans("sqlite select").Last(a => a.ParentSpanId == parent!.SpanId);
                    Assert2.Equal(ActivityKind.Client, select.Kind, "db span kind");
                    Assert2.Equal("sqlite", select.GetTagItem(TelemetryConstants.AttrDbSystem) as string, "db.system.name tag");
                    Assert2.True(select.Tags.All(t => t.Value == null || (!t.Value.Contains("SELECT 1", StringComparison.Ordinal) && !t.Value.Contains("telemetry_missing_table", StringComparison.Ordinal))), "no SQL text on the span");
                    Assert2.True(capture.Spans("sqlite select").Any(a => a.ParentSpanId == parent!.SpanId && a.Status == ActivityStatusCode.Error), "failed query span is an error");
                }

                Assert2.True(capture.Sum(TelemetryConstants.IntegrationRequests, "service=sqlite", "operation=select", "outcome=success") >= 1, "db success counted");
                Assert2.True(capture.Sum(TelemetryConstants.IntegrationRequests, "service=sqlite", "operation=select", "outcome=exception") >= 1, "db failure counted");
                Assert2.True(capture.Sum(TelemetryConstants.Errors, "component=database") >= 1, "db error counted");
                Assert2.True(capture.Count(TelemetryConstants.LimiterWait, "limiter=sqlite_write") >= 2, "sqlite write lock wait recorded");
                Assert2.False(capture.Activities.Any(a => a.OperationName.StartsWith("sqlite ", StringComparison.Ordinal) && a.Parent == null && a.ParentSpanId == default), "database spans are never trace roots");
            }
            finally
            {
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
            }
        }

        private static async Task CapacityLimiterTelemetryAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            ExternalRuntimeCapacityManager manager = new ExternalRuntimeCapacityManager(new ExternalExecutionSettings
            {
                MaxConcurrentProcessesServerWide = 1,
                MaxConcurrentProcessesPerTenant = 1
            });

            ExternalRuntimeCapacityLease first = await manager.AcquireAsync("ten_telemetry", "sru_1", ct).ConfigureAwait(false);
            Task<ExternalRuntimeCapacityLease> second = manager.AcquireAsync("ten_telemetry", "sru_2", ct);
            await Task.Delay(50, ct).ConfigureAwait(false);
            Assert2.Equal(1.0, capture.Sum(TelemetryConstants.CapacityQueued), "one request queued");
            first.Dispose();
            using (ExternalRuntimeCapacityLease lease = await second.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false)) { }

            using CancellationTokenSource cancelled = new CancellationTokenSource();
            ExternalRuntimeCapacityLease blocker = await manager.AcquireAsync("ten_telemetry", "sru_3", ct).ConfigureAwait(false);
            Task<ExternalRuntimeCapacityLease> waiting = manager.AcquireAsync("ten_telemetry", "sru_4", cancelled.Token);
            cancelled.Cancel();
            try { await waiting.ConfigureAwait(false); } catch (OperationCanceledException) { }
            blocker.Dispose();

            Assert2.True(capture.Count(TelemetryConstants.CapacityWait, "outcome=acquired") >= 3, "acquired waits recorded");
            Assert2.True(capture.Count(TelemetryConstants.CapacityWait, "outcome=cancelled") >= 1, "cancelled wait recorded");
            Assert2.Equal(0.0, capture.Sum(TelemetryConstants.CapacityInUse), "in-use returns to zero");
            Assert2.Equal(0.0, capture.Sum(TelemetryConstants.CapacityQueued), "queued returns to zero");
            Assert2.True(capture.Spans(TelemetryConstants.SpanCapacityAcquire).Any(a => a.Status == ActivityStatusCode.Error), "cancelled acquire span is an error");
        }

        private static async Task ProcessStepPropagatesTraceContextAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using ArtifactProcessSuite.TestRuntime runtime = await ArtifactProcessSuite.TestRuntime.CreateAsync(ct).ConfigureAwait(false);
            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "Telemetry" }, ct).ConfigureAwait(false);
            ArtifactVersionRecord version = await runtime.CreateProcessArtifactAsync(tenant.Id, "telemetry-tool", "1", "success", ct).ConfigureAwait(false);
            StepResult result = await runtime.RunArtifactProcessStepAsync(tenant.Id, version.ArtifactId, "1", "telemetry-process-step", ct).ConfigureAwait(false);
            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "process step succeeded");

            Activity process = capture.Spans("process artifact_process").Last();
            Assert2.Equal(ActivityStatusCode.Ok, process.Status, "process span ok");
            Assert2.Equal(0, Convert.ToInt32(process.GetTagItem(TelemetryConstants.AttrProcessExitCode)), "exit code tag");
            string json = JsonSerializer.Serialize(result.Data);
            Assert2.True(json.Contains(process.TraceId.ToHexString(), StringComparison.Ordinal), "child process received TRACEPARENT with the step trace id");

            Assert2.True(capture.Sum(TelemetryConstants.IntegrationRequests, "service=process", "operation=artifact_process", "outcome=success") >= 1, "process integration counted");
            Assert2.True(capture.Find(TelemetryConstants.CacheLookups, "cache=artifact_package").Any(), "artifact package cache lookup recorded");
            Assert2.True(capture.Sum(TelemetryConstants.StageEvents, "pipeline=flow", "stage=prepare", "outcome=success") >= 1, "prepare stage recorded");
            Assert2.True(capture.Sum(TelemetryConstants.StageEvents, "pipeline=flow", "stage=resolve", "outcome=success") >= 1, "resolve stage recorded");
            Assert2.True(capture.Sum(TelemetryConstants.FlowRuns, "runner=registry", "outcome=success") >= 1, "registry flow counted");
        }

        private static async Task LocalDispatchPipelineIsOneTraceAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = DistributedExecutionSuite.NewTempRoot("tempo-telemetry-local");
            using DistributedExecutionSuite.OneShotHttpServer target = new DistributedExecutionSuite.OneShotHttpServer(DistributedExecutionSuite.FreePort(), "{\"local\":true}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Telemetry" }, ct).ConfigureAwait(false);
                DataFlowRecord flow = await DistributedExecutionSuite.CreateRestFlowAsync(driver, tenant.Id, target.Url, null, ct).ConfigureAwait(false);
                int port = DistributedExecutionSuite.FreePort();
                server = new TempoServer(DistributedExecutionSuite.CreateServerSettings(root, port, serverCanExecuteWorkload: true), DistributedExecutionSuite.SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                Task serve = target.ServeOnceAsync(ct);
                FlowRun run;
                ActivityTraceId requestTrace;
                using (Activity? request = TempoTelemetry.StartActivity("tempo.test.request", ActivityKind.Server))
                {
                    requestTrace = request!.TraceId;
                    run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{}", null, null, null, ct).ConfigureAwait(false);
                }

                FlowRun completed = await DistributedExecutionSuite.WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);
                await serve.ConfigureAwait(false);
                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "run succeeded");

                bool complete = await capture.WaitForAsync(c => c.Spans(TelemetryConstants.SpanDispatchComplete).Any(a => a.TraceId == requestTrace), ct).ConfigureAwait(false);
                Assert2.True(complete, "completion span joined the request trace");

                List<Activity> trace = capture.Activities.Where(a => a.TraceId == requestTrace).ToList();
                foreach (string name in new[]
                {
                    TelemetryConstants.SpanDispatchEnqueue, TelemetryConstants.SpanDispatchSchedule, TelemetryConstants.SpanStagePrefix + "plan",
                    TelemetryConstants.SpanStagePrefix + "select", TelemetryConstants.SpanStagePrefix + "assign", TelemetryConstants.SpanStagePrefix + "execute",
                    TelemetryConstants.SpanFlowRun, "http GET", TelemetryConstants.SpanDispatchComplete
                })
                {
                    Assert2.True(trace.Any(a => a.OperationName == name), "trace contains " + name);
                }

                Activity enqueue = trace.Single(a => a.OperationName == TelemetryConstants.SpanDispatchEnqueue);
                Activity schedule = trace.Single(a => a.OperationName == TelemetryConstants.SpanDispatchSchedule);
                Assert2.Equal(enqueue.SpanId, schedule.ParentSpanId, "schedule span is a child of the enqueue span");
                Assert2.Equal(run.Id, schedule.GetTagItem(TelemetryConstants.AttrFlowRunId) as string, "schedule span carries the run id");

                Assert2.True(capture.Sum(TelemetryConstants.DispatchEnqueued, "source=api", "outcome=accepted") >= 1, "enqueue counted");
                Assert2.True(capture.Sum(TelemetryConstants.DispatchAssignments, "node_kind=server", "outcome=assigned") >= 1, "assignment counted");
                Assert2.True(capture.Sum(TelemetryConstants.DispatchCompletions, "node_kind=server", "state=succeeded", "outcome=applied") >= 1, "completion counted");
                foreach (string stage in new[] { "queued", "plan", "assign", "execute", "complete" })
                {
                    Assert2.True(capture.Count(TelemetryConstants.StageDuration, "pipeline=dispatch", "stage=" + stage) >= 1, "dispatch stage " + stage + " recorded");
                }

                Assert2.True(capture.Count(TelemetryConstants.LimiterWait, "limiter=dispatch_gate") >= 1, "dispatch gate wait recorded");
                capture.RecordObservables();
                Assert2.True(capture.Find(TelemetryConstants.WorkersConnected, "node_kind=server").Any(m => m.Value >= 1), "server executor gauge");
                Assert2.True(capture.Find(TelemetryConstants.DispatchSchedulerActive).Any(m => m.Value == 1), "scheduler active gauge");
                Assert2.True(capture.Find(TelemetryConstants.LastSuccess, "task=dispatch").Any(m => m.Value > 0), "dispatch last-success gauge");
                Assert2.True(capture.Find(TelemetryConstants.DispatchQueueDepth).Any(), "queue depth gauge sampled");
            }
            finally
            {
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DistributedExecutionSuite.DeleteDirectory(root);
            }
        }

        private static async Task RemoteWorkerJoinsDispatchTraceAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            CancellationTokenSource? workerCts = null;
            Task? workerTask = null;
            string root = DistributedExecutionSuite.NewTempRoot("tempo-telemetry-remote");
            using DistributedExecutionSuite.OneShotHttpServer target = new DistributedExecutionSuite.OneShotHttpServer(DistributedExecutionSuite.FreePort(), "{\"remote\":true}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Telemetry" }, ct).ConfigureAwait(false);
                DataFlowRecord flow = await DistributedExecutionSuite.CreateRestFlowAsync(driver, tenant.Id, target.Url, null, ct).ConfigureAwait(false);
                int port = DistributedExecutionSuite.FreePort();
                server = new TempoServer(DistributedExecutionSuite.CreateServerSettings(root, port, serverCanExecuteWorkload: false), DistributedExecutionSuite.SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                Tempo.Core.Models.WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_telemetry_1", null, ct).ConfigureAwait(false);
                (workerCts, workerTask) = DistributedExecutionSuite.StartWorkerTask(DistributedExecutionSuite.CreateWorkerSettings(root, port, "wrk_telemetry_1", token.Token), ct);
                await DistributedExecutionSuite.WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_telemetry_1", ct).ConfigureAwait(false);

                Task serve = target.ServeOnceAsync(ct);
                FlowRun run;
                ActivityTraceId requestTrace;
                using (Activity? request = TempoTelemetry.StartActivity("tempo.test.request", ActivityKind.Server))
                {
                    requestTrace = request!.TraceId;
                    run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{}", null, null, null, ct).ConfigureAwait(false);
                }

                FlowRun completed = await DistributedExecutionSuite.WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);
                await serve.ConfigureAwait(false);
                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "remote run succeeded");

                bool complete = await capture.WaitForAsync(c => c.Spans(TelemetryConstants.SpanDispatchComplete).Any(a => a.TraceId == requestTrace), ct).ConfigureAwait(false);
                Assert2.True(complete, "server completion joined the trace via the completion frame");

                List<Activity> trace = capture.Activities.Where(a => a.TraceId == requestTrace).ToList();
                Activity send = trace.Single(a => a.OperationName == TelemetryConstants.SpanWorkerAssignSend);
                Activity assignment = trace.Single(a => a.OperationName == TelemetryConstants.SpanWorkerAssignment);
                Assert2.Equal(ActivityKind.Producer, send.Kind, "assign send is a producer span");
                Assert2.Equal(ActivityKind.Consumer, assignment.Kind, "worker assignment is a consumer span");
                Assert2.Equal(send.SpanId, assignment.ParentSpanId, "worker span is parented on the server's send span across the websocket");
                Assert2.True(trace.Any(a => a.OperationName == TelemetryConstants.SpanFlowRun && a.ParentSpanId == assignment.SpanId), "worker flow span is a child of the assignment span");
                Activity completeSpan = trace.Single(a => a.OperationName == TelemetryConstants.SpanDispatchComplete);
                Assert2.Equal(assignment.SpanId, completeSpan.ParentSpanId, "server completion is parented on the worker assignment span");

                string? traceparent = target.RequestHeaderLines.FirstOrDefault(h => h.StartsWith("traceparent:", StringComparison.OrdinalIgnoreCase));
                Assert2.True(traceparent != null && traceparent.Contains(requestTrace.ToHexString(), StringComparison.Ordinal), "worker's outbound REST call carries the dispatch trace id");

                Assert2.True(capture.Sum(TelemetryConstants.WorkerSessions, "event=connected") >= 1, "worker session connected counted");
                Assert2.True(capture.Sum(TelemetryConstants.WorkerAssignments, "outcome=accepted") >= 1, "worker accepted assignment counted");
                Assert2.True(capture.Sum(TelemetryConstants.WorkerFrames, "direction=out", "frame=assign") >= 1, "assign frame out counted");
                Assert2.True(capture.Sum(TelemetryConstants.WorkerFrames, "direction=in", "frame=run-completed") >= 1, "completion frame in counted");
                Assert2.True(capture.Sum(TelemetryConstants.DispatchCompletions, "node_kind=worker", "state=succeeded", "outcome=applied") >= 1, "worker completion counted");
                Assert2.True(capture.Count(TelemetryConstants.StageDuration, "pipeline=worker", "stage=execute") >= 1, "worker execute stage recorded");
                capture.RecordObservables();
                Assert2.True(capture.Find(TelemetryConstants.WorkersConnected, "node_kind=worker").Any(m => m.Value >= 1), "worker pool gauge");
            }
            finally
            {
                await DistributedExecutionSuite.StopWorkerTaskAsync(workerCts, workerTask).ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DistributedExecutionSuite.DeleteDirectory(root);
            }
        }

        private static async Task EnqueueFailureIsRecordedAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = DistributedExecutionSuite.NewTempRoot("tempo-telemetry-enqueue");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Telemetry" }, ct).ConfigureAwait(false);
                server = new TempoServer(DistributedExecutionSuite.CreateServerSettings(root, DistributedExecutionSuite.FreePort(), serverCanExecuteWorkload: true), DistributedExecutionSuite.SilentLogger(), driver, new StepManager());
                bool threw = false;
                try
                {
                    await server.Dispatch.EnqueueAsync(tenant.Id, "dataflow_does_not_exist", null, null, "trg_telemetry", null, ct).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    threw = true;
                }

                Assert2.True(threw, "enqueue of a missing flow is rejected");
                Assert2.True(capture.Sum(TelemetryConstants.DispatchEnqueued, "source=trigger", "outcome=rejected") >= 1, "rejected enqueue counted");
                Assert2.True(capture.Spans(TelemetryConstants.SpanDispatchEnqueue).Any(a => a.Status == ActivityStatusCode.Error), "enqueue span marked failed");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DistributedExecutionSuite.DeleteDirectory(root);
            }
        }

        private static async Task McpToolCallsPropagateToServerAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = DistributedExecutionSuite.NewTempRoot("tempo-telemetry-mcp");
            try
            {
                int port = DistributedExecutionSuite.FreePort();
                server = new TempoServer(DistributedExecutionSuite.CreateServerSettings(root, port, serverCanExecuteWorkload: true), DistributedExecutionSuite.SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                using TempoApiClient client = new TempoApiClient(new TempoEndpointSettings { Endpoint = "http://127.0.0.1:" + port });
                TempoToolDefinition health = TempoToolRegistrar.CreateDefinitions(client).Single(t => t.Name == "tempo_health");
                await TempoToolRegistrar.InvokeToolAsync(health, client, null, ct).ConfigureAwait(false);

                Activity tool = capture.Spans(TelemetryConstants.SpanMcpToolPrefix + "tempo_health").Last();
                Assert2.Equal(ActivityStatusCode.Ok, tool.Status, "tool span ok");
                Activity call = capture.Spans("tempo-server GET").Last(a => a.TraceId == tool.TraceId);
                Assert2.Equal(tool.SpanId, call.ParentSpanId, "client span is a child of the tool span");
                bool serverJoined = await capture.WaitForAsync(c => c.Activities.Any(watson =>
                    watson.Source.Name == TelemetryConstants.WatsonSourceName && watson.TraceId == tool.TraceId &&
                    c.Activities.Any(http => http.Source.Name == TelemetryConstants.HttpClientSourceName && http.SpanId == watson.ParentSpanId && http.ParentSpanId == call.SpanId)), ct).ConfigureAwait(false);
                Assert2.True(serverJoined, "Watson's server span joined the MCP trace through traceparent; call " + call.TraceId + "/" + call.SpanId + "; watson spans: " +
                    string.Join(" | ", capture.Activities.Where(a => a.Source.Name == TelemetryConstants.WatsonSourceName).Select(a => a.OperationName + " " + a.TraceId + " parent=" + a.ParentSpanId + " kind=" + a.Kind)));
                Assert2.True(capture.Sum(TelemetryConstants.McpToolCalls, "tool=tempo_health", "outcome=success") >= 1, "tool call counted");
                Assert2.True(capture.Sum(TelemetryConstants.IntegrationRequests, "service=tempo-server", "operation=GET", "outcome=success") >= 1, "API call counted");

                using TempoApiClient offline = new TempoApiClient(new TempoEndpointSettings { Endpoint = "http://127.0.0.1:" + DistributedExecutionSuite.FreePort(), TimeoutMs = 2000 });
                TempoToolDefinition offlineHealth = TempoToolRegistrar.CreateDefinitions(offline).Single(t => t.Name == "tempo_health");
                await TempoToolRegistrar.InvokeToolAsync(offlineHealth, offline, null, ct).ConfigureAwait(false);
                Assert2.True(capture.Sum(TelemetryConstants.McpToolCalls, "tool=tempo_health", "outcome=exception") >= 1, "failed tool call counted");
                Assert2.True(capture.Sum(TelemetryConstants.IntegrationRequests, "service=tempo-server", "operation=GET", "outcome=exception") >= 1, "failed API call counted");
                Assert2.True(capture.Sum(TelemetryConstants.Errors, "component=mcp") >= 1, "mcp error counted");
            }
            finally
            {
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DistributedExecutionSuite.DeleteDirectory(root);
            }
        }

        private static async Task AuthenticationOutcomesAreCountedAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = DistributedExecutionSuite.NewTempRoot("tempo-telemetry-auth");
            try
            {
                int port = DistributedExecutionSuite.FreePort();
                server = new TempoServer(DistributedExecutionSuite.CreateServerSettings(root, port, serverCanExecuteWorkload: true, adminApiKey: "telemetry-admin-key"), DistributedExecutionSuite.SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                using HttpClient http = new HttpClient();
                using (HttpRequestMessage bad = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:" + port + "/v1.0/tenants"))
                {
                    bad.Headers.Add(Tempo.Core.Constants.HeaderApiKey, "wrong-key");
                    using HttpResponseMessage response = await http.SendAsync(bad, ct).ConfigureAwait(false);
                }

                using (HttpRequestMessage good = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:" + port + "/v1.0/tenants"))
                {
                    good.Headers.Add(Tempo.Core.Constants.HeaderApiKey, "telemetry-admin-key");
                    using HttpResponseMessage response = await http.SendAsync(good, ct).ConfigureAwait(false);
                    Assert2.True(response.IsSuccessStatusCode, "admin request succeeded");
                }

                Assert2.True(capture.Sum(TelemetryConstants.AuthAttempts, "method=api_key", "outcome=success") >= 1, "successful api key counted");
                Assert2.True(capture.Find(TelemetryConstants.AuthAttempts, "method=api_key").Any(m => m.Tags[TelemetryConstants.AttrOutcome] != "success"), "failed api key counted");
                await server.Authorization.AuthorizeAsync(new Tempo.Core.Security.RequestContext(), ResourceTypeEnum.Tenant, OperationTypeEnum.Read, ct).ConfigureAwait(false);
                Assert2.True(capture.Sum(TelemetryConstants.AuthzDecisions, "outcome=deniedimplicit") >= 1, "authorization denial counted");
            }
            finally
            {
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DistributedExecutionSuite.DeleteDirectory(root);
            }
        }

        private static async Task BackgroundTasksRecordRunsAndLastSuccessAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            try
            {
                RequestHistoryCaptureService service = new RequestHistoryCaptureService(driver, new RequestHistorySettings { Enabled = true });
                service.Capture(new RequestHistoryEntry { Method = "GET", Path = "/v1.0/telemetry", StatusCode = 200 });

                bool recorded = await capture.WaitForAsync(c => c.Sum(TelemetryConstants.TaskRuns, "task=request_history_capture", "outcome=success") >= 1, ct).ConfigureAwait(false);
                Assert2.True(recorded, "capture task run counted");
                Assert2.True(capture.Count(TelemetryConstants.TaskDuration, "task=request_history_capture") >= 1, "capture task duration recorded");
                Assert2.Equal(0.0, capture.Sum(TelemetryConstants.TaskPending, "task=request_history_capture"), "pending items return to zero");
                capture.RecordObservables();
                Assert2.True(capture.Find(TelemetryConstants.LastSuccess, "task=request_history_capture").Any(m => m.Value > 1_600_000_000), "last-success timestamp is a Unix time");
            }
            finally
            {
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
            }
        }

        private static Task BuildInfoAndConfigGaugesAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            TempoTelemetry.SetConfig("test.setting", 3);
            capture.RecordObservables();

            CapturedMeasurement build = capture.Find(TelemetryConstants.BuildInfo).Last();
            Assert2.Equal(1.0, build.Value, "build info value");
            Assert2.True(!string.IsNullOrWhiteSpace(build.Tags[TelemetryConstants.AttrVersion]) && build.Tags[TelemetryConstants.AttrVersion] != "0.0.0", "build version label");
            Assert2.True(capture.Find(TelemetryConstants.ConfigSetting, "setting=test.setting").Any(m => m.Value == 3), "config gauge value");
            return Task.CompletedTask;
        }

        private static async Task TelemetryHostExportsMetricsTracesAndLogsAsync(CancellationToken ct)
        {
            int otlpPort = DistributedExecutionSuite.FreePort();
            int prometheusPort = DistributedExecutionSuite.FreePort();
            using OtlpRecordingEndpoint otlp = new OtlpRecordingEndpoint(otlpPort);
            LoggingModule logging = DistributedExecutionSuite.SilentLogger();
            string previousComponent = TempoTelemetry.Component;
            TelemetrySettings settings = new TelemetrySettings
            {
                OtlpEnabled = true,
                OtlpEndpoint = otlp.BaseUrl,
                OtlpProtocol = "httpprotobuf",
                PrometheusEnabled = true,
                PrometheusHostname = "127.0.0.1",
                PrometheusPort = prometheusPort,
                LogsEnabled = true,
                MetricsExportIntervalMs = 1000
            };

            TelemetryHost host = TelemetryHost.Start(settings, "tempo-telemetry-test", "test", logging);
            try
            {
                Assert2.True(host.IsEnabled, "telemetry host started");
                Assert2.Equal("tempo-telemetry-test", host.ServiceName, "service name");

                StepResult result = await RunEngineFlowAsync(new[] { "succeed" }, ct).ConfigureAwait(false);
                Assert2.Equal(StepResultTypeEnum.Success, result.Result, "flow ran under the host");

                string marker = "telemetry-bridge-" + Guid.NewGuid().ToString("N");
                host.BridgeLogs(logging);
                using (Activity? span = TempoTelemetry.StartActivity("tempo.test.logged"))
                {
                    logging.Warn(marker);
                }

                using HttpClient http = new HttpClient();
                string scrape = await http.GetStringAsync("http://127.0.0.1:" + prometheusPort + "/metrics", ct).ConfigureAwait(false);
                Assert2.True(scrape.Contains("tempo_flow_runs", StringComparison.Ordinal), "Prometheus exposes tempo_flow_runs");
                Assert2.True(scrape.Contains("tempo_build_info", StringComparison.Ordinal), "Prometheus exposes tempo_build_info");
                Assert2.True(scrape.Contains("component=\"test\"", StringComparison.Ordinal), "build info carries the component label");

                host.Flush();
                // With OTLP/HTTP the exporter posts every signal to the configured endpoint verbatim, so match on body content.
                bool exported = await WaitUntilAsync(() => otlp.Received("/", TelemetryConstants.SpanFlowRun) && otlp.Received("/", marker), ct).ConfigureAwait(false);
                Assert2.True(otlp.Received("/", TelemetryConstants.SpanFlowRun), "OTLP traces exported (paths: " + string.Join(",", otlp.Paths.Distinct()) + ")");
                Assert2.True(otlp.Received("/", marker), "bridged log exported over OTLP");
                Assert2.True(exported, "export completed");
            }
            finally
            {
                host.Dispose();
                TempoTelemetry.Component = previousComponent;
                logging.Dispose();
            }
        }

        private static Task TelemetryHostDisabledOrFailingIsInertAsync(CancellationToken ct)
        {
            TelemetryHost disabled = TelemetryHost.Start(new TelemetrySettings { Enabled = false }, "tempo-test", "test");
            Assert2.False(disabled.IsEnabled, "disabled host is inert");
            disabled.BridgeLogs(DistributedExecutionSuite.SilentLogger());
            disabled.Flush();
            disabled.Dispose();
            disabled.Dispose();

            TelemetryHost nullSettings = TelemetryHost.Start(null, "tempo-test", "test");
            nullSettings.Dispose();

            int port = DistributedExecutionSuite.FreePort();
            using TelemetryHost first = TelemetryHost.Start(new TelemetrySettings { OtlpEnabled = false, PrometheusEnabled = true, PrometheusPort = port }, "tempo-test", "test");
            using TelemetryHost second = TelemetryHost.Start(new TelemetrySettings { OtlpEnabled = false, PrometheusEnabled = true, PrometheusPort = port }, "tempo-test", "test");
            Assert2.True(first.IsEnabled, "first host on the port started");
            Assert2.False(second.IsEnabled, "a host that cannot start degrades to inert instead of throwing");
            TempoTelemetry.Component = "library";
            return Task.CompletedTask;
        }

        private static Task TelemetrySettingsValidateAndApplyEnvironmentAsync(CancellationToken ct)
        {
            TelemetrySettings settings = new TelemetrySettings();
            Assert2.Equal("http://127.0.0.1:4317", settings.OtlpEndpoint, "loopback OTLP default");
            Assert2.Equal("127.0.0.1", settings.PrometheusHostname, "loopback Prometheus default");
            Assert2.Throws<ArgumentException>(() => settings.OtlpEndpoint = "not a uri", "relative endpoint rejected");
            Assert2.Throws<ArgumentException>(() => settings.OtlpProtocol = "thrift", "unknown protocol rejected");
            Assert2.Throws<ArgumentOutOfRangeException>(() => settings.PrometheusPort = 0, "port 0 rejected");
            Assert2.Throws<ArgumentOutOfRangeException>(() => settings.SamplingRatio = 1.5, "sampling ratio above 1 rejected");

            Dictionary<string, string?> previous = new Dictionary<string, string?>();
            Dictionary<string, string> overrides = new Dictionary<string, string>
            {
                [TelemetrySettings.EnvEnabled] = "false",
                [TelemetrySettings.EnvServiceName] = "tempo-override",
                [TelemetrySettings.EnvOtlpEndpoint] = "http://collector:4318",
                [TelemetrySettings.EnvOtlpProtocol] = "httpprotobuf",
                [TelemetrySettings.EnvPrometheusEnabled] = "true",
                [TelemetrySettings.EnvPrometheusHostname] = "*",
                [TelemetrySettings.EnvPrometheusPort] = "9555",
                [TelemetrySettings.EnvSamplingRatio] = "0.25"
            };

            try
            {
                foreach (KeyValuePair<string, string> entry in overrides)
                {
                    previous[entry.Key] = Environment.GetEnvironmentVariable(entry.Key);
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }

                settings.ApplyEnvironmentOverrides();
                Assert2.False(settings.Enabled, "enabled override");
                Assert2.Equal("tempo-override", settings.ServiceName!, "service name override");
                Assert2.Equal("http://collector:4318", settings.OtlpEndpoint, "endpoint override");
                Assert2.Equal("httpprotobuf", settings.OtlpProtocol, "protocol override");
                Assert2.True(settings.PrometheusEnabled, "prometheus override");
                Assert2.Equal("*", settings.PrometheusHostname, "hostname override");
                Assert2.Equal(9555, settings.PrometheusPort, "port override");
                Assert2.Equal(0.25, settings.SamplingRatio, "sampling override");
            }
            finally
            {
                foreach (KeyValuePair<string, string?> entry in previous) Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }

            Settings server = new Settings();
            Assert2.NotNull(server.Telemetry, "server settings carry telemetry");
            Assert2.NotNull(new Tempo.Worker.WorkerSettings().Telemetry, "worker settings carry telemetry");
            Assert2.NotNull(new TempoMcpServerSettings().Telemetry, "mcp settings carry telemetry");
            return Task.CompletedTask;
        }

        private static async Task MetricLabelsStayBoundedAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            await RunEngineFlowAsync(new[] { "succeed", "fail" }, ct).ConfigureAwait(false);
            await RunEngineFlowAsync(new[] { "throw" }, ct).ConfigureAwait(false);
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Labels" }, ct).ConfigureAwait(false);
                await driver.Tenants.ReadAsync(tenant.Id, ct).ConfigureAwait(false);
            }
            finally
            {
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
            }

            TempoTelemetry.SetConfig("label.check", 1);
            capture.RecordObservables();
            List<CapturedMeasurement> measurements = capture.Measurements;
            Assert2.True(measurements.Count > 10, "workload produced measurements");

            Regex identifier = new Regex("^[a-z]{2,12}_[A-Za-z0-9]{12,}$|[0-9a-f]{32}|^[0-9a-f]{8}-", RegexOptions.CultureInvariant);
            foreach (CapturedMeasurement m in measurements)
            {
                foreach (KeyValuePair<string, string> tag in m.Tags)
                {
                    Assert2.True(_AllowedMetricLabels.Contains(tag.Key), "label key '" + tag.Key + "' on " + m.Instrument + " is documented");
                    Assert2.True(tag.Value.Length <= 48, "label value on " + m.Instrument + " is short: " + tag.Value);
                    Assert2.False(identifier.IsMatch(tag.Value), "label value on " + m.Instrument + " is not an identifier: " + tag.Value);
                    Assert2.False(tag.Value.Contains(' '), "label value on " + m.Instrument + " is not free-form text: " + tag.Value);
                }
            }
        }

        private static async Task<StepResult> RunEngineFlowAsync(string[] stepKinds, CancellationToken ct, int stepTimeoutMs = 0)
        {
            StepManager manager = new StepManager();
            DataFlow flow = new DataFlow { TenantId = "ten_telemetry", Name = "telemetry" };
            for (int i = 0; i < stepKinds.Length; i++)
            {
                string kind = stepKinds[i];
                string methodName = kind switch
                {
                    "succeed" => nameof(TelemetryTestSteps.Succeed),
                    "fail" => nameof(TelemetryTestSteps.Fail),
                    "throw" => nameof(TelemetryTestSteps.Throw),
                    "slow" => nameof(TelemetryTestSteps.Slow),
                    _ => throw new ArgumentException("unknown step kind " + kind)
                };

                string id = stepKinds.Length > 1 ? kind + "-" + i : kind;
                manager.RegisterMethod(id, typeof(TelemetryTestSteps).GetMethod(methodName)!, maxRuntimeMs: stepTimeoutMs);
                string next = i + 1 < stepKinds.Length ? stepKinds[i + 1] + "-" + (i + 1) : null!;
                flow.Steps[id] = new StepTransition { OnSuccess = next, OnFailure = next };
                if (i == 0) flow.StartStepId = id;
            }

            DataFlowRunner runner = new DataFlowRunner(manager);
            StepRequest request = new StepRequest { DataFlowId = flow.Identifier, RequestId = "req_" + Guid.NewGuid().ToString("N"), Data = "input" };
            return await runner.Run(flow, request, ct).ConfigureAwait(false);
        }

        private static async Task<StepResult> RunRestFlowAsync(string url, CancellationToken ct)
        {
            DataFlow flow = new DataFlow { TenantId = "ten_telemetry", Name = "telemetry-rest", StartStepId = "rest" };
            flow.Steps["rest"] = new StepTransition
            {
                StepType = StepTypeEnum.Rest,
                Rest = new RestStepConfiguration { Method = "GET", Url = url, TimeoutMs = 5000 }
            };

            DataFlowRunner runner = new DataFlowRunner(new StepManager());
            StepRequest request = new StepRequest { DataFlowId = flow.Identifier, RequestId = "req_" + Guid.NewGuid().ToString("N") };
            return await runner.Run(flow, request, ct).ConfigureAwait(false);
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, CancellationToken ct, int timeoutMs = 15000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            return condition();
        }
    }
}
