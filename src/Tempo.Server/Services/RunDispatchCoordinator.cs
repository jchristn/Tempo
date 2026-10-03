namespace Tempo.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Workers;
    using Tempo.Telemetry;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Single authoritative in-process scheduler, worker-session manager, and completion coordinator.
    /// </summary>
    public class RunDispatchCoordinator : IRunDispatchCoordinator, IRunScheduler, ILoadBalancer
    {
        private readonly Tempo.Core.Settings.EngineSettings _Settings;
        private readonly LoggingModule? _Logging;
        private readonly FlowDispatchService _DispatchService;
        private readonly IRunAssignmentStore _Assignments;
        private readonly RunAssignmentStore? _AssignmentStore;
        private readonly FlowRunExecutionPlanBuilder _PlanBuilder;
        private readonly IRunExecutor? _LocalExecutor;
        private readonly Dictionary<string, RemoteWorkerRunExecutor> _RemoteExecutors = new Dictionary<string, RemoteWorkerRunExecutor>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _Gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();
        private readonly ServerInstanceRecord _ServerInstance = new ServerInstanceRecord
        {
            HostName = Environment.MachineName,
            Version = typeof(RunDispatchCoordinator).Assembly.GetName().Version?.ToString()
        };
        private readonly string _Header = "[RunDispatchCoordinator] ";
        private readonly ConcurrentDictionary<string, string> _PendingTraceParents = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private const int MaxPendingTraceParents = 10000;
        private const string DispatchPipeline = "dispatch";
        private const string GateLimiter = "dispatch_gate";
        private Task? _Loop;
        private bool _Disposed = false;
        private bool _SchedulerSuppressed = false;
        private DateTime _LastQueueDepthSampleUtc = DateTime.MinValue;

        /// <summary>Instantiate.</summary>
        public RunDispatchCoordinator(
            DatabaseDriverBase database,
            StepManager stepManager,
            Tempo.Core.Settings.EngineSettings settings,
            LoggingModule? logging = null,
            StepRuntimeRegistry? runtimeRegistry = null,
            IRunAssignmentStore? assignments = null,
            Tempo.Core.Settings.RunLogSettings? runLogSettings = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (stepManager == null) throw new ArgumentNullException(nameof(stepManager));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging;
            _DispatchService = new FlowDispatchService(database);
            _Assignments = assignments ?? new RunAssignmentStore(database, _Settings);
            _AssignmentStore = _Assignments as RunAssignmentStore;
            _PlanBuilder = new FlowRunExecutionPlanBuilder(database, stepManager);

            if (_Settings.ServerCanExecuteWorkload)
            {
                StepRuntimeRegistry registry = runtimeRegistry ?? StepRuntimeRegistry.CreateDefault(stepManager, database: database);
                _LocalExecutor = new LocalServerRunExecutor(database, registry, _Settings, logging, runLogSettings);
            }
        }

        /// <summary>Local pseudo-worker descriptor, when enabled.</summary>
        public RunExecutorDescriptor? LocalExecutor => _LocalExecutor?.Descriptor;

        /// <summary>Whether this server currently owns scheduling responsibility.</summary>
        public bool SchedulingEnabled => !_SchedulerSuppressed || _Settings.AllowDuplicateScheduler;

        /// <inheritdoc/>
        public void Start()
        {
            if (_Loop != null) return;
            PrimeCoordinatorStateAsync(CancellationToken.None).GetAwaiter().GetResult();
            _Loop = Task.Run(() => RunLoopAsync(_Cts.Token));
        }

        /// <inheritdoc/>
        public void Stop()
        {
            try { _Cts.Cancel(); } catch { /* ignore */ }
            if (_LocalExecutor != null)
            {
                try { _Assignments.MarkWorkerDisconnectedAsync(_LocalExecutor.Descriptor, "server_stopped", CancellationToken.None).GetAwaiter().GetResult(); } catch { /* ignore */ }
            }

            List<RemoteWorkerRunExecutor> remoteWorkers;
            lock (_RemoteExecutors)
            {
                remoteWorkers = _RemoteExecutors.Values.ToList();
                _RemoteExecutors.Clear();
            }

            foreach (RemoteWorkerRunExecutor worker in remoteWorkers)
            {
                try { _Assignments.MarkWorkerDisconnectedAsync(worker.Descriptor, "server_stopped", CancellationToken.None).GetAwaiter().GetResult(); } catch { /* ignore */ }
                try { worker.SendDrainAsync("server_stopped", CancellationToken.None).GetAwaiter().GetResult(); } catch { /* ignore */ }
            }
        }

        /// <inheritdoc/>
        public async Task<FlowRun> EnqueueAsync(
            string tenantId,
            string dataFlowId,
            string? inputData = null,
            string? triggeredByUserId = null,
            string? triggerId = null,
            string? sourceIp = null,
            CancellationToken token = default)
        {
            string source = string.IsNullOrWhiteSpace(triggerId) ? "api" : "trigger";
            using Activity? activity = TempoTelemetry.StartActivity(TelemetryConstants.SpanDispatchEnqueue, ActivityKind.Producer);
            activity?.SetTag(TelemetryConstants.AttrTenantId, tenantId);
            activity?.SetTag(TelemetryConstants.AttrDataFlowId, dataFlowId);
            activity?.SetTag(TelemetryConstants.AttrSource, source);

            if (!_Settings.AllowDuplicateScheduler && _SchedulerSuppressed)
            {
                TempoTelemetry.RecordEnqueue(source, "rejected");
                TempoTelemetry.SetError(activity, "scheduler_suppressed");
                throw new InvalidOperationException("Scheduling is disabled on this server because another active scheduler was detected.");
            }

            FlowRun run;
            try
            {
                run = await _DispatchService.EnqueueAsync(tenantId, dataFlowId, inputData, triggeredByUserId, triggerId, sourceIp, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TempoTelemetry.RecordEnqueue(source, "rejected");
                TempoTelemetry.RecordException(activity, ex);
                throw;
            }

            TempoTelemetry.RecordEnqueue(source, "accepted");
            activity?.SetTag(TelemetryConstants.AttrFlowRunId, run.Id);
            TempoTelemetry.SetOk(activity);
            string? traceParent = TempoTelemetry.TraceParentOf(activity);
            if (traceParent != null && _PendingTraceParents.Count < MaxPendingTraceParents)
            {
                _PendingTraceParents[run.Id] = traceParent;
            }

            if (_Settings.QueueEnabled)
            {
                _ = Task.Run(() => TryScheduleNextAsync(CancellationToken.None));
            }
            return run;
        }

        /// <inheritdoc/>
        public async Task<bool> CancelQueuedAsync(string tenantId, string flowRunId, CancellationToken token = default)
        {
            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                return await _Assignments.CancelQueuedAsync(tenantId, flowRunId, token).ConfigureAwait(false);
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task HandleCompletionAsync(RunCompletionReport completion, CancellationToken token = default)
        {
            if (completion == null) throw new ArgumentNullException(nameof(completion));

            long start = Stopwatch.GetTimestamp();
            string nodeKind = NodeKindOf(completion.WorkerId);
            string state = completion.FinalState.ToString().ToLowerInvariant();
            using Activity? activity = TempoTelemetry.StartActivity(TelemetryConstants.SpanDispatchComplete, ActivityKind.Consumer, completion.TraceParent, completion.TraceState);
            activity?.SetTag(TelemetryConstants.AttrFlowRunId, completion.FlowRunId);
            activity?.SetTag(TelemetryConstants.AttrAssignmentId, completion.RunAssignmentId);
            activity?.SetTag(TelemetryConstants.AttrWorkerId, completion.WorkerId);
            activity?.SetTag(TelemetryConstants.AttrNodeKind, nodeKind);
            activity?.SetTag(TelemetryConstants.AttrState, state);

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                bool applied;
                try
                {
                    applied = await _Assignments.CompleteAssignmentAsync(completion, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    TempoTelemetry.RecordException(activity, ex);
                    TempoTelemetry.RecordCompletion(nodeKind, state, "failed");
                    TempoTelemetry.RecordStage(DispatchPipeline, "complete", TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(start));
                    TempoTelemetry.RecordError("dispatch", ex);
                    throw;
                }

                CompleteWorkerAssignment(completion.WorkerSessionId, completion.RunAssignmentId);
                TempoTelemetry.RecordCompletion(nodeKind, state, applied ? "applied" : "stale");
                TempoTelemetry.RecordStage(DispatchPipeline, "complete", applied ? TelemetryConstants.OutcomeSuccess : "stale", TempoTelemetry.SecondsSince(start));
                if (applied && completion.FinalState == FlowRunStateEnum.Succeeded) TempoTelemetry.MarkSuccess("dispatch");
                if (applied) TempoTelemetry.SetOk(activity);
                else TempoTelemetry.SetError(activity, "stale_completion");

                if (!applied)
                {
                    _Logging?.Warn(_Header + "ignored stale completion for assignment " + completion.RunAssignmentId);
                    if (_AssignmentStore != null)
                    {
                        await _AssignmentStore.RecordWorkerActivityAsync(new WorkerActivityRecord
                        {
                            WorkerId = completion.WorkerId,
                            WorkerSessionId = completion.WorkerSessionId,
                            FlowRunId = completion.FlowRunId,
                            RunAssignmentId = completion.RunAssignmentId,
                            EventType = "orphan_completion",
                            Severity = "Warning",
                            Message = "Ignored a stale or mismatched completion frame",
                            PayloadJson = System.Text.Json.JsonSerializer.Serialize(completion, WorkerProtocolSerialization.Options)
                        }, token).ConfigureAwait(false);
                    }
                }
                else if (_Settings.QueueEnabled)
                {
                    _ = Task.Run(() => TryScheduleNextAsync(CancellationToken.None));
                }
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<int> HandleLeaseExpiryAsync(CancellationToken token = default)
        {
            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                int recovered = await _Assignments.RecoverExpiredAssignmentsAsync(DateTime.UtcNow, token).ConfigureAwait(false);
                TempoTelemetry.RecordRecoveries("lease_expired", recovered);
                return recovered;
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> TryScheduleNextAsync(CancellationToken token = default)
        {
            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                return await TryScheduleNextInternalAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <inheritdoc/>
        public Task<IRunExecutor?> SelectExecutorAsync(FlowRunExecutionPlan plan, CancellationToken token = default)
        {
            List<IRunExecutor> candidates = new List<IRunExecutor>();

            if (_LocalExecutor != null && _LocalExecutor.CanAcceptWork(plan))
            {
                candidates.Add(_LocalExecutor);
            }

            lock (_RemoteExecutors)
            {
                candidates.AddRange(_RemoteExecutors.Values.Where(worker => worker.CanAcceptWork(plan)));
            }

            if (candidates.Count < 1)
            {
                return Task.FromResult<IRunExecutor?>(null);
            }

            IOrderedEnumerable<IRunExecutor> ordered = candidates
                .OrderBy(candidate => candidate.Descriptor.CurrentRunCount);

            if (string.Equals(_Settings.LoadBalancingStrategy, "LabelPinned", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(plan.PlacementLabel))
            {
                ordered = ordered.ThenBy(candidate => WorkerDescriptorJson.HasLabel(candidate.Descriptor.LabelsJson, plan.PlacementLabel) ? 0 : 1);
            }

            IRunExecutor selected = ordered
                .ThenBy(candidate => candidate.Descriptor.NodeKind == ExecutionNodeKindEnum.Worker ? 0 : 1)
                .First();

            return Task.FromResult<IRunExecutor?>(selected);
        }

        /// <summary>Authenticate a worker token.</summary>
        public Task<WorkerRecord?> AuthenticateWorkerAsync(string workerId, string workerToken, CancellationToken token = default)
        {
            return RequireConcreteStore().AuthenticateWorkerAsync(workerId, workerToken, token);
        }

        /// <summary>Register a connected worker websocket session and return the server acknowledgement frame.</summary>
        public async Task<WorkerHelloAckMessage> RegisterWorkerAsync(WorkerRecord authenticatedWorker, WorkerHelloMessage hello, WebSocketSession session, CancellationToken token = default)
        {
            if (authenticatedWorker == null) throw new ArgumentNullException(nameof(authenticatedWorker));
            if (hello == null) throw new ArgumentNullException(nameof(hello));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!string.Equals(authenticatedWorker.Id, hello.WorkerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Authenticated worker id does not match hello frame.");
            }

            using Activity? activity = TempoTelemetry.StartActivity(TelemetryConstants.SpanWorkerRegister);
            activity?.SetTag(TelemetryConstants.AttrWorkerId, authenticatedWorker.Id);

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                string workerSessionId = IdGenerator.GenerateWorkerSessionId();
                activity?.SetTag(TelemetryConstants.AttrWorkerSessionId, workerSessionId);
                RunExecutorDescriptor descriptor = new RunExecutorDescriptor
                {
                    WorkerId = authenticatedWorker.Id,
                    WorkerSessionId = workerSessionId,
                    Name = string.IsNullOrWhiteSpace(hello.Name) ? authenticatedWorker.Name : hello.Name.Trim(),
                    Kind = string.IsNullOrWhiteSpace(hello.Kind) ? "Worker" : hello.Kind.Trim(),
                    NodeKind = ExecutionNodeKindEnum.Worker,
                    State = "Online",
                    Enabled = authenticatedWorker.Enabled,
                    DrainMode = authenticatedWorker.DrainMode,
                    Version = string.IsNullOrWhiteSpace(hello.Version) ? (authenticatedWorker.Version ?? string.Empty) : hello.Version.Trim(),
                    HostName = string.IsNullOrWhiteSpace(hello.HostName) ? (authenticatedWorker.HostName ?? string.Empty) : hello.HostName.Trim(),
                    LabelsJson = WorkerDescriptorJson.SerializeLabels(hello.Labels),
                    CapabilitiesJson = WorkerDescriptorJson.SerializeCapabilities(hello.Capabilities),
                    MaxConcurrentRuns = Math.Max(1, hello.MaxConcurrentRuns),
                    MaxTaskTimeoutMs = Math.Max(0, hello.MaxTaskTimeoutMs)
                };

                List<RemoteWorkerRunExecutor> replaced = new List<RemoteWorkerRunExecutor>();
                lock (_RemoteExecutors)
                {
                    foreach (RemoteWorkerRunExecutor existing in _RemoteExecutors.Values.Where(x => string.Equals(x.Descriptor.WorkerId, descriptor.WorkerId, StringComparison.Ordinal)).ToList())
                    {
                        replaced.Add(existing);
                        _RemoteExecutors.Remove(existing.WorkerSessionId);
                    }
                }

                foreach (RemoteWorkerRunExecutor existing in replaced)
                {
                    TempoTelemetry.RecordWorkerSession("superseded");
                    await _Assignments.MarkWorkerDisconnectedAsync(existing.Descriptor, "superseded_session", token).ConfigureAwait(false);
                    if (_AssignmentStore != null)
                    {
                        int recovered = await _AssignmentStore.RecoverAssignmentsForWorkerSessionAsync(existing.Descriptor.WorkerId, existing.Descriptor.WorkerSessionId, DateTime.UtcNow, token).ConfigureAwait(false);
                        TempoTelemetry.RecordRecoveries("superseded_session", recovered);
                    }
                    try { await existing.SendDrainAsync("superseded_session", token).ConfigureAwait(false); } catch { /* ignore */ }
                }

                await _Assignments.EnsureWorkerAsync(descriptor, token).ConfigureAwait(false);
                RemoteWorkerRunExecutor executor = new RemoteWorkerRunExecutor(descriptor, session);
                executor.SetDrainMode(authenticatedWorker.DrainMode);
                executor.TouchHeartbeat(0, DateTime.UtcNow);

                lock (_RemoteExecutors)
                {
                    _RemoteExecutors[workerSessionId] = executor;
                }

                TempoTelemetry.RecordWorkerSession("connected");
                TempoTelemetry.SetOk(activity);
                PublishPoolGauges();

                if (_AssignmentStore != null)
                {
                    await _AssignmentStore.RecordWorkerActivityAsync(new WorkerActivityRecord
                    {
                        WorkerId = descriptor.WorkerId,
                        WorkerSessionId = descriptor.WorkerSessionId,
                        EventType = WorkerFrameTypes.Hello,
                        Severity = "Info",
                        Message = "Worker connected and registered",
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(hello, WorkerProtocolSerialization.Options)
                    }, token).ConfigureAwait(false);
                }

                return new WorkerHelloAckMessage
                {
                    WorkerId = descriptor.WorkerId,
                    WorkerSessionId = workerSessionId,
                    HeartbeatIntervalMs = HeartbeatIntervalMs(),
                    HeartbeatTimeoutMs = _Settings.WorkerHeartbeatTimeoutMs,
                    LeaseDurationMs = _Settings.LeaseDurationMs,
                    DrainMode = descriptor.DrainMode
                };
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <summary>Record a worker heartbeat.</summary>
        public async Task<bool> HandleWorkerHeartbeatAsync(WorkerHeartbeatMessage heartbeat, CancellationToken token = default)
        {
            if (heartbeat == null) throw new ArgumentNullException(nameof(heartbeat));

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                if (!_RemoteExecutors.TryGetValue(heartbeat.WorkerSessionId, out RemoteWorkerRunExecutor? executor)) return false;
                if (!string.Equals(executor.Descriptor.WorkerId, heartbeat.WorkerId, StringComparison.Ordinal)) return false;

                executor.TouchHeartbeat(heartbeat.ActiveRuns, DateTime.UtcNow);
                await _Assignments.TouchWorkerHeartbeatAsync(executor.Descriptor, DateTime.UtcNow, token).ConfigureAwait(false);
                return true;
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <summary>Record an assignment acknowledgement frame.</summary>
        public async Task<bool> HandleWorkerAssignAckAsync(WorkerAssignAckMessage ack, CancellationToken token = default)
        {
            if (ack == null) throw new ArgumentNullException(nameof(ack));

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                if (!_RemoteExecutors.TryGetValue(ack.WorkerSessionId, out RemoteWorkerRunExecutor? executor)) return false;
                if (!string.Equals(executor.Descriptor.WorkerId, ack.WorkerId, StringComparison.Ordinal)) return false;

                if (_AssignmentStore != null)
                {
                    await _AssignmentStore.RecordWorkerActivityAsync(new WorkerActivityRecord
                    {
                        WorkerId = ack.WorkerId,
                        WorkerSessionId = ack.WorkerSessionId,
                        RunAssignmentId = ack.RunAssignmentId,
                        EventType = WorkerFrameTypes.AssignAck,
                        Severity = ack.Accepted ? "Info" : "Warning",
                        Message = ack.Accepted ? "Assignment acknowledged" : (ack.Message ?? "Assignment rejected"),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(ack, WorkerProtocolSerialization.Options)
                    }, token).ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <summary>Handle a worker websocket disconnect or timeout.</summary>
        public async Task<bool> UnregisterWorkerSessionAsync(string workerSessionId, string reason, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerSessionId)) return false;

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                if (!_RemoteExecutors.TryGetValue(workerSessionId, out RemoteWorkerRunExecutor? executor)) return false;
                _RemoteExecutors.Remove(workerSessionId);
                TempoTelemetry.RecordWorkerSession("disconnected");

                await _Assignments.MarkWorkerDisconnectedAsync(executor.Descriptor, reason, token).ConfigureAwait(false);
                if (_AssignmentStore != null)
                {
                    int recovered = await _AssignmentStore.RecoverAssignmentsForWorkerSessionAsync(executor.Descriptor.WorkerId, executor.Descriptor.WorkerSessionId, DateTime.UtcNow, token).ConfigureAwait(false);
                    TempoTelemetry.RecordRecoveries("disconnected", recovered);
                }

                PublishPoolGauges();
                return true;
            }
            finally
            {
                _Gate.Release();
            }
        }

        /// <summary>List persisted workers.</summary>
        public Task<List<WorkerRecord>> ListWorkersAsync(CancellationToken token = default)
        {
            return RequireConcreteStore().ListWorkersAsync(token);
        }

        /// <summary>Read one worker.</summary>
        public Task<WorkerRecord?> ReadWorkerAsync(string workerId, CancellationToken token = default)
        {
            return RequireConcreteStore().ReadWorkerAsync(workerId, token);
        }

        /// <summary>Read active assignment counts keyed by worker id.</summary>
        public Task<Dictionary<string, int>> ReadActiveAssignmentCountsAsync(CancellationToken token = default)
        {
            return RequireConcreteStore().ReadActiveAssignmentCountsAsync(token);
        }

        /// <summary>Read the latest worker session.</summary>
        public Task<WorkerSessionRecord?> ReadLatestWorkerSessionAsync(string workerId, CancellationToken token = default)
        {
            return RequireConcreteStore().ReadLatestWorkerSessionAsync(workerId, token);
        }

        /// <summary>List assignment attempts for one flow run.</summary>
        public Task<List<RunAssignmentRecord>> ListAssignmentsByRunAsync(string flowRunId, CancellationToken token = default)
        {
            return RequireConcreteStore().ListAssignmentsByRunAsync(flowRunId, token);
        }

        /// <summary>List worker activity correlated to one flow run.</summary>
        public Task<List<WorkerActivityRecord>> ListWorkerActivityByRunAsync(string flowRunId, CancellationToken token = default)
        {
            return RequireConcreteStore().ListWorkerActivityByRunAsync(flowRunId, token);
        }

        /// <summary>Rotate a worker token.</summary>
        public Task<WorkerTokenIssueResult> RotateWorkerTokenAsync(string workerId, string? workerName = null, CancellationToken token = default)
        {
            return RequireConcreteStore().RotateWorkerTokenAsync(workerId, workerName, token);
        }

        /// <summary>Set worker drain mode and notify the live session when present.</summary>
        public async Task<bool> SetWorkerDrainModeAsync(string workerId, bool drainMode, CancellationToken token = default)
        {
            bool exists = await RequireConcreteStore().SetWorkerDrainModeAsync(workerId, drainMode, token).ConfigureAwait(false);
            if (!exists) return false;

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                if (_LocalExecutor != null && string.Equals(_LocalExecutor.Descriptor.WorkerId, workerId, StringComparison.Ordinal))
                {
                    _LocalExecutor.Descriptor.DrainMode = drainMode;
                }

                RemoteWorkerRunExecutor? live = null;
                lock (_RemoteExecutors)
                {
                    live = _RemoteExecutors.Values.FirstOrDefault(worker => string.Equals(worker.Descriptor.WorkerId, workerId, StringComparison.Ordinal));
                }

                if (live != null)
                {
                    live.SetDrainMode(drainMode);
                    if (drainMode) await live.SendDrainAsync("operator_request", token).ConfigureAwait(false);
                    else await live.SendResumeAsync("operator_request", token).ConfigureAwait(false);
                }
            }
            finally
            {
                _Gate.Release();
            }

            return true;
        }

        /// <summary>Set worker enabled state and disconnect a live remote worker when blocking it.</summary>
        public async Task<bool> SetWorkerEnabledAsync(string workerId, bool enabled, CancellationToken token = default)
        {
            bool exists = await RequireConcreteStore().SetWorkerEnabledAsync(workerId, enabled, token).ConfigureAwait(false);
            if (!exists) return false;

            RemoteWorkerRunExecutor? live = null;

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                if (_LocalExecutor != null && string.Equals(_LocalExecutor.Descriptor.WorkerId, workerId, StringComparison.Ordinal))
                {
                    _LocalExecutor.Descriptor.Enabled = enabled;
                    if (!enabled) _LocalExecutor.Descriptor.State = "Offline";
                    else _LocalExecutor.Descriptor.State = "Online";
                }

                lock (_RemoteExecutors)
                {
                    live = _RemoteExecutors.Values.FirstOrDefault(worker => string.Equals(worker.Descriptor.WorkerId, workerId, StringComparison.Ordinal));
                }

                if (live != null)
                {
                    live.SetEnabled(enabled);
                    if (!enabled) live.Descriptor.State = "Offline";
                }
            }
            finally
            {
                _Gate.Release();
            }

            if (!enabled && live != null)
            {
                try
                {
                    await live.DisconnectAsync("worker_blocked", token).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore operator-requested disconnect failures; websocket cleanup will retry on close.
                }
            }

            return true;
        }

        /// <summary>Validate worker access to an artifact download endpoint.</summary>
        public Task<bool> ValidateWorkerArtifactAccessAsync(
            string workerId,
            string runAssignmentId,
            string leaseToken,
            string tenantId,
            string sha256,
            CancellationToken token = default)
        {
            return RequireConcreteStore().ValidateWorkerArtifactAccessAsync(workerId, runAssignmentId, leaseToken, tenantId, sha256, token);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_Disposed) return;
            Stop();
            try { _Loop?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
            _Gate.Dispose();
            _Cts.Dispose();
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool progressed = false;

                try
                {
                    DateTime now = await PrimeCoordinatorStateAsync(token).ConfigureAwait(false);
                    PublishPoolGauges();
                    await SampleQueueDepthAsync(now, token).ConfigureAwait(false);

                    int staleWorkers = await HandleRemoteWorkerTimeoutsAsync(now, token).ConfigureAwait(false);
                    progressed = staleWorkers > 0;

                    if (!_SchedulerSuppressed || _Settings.AllowDuplicateScheduler)
                    {
                        int recovered = await HandleLeaseExpiryAsync(token).ConfigureAwait(false);
                        progressed = progressed || recovered > 0;

                        if (_Settings.QueueEnabled)
                        {
                            while (!token.IsCancellationRequested)
                            {
                                bool next = await TryScheduleNextAsync(token).ConfigureAwait(false);
                                if (!next) break;
                                progressed = true;
                            }
                        }
                    }

                    if (!progressed)
                    {
                        await Task.Delay(_Settings.PollIntervalMs, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    TempoTelemetry.RecordError("dispatch", ex);
                    _Logging?.Warn(_Header + "loop error: " + ex.Message);
                    try { await Task.Delay(_Settings.PollIntervalMs, CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
                }
            }
        }

        private async Task<int> HandleRemoteWorkerTimeoutsAsync(DateTime utcNow, CancellationToken token)
        {
            List<RemoteWorkerRunExecutor> stale = new List<RemoteWorkerRunExecutor>();

            await WaitGateAsync(token).ConfigureAwait(false);
            try
            {
                foreach (RemoteWorkerRunExecutor worker in _RemoteExecutors.Values.ToList())
                {
                    if ((utcNow - worker.LastHeartbeatUtc).TotalMilliseconds <= _Settings.WorkerHeartbeatTimeoutMs) continue;
                    stale.Add(worker);
                    _RemoteExecutors.Remove(worker.WorkerSessionId);
                }
            }
            finally
            {
                _Gate.Release();
            }

            foreach (RemoteWorkerRunExecutor worker in stale)
            {
                TempoTelemetry.RecordWorkerSession("heartbeat_timeout");
                try { await _Assignments.MarkWorkerDisconnectedAsync(worker.Descriptor, "heartbeat_timeout", token).ConfigureAwait(false); } catch { /* ignore */ }
                if (_AssignmentStore != null)
                {
                    try
                    {
                        int recovered = await _AssignmentStore.RecoverAssignmentsForWorkerSessionAsync(worker.Descriptor.WorkerId, worker.Descriptor.WorkerSessionId, utcNow, token).ConfigureAwait(false);
                        TempoTelemetry.RecordRecoveries("heartbeat_timeout", recovered);
                    }
                    catch { /* ignore */ }
                }
                try { await worker.SendDrainAsync("heartbeat_timeout", token).ConfigureAwait(false); } catch { /* ignore */ }
            }

            return stale.Count;
        }

        private async Task<bool> TryScheduleNextInternalAsync(CancellationToken token)
        {
            FlowRun? run = await _Assignments.ReadNextPendingAsync(token).ConfigureAwait(false);
            if (run == null) return false;

            // A scheduling attempt that finds no free executor is retried on the next poll, so its spans are only
            // emitted (retroactively) once the attempt reaches a terminal decision. Metrics are always recorded.
            DateTimeOffset attemptStart = DateTimeOffset.UtcNow;
            long planStart = Stopwatch.GetTimestamp();
            _PendingTraceParents.TryGetValue(run.Id, out string? enqueueTraceParent);

            FlowRunExecutionPlan plan;
            try
            {
                plan = await _PlanBuilder.BuildAsync(run, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DateTimeOffset failedAt = DateTimeOffset.UtcNow;
                TempoTelemetry.RecordStage(DispatchPipeline, "plan", TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(planStart));
                TempoTelemetry.RecordAssignment("none", "plan_failed");
                TempoTelemetry.RecordError("dispatch", ex);
                using (Activity? scheduleActivity = StartScheduleActivity(run, attemptStart, enqueueTraceParent))
                {
                    Activity? planActivity = TempoTelemetry.StartActivityAt(TelemetryConstants.SpanStagePrefix + "plan", attemptStart);
                    TempoTelemetry.RecordException(planActivity, ex);
                    TempoTelemetry.StopActivityAt(planActivity, failedAt);
                    TempoTelemetry.SetError(scheduleActivity, "plan_failed", ex.Message);
                }

                _PendingTraceParents.TryRemove(run.Id, out _);
                _Logging?.Warn(_Header + "run " + run.Id + " failed before assignment: " + ex.Message);
                await _Assignments.FailPendingRunAsync(run, FlowRunStateEnum.Failed, ex.Message, token).ConfigureAwait(false);
                return true;
            }

            DateTimeOffset planEnd = DateTimeOffset.UtcNow;
            double planSeconds = TempoTelemetry.SecondsSince(planStart);
            IRunExecutor? executor = await SelectExecutorAsync(plan, token).ConfigureAwait(false);
            DateTimeOffset selectEnd = DateTimeOffset.UtcNow;
            if (executor == null)
            {
                if (HasAnyLiveExecutor() && !HasPotentialExecutorForPlan(plan))
                {
                    string message = "No eligible worker was available for run " + run.Id;
                    TempoTelemetry.RecordStage(DispatchPipeline, "plan", TelemetryConstants.OutcomeSuccess, planSeconds);
                    TempoTelemetry.RecordAssignment("none", "no_eligible_worker");
                    using (Activity? scheduleActivity = StartScheduleActivity(run, attemptStart, enqueueTraceParent))
                    {
                        RecordRetroStage("plan", attemptStart, planEnd, null);
                        RecordRetroStage("select", planEnd, selectEnd, "no_eligible_worker");
                        TempoTelemetry.SetError(scheduleActivity, "no_eligible_worker", message);
                    }

                    _PendingTraceParents.TryRemove(run.Id, out _);
                    _Logging?.Warn(_Header + "no_eligible_worker: " + message);
                    await _Assignments.FailPendingRunAsync(run, FlowRunStateEnum.Failed, message, token).ConfigureAwait(false);
                    return true;
                }

                TempoTelemetry.RecordAssignment("none", "no_executor");
                return false;
            }

            string nodeKind = NodeKindOf(executor.Descriptor);
            TempoTelemetry.RecordStage(DispatchPipeline, "plan", TelemetryConstants.OutcomeSuccess, planSeconds);
            TempoTelemetry.RecordStage(DispatchPipeline, "queued", TelemetryConstants.OutcomeSuccess, Math.Max(0, (DateTime.UtcNow - run.CreatedUtc).TotalSeconds));
            _PendingTraceParents.TryRemove(run.Id, out _);

            Activity? schedule = StartScheduleActivity(run, attemptStart, enqueueTraceParent);
            try
            {
                schedule?.SetTag(TelemetryConstants.AttrNodeKind, nodeKind);
                schedule?.SetTag(TelemetryConstants.AttrWorkerId, executor.Descriptor.WorkerId);
                RecordRetroStage("plan", attemptStart, planEnd, null);
                RecordRetroStage("select", planEnd, selectEnd, null);

                long assignStart = Stopwatch.GetTimestamp();
                RunAssignmentRecord assignment;
                using (Activity? assignActivity = TempoTelemetry.StartStage(DispatchPipeline, "assign"))
                {
                    try
                    {
                        assignment = await _Assignments.CreateAssignmentAsync(run, executor.Descriptor, plan, token).ConfigureAwait(false);
                        assignActivity?.SetTag(TelemetryConstants.AttrAssignmentId, assignment.Id);
                        assignActivity?.SetTag(TelemetryConstants.AttrAttempt, assignment.AttemptNumber);
                        TempoTelemetry.SetOk(assignActivity);
                        TempoTelemetry.RecordStage(DispatchPipeline, "assign", TelemetryConstants.OutcomeSuccess, TempoTelemetry.SecondsSince(assignStart));
                    }
                    catch (Exception ex)
                    {
                        TempoTelemetry.RecordException(assignActivity, ex);
                        TempoTelemetry.RecordStage(DispatchPipeline, "assign", TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(assignStart));
                        TempoTelemetry.RecordAssignment(nodeKind, "assign_failed");
                        TempoTelemetry.RecordException(schedule, ex);
                        throw;
                    }
                }

                StampBudget(plan, assignment);
                TempoTelemetry.RecordAssignment(nodeKind, "assigned");
                schedule?.SetTag(TelemetryConstants.AttrAssignmentId, assignment.Id);
                TempoTelemetry.SetOk(schedule);
                _ = ExecuteAssignmentAsync(executor, assignment, plan);
                return true;
            }
            finally
            {
                schedule?.Dispose();
            }
        }

        private Activity? StartScheduleActivity(FlowRun run, DateTimeOffset startTime, string? enqueueTraceParent)
        {
            Activity? activity;
            if (enqueueTraceParent != null)
            {
                activity = TempoTelemetry.StartActivityAt(TelemetryConstants.SpanDispatchSchedule, startTime, ActivityKind.Internal, enqueueTraceParent);
            }
            else
            {
                Activity? previous = Activity.Current;
                Activity.Current = null;
                activity = TempoTelemetry.StartActivityAt(TelemetryConstants.SpanDispatchSchedule, startTime);
                if (activity == null) Activity.Current = previous;
            }

            activity?.SetTag(TelemetryConstants.AttrFlowRunId, run.Id);
            activity?.SetTag(TelemetryConstants.AttrTenantId, run.TenantId);
            activity?.SetTag(TelemetryConstants.AttrDataFlowId, run.DataFlowId);
            return activity;
        }

        private static void RecordRetroStage(string stage, DateTimeOffset start, DateTimeOffset end, string? errorType)
        {
            Activity? activity = TempoTelemetry.StartActivityAt(TelemetryConstants.SpanStagePrefix + stage, start);
            activity?.SetTag(TelemetryConstants.AttrPipeline, DispatchPipeline);
            activity?.SetTag(TelemetryConstants.AttrStage, stage);
            if (errorType == null) TempoTelemetry.SetOk(activity);
            else TempoTelemetry.SetError(activity, errorType);
            TempoTelemetry.StopActivityAt(activity, end);
        }

        private async Task WaitGateAsync(CancellationToken token)
        {
            long start = Stopwatch.GetTimestamp();
            await _Gate.WaitAsync(token).ConfigureAwait(false);
            TempoTelemetry.RecordLimiterWait(GateLimiter, TempoTelemetry.SecondsSince(start));
        }

        private static string NodeKindOf(RunExecutorDescriptor descriptor)
        {
            return descriptor.NodeKind == ExecutionNodeKindEnum.Server ? "server" : "worker";
        }

        private static string NodeKindOf(string? workerId)
        {
            if (string.IsNullOrWhiteSpace(workerId)) return "unknown";
            return string.Equals(workerId, LocalServerRunExecutor.WorkerId, StringComparison.Ordinal) ? "server" : "worker";
        }

        private void PublishPoolGauges()
        {
            try
            {
                TempoTelemetry.SetSchedulerActive(SchedulingEnabled);
                if (_LocalExecutor != null)
                {
                    RunExecutorDescriptor local = _LocalExecutor.Descriptor;
                    bool available = local.Enabled && !local.DrainMode;
                    TempoTelemetry.SetWorkerPool("server", available ? 1 : 0, available ? local.MaxConcurrentRuns : 0, local.CurrentRunCount);
                }

                long connected = 0;
                long capacity = 0;
                long inUse = 0;
                lock (_RemoteExecutors)
                {
                    foreach (RemoteWorkerRunExecutor worker in _RemoteExecutors.Values)
                    {
                        if (!worker.IsConnected) continue;
                        inUse += worker.Descriptor.CurrentRunCount;
                        if (!worker.Descriptor.Enabled || worker.Descriptor.DrainMode) continue;
                        connected++;
                        capacity += worker.Descriptor.MaxConcurrentRuns;
                    }
                }

                TempoTelemetry.SetWorkerPool("worker", connected, capacity, inUse);
            }
            catch (Exception)
            {
                // Gauge publication is best-effort.
            }
        }

        private async Task SampleQueueDepthAsync(DateTime utcNow, CancellationToken token)
        {
            if (_AssignmentStore == null) return;
            if ((utcNow - _LastQueueDepthSampleUtc).TotalMilliseconds < _Settings.QueueDepthSampleIntervalMs) return;
            _LastQueueDepthSampleUtc = utcNow;

            try
            {
                TempoTelemetry.SetQueueDepth(await _AssignmentStore.CountPendingAsync(token).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                TempoTelemetry.RecordError("dispatch", ex);
            }
        }

        private async Task ExecuteAssignmentAsync(IRunExecutor executor, RunAssignmentRecord assignment, FlowRunExecutionPlan plan)
        {
            RunCompletionReport? completion;
            string nodeKind = NodeKindOf(executor.Descriptor);
            long executeStart = Stopwatch.GetTimestamp();
            Activity? executeActivity = TempoTelemetry.StartStage(DispatchPipeline, nodeKind == "server" ? "execute" : "handoff");
            executeActivity?.SetTag(TelemetryConstants.AttrAssignmentId, assignment.Id);
            executeActivity?.SetTag(TelemetryConstants.AttrNodeKind, nodeKind);
            try
            {
                completion = await executor.ExecuteAsync(assignment, plan, _Cts.Token).ConfigureAwait(false);
                TempoTelemetry.SetOk(executeActivity);
                TempoTelemetry.RecordStage(DispatchPipeline, nodeKind == "server" ? "execute" : "handoff", TelemetryConstants.OutcomeSuccess, TempoTelemetry.SecondsSince(executeStart));
                executeActivity?.Dispose();
                executeActivity = null;
                if (completion == null) return;
            }
            catch (OperationCanceledException)
            {
                TempoTelemetry.SetError(executeActivity, TelemetryConstants.OutcomeCancelled);
                TempoTelemetry.RecordStage(DispatchPipeline, nodeKind == "server" ? "execute" : "handoff", TelemetryConstants.OutcomeCancelled, TempoTelemetry.SecondsSince(executeStart));
                executeActivity?.Dispose();
                executeActivity = null;
                completion = new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = assignment.WorkerId,
                    WorkerSessionId = assignment.WorkerSessionId,
                    LeaseToken = assignment.LeaseToken,
                    FinalState = FlowRunStateEnum.Cancelled,
                    ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                    CompletedUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                TempoTelemetry.RecordException(executeActivity, ex);
                TempoTelemetry.RecordStage(DispatchPipeline, nodeKind == "server" ? "execute" : "handoff", TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(executeStart));
                TempoTelemetry.RecordAssignment(nodeKind, "send_failed");
                executeActivity?.Dispose();
                executeActivity = null;
                completion = new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = assignment.WorkerId,
                    WorkerSessionId = assignment.WorkerSessionId,
                    LeaseToken = assignment.LeaseToken,
                    FinalState = FlowRunStateEnum.Exception,
                    ErrorMessage = ex.Message,
                    ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                    CompletedUtc = DateTime.UtcNow
                };
            }

            try
            {
                await HandleCompletionAsync(completion, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "completion handling failed for assignment " + assignment.Id + ": " + ex.Message);
            }
        }

        private void CompleteWorkerAssignment(string? workerSessionId, string runAssignmentId)
        {
            if (string.IsNullOrWhiteSpace(workerSessionId)) return;

            lock (_RemoteExecutors)
            {
                if (_RemoteExecutors.TryGetValue(workerSessionId, out RemoteWorkerRunExecutor? executor))
                {
                    executor.CompleteAssignment(runAssignmentId);
                }
            }
        }

        private int HeartbeatIntervalMs()
        {
            return Math.Max(1000, Math.Min(_Settings.PollIntervalMs, _Settings.WorkerHeartbeatTimeoutMs / 2));
        }

        private async Task<DateTime> PrimeCoordinatorStateAsync(CancellationToken token)
        {
            DateTime now = DateTime.UtcNow;

            try
            {
                if (_LocalExecutor != null)
                {
                    if (_AssignmentStore != null)
                    {
                        WorkerRecord? persisted = await _AssignmentStore.ReadWorkerAsync(_LocalExecutor.Descriptor.WorkerId, token).ConfigureAwait(false);
                        if (persisted != null)
                        {
                            _LocalExecutor.Descriptor.Enabled = persisted.Enabled;
                            _LocalExecutor.Descriptor.DrainMode = persisted.DrainMode;
                        }
                    }

                    _LocalExecutor.Descriptor.State = _LocalExecutor.Descriptor.Enabled ? "Online" : "Offline";
                    await _Assignments.EnsureWorkerAsync(_LocalExecutor.Descriptor, token).ConfigureAwait(false);
                    if (_LocalExecutor.Descriptor.Enabled)
                    {
                        await _Assignments.TouchWorkerHeartbeatAsync(_LocalExecutor.Descriptor, now, token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "worker registration failed: " + ex.Message);
            }

            if (_AssignmentStore != null)
            {
                await _AssignmentStore.TouchServerInstanceAsync(_ServerInstance, now, token).ConfigureAwait(false);
                List<ServerInstanceRecord> activeInstances = await _AssignmentStore.ListActiveServerInstancesAsync(now.AddMilliseconds(-_Settings.WorkerHeartbeatTimeoutMs), token).ConfigureAwait(false);
                bool shouldSuppress = !_Settings.AllowDuplicateScheduler && activeInstances.Any(instance => !string.Equals(instance.Id, _ServerInstance.Id, StringComparison.Ordinal));

                if (shouldSuppress != _SchedulerSuppressed)
                {
                    _SchedulerSuppressed = shouldSuppress;
                    if (_SchedulerSuppressed)
                        _Logging?.Warn(_Header + "scheduler disabled because another active server instance was detected.");
                    else
                        _Logging?.Info(_Header + "scheduler ownership restored on this server.");
                }
            }

            return now;
        }

        private bool HasAnyLiveExecutor()
        {
            if (_LocalExecutor != null && _LocalExecutor.Descriptor.Enabled) return true;

            lock (_RemoteExecutors)
            {
                return _RemoteExecutors.Values.Any(worker => worker.IsConnected && worker.Descriptor.Enabled);
            }
        }

        private bool HasPotentialExecutorForPlan(FlowRunExecutionPlan plan)
        {
            if (plan == null) return false;

            if (_LocalExecutor != null &&
                _LocalExecutor.Descriptor.Enabled &&
                WorkerDescriptorJson.HasLabel(_LocalExecutor.Descriptor.LabelsJson, plan.PlacementLabel))
            {
                return true;
            }

            lock (_RemoteExecutors)
            {
                foreach (RemoteWorkerRunExecutor worker in _RemoteExecutors.Values)
                {
                    if (!worker.IsConnected || !worker.Descriptor.Enabled) continue;
                    if (!WorkerDescriptorJson.HasLabel(worker.Descriptor.LabelsJson, plan.PlacementLabel)) continue;
                    if (WorkerDescriptorJson.SupportsPlan(worker.Descriptor.CapabilitiesJson, plan)) return true;
                }
            }

            return false;
        }

        private RunAssignmentStore RequireConcreteStore()
        {
            return _AssignmentStore ?? throw new InvalidOperationException("Worker management requires RunAssignmentStore.");
        }

        private void StampBudget(FlowRunExecutionPlan plan, RunAssignmentRecord assignment)
        {
            plan.Budget.DispatchAttempt = assignment.AttemptNumber;
            plan.Budget.RunAssignmentId = assignment.Id;
            plan.Budget.LeaseToken = assignment.LeaseToken;
            plan.Budget.AssignedUtc = assignment.AssignedUtc;
            plan.Budget.LeaseExpiresUtc = assignment.LeaseExpiresUtc;
            plan.Budget.LeaseDurationMs = _Settings.LeaseDurationMs;
        }
    }
}
