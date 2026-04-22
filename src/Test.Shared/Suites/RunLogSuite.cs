namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo;
    using Tempo.Core.Artifacts;
    using Tempo.Core;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Tempo.Core.Workers;
    using Tempo.Server;
    using Tempo.Worker;
    using Touchstone.Core;
    using CoreTenant = Tempo.Core.Models.Tenant;

    public static class RunLogSuite
    {
        private const string AdminApiKey = "tempo-run-log-suite-admin-key";

        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RunLogs",
                displayName: "Per-run log capture, routes, and retention",
                cases: new[]
                {
                    new TestCaseDescriptor("RunLogs", "ServiceIndexesReadsAndDeletes", "RunLogService indexes files, enforces bounded reads, and protects active files", ServiceIndexesReadsAndDeletesAsync),
                    new TestCaseDescriptor("RunLogs", "RunListFiltersByWorkerAndSourceIp", "Flow-run enumeration filters support assigned worker id and source IP", RunListFiltersByWorkerAndSourceIpAsync),
                    new TestCaseDescriptor("RunLogs", "LocalRoutesExposeActivityAndLogs", "Tenant-scoped run activity and log routes expose local server execution details", LocalRoutesExposeActivityAndLogsAsync),
                    new TestCaseDescriptor("RunLogs", "RemoteRoutesExposeWorkerLogs", "Tenant-scoped run log routes expose worker execution details for remote assignments", RemoteRoutesExposeWorkerLogsAsync),
                    new TestCaseDescriptor("RunLogs", "JavaScriptSourceLogsUseRealLineBreaks", "JavaScript source-step logging writes real line breaks instead of literal backslash-n sequences", JavaScriptSourceLogsUseRealLineBreaksAsync),
                    new TestCaseDescriptor("RunLogs", "PruneRemovesExpiredCompletedRuns", "Server-owned retention pruning removes expired completed run-log directories", PruneRemovesExpiredCompletedRunsAsync)
                });
        }

        private static async Task ServiceIndexesReadsAndDeletesAsync(CancellationToken ct)
        {
            string root = NewTempRoot("tempo-runlog-service");
            try
            {
                RunLogService service = new RunLogService(new RunLogSettings
                {
                    Enabled = true,
                    RootPath = root,
                    DefaultTailLines = 2,
                    DefaultMaxBytes = 4096,
                    MaxTailLines = 100,
                    MaxReadBytes = 16384
                });

                RunLogSession session = (await service.CreateSessionAsync(new RunLogSessionContext
                {
                    FlowRunId = "run_service_1",
                    TenantId = "ten_service_1",
                    DataFlowId = "flow_service_1",
                    AttemptNumber = 1,
                    RunAssignmentId = "ras_service_1",
                    WorkerId = "wrk_service_1",
                    NodeKind = "Worker"
                }, ct).ConfigureAwait(false))!;

                await session.AppendRunAsync("Info", "run line 1", ct).ConfigureAwait(false);
                await session.AppendRunAsync("Info", "run line 2", ct).ConfigureAwait(false);
                await session.AppendWorkerAsync("Info", "worker line 1", ct).ConfigureAwait(false);
                await session.AppendHostAsync("Warn", "host line 1", ct).ConfigureAwait(false);
                RunLogStepScope step = await session.CreateStepScopeAsync(1, "step.echo", "sru_service_1", ct).ConfigureAwait(false);
                await session.AppendStepAsync(step, "Info", "step line 1", ct).ConfigureAwait(false);
                await session.AppendStepAsync(step, "Info", "step line 2", ct).ConfigureAwait(false);
                await session.AppendStepStdErrAsync(step, "stderr line 1\n", ct).ConfigureAwait(false);

                List<Tempo.Core.Responses.RunLogFileSummaryResponse> activeFiles = await service.ListFilesAsync("run_service_1", activeRun: true, ct).ConfigureAwait(false);
                Assert2.True(activeFiles.Count >= 5, "run, worker, host, step, and stderr files were indexed");
                Assert2.True(activeFiles.Any(file => file.Kind == "Run"), "run log listed");
                Assert2.True(activeFiles.Any(file => file.Kind == "Worker"), "worker log listed");
                Assert2.True(activeFiles.Any(file => file.Kind == "Host"), "host log listed");
                Assert2.True(activeFiles.Any(file => file.Kind == "Step" && file.StepId == "step.echo"), "step log listed");
                Assert2.True(activeFiles.Any(file => file.Kind == "StepStderr" && file.StepRunId == "sru_service_1"), "step stderr log listed");

                Tempo.Core.Responses.RunLogFileSummaryResponse runFile = activeFiles.First(file => file.Kind == "Run");
                Assert2.True(runFile.Active, "run log marked active while run is active");
                Assert2.True(!runFile.DeleteAllowed, "active run log is protected from deletion");

                Tempo.Core.Responses.RunLogFileReadResponse stepTail = await service.ReadAsync("run_service_1", step.RelativeLogPath, activeRun: false, tailLines: 1, maxBytes: 4096, ct).ConfigureAwait(false);
                Assert2.True(stepTail.Content.Contains("step line 2", StringComparison.Ordinal), "bounded read returns the latest line");
                Assert2.True(stepTail.Truncated, "bounded read reports truncation when older lines are omitted");
                Assert2.Equal("step.echo", stepTail.StepId!, "step metadata preserved");

                (byte[] bytes, string contentType, string downloadFileName) = await service.DownloadAsync("run_service_1", "run.log", ct).ConfigureAwait(false);
                string downloaded = Encoding.UTF8.GetString(bytes);
                Assert2.Equal("text/plain", contentType, "download content type");
                Assert2.True(downloaded.Contains("run line 1", StringComparison.Ordinal), "download returns full file");
                Assert2.True(downloadFileName.Contains("run_service_1", StringComparison.Ordinal), "download filename includes run id");

                bool activeDeleteRejected = false;
                try
                {
                    await service.DeleteFileAsync("run_service_1", "run.log", activeRun: true, ct).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    activeDeleteRejected = true;
                }

                Assert2.True(activeDeleteRejected, "active log deletion is rejected");

                Tempo.Core.Responses.RunLogDeleteResponse deleteStep = await service.DeleteFileAsync("run_service_1", step.RelativeLogPath, activeRun: false, ct).ConfigureAwait(false);
                Assert2.Equal("Deleted", deleteStep.Action, "archived step log is deleted");
                Assert2.True(!File.Exists(step.LogPath), "deleted step log removed from disk");

                bool traversalRejected = false;
                try
                {
                    await service.ReadAsync("run_service_1", "../secret.txt", activeRun: false, tailLines: 1, maxBytes: 32, ct).ConfigureAwait(false);
                }
                catch (ArgumentException)
                {
                    traversalRejected = true;
                }

                Assert2.True(traversalRejected, "parent traversal is rejected");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static async Task LocalRoutesExposeActivityAndLogsAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-runlog-local");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"local\":true}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Run Log Local" }, ct).ConfigureAwait(false);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, ct).ConfigureAwait(false);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: true), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"source\":\"local\"}", null, null, "198.51.100.20", ct).ConfigureAwait(false);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);
                await serveTask.ConfigureAwait(false);

                string runRoot = server.RunLogs.ResolveRunRoot(run.Id);
                string[] diskFiles = Directory.Exists(runRoot)
                    ? Directory.EnumerateFiles(runRoot, "*", SearchOption.AllDirectories)
                        .Select(Path.GetFileName)
                        .Where(file => !string.IsNullOrWhiteSpace(file))
                        .Select(file => file!)
                        .ToArray()
                    : Array.Empty<string>();
                Assert2.True(diskFiles.Length >= 1, "local run created disk files: " + string.Join(", ", diskFiles));

                using HttpClient client = CreateAdminClient(port);
                using JsonDocument activity = await ReadJsonAsync(client, "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/activity", ct).ConfigureAwait(false);
                JsonElement activityRoot = activity.RootElement;
                Assert2.Equal(run.Id, activityRoot.GetProperty("run").GetProperty("id").GetString()!, "activity returns run");
                Assert2.Equal("198.51.100.20", activityRoot.GetProperty("run").GetProperty("sourceIp").GetString()!, "activity includes source ip");
                Assert2.True(activityRoot.GetProperty("assignments").GetArrayLength() >= 1, "assignment history returned");
                Assert2.True(activityRoot.GetProperty("activity").GetArrayLength() >= 1, "worker activity returned");

                using JsonDocument files = await ReadJsonAsync(client, "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs", ct).ConfigureAwait(false);
                JsonElement[] logFiles = files.RootElement.EnumerateArray().ToArray();
                Assert2.True(logFiles.Any(file => file.GetProperty("kind").GetString() == "Run"), "run log listed");
                Assert2.True(logFiles.Any(file => file.GetProperty("kind").GetString() == "Worker"), "worker log listed");
                JsonElement runLog = logFiles.First(file => file.GetProperty("kind").GetString() == "Run");
                JsonElement workerLog = logFiles.First(file => file.GetProperty("kind").GetString() == "Worker");
                Assert2.True(!runLog.GetProperty("active").GetBoolean(), "completed run logs are not marked active");

                string runLogPath = Uri.EscapeDataString(runLog.GetProperty("path").GetString()!);
                using JsonDocument runLogRead = await ReadJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs/content?path=" + runLogPath + "&tailLines=50&maxBytes=65536",
                    ct).ConfigureAwait(false);
                string runLogContent = runLogRead.RootElement.GetProperty("content").GetString() ?? string.Empty;
                Assert2.True(runLogContent.Contains("Flow run started", StringComparison.Ordinal), "run log content returned");
                Assert2.True(runLogContent.Contains("Flow run completed", StringComparison.Ordinal), "run log completion returned");

                string workerLogPath = Uri.EscapeDataString(workerLog.GetProperty("path").GetString()!);
                using JsonDocument workerLogRead = await ReadJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs/content?path=" + workerLogPath,
                    ct).ConfigureAwait(false);
                string workerLogContent = workerLogRead.RootElement.GetProperty("content").GetString() ?? string.Empty;
                Assert2.True(workerLogContent.Contains("server-local executor", StringComparison.Ordinal), "local worker log content returned");

                using HttpResponseMessage download = await client.GetAsync(
                    "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs/download?path=" + runLogPath,
                    ct).ConfigureAwait(false);
                string downloadBody = await download.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                Assert2.Equal(HttpStatusCode.OK, download.StatusCode, "run-log download succeeded");
                Assert2.True(downloadBody.Contains("Flow run completed", StringComparison.Ordinal), "download contains run log text");

                using HttpResponseMessage traversal = await client.GetAsync(
                    "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs/content?path=" + Uri.EscapeDataString("../secret.txt"),
                    ct).ConfigureAwait(false);
                Assert2.Equal(HttpStatusCode.BadRequest, traversal.StatusCode, "run-log traversal rejected");

                using HttpResponseMessage deleteAll = await client.DeleteAsync("/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs", ct).ConfigureAwait(false);
                Assert2.Equal(HttpStatusCode.NoContent, deleteAll.StatusCode, "delete all run logs succeeded");

                using JsonDocument filesAfterDelete = await ReadJsonAsync(client, "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs", ct).ConfigureAwait(false);
                Assert2.Equal(0, filesAfterDelete.RootElement.GetArrayLength(), "run logs removed after delete-all");
                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "run still completed successfully");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task RunListFiltersByWorkerAndSourceIpAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-runlog-filters");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Run Filter Tenant" }, ct).ConfigureAwait(false);
                DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenant.Id,
                    Name = "Run filter flow",
                    StartStepId = "noop",
                    Transitions = new Dictionary<string, StepTransition> { ["noop"] = new StepTransition() }
                }, ct).ConfigureAwait(false);

                await driver.FlowRuns.CreateAsync(new FlowRun
                {
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    SourceIp = "198.51.100.41",
                    AssignedWorkerId = "wrk_filter_a",
                    State = FlowRunStateEnum.Succeeded,
                    DispatchState = FlowRunDispatchStateEnum.Completed
                }, ct).ConfigureAwait(false);

                await driver.FlowRuns.CreateAsync(new FlowRun
                {
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    SourceIp = "198.51.100.42",
                    AssignedWorkerId = "wrk_filter_a",
                    State = FlowRunStateEnum.Succeeded,
                    DispatchState = FlowRunDispatchStateEnum.Completed
                }, ct).ConfigureAwait(false);

                FlowRun matchingRun = await driver.FlowRuns.CreateAsync(new FlowRun
                {
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    SourceIp = "198.51.100.41",
                    AssignedWorkerId = "wrk_filter_b",
                    State = FlowRunStateEnum.Succeeded,
                    DispatchState = FlowRunDispatchStateEnum.Completed
                }, ct).ConfigureAwait(false);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                using HttpClient client = CreateAdminClient(port);

                using JsonDocument workerFiltered = await ReadJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs?workerId=wrk_filter_a",
                    ct).ConfigureAwait(false);
                Assert2.Equal(2, workerFiltered.RootElement.GetProperty("items").GetArrayLength(), "worker filter returns both wrk_filter_a runs");

                using JsonDocument sourceFiltered = await ReadJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs?sourceIp=198.51.100.41",
                    ct).ConfigureAwait(false);
                Assert2.Equal(2, sourceFiltered.RootElement.GetProperty("items").GetArrayLength(), "source-ip filter returns both matching runs");

                using JsonDocument combinedFiltered = await ReadJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs?workerId=wrk_filter_b&sourceIp=198.51.100.41",
                    ct).ConfigureAwait(false);
                JsonElement[] combinedItems = combinedFiltered.RootElement.GetProperty("items").EnumerateArray().ToArray();
                Assert2.Equal(1, combinedItems.Length, "combined filters narrow results to a single run");
                Assert2.Equal(matchingRun.Id, combinedItems[0].GetProperty("id").GetString()!, "combined filters return the expected run");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task RemoteRoutesExposeWorkerLogsAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            CancellationTokenSource? workerCts = null;
            Task? workerTask = null;
            string root = NewTempRoot("tempo-runlog-remote");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"remote\":true}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Run Log Remote" }, ct).ConfigureAwait(false);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, ct).ConfigureAwait(false);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_run_logs_1", null, ct).ConfigureAwait(false);
                (workerCts, workerTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_run_logs_1", token.Token), ct);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_run_logs_1", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"source\":\"remote\"}", null, null, "198.51.100.30", ct).ConfigureAwait(false);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);
                await serveTask.ConfigureAwait(false);

                using HttpClient client = CreateAdminClient(port);
                using JsonDocument activity = await ReadJsonAsync(client, "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/activity", ct).ConfigureAwait(false);
                JsonElement[] assignments = activity.RootElement.GetProperty("assignments").EnumerateArray().ToArray();
                Assert2.True(assignments.Length >= 1, "remote assignment history returned");
                Assert2.Equal("wrk_run_logs_1", assignments[0].GetProperty("workerId").GetString()!, "worker id recorded in assignment history");
                Assert2.Equal("198.51.100.30", activity.RootElement.GetProperty("run").GetProperty("sourceIp").GetString()!, "source ip preserved for remote run");

                using JsonDocument files = await ReadJsonAsync(client, "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs", ct).ConfigureAwait(false);
                JsonElement[] logFiles = files.RootElement.EnumerateArray().ToArray();
                JsonElement workerLog = logFiles.First(file => file.GetProperty("kind").GetString() == "Worker");
                JsonElement stepLog = logFiles.First(file => file.GetProperty("kind").GetString() == "Step");

                string workerLogPath = Uri.EscapeDataString(workerLog.GetProperty("path").GetString()!);
                using JsonDocument workerRead = await ReadJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs/content?path=" + workerLogPath,
                    ct).ConfigureAwait(false);
                string workerText = workerRead.RootElement.GetProperty("content").GetString() ?? string.Empty;
                Assert2.True(workerText.Contains("Worker accepted the assignment and started execution", StringComparison.Ordinal), "worker start message logged");
                Assert2.True(workerText.Contains("Assignment completed with result Success", StringComparison.Ordinal), "worker completion message logged");

                string stepLogPath = Uri.EscapeDataString(stepLog.GetProperty("path").GetString()!);
                using JsonDocument deleteOne = await DeleteJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs/content?path=" + stepLogPath,
                    ct).ConfigureAwait(false);
                Assert2.True(
                    string.Equals(deleteOne.RootElement.GetProperty("action").GetString(), "Deleted", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(deleteOne.RootElement.GetProperty("action").GetString(), "Truncated", StringComparison.OrdinalIgnoreCase),
                    "single run-log delete succeeded");

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "remote run completed successfully");
                Assert2.Equal("wrk_run_logs_1", completed.AssignedWorkerId!, "remote worker assigned");
            }
            finally
            {
                await StopWorkerTaskAsync(workerCts, workerTask).ConfigureAwait(false);
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task JavaScriptSourceLogsUseRealLineBreaksAsync(CancellationToken ct)
        {
            if (!await NodeAvailableAsync(ct).ConfigureAwait(false)) return;

            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-runlog-javascript-linebreaks");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Run Log JavaScript" }, ct).ConfigureAwait(false);

                int port = FreePort();
                Settings settings = CreateServerSettings(root, port, serverCanExecuteWorkload: true);
                server = new TempoServer(settings, SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                DataFlowRecord flow = await CreateJavaScriptSourceFlowAsync(driver, settings, tenant.Id, ct).ConfigureAwait(false);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":123}", null, null, "198.51.100.40", ct).ConfigureAwait(false);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);

                using HttpClient client = CreateAdminClient(port);
                using JsonDocument files = await ReadJsonAsync(client, "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs", ct).ConfigureAwait(false);
                JsonElement stepLog = files.RootElement.EnumerateArray().First(file => file.GetProperty("kind").GetString() == "Step");
                string stepLogPath = Uri.EscapeDataString(stepLog.GetProperty("path").GetString()!);

                using JsonDocument stepRead = await ReadJsonAsync(
                    client,
                    "/v1.0/tenants/" + tenant.Id + "/runs/" + run.Id + "/logs/content?path=" + stepLogPath + "&tailLines=50&maxBytes=65536",
                    ct).ConfigureAwait(false);
                string stepLogContent = stepRead.RootElement.GetProperty("content").GetString() ?? string.Empty;

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "javascript source run completed successfully");
                Assert2.True(stepLogContent.Contains("Echo step received input: {\"value\":123}", StringComparison.Ordinal), "javascript step log contains the emitted message");
                Assert2.True(!stepLogContent.Contains("\\n", StringComparison.Ordinal), "javascript step log does not contain literal newline escape sequences");
                Assert2.True(stepLogContent.Contains("completed with result Success", StringComparison.Ordinal), "completion line remains on its own line");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task PruneRemovesExpiredCompletedRunsAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-runlog-prune");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Run Log Prune" }, ct).ConfigureAwait(false);
                DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenant.Id,
                    Name = "Prune flow",
                    StartStepId = "noop",
                    Transitions = new Dictionary<string, StepTransition> { ["noop"] = new StepTransition() }
                }, ct).ConfigureAwait(false);

                FlowRun oldRun = await driver.FlowRuns.CreateAsync(new FlowRun
                {
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    State = FlowRunStateEnum.Succeeded,
                    DispatchState = FlowRunDispatchStateEnum.Completed,
                    CreatedUtc = DateTime.UtcNow.AddDays(-3),
                    StartedUtc = DateTime.UtcNow.AddDays(-3).AddMinutes(1),
                    CompletedUtc = DateTime.UtcNow.AddDays(-3).AddMinutes(2),
                    LastUpdateUtc = DateTime.UtcNow.AddDays(-3).AddMinutes(2)
                }, ct).ConfigureAwait(false);

                FlowRun recentRun = await driver.FlowRuns.CreateAsync(new FlowRun
                {
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    State = FlowRunStateEnum.Succeeded,
                    DispatchState = FlowRunDispatchStateEnum.Completed,
                    CreatedUtc = DateTime.UtcNow,
                    StartedUtc = DateTime.UtcNow,
                    CompletedUtc = DateTime.UtcNow,
                    LastUpdateUtc = DateTime.UtcNow
                }, ct).ConfigureAwait(false);

                Settings settings = CreateServerSettings(root, FreePort(), serverCanExecuteWorkload: false);
                settings.RunLogs.RetentionDays = 1;
                server = new TempoServer(settings, SilentLogger(), driver, new StepManager());

                RunLogSession oldSession = (await server.RunLogs.CreateSessionAsync(new RunLogSessionContext
                {
                    FlowRunId = oldRun.Id,
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    AttemptNumber = 1,
                    RunAssignmentId = "ras_prune_old",
                    WorkerId = "wrk_prune_old",
                    NodeKind = "Worker"
                }, ct).ConfigureAwait(false))!;
                await oldSession.AppendRunAsync("Info", "old run", ct).ConfigureAwait(false);

                RunLogSession recentSession = (await server.RunLogs.CreateSessionAsync(new RunLogSessionContext
                {
                    FlowRunId = recentRun.Id,
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    AttemptNumber = 1,
                    RunAssignmentId = "ras_prune_recent",
                    WorkerId = "wrk_prune_recent",
                    NodeKind = "Worker"
                }, ct).ConfigureAwait(false))!;
                await recentSession.AppendRunAsync("Info", "recent run", ct).ConfigureAwait(false);

                string oldRoot = server.RunLogs.ResolveRunRoot(oldRun.Id);
                string recentRoot = server.RunLogs.ResolveRunRoot(recentRun.Id);
                Directory.SetLastWriteTimeUtc(oldRoot, DateTime.UtcNow.AddDays(-3));
                Directory.SetLastWriteTimeUtc(recentRoot, DateTime.UtcNow);

                int deleted = await server.PruneRunLogsOnceAsync(ct).ConfigureAwait(false);
                Assert2.Equal(1, deleted, "one expired run-log directory pruned");
                Assert2.True(!Directory.Exists(oldRoot), "expired completed run logs removed");
                Assert2.True(Directory.Exists(recentRoot), "recent run logs retained");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static Settings CreateServerSettings(string root, int port, bool serverCanExecuteWorkload)
        {
            Settings settings = new Settings();
            settings.Rest.Hostname = "127.0.0.1";
            settings.Rest.Port = port;
            settings.Auth.AdminApiKey = AdminApiKey;
            settings.Logging.ConsoleLogging = false;
            settings.Logging.FileLogging = false;
            settings.Logging.LogDirectory = Path.Combine(root, "server-logs");
            settings.Logging.LogFilename = "tempo.log";
            settings.RequestHistory.Enabled = false;
            settings.Hydration.SeedDefaults = false;
            settings.Artifacts.RootPath = Path.Combine(root, "artifacts");
            settings.Runtimes.ExternalExecution.CacheRoot = Path.Combine(root, "server-cache");
            settings.Runtimes.ExternalExecution.ScratchRoot = Path.Combine(root, "server-scratch");
            settings.RunLogs.Enabled = true;
            settings.RunLogs.RootPath = Path.Combine(root, "run-logs");
            settings.RunLogs.RetentionDays = 7;
            settings.RunLogs.PruneIntervalMinutes = 60;
            settings.Engine.QueueEnabled = true;
            settings.Engine.ServerCanExecuteWorkload = serverCanExecuteWorkload;
            settings.Engine.MaxConcurrentRuns = 1;
            settings.Engine.PollIntervalMs = 25;
            settings.Engine.LeaseDurationMs = 60000;
            settings.Engine.WorkerHeartbeatTimeoutMs = 30000;
            settings.Engine.MaxAssignmentAttempts = 3;
            return settings;
        }

        private static WorkerSettings CreateWorkerSettings(string root, int port, string workerId, string workerToken)
        {
            WorkerSettings settings = new WorkerSettings
            {
                ServerEndpoint = "http://127.0.0.1:" + port,
                WorkerId = workerId,
                WorkerToken = workerToken,
                Name = workerId,
                Kind = "Worker",
                MaxConcurrentRuns = 1,
                MaxTaskTimeoutMs = 30000,
                ReconnectDelayMs = 1000,
                RequestTimeoutMs = 10000,
                Logging = new Tempo.Core.Settings.LoggingSettings
                {
                    ConsoleLogging = false,
                    FileLogging = false,
                    LogDirectory = Path.Combine(root, workerId, "logs"),
                    LogFilename = "tempo.worker.log"
                }
            };

            settings.Runtimes.ExternalExecution.CacheRoot = Path.Combine(root, workerId, "cache");
            settings.Runtimes.ExternalExecution.ScratchRoot = Path.Combine(root, workerId, "scratch");
            settings.RunLogs.Enabled = true;
            settings.RunLogs.RootPath = Path.Combine(root, "run-logs");
            return settings;
        }

        private static async Task<DataFlowRecord> CreateRestFlowAsync(SqliteDatabaseDriver driver, string tenantId, string url, CancellationToken token)
        {
            string executionKey = "tempo.runlog.rest." + Guid.NewGuid().ToString("N");
            await driver.Steps.CreateAsync(new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = executionKey,
                Name = "REST " + executionKey,
                RuntimeKey = StepRuntimeKeys.ExternalRest,
                RuntimeConfig = new ExternalRestRuntimeConfig
                {
                    Method = "GET",
                    Url = url,
                    TimeoutMs = 5000
                }
            }, token).ConfigureAwait(false);

            return await driver.DataFlows.CreateAsync(new DataFlowRecord
            {
                TenantId = tenantId,
                Name = "flow-" + executionKey,
                StartStepId = executionKey,
                Transitions = new Dictionary<string, StepTransition>
                {
                    [executionKey] = new StepTransition()
                }
            }, token).ConfigureAwait(false);
        }

        private static async Task<DataFlowRecord> CreateJavaScriptSourceFlowAsync(SqliteDatabaseDriver driver, Settings settings, string tenantId, CancellationToken token)
        {
            LocalFilesystemArtifactBlobStore blobStore = new LocalFilesystemArtifactBlobStore(settings.Artifacts);
            SourceStepPackageService sourceSteps = new SourceStepPackageService(driver, blobStore, settings.Runtimes.ExternalExecution);
            string executionKey = "tempo.runlog.javascript." + Guid.NewGuid().ToString("N");

            await sourceSteps.CreateAsync(tenantId, new SourceStepCreateRequest
            {
                ExecutionKey = executionKey,
                Name = "JavaScript log echo",
                Language = "JavaScript",
                FileName = "handler.js",
                Function = "run",
                Code = "exports.run = async function(req) {\n  console.log(\"Echo step received input:\", req.data);\n  return { ok: true, input: req.data };\n};\n"
            }, token).ConfigureAwait(false);

            return await driver.DataFlows.CreateAsync(new DataFlowRecord
            {
                TenantId = tenantId,
                Name = "flow-" + executionKey,
                StartStepId = executionKey,
                Transitions = new Dictionary<string, StepTransition>
                {
                    [executionKey] = new StepTransition()
                }
            }, token).ConfigureAwait(false);
        }

        private static (CancellationTokenSource, Task) StartWorkerTask(WorkerSettings settings, CancellationToken token)
        {
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            WorkerNode worker = new WorkerNode(settings, SilentLogger());
            Task task = Task.Run(() => worker.RunAsync(cts.Token), CancellationToken.None);
            return (cts, task);
        }

        private static async Task StopWorkerTaskAsync(CancellationTokenSource? cts, Task? task)
        {
            if (cts != null)
            {
                try { cts.Cancel(); } catch { /* ignore */ }
            }

            if (task != null)
            {
                try { await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch { /* ignore */ }
            }

            cts?.Dispose();
        }

        private static async Task WaitForWorkerOnlineAsync(Tempo.Server.Services.RunDispatchCoordinator coordinator, string workerId, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                WorkerRecord? worker = await coordinator.ReadWorkerAsync(workerId, token).ConfigureAwait(false);
                if (worker != null && string.Equals(worker.State, "Online", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                await Task.Delay(50, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for worker '" + workerId + "' to become online.");
        }

        private static async Task<FlowRun> WaitForTerminalAsync(SqliteDatabaseDriver driver, string tenantId, string runId, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            FlowRun? latest = null;
            while (DateTime.UtcNow < deadline)
            {
                latest = await driver.FlowRuns.ReadAsync(tenantId, runId, token).ConfigureAwait(false);
                if (latest != null && latest.State != FlowRunStateEnum.Queued && latest.State != FlowRunStateEnum.Running)
                {
                    return latest;
                }

                await Task.Delay(50, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Run '" + runId + "' did not complete in time. Last state: " + (latest?.State.ToString() ?? "missing"));
        }

        private static LoggingModule SilentLogger()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            return logging;
        }

        private static HttpClient CreateAdminClient(int port)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:" + port, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(15)
            };
            client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, AdminApiKey);
            return client;
        }

        private static async Task<JsonDocument> ReadJsonAsync(HttpClient client, string relativeUrl, CancellationToken token)
        {
            using HttpResponseMessage response = await client.GetAsync(relativeUrl, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "GET succeeded for " + relativeUrl);
            return JsonDocument.Parse(content);
        }

        private static async Task<JsonDocument> DeleteJsonAsync(HttpClient client, string relativeUrl, CancellationToken token)
        {
            using HttpResponseMessage response = await client.DeleteAsync(relativeUrl, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "DELETE succeeded for " + relativeUrl);
            return JsonDocument.Parse(content);
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<bool> NodeAvailableAsync(CancellationToken token)
        {
            try
            {
                using Process process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.StartInfo.ArgumentList.Add("--version");
                if (!process.Start()) return false;
                await process.WaitForExitAsync(token).ConfigureAwait(false);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string NewTempRoot(string prefix)
        {
            string path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Ignore temp cleanup failures.
            }
        }

        private sealed class OneShotHttpServer : IDisposable
        {
            private readonly TcpListener _Listener;
            private readonly string _ResponseBody;

            public OneShotHttpServer(int port, string responseBody)
            {
                _Listener = new TcpListener(System.Net.IPAddress.Loopback, port);
                _Listener.Start();
                _ResponseBody = responseBody;
                Url = "http://127.0.0.1:" + port + "/";
            }

            public string Url { get; }

            public async Task ServeOnceAsync(CancellationToken token)
            {
                using TcpClient client = await _Listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                await using NetworkStream stream = client.GetStream();
                await DrainRequestHeadersAsync(stream, token).ConfigureAwait(false);
                byte[] body = Encoding.UTF8.GetBytes(_ResponseBody);
                byte[] header = Encoding.UTF8.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json\r\n" +
                    "Content-Length: " + body.Length + "\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(header.AsMemory(0, header.Length), token).ConfigureAwait(false);
                await stream.WriteAsync(body.AsMemory(0, body.Length), token).ConfigureAwait(false);
            }

            private static async Task DrainRequestHeadersAsync(NetworkStream stream, CancellationToken token)
            {
                using StreamReader reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                try
                {
                    while (true)
                    {
                        string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                        if (string.IsNullOrEmpty(line)) break;
                    }
                }
                catch
                {
                    // Ignore request-drain failures in the test helper.
                }
            }

            public void Dispose()
            {
                try { _Listener.Stop(); } catch { /* ignore */ }
            }
        }
    }
}
