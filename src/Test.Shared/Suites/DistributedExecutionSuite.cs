namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo;
    using Tempo.Core;
    using Tempo.Core.Database;
    using Tempo.Core.Database.Common;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Tempo.Core.Workers;
    using Tempo.Enums;
    using Tempo.Server;
    using Tempo.Server.Services;
    using Tempo.Worker;
    using Touchstone.Core;
    using CoreTenant = Tempo.Core.Models.Tenant;

    public static class DistributedExecutionSuite
    {
        private const string EchoStepExecutionKey = "tempo.test.echo";
        private const string BuiltinOnlyExecutionKey = "tempo.test.builtin_only";
        private const string AdminApiKey = "tempo-test-admin-api-key";

        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "DistributedExecution",
                displayName: "Coordinator-based distributed execution foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DistributedExecution", "LocalCoordinatorPathExecutesRun", "Server-local execution flows through the coordinator and pseudo-worker path", LocalCoordinatorPathExecutesRunAsync),
                    new TestCaseDescriptor("DistributedExecution", "CoordinatorOwnsQueuedCancel", "Queued cancel is centralized in the coordinator", CoordinatorOwnsQueuedCancelAsync),
                    new TestCaseDescriptor("DistributedExecution", "TriggerReturns202WhenNoExecutor", "Trigger wait remains backward-compatible when no executor is available", TriggerReturns202WhenNoExecutorAsync),
                    new TestCaseDescriptor("DistributedExecution", "ApiAuthenticatedTriggerRequiresTempoAuth", "HTTP triggers can require normal Tempo API authentication based on the flow policy", ApiAuthenticatedTriggerRequiresTempoAuthAsync),
                    new TestCaseDescriptor("DistributedExecution", "TriggerIncludesWorkerHeaderWhenRunAssigned", "HTTP trigger responses include the assigned worker id when a worker executes the run", TriggerIncludesWorkerHeaderWhenRunAssignedAsync),
                    new TestCaseDescriptor("DistributedExecution", "RemoteWorkerExecutesPersistedRestFlow", "A remote worker can execute a persisted REST flow end to end", RemoteWorkerExecutesPersistedRestFlowAsync),
                    new TestCaseDescriptor("DistributedExecution", "WorkerMaxTaskTimeoutIsPersistedAndEnforced", "Worker max task timeout is persisted and cancels slow assignments", WorkerMaxTaskTimeoutIsPersistedAndEnforcedAsync),
                    new TestCaseDescriptor("DistributedExecution", "LabelPinnedPrefersMatchingWorker", "LabelPinned prefers the worker whose labels satisfy the flow routing hint", LabelPinnedPrefersMatchingWorkerAsync),
                    new TestCaseDescriptor("DistributedExecution", "CapabilityMismatchFailsFast", "Runs fail fast when live workers cannot satisfy the execution plan", CapabilityMismatchFailsFastAsync),
                    new TestCaseDescriptor("DistributedExecution", "WorkerDisconnectBeforeAssignAckRequeuesToLocalServer", "A worker disconnect before assign-ack causes the run to recover and requeue", WorkerDisconnectBeforeAssignAckRequeuesToLocalServerAsync),
                    new TestCaseDescriptor("DistributedExecution", "WorkerDisconnectAfterAssignAckRequeuesToLocalServer", "A worker disconnect after assign-ack still recovers to exactly one terminal outcome", WorkerDisconnectAfterAssignAckRequeuesToLocalServerAsync),
                    new TestCaseDescriptor("DistributedExecution", "StaleHeartbeatRequeuesToLocalServer", "A stale worker heartbeat causes the active assignment to recover and requeue", StaleHeartbeatRequeuesToLocalServerAsync),
                    new TestCaseDescriptor("DistributedExecution", "DuplicateCompletionIsRecordedAsOrphan", "A duplicate completion frame is ignored and recorded as orphan_completion", DuplicateCompletionIsRecordedAsOrphanAsync),
                    new TestCaseDescriptor("DistributedExecution", "DuplicateServerStartsApiOnlyAndSuppressesScheduling", "A second live server suppresses scheduling instead of competing for ownership", DuplicateServerStartsApiOnlyAndSuppressesSchedulingAsync),
                    new TestCaseDescriptor("DistributedExecution", "WorkerManagementRoutesSupportListDrainResumeBlockUnblock", "Operators can list workers, inspect one worker, drain or resume it, and block or unblock it end to end", WorkerManagementRoutesSupportListDrainResumeAsync)
                });
        }

        private static async Task LocalCoordinatorPathExecutesRunAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            RunDispatchCoordinator? coordinator = null;
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                StepManager stepManager = new StepManager();
                stepManager.Add(new EchoStep(tenant.Id));

                DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenant.Id,
                    Name = "f",
                    StartStepId = EchoStepExecutionKey,
                    Transitions = new Dictionary<string, StepTransition>
                    {
                        [EchoStepExecutionKey] = new StepTransition()
                    }
                }, ct);

                coordinator = new RunDispatchCoordinator(
                    driver,
                    stepManager,
                    new EngineSettings
                    {
                        QueueEnabled = true,
                        ServerCanExecuteWorkload = true,
                        MaxConcurrentRuns = 1,
                        PollIntervalMs = 25,
                        LeaseDurationMs = 60000
                    },
                    SilentLogger());

                coordinator.Start();
                FlowRun run = await coordinator.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":123}", null, null, null, ct);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct);

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "flow succeeded");
                Assert2.Equal(FlowRunDispatchStateEnum.Completed, completed.DispatchState, "dispatch completed");
                Assert2.NotNull(completed.ExecutionNodeKind, "execution node kind set");
                Assert2.Equal(ExecutionNodeKindEnum.Server, completed.ExecutionNodeKind!.Value, "server node kind");
                Assert2.Equal(LocalServerRunExecutor.WorkerId, completed.AssignedWorkerId!, "local worker assigned");
                Assert2.NotNull(completed.RunAssignmentId, "assignment created");
                Assert2.NotNull(completed.AssignedUtc, "assigned time recorded");
                Assert2.NotNull(completed.StartedUtc, "started recorded");
                Assert2.NotNull(completed.CompletedUtc, "completed recorded");
                Assert2.True((completed.OutputData ?? string.Empty).Contains("\"echo\":true", StringComparison.Ordinal), "output returned");

                List<StepRun> steps = await driver.FlowRuns.EnumerateStepRunsAsync(tenant.Id, run.Id, ct);
                Assert2.Equal(1, steps.Count, "one step run");
            }
            finally
            {
                try { coordinator?.Stop(); } catch { /* ignore */ }
                try { coordinator?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
            }
        }

        private static async Task CoordinatorOwnsQueuedCancelAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            RunDispatchCoordinator? coordinator = null;
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenant.Id,
                    Name = "f",
                    StartStepId = "noop",
                    Transitions = new Dictionary<string, StepTransition> { ["noop"] = new StepTransition() }
                }, ct);

                coordinator = new RunDispatchCoordinator(
                    driver,
                    new StepManager(),
                    new EngineSettings
                    {
                        QueueEnabled = false,
                        ServerCanExecuteWorkload = false,
                        PollIntervalMs = 25,
                        LeaseDurationMs = 60000
                    },
                    SilentLogger());

                coordinator.Start();
                FlowRun run = await coordinator.EnqueueAsync(tenant.Id, flow.Id, null, null, null, null, ct);
                bool cancelled = await coordinator.CancelQueuedAsync(tenant.Id, run.Id, ct);
                FlowRun? stored = await driver.FlowRuns.ReadAsync(tenant.Id, run.Id, ct);

                Assert2.True(cancelled, "cancel succeeded");
                Assert2.NotNull(stored, "stored run");
                Assert2.Equal(FlowRunStateEnum.Cancelled, stored!.State, "run cancelled");
                Assert2.Equal(FlowRunDispatchStateEnum.Cancelled, stored.DispatchState, "dispatch cancelled");
            }
            finally
            {
                try { coordinator?.Stop(); } catch { /* ignore */ }
                try { coordinator?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
            }
        }

        private static async Task TriggerReturns202WhenNoExecutorAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-trigger202");
            try
            {
                int port = FreePort();
                Settings settings = CreateServerSettings(root, port, serverCanExecuteWorkload: false);

                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, "https://example.com/not-called", null, ct).ConfigureAwait(false);
                flow.MaxRuntimeMs = 1;
                flow = await driver.DataFlows.UpdateAsync(flow, ct).ConfigureAwait(false);
                TriggerRecord trigger = await driver.Triggers.CreateAsync(new TriggerRecord
                {
                    TenantId = tenant.Id,
                    Name = "http",
                    TriggerType = TriggerTypeEnum.Http,
                    DataFlowId = flow.Id,
                    Configuration = "{\"allowedMethods\":[\"GET\"]}"
                }, ct);

                server = new TempoServer(settings, SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                using HttpResponseMessage response = await client.GetAsync("http://127.0.0.1:" + port + "/v1.0/triggers/http/" + trigger.Id, ct).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                Assert2.Equal(HttpStatusCode.Accepted, response.StatusCode, "202 accepted");
                Assert2.True(response.Headers.TryGetValues(Constants.HeaderRunId, out IEnumerable<string>? runIds), "run id header");
                string runId = runIds!.First();
                Assert2.True(response.Headers.TryGetValues(Constants.HeaderRunState, out IEnumerable<string>? states), "run state header");
                Assert2.Equal(FlowRunStateEnum.Queued.ToString(), states!.First(), "run still queued");
                Assert2.Equal("null", body, "pending trigger body");

                FlowRun? stored = await driver.FlowRuns.ReadAsync(tenant.Id, runId, ct);
                Assert2.NotNull(stored, "stored run");
                Assert2.Equal(FlowRunStateEnum.Queued, stored!.State, "queued in database");
                Assert2.Equal(FlowRunDispatchStateEnum.Pending, stored.DispatchState, "dispatch pending in database");
            }
            finally
            {
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task ApiAuthenticatedTriggerRequiresTempoAuthAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-trigger-auth");
            try
            {
                int port = FreePort();
                Settings settings = CreateServerSettings(root, port, serverCanExecuteWorkload: false, adminApiKey: AdminApiKey);

                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Protected Trigger" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, "https://example.com/not-called", null, ct).ConfigureAwait(false);
                flow.MaxRuntimeMs = 1;
                flow.InvocationAuthMode = DataFlowInvocationAuthModeEnum.ApiAuthenticated;
                flow = await driver.DataFlows.UpdateAsync(flow, ct).ConfigureAwait(false);
                TriggerRecord trigger = await driver.Triggers.CreateAsync(new TriggerRecord
                {
                    TenantId = tenant.Id,
                    Name = "http-auth",
                    TriggerType = TriggerTypeEnum.Http,
                    DataFlowId = flow.Id,
                    Configuration = "{\"allowedMethods\":[\"GET\"]}"
                }, ct).ConfigureAwait(false);

                server = new TempoServer(settings, SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string triggerUrl = "http://127.0.0.1:" + port + "/v1.0/triggers/http/" + trigger.Id;
                using HttpResponseMessage unauthenticated = await client.GetAsync(triggerUrl, ct).ConfigureAwait(false);
                Assert2.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode, "unauthenticated trigger rejected");

                var runsAfterReject = await driver.FlowRuns.EnumerateAsync(new FlowRunFilter
                {
                    TenantId = tenant.Id,
                    DataFlowId = flow.Id,
                    PageSize = 10
                }, ct).ConfigureAwait(false);
                Assert2.Equal(0, runsAfterReject.TotalCount, "unauthenticated trigger did not enqueue");

                using HttpRequestMessage authenticatedRequest = new HttpRequestMessage(HttpMethod.Get, triggerUrl);
                authenticatedRequest.Headers.TryAddWithoutValidation(Constants.HeaderApiKey, AdminApiKey);
                using HttpResponseMessage authenticated = await client.SendAsync(authenticatedRequest, ct).ConfigureAwait(false);
                string body = await authenticated.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                Assert2.Equal(HttpStatusCode.Accepted, authenticated.StatusCode, "authenticated trigger accepted");
                Assert2.True(authenticated.Headers.TryGetValues(Constants.HeaderRunId, out IEnumerable<string>? runIds), "run id header");
                string runId = runIds!.First();
                Assert2.Equal("null", body, "pending trigger body");

                FlowRun? stored = await driver.FlowRuns.ReadAsync(tenant.Id, runId, ct).ConfigureAwait(false);
                Assert2.NotNull(stored, "stored run");
                Assert2.Equal(FlowRunStateEnum.Queued, stored!.State, "queued in database");
                Assert2.Equal(trigger.Id, stored.TriggerId!, "trigger id persisted");
            }
            finally
            {
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task TriggerIncludesWorkerHeaderWhenRunAssignedAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            CancellationTokenSource? workerCts = null;
            Task? workerTask = null;
            string root = NewTempRoot("tempo-trigger-worker-header");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"header\":true}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Headers" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, null, ct).ConfigureAwait(false);
                TriggerRecord trigger = await driver.Triggers.CreateAsync(new TriggerRecord
                {
                    TenantId = tenant.Id,
                    Name = "http",
                    TriggerType = TriggerTypeEnum.Http,
                    DataFlowId = flow.Id,
                    Configuration = "{\"allowedMethods\":[\"POST\"]}"
                }, ct).ConfigureAwait(false);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_trigger_header_1", null, ct).ConfigureAwait(false);
                (workerCts, workerTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_trigger_header_1", token.Token), ct);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_trigger_header_1", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                using StringContent body = new StringContent("{\"value\":99}", Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await client.PostAsync("http://127.0.0.1:" + port + "/v1.0/triggers/http/" + trigger.Id, body, ct).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "trigger completed synchronously");
                Assert2.True(response.Headers.TryGetValues(Constants.HeaderWorkerId, out IEnumerable<string>? workerIds), "worker id header");
                Assert2.Equal("wrk_trigger_header_1", workerIds!.First(), "assigned worker header");
                Assert2.True(response.Headers.TryGetValues(Constants.HeaderRunId, out IEnumerable<string>? runIds), "run id header");
                Assert2.True(!string.IsNullOrWhiteSpace(runIds!.FirstOrDefault()), "run id populated");
                Assert2.True(!string.IsNullOrWhiteSpace(responseBody), "response body returned");

                await serveTask.ConfigureAwait(false);
            }
            finally
            {
                await StopWorkerTaskAsync(workerCts, workerTask).ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task RemoteWorkerExecutesPersistedRestFlowAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            CancellationTokenSource? workerCts = null;
            Task? workerTask = null;
            string root = NewTempRoot("tempo-remote-worker");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"remote\":true}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Remote" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, null, ct);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_remote_1", null, ct).ConfigureAwait(false);
                (workerCts, workerTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_remote_1", token.Token), ct);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_remote_1", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":42}", null, null, null, ct).ConfigureAwait(false);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "run succeeded");
                Assert2.Equal(FlowRunDispatchStateEnum.Completed, completed.DispatchState, "dispatch completed");
                Assert2.NotNull(completed.ExecutionNodeKind, "execution node kind set");
                Assert2.Equal(ExecutionNodeKindEnum.Worker, completed.ExecutionNodeKind!.Value, "worker node kind");
                Assert2.Equal("wrk_remote_1", completed.AssignedWorkerId!, "remote worker assigned");
                Assert2.Equal(1, completed.DispatchAttempt, "single assignment");
                Assert2.True(!string.IsNullOrWhiteSpace(completed.OutputData), "remote output recorded");

                await serveTask.ConfigureAwait(false);
            }
            finally
            {
                await StopWorkerTaskAsync(workerCts, workerTask).ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task WorkerMaxTaskTimeoutIsPersistedAndEnforcedAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            CancellationTokenSource? workerCts = null;
            Task? workerTask = null;
            string root = NewTempRoot("tempo-worker-timeout");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"slow\":true}", responseDelayMs: 1000);
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Timeout" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, null, ct);

                StepRecord? restStep = await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, flow.StartStepId, ct).ConfigureAwait(false);
                Assert2.NotNull(restStep, "rest step exists");
                ExternalRestRuntimeConfig? runtimeConfig = restStep!.RuntimeConfig as ExternalRestRuntimeConfig;
                Assert2.NotNull(runtimeConfig, "runtime config exists");
                runtimeConfig!.TimeoutMs = 5000;
                restStep.RuntimeConfig = runtimeConfig;
                await driver.Steps.UpdateAsync(restStep, ct).ConfigureAwait(false);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_timeout_1", null, ct).ConfigureAwait(false);
                (workerCts, workerTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_timeout_1", token.Token, 250), ct);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_timeout_1", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":7}", null, null, null, ct).ConfigureAwait(false);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);
                WorkerRecord? worker = await server.DispatchCoordinator.ReadWorkerAsync("wrk_timeout_1", ct).ConfigureAwait(false);

                Assert2.Equal(FlowRunStateEnum.Exception, completed.State, "slow run timed out on worker");
                Assert2.True((completed.ErrorMessage ?? string.Empty).Contains("maxTaskTimeoutMs", StringComparison.Ordinal), "timeout message recorded");
                Assert2.NotNull(worker, "worker persisted");
                Assert2.Equal(1, worker!.MaxConcurrentRuns, "worker max concurrency persisted");
                Assert2.Equal(250, worker.MaxTaskTimeoutMs, "worker max task timeout persisted");

                try { await serveTask.ConfigureAwait(false); } catch { /* timeout raced the slow server; ignore test helper shutdown */ }
            }
            finally
            {
                await StopWorkerTaskAsync(workerCts, workerTask).ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task LabelPinnedPrefersMatchingWorkerAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            CancellationTokenSource? gpuCts = null;
            CancellationTokenSource? cpuCts = null;
            Task? gpuTask = null;
            Task? cpuTask = null;
            string root = NewTempRoot("tempo-label-pinned");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"pinned\":true}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Pinned" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, "gpu", ct);

                int port = FreePort();
                server = new TempoServer(
                    CreateServerSettings(root, port, serverCanExecuteWorkload: false, loadBalancingStrategy: "LabelPinned"),
                    SilentLogger(),
                    driver,
                    new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult gpuToken = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_gpu_1", null, ct).ConfigureAwait(false);
                WorkerTokenIssueResult cpuToken = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_cpu_1", null, ct).ConfigureAwait(false);

                (gpuCts, gpuTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_gpu_1", gpuToken.Token, 30000, "gpu"), ct);
                (cpuCts, cpuTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_cpu_1", cpuToken.Token, 30000, "cpu"), ct);

                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_gpu_1", ct).ConfigureAwait(false);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_cpu_1", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":1}", null, null, null, ct).ConfigureAwait(false);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "run succeeded");
                Assert2.Equal("wrk_gpu_1", completed.AssignedWorkerId!, "label-matched worker chosen");
                Assert2.NotNull(completed.ExecutionNodeKind, "execution node kind set");
                Assert2.Equal(ExecutionNodeKindEnum.Worker, completed.ExecutionNodeKind!.Value, "remote worker used");

                await serveTask.ConfigureAwait(false);
            }
            finally
            {
                await StopWorkerTaskAsync(gpuCts, gpuTask).ConfigureAwait(false);
                await StopWorkerTaskAsync(cpuCts, cpuTask).ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task CapabilityMismatchFailsFastAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            CancellationTokenSource? workerCts = null;
            Task? workerTask = null;
            string root = NewTempRoot("tempo-mismatch");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "Mismatch" }, ct);
                StepManager stepManager = new StepManager();
                stepManager.Add(new EchoStep(tenant.Id, BuiltinOnlyExecutionKey));

                DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenant.Id,
                    Name = "builtin-only",
                    StartStepId = BuiltinOnlyExecutionKey,
                    Transitions = new Dictionary<string, StepTransition>
                    {
                        [BuiltinOnlyExecutionKey] = new StepTransition()
                    }
                }, ct).ConfigureAwait(false);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false), SilentLogger(), driver, stepManager);
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_external_only", null, ct).ConfigureAwait(false);
                (workerCts, workerTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_external_only", token.Token), ct);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_external_only", ct).ConfigureAwait(false);

                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, null, null, null, null, ct).ConfigureAwait(false);
                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);

                Assert2.Equal(FlowRunStateEnum.Failed, completed.State, "run failed fast");
                Assert2.Equal(FlowRunDispatchStateEnum.Failed, completed.DispatchState, "dispatch failed");
                Assert2.True((completed.ErrorMessage ?? string.Empty).Contains("No eligible worker", StringComparison.OrdinalIgnoreCase), "no eligible worker message");
            }
            finally
            {
                await StopWorkerTaskAsync(workerCts, workerTask).ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task WorkerDisconnectBeforeAssignAckRequeuesToLocalServerAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            ManualWorkerClient? worker = null;
            string root = NewTempRoot("tempo-before-ack");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"recovered\":\"before-ack\"}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "RecoverBeforeAck" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, null, ct);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: true), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_flaky_before_ack", null, ct).ConfigureAwait(false);
                worker = await ManualWorkerClient.ConnectAsync(port, "wrk_flaky_before_ack", token.Token, ct).ConfigureAwait(false);
                await worker.SendHeartbeatAsync(ct).ConfigureAwait(false);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_flaky_before_ack", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":7}", null, null, null, ct).ConfigureAwait(false);
                WorkerAssignMessage assign = await worker.ReceiveAssignAsync(ct).ConfigureAwait(false);
                await worker.CloseAsync().ConfigureAwait(false);

                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "run succeeded after recovery");
                Assert2.Equal(2, completed.DispatchAttempt, "requeued onto second attempt");
                Assert2.Equal(LocalServerRunExecutor.WorkerId, completed.AssignedWorkerId!, "recovered onto local pseudo-worker");
                Assert2.NotNull(completed.ExecutionNodeKind, "execution node kind set");
                Assert2.Equal(ExecutionNodeKindEnum.Server, completed.ExecutionNodeKind!.Value, "completed on server");
                Assert2.True((assign.Assignment?.Id ?? string.Empty).Length > 0, "initial remote assignment existed");

                await serveTask.ConfigureAwait(false);
            }
            finally
            {
                if (worker != null) await worker.DisposeAsync().ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task WorkerDisconnectAfterAssignAckRequeuesToLocalServerAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            ManualWorkerClient? worker = null;
            string root = NewTempRoot("tempo-after-ack");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"recovered\":\"after-ack\"}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "RecoverAfterAck" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, null, ct);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: true), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_flaky_after_ack", null, ct).ConfigureAwait(false);
                worker = await ManualWorkerClient.ConnectAsync(port, "wrk_flaky_after_ack", token.Token, ct).ConfigureAwait(false);
                await worker.SendHeartbeatAsync(ct).ConfigureAwait(false);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_flaky_after_ack", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":9}", null, null, null, ct).ConfigureAwait(false);
                WorkerAssignMessage assign = await worker.ReceiveAssignAsync(ct).ConfigureAwait(false);
                await worker.SendAssignAckAsync(assign, accepted: true, message: null, ct).ConfigureAwait(false);
                await worker.CloseAsync().ConfigureAwait(false);

                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "run succeeded after recovery");
                Assert2.Equal(2, completed.DispatchAttempt, "second attempt issued");
                Assert2.Equal(LocalServerRunExecutor.WorkerId, completed.AssignedWorkerId!, "recovered onto local pseudo-worker");
                Assert2.NotNull(completed.ExecutionNodeKind, "execution node kind set");
                Assert2.Equal(ExecutionNodeKindEnum.Server, completed.ExecutionNodeKind!.Value, "completed on server");

                await serveTask.ConfigureAwait(false);
            }
            finally
            {
                if (worker != null) await worker.DisposeAsync().ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task StaleHeartbeatRequeuesToLocalServerAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            ManualWorkerClient? worker = null;
            string root = NewTempRoot("tempo-stale-heartbeat");
            using OneShotHttpServer restServer = new OneShotHttpServer(FreePort(), "{\"recovered\":\"heartbeat-timeout\"}");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "HeartbeatRecovery" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, restServer.Url, null, ct);

                int port = FreePort();
                server = new TempoServer(
                    CreateServerSettings(root, port, serverCanExecuteWorkload: true, workerHeartbeatTimeoutMs: 250, leaseDurationMs: 5000),
                    SilentLogger(),
                    driver,
                    new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_stale_heartbeat", null, ct).ConfigureAwait(false);
                worker = await ManualWorkerClient.ConnectAsync(port, "wrk_stale_heartbeat", token.Token, ct).ConfigureAwait(false);
                await worker.SendHeartbeatAsync(ct).ConfigureAwait(false);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_stale_heartbeat", ct).ConfigureAwait(false);

                Task serveTask = restServer.ServeOnceAsync(ct);
                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":5}", null, null, null, ct).ConfigureAwait(false);
                WorkerAssignMessage assign = await worker.ReceiveAssignAsync(ct).ConfigureAwait(false);
                await worker.SendAssignAckAsync(assign, accepted: true, message: null, ct).ConfigureAwait(false);

                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);

                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "run succeeded after heartbeat timeout recovery");
                Assert2.Equal(2, completed.DispatchAttempt, "second attempt issued");
                Assert2.Equal(LocalServerRunExecutor.WorkerId, completed.AssignedWorkerId!, "recovered onto local pseudo-worker");
                Assert2.NotNull(completed.ExecutionNodeKind, "execution node kind set");
                Assert2.Equal(ExecutionNodeKindEnum.Server, completed.ExecutionNodeKind!.Value, "completed on server");

                await serveTask.ConfigureAwait(false);
            }
            finally
            {
                if (worker != null) await worker.DisposeAsync().ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task DuplicateCompletionIsRecordedAsOrphanAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            ManualWorkerClient? worker = null;
            string root = NewTempRoot("tempo-duplicate-completion");
            try
            {
                CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "DuplicateCompletion" }, ct);
                DataFlowRecord flow = await CreateRestFlowAsync(driver, tenant.Id, "https://example.com/not-called", null, ct);

                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                WorkerTokenIssueResult token = await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_duplicate_completion", null, ct).ConfigureAwait(false);
                worker = await ManualWorkerClient.ConnectAsync(port, "wrk_duplicate_completion", token.Token, ct).ConfigureAwait(false);
                await worker.SendHeartbeatAsync(ct).ConfigureAwait(false);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_duplicate_completion", ct).ConfigureAwait(false);

                FlowRun run = await server.Dispatch.EnqueueAsync(tenant.Id, flow.Id, "{\"value\":3}", null, null, null, ct).ConfigureAwait(false);
                WorkerAssignMessage assign = await worker.ReceiveAssignAsync(ct).ConfigureAwait(false);
                await worker.SendAssignAckAsync(assign, accepted: true, message: null, ct).ConfigureAwait(false);
                await worker.SendCompletionAsync(assign, FlowRunStateEnum.Succeeded, "{\"ok\":true}", null, ct).ConfigureAwait(false);

                FlowRun completed = await WaitForTerminalAsync(driver, tenant.Id, run.Id, ct).ConfigureAwait(false);
                Assert2.Equal(FlowRunStateEnum.Succeeded, completed.State, "first completion applied");
                Assert2.Equal(1, completed.DispatchAttempt, "no retry needed");

                await worker.SendCompletionAsync(assign, FlowRunStateEnum.Succeeded, "{\"ok\":true}", null, ct).ConfigureAwait(false);
                int orphanEvents = await WaitForWorkerActivityCountAsync(driver, assign.Assignment!.Id, "orphan_completion", ct).ConfigureAwait(false);
                Assert2.True(orphanEvents >= 1, "orphan completion recorded");
            }
            finally
            {
                if (worker != null) await worker.DisposeAsync().ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static async Task DuplicateServerStartsApiOnlyAndSuppressesSchedulingAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server1 = null;
            TempoServer? server2 = null;
            string root1 = NewTempRoot("tempo-scheduler-primary");
            string root2 = NewTempRoot("tempo-scheduler-secondary");
            try
            {
                server1 = new TempoServer(CreateServerSettings(root1, FreePort(), serverCanExecuteWorkload: true), SilentLogger(), driver, new StepManager());
                await server1.StartAsync().ConfigureAwait(false);

                server2 = new TempoServer(CreateServerSettings(root2, FreePort(), serverCanExecuteWorkload: true), SilentLogger(), driver, new StepManager());
                await server2.StartAsync().ConfigureAwait(false);

                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && server2.DispatchCoordinator.SchedulingEnabled)
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                }

                Assert2.True(!server2.DispatchCoordinator.SchedulingEnabled, "second scheduler suppressed");

                bool threw = false;
                try
                {
                    await server2.Dispatch.EnqueueAsync("ten_missing", "flow_missing", null, null, null, null, ct).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Scheduling is disabled", StringComparison.OrdinalIgnoreCase))
                {
                    threw = true;
                }

                Assert2.True(threw, "enqueue is rejected when scheduling is suppressed");
            }
            finally
            {
                try { server2?.Stop(); } catch { /* ignore */ }
                try { server2?.Dispose(); } catch { /* ignore */ }
                try { server1?.Stop(); } catch { /* ignore */ }
                try { server1?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root1);
                DeleteDirectory(root2);
            }
        }

        private static async Task WorkerManagementRoutesSupportListDrainResumeAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
            TempoServer? server = null;
            CancellationTokenSource? workerCts = null;
            Task? workerTask = null;
            string root = NewTempRoot("tempo-worker-routes");
            try
            {
                int port = FreePort();
                server = new TempoServer(CreateServerSettings(root, port, serverCanExecuteWorkload: false, adminApiKey: AdminApiKey), SilentLogger(), driver, new StepManager());
                await server.StartAsync().ConfigureAwait(false);

                using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, AdminApiKey);

                string token = await RotateWorkerTokenViaRouteAsync(client, port, "wrk_routes_1", ct).ConfigureAwait(false);
                (workerCts, workerTask) = StartWorkerTask(CreateWorkerSettings(root, port, "wrk_routes_1", token), ct);
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_routes_1", ct).ConfigureAwait(false);

                JsonDocument list = await ReadJsonAsync(client, "http://127.0.0.1:" + port + "/v1.0/workers?search=wrk_routes_1", ct).ConfigureAwait(false);
                JsonElement listRoot = list.RootElement;
                Assert2.True(listRoot.GetProperty("items").GetArrayLength() >= 1, "worker appears in list");

                JsonDocument read = await ReadJsonAsync(client, "http://127.0.0.1:" + port + "/v1.0/workers/wrk_routes_1", ct).ConfigureAwait(false);
                JsonElement readRoot = read.RootElement;
                Assert2.Equal("wrk_routes_1", readRoot.GetProperty("id").GetString()!, "read worker id");
                Assert2.Equal("Online", readRoot.GetProperty("state").GetString()!, "worker online");
                Assert2.Equal(1, readRoot.GetProperty("maxConcurrentRuns").GetInt32(), "max concurrency exposed");
                Assert2.Equal(30000, readRoot.GetProperty("maxTaskTimeoutMs").GetInt32(), "max task timeout exposed");
                Assert2.True(readRoot.TryGetProperty("latestSession", out JsonElement latestSession), "latest session present");
                Assert2.Equal(JsonValueKind.Object, latestSession.ValueKind, "latest session object");

                JsonDocument drained = await PostJsonAsync(client, "http://127.0.0.1:" + port + "/v1.0/workers/wrk_routes_1/drain", ct).ConfigureAwait(false);
                Assert2.True(drained.RootElement.GetProperty("drainMode").GetBoolean(), "worker drained");

                JsonDocument resumed = await PostJsonAsync(client, "http://127.0.0.1:" + port + "/v1.0/workers/wrk_routes_1/resume", ct).ConfigureAwait(false);
                Assert2.True(!resumed.RootElement.GetProperty("drainMode").GetBoolean(), "worker resumed");

                JsonDocument blocked = await PostJsonAsync(client, "http://127.0.0.1:" + port + "/v1.0/workers/wrk_routes_1/block", ct).ConfigureAwait(false);
                Assert2.True(!blocked.RootElement.GetProperty("enabled").GetBoolean(), "worker blocked");
                WorkerRecord blockedWorker = await WaitForWorkerAsync(
                    server.DispatchCoordinator,
                    "wrk_routes_1",
                    worker => worker != null && !worker.Enabled && string.Equals(worker.State, "Offline", StringComparison.OrdinalIgnoreCase),
                    "blocked worker to disconnect",
                    ct).ConfigureAwait(false);
                Assert2.True(!blockedWorker.Enabled, "blocked worker disabled");
                await Task.Delay(1500, ct).ConfigureAwait(false);
                WorkerRecord? stillBlocked = await server.DispatchCoordinator.ReadWorkerAsync("wrk_routes_1", ct).ConfigureAwait(false);
                Assert2.NotNull(stillBlocked, "blocked worker still exists");
                Assert2.True(!stillBlocked!.Enabled, "blocked worker remains disabled while reconnect attempts are denied");

                JsonDocument unblocked = await PostJsonAsync(client, "http://127.0.0.1:" + port + "/v1.0/workers/wrk_routes_1/unblock", ct).ConfigureAwait(false);
                Assert2.True(unblocked.RootElement.GetProperty("enabled").GetBoolean(), "worker unblocked");
                await WaitForWorkerOnlineAsync(server.DispatchCoordinator, "wrk_routes_1", ct).ConfigureAwait(false);

                list.Dispose();
                read.Dispose();
                drained.Dispose();
                resumed.Dispose();
                blocked.Dispose();
                unblocked.Dispose();
            }
            finally
            {
                await StopWorkerTaskAsync(workerCts, workerTask).ConfigureAwait(false);
                try { server?.Stop(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver);
                DeleteDirectory(root);
            }
        }

        private static Settings CreateServerSettings(
            string root,
            int port,
            bool serverCanExecuteWorkload,
            string loadBalancingStrategy = "LeastLoaded",
            int workerHeartbeatTimeoutMs = 5000,
            int leaseDurationMs = 3000,
            int maxAssignmentAttempts = 3,
            string? adminApiKey = null)
        {
            RuntimeSettings runtimes = new RuntimeSettings();
            runtimes.ExternalExecution.CacheRoot = Path.Combine(root, "runtime-cache");
            runtimes.ExternalExecution.ScratchRoot = Path.Combine(root, "scratch");

            return new Settings
            {
                Rest = new RestSettings { Hostname = "127.0.0.1", Port = port, Ssl = false },
                Logging = new Tempo.Core.Settings.LoggingSettings
                {
                    ConsoleLogging = false,
                    FileLogging = false,
                    LogDirectory = Path.Combine(root, "logs"),
                    LogFilename = "tempo.log"
                },
                RequestHistory = new RequestHistorySettings { Enabled = false },
                Auth = new AuthSettings { AdminApiKey = adminApiKey ?? string.Empty },
                Artifacts = new ArtifactSettings { RootPath = Path.Combine(root, "artifacts") },
                Runtimes = runtimes,
                Hydration = new HydrationSettings { SeedDefaults = false },
                Engine = new EngineSettings
                {
                    QueueEnabled = true,
                    ServerCanExecuteWorkload = serverCanExecuteWorkload,
                    MaxConcurrentRuns = 1,
                    PollIntervalMs = 25,
                    LoadBalancingStrategy = loadBalancingStrategy,
                    WorkerHeartbeatTimeoutMs = workerHeartbeatTimeoutMs,
                    LeaseDurationMs = leaseDurationMs,
                    MaxAssignmentAttempts = maxAssignmentAttempts,
                    AllowDuplicateScheduler = false
                }
            };
        }

        private static WorkerSettings CreateWorkerSettings(string root, int port, string workerId, string workerToken, int maxTaskTimeoutMs = 30000, params string[] labels)
        {
            WorkerSettings settings = new WorkerSettings
            {
                ServerEndpoint = "http://127.0.0.1:" + port,
                WorkerId = workerId,
                WorkerToken = workerToken,
                Name = workerId,
                Kind = "Worker",
                MaxConcurrentRuns = 1,
                MaxTaskTimeoutMs = maxTaskTimeoutMs,
                ReconnectDelayMs = 1000,
                RequestTimeoutMs = 10000,
                Labels = labels.Where(label => !string.IsNullOrWhiteSpace(label)).ToList(),
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
            return settings;
        }

        private static async Task<DataFlowRecord> CreateRestFlowAsync(
            SqliteDatabaseDriver driver,
            string tenantId,
            string url,
            string? routingHintLabel,
            CancellationToken token)
        {
            string executionKey = "tempo.rest." + Guid.NewGuid().ToString("N");
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
                RoutingHintLabel = routingHintLabel,
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

        private static async Task WaitForWorkerOnlineAsync(RunDispatchCoordinator coordinator, string workerId, CancellationToken token)
        {
            await WaitForWorkerAsync(
                coordinator,
                workerId,
                worker => worker != null && string.Equals(worker.State, "Online", StringComparison.OrdinalIgnoreCase),
                "worker to become online",
                token).ConfigureAwait(false);
        }

        private static async Task<WorkerRecord> WaitForWorkerAsync(
            RunDispatchCoordinator coordinator,
            string workerId,
            Func<WorkerRecord?, bool> predicate,
            string description,
            CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                WorkerRecord? worker = await coordinator.ReadWorkerAsync(workerId, token).ConfigureAwait(false);
                if (predicate(worker))
                {
                    return worker!;
                }

                await Task.Delay(50, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for " + description + " (" + workerId + ").");
        }

        private static async Task<int> WaitForWorkerActivityCountAsync(SqliteDatabaseDriver driver, string runAssignmentId, string eventType, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                int count = await ReadWorkerActivityCountAsync(driver, runAssignmentId, eventType, token).ConfigureAwait(false);
                if (count > 0) return count;
                await Task.Delay(50, token).ConfigureAwait(false);
            }

            return await ReadWorkerActivityCountAsync(driver, runAssignmentId, eventType, token).ConfigureAwait(false);
        }

        private static async Task<int> ReadWorkerActivityCountAsync(SqliteDatabaseDriver driver, string runAssignmentId, string eventType, CancellationToken token)
        {
            string escapedAssignment = SqlDialect.Ansi.Quote(runAssignmentId);
            string escapedEvent = SqlDialect.Ansi.Quote(eventType);
            DataTable dt = await driver.ExecuteQueryAsync(
                "SELECT COUNT(*) AS event_count FROM worker_activity WHERE run_assignment_id = " + escapedAssignment +
                " AND event_type = " + escapedEvent + ";",
                false,
                token).ConfigureAwait(false);

            if (dt.Rows.Count < 1) return 0;
            return Converters.Int(dt.Rows[0], "event_count");
        }

        private static async Task<string> RotateWorkerTokenViaRouteAsync(HttpClient client, int port, string workerId, CancellationToken token)
        {
            using HttpResponseMessage response = await client.PostAsync("http://127.0.0.1:" + port + "/v1.0/workers/" + workerId + "/rotate-token", null, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "rotate token route succeeded");

            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty("token").GetString() ?? string.Empty;
        }

        private static async Task<JsonDocument> ReadJsonAsync(HttpClient client, string url, CancellationToken token)
        {
            using HttpResponseMessage response = await client.GetAsync(url, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "GET succeeded for " + url);
            return JsonDocument.Parse(content);
        }

        private static async Task<JsonDocument> PostJsonAsync(HttpClient client, string url, CancellationToken token)
        {
            using HttpResponseMessage response = await client.PostAsync(url, null, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "POST succeeded for " + url);
            return JsonDocument.Parse(content);
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

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
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
                // Ignore temp directory cleanup failures.
            }
        }

        private sealed class EchoStep : Step
        {
            public EchoStep(string tenantId, string executionKey = EchoStepExecutionKey)
            {
                Identifier = executionKey;
                TenantId = tenantId;
                Name = executionKey;
            }

            public override Task<StepResult> Run(StepRequest req)
            {
                return Task.FromResult(new StepResult
                {
                    ProtocolVersion = req.ProtocolVersion,
                    TenantId = req.TenantId,
                    DataFlowId = req.DataFlowId,
                    FlowRunId = req.FlowRunId,
                    StepRunId = req.StepRunId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Success,
                    Data = new Dictionary<string, object?>
                    {
                        ["echo"] = true,
                        ["payload"] = req.Data
                    }
                });
            }
        }

        private sealed class OneShotHttpServer : IDisposable
        {
            private readonly TcpListener _Listener;
            private readonly string _ResponseBody;
            private readonly int _ResponseDelayMs;

            public OneShotHttpServer(int port, string responseBody, int responseDelayMs = 0)
            {
                _Listener = new TcpListener(IPAddress.Loopback, port);
                _Listener.Start();
                _ResponseBody = responseBody;
                _ResponseDelayMs = Math.Max(0, responseDelayMs);
                Url = "http://127.0.0.1:" + port + "/";
            }

            public string Url { get; }

            public async Task ServeOnceAsync(CancellationToken token)
            {
                using TcpClient client = await _Listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                await using NetworkStream stream = client.GetStream();
                await DrainRequestHeadersAsync(stream, token).ConfigureAwait(false);
                if (_ResponseDelayMs > 0)
                {
                    await Task.Delay(_ResponseDelayMs, token).ConfigureAwait(false);
                }
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
                    // Ignore request-drain failures in test helper.
                }
            }

            public void Dispose()
            {
                try { _Listener.Stop(); } catch { /* ignore */ }
            }
        }

        private sealed class ManualWorkerClient : IAsyncDisposable
        {
            private readonly ClientWebSocket _Socket;
            private readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);
            private readonly string _WorkerId;
            private readonly string _WorkerSessionId;

            private ManualWorkerClient(ClientWebSocket socket, string workerId, string workerSessionId)
            {
                _Socket = socket;
                _WorkerId = workerId;
                _WorkerSessionId = workerSessionId;
            }

            public static async Task<ManualWorkerClient> ConnectAsync(int port, string workerId, string workerToken, CancellationToken token)
            {
                ClientWebSocket socket = new ClientWebSocket();
                socket.Options.SetRequestHeader(Constants.HeaderWorkerId, workerId);
                socket.Options.SetRequestHeader(Constants.HeaderWorkerToken, workerToken);

                Uri endpoint = new Uri("ws://127.0.0.1:" + port + "/v1.0/workers/connect", UriKind.Absolute);
                await socket.ConnectAsync(endpoint, token).ConfigureAwait(false);

                WorkerHelloMessage hello = new WorkerHelloMessage
                {
                    WorkerId = workerId,
                    Name = workerId,
                    Kind = "Worker",
                    Version = "0.3.0-test",
                    HostName = Environment.MachineName,
                    MaxConcurrentRuns = 1,
                    Capabilities = new List<WorkerCapabilityDescriptor>
                    {
                        new WorkerCapabilityDescriptor
                        {
                            ExecutionKey = "*",
                            TenantScope = "*",
                            SourceKind = "Registry",
                            RuntimeKey = StepRuntimeKeys.ExternalRest.ToString(),
                            SignatureHash = "*"
                        }
                    }
                };

                string helloJson = JsonSerializer.Serialize(hello, WorkerProtocolSerialization.Options);
                byte[] payload = Encoding.UTF8.GetBytes(helloJson);
                await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, endOfMessage: true, token).ConfigureAwait(false);

                string ackJson = await ReceiveTextAsync(socket, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing hello-ack.");
                WorkerHelloAckMessage? ack = JsonSerializer.Deserialize<WorkerHelloAckMessage>(ackJson, WorkerProtocolSerialization.Options);
                if (ack == null || !string.Equals(ack.Type, WorkerFrameTypes.HelloAck, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid hello-ack.");
                }

                return new ManualWorkerClient(socket, workerId, ack.WorkerSessionId);
            }

            public async Task SendHeartbeatAsync(CancellationToken token)
            {
                await SendFrameAsync(new WorkerHeartbeatMessage
                {
                    WorkerId = _WorkerId,
                    WorkerSessionId = _WorkerSessionId,
                    ActiveRuns = 0,
                    SentUtc = DateTime.UtcNow
                }, token).ConfigureAwait(false);
            }

            public async Task<WorkerAssignMessage> ReceiveAssignAsync(CancellationToken token)
            {
                while (true)
                {
                    string? json = await ReceiveTextAsync(_Socket, token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Worker socket closed before assignment.");

                    using JsonDocument document = JsonDocument.Parse(json);
                    string? type = document.RootElement.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() : null;
                    if (string.Equals(type, WorkerFrameTypes.Assign, StringComparison.Ordinal))
                    {
                        WorkerAssignMessage? assign = JsonSerializer.Deserialize<WorkerAssignMessage>(json, WorkerProtocolSerialization.Options);
                        if (assign == null) throw new InvalidOperationException("Invalid assign frame.");
                        return assign;
                    }
                }
            }

            public Task SendAssignAckAsync(WorkerAssignMessage assign, bool accepted, string? message, CancellationToken token)
            {
                return SendFrameAsync(new WorkerAssignAckMessage
                {
                    WorkerId = _WorkerId,
                    WorkerSessionId = _WorkerSessionId,
                    RunAssignmentId = assign.Assignment.Id,
                    LeaseToken = assign.Assignment.LeaseToken,
                    Accepted = accepted,
                    Message = message
                }, token);
            }

            public Task SendCompletionAsync(WorkerAssignMessage assign, FlowRunStateEnum finalState, string? outputData, string? errorMessage, CancellationToken token)
            {
                return SendFrameAsync(new WorkerRunCompletedMessage
                {
                    Completion = new RunCompletionReport
                    {
                        FlowRunId = assign.Assignment.FlowRunId,
                        RunAssignmentId = assign.Assignment.Id,
                        WorkerId = _WorkerId,
                        WorkerSessionId = _WorkerSessionId,
                        LeaseToken = assign.Assignment.LeaseToken,
                        FinalState = finalState,
                        OutputData = outputData,
                        ErrorMessage = errorMessage,
                        ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(assign.Plan.ExecutionSnapshot),
                        CompletedUtc = DateTime.UtcNow
                    }
                }, token);
            }

            public async Task CloseAsync()
            {
                if (_Socket.State == WebSocketState.Open || _Socket.State == WebSocketState.CloseReceived)
                {
                    try
                    {
                        await _Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test worker closing", CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore close failures in tests.
                    }
                }
            }

            private async Task SendFrameAsync(object frame, CancellationToken token)
            {
                string json = JsonSerializer.Serialize(frame, WorkerProtocolSerialization.Options);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                await _SendLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await _Socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, endOfMessage: true, token).ConfigureAwait(false);
                }
                finally
                {
                    _SendLock.Release();
                }
            }

            private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, CancellationToken token)
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);
                using MemoryStream ms = new MemoryStream();

                while (true)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return null;
                    if (result.Count > 0) ms.Write(buffer.Array!, buffer.Offset, result.Count);
                    if (result.EndOfMessage)
                    {
                        if (result.MessageType != WebSocketMessageType.Text)
                            throw new InvalidOperationException("Only text frames are supported in tests.");
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }

            public async ValueTask DisposeAsync()
            {
                await CloseAsync().ConfigureAwait(false);
                _SendLock.Dispose();
                _Socket.Dispose();
            }
        }
    }
}
