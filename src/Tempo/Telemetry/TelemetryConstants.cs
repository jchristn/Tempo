namespace Tempo.Telemetry
{
    /// <summary>
    /// Every telemetry name Tempo emits: meter and activity source names, metric instrument names,
    /// span names, and attribute keys. These strings are a public contract consumed by collectors,
    /// Prometheus queries, Grafana dashboards, and alert rules. Treat a rename as a breaking change.
    /// This class is immutable and thread-safe.
    /// </summary>
    public static class TelemetryConstants
    {
        #region Sources

        /// <summary>Name of the <see cref="System.Diagnostics.Metrics.Meter"/> every Tempo component records into.</summary>
        public const string MeterName = "Tempo";

        /// <summary>Name of the <see cref="System.Diagnostics.ActivitySource"/> every Tempo component starts spans on.</summary>
        public const string ActivitySourceName = "Tempo";

        /// <summary>Name of the Watson webserver meter and activity source (HTTP server metrics and per-request spans).</summary>
        public const string WatsonSourceName = "Watson";

        /// <summary>
        /// Name of the .NET HttpClient activity source. HttpClient starts a child span under any current activity and
        /// propagates that span's id, so hosts subscribe to it to keep outbound HTTP traces gap-free.
        /// </summary>
        public const string HttpClientSourceName = "System.Net.Http";

        #endregion

        #region Flow-and-Step-Metrics

        /// <summary>Counter of completed data flow runs. Labels: runner, outcome.</summary>
        public const string FlowRuns = "tempo.flow.runs";

        /// <summary>Histogram of data flow run duration in seconds. Labels: runner, outcome.</summary>
        public const string FlowDuration = "tempo.flow.duration";

        /// <summary>Up/down counter of data flow runs currently executing in this process. Labels: runner.</summary>
        public const string FlowActive = "tempo.flow.active";

        /// <summary>Counter of step executions. Labels: runner, outcome.</summary>
        public const string StepExecutions = "tempo.step.executions";

        /// <summary>Histogram of step execution duration in seconds. Labels: runner, outcome.</summary>
        public const string StepDuration = "tempo.step.duration";

        /// <summary>Counter of step transitions taken by the flow state machine. Labels: path.</summary>
        public const string StepTransitions = "tempo.step.transitions";

        #endregion

        #region Pipeline-Metrics

        /// <summary>Counter of pipeline stage events. Labels: pipeline, stage, outcome.</summary>
        public const string StageEvents = "tempo.pipeline.stage.events";

        /// <summary>Histogram of pipeline stage duration in seconds (includes the queued stage). Labels: pipeline, stage, outcome.</summary>
        public const string StageDuration = "tempo.pipeline.stage.duration";

        /// <summary>Gauge of the Unix time (seconds) of the last successful completion per pipeline or task. Labels: task.</summary>
        public const string LastSuccess = "tempo.last_success.timestamp";

        #endregion

        #region Dispatch-Metrics

        /// <summary>Counter of flow-run enqueue requests. Labels: source, outcome.</summary>
        public const string DispatchEnqueued = "tempo.dispatch.enqueued";

        /// <summary>Counter of scheduling decisions. Labels: node_kind, outcome.</summary>
        public const string DispatchAssignments = "tempo.dispatch.assignments";

        /// <summary>Counter of run completions reported to the coordinator. Labels: node_kind, state, outcome.</summary>
        public const string DispatchCompletions = "tempo.dispatch.completions";

        /// <summary>Counter of assignments recovered and requeued. Labels: reason.</summary>
        public const string DispatchRecoveries = "tempo.dispatch.recoveries";

        /// <summary>Gauge of flow runs waiting in the dispatch queue (sampled by the scheduler loop).</summary>
        public const string DispatchQueueDepth = "tempo.dispatch.queue.depth";

        /// <summary>Gauge that is 1 when this server owns scheduling and 0 when it is suppressed.</summary>
        public const string DispatchSchedulerActive = "tempo.dispatch.scheduler.active";

        /// <summary>Gauge of executors (local pseudo-worker and remote workers) that can accept work. Labels: node_kind.</summary>
        public const string WorkersConnected = "tempo.workers.connected";

        /// <summary>Gauge of run slots offered by connected executors. Labels: node_kind.</summary>
        public const string WorkersCapacity = "tempo.workers.capacity";

        /// <summary>Gauge of run slots currently in use on connected executors. Labels: node_kind.</summary>
        public const string WorkersInUse = "tempo.workers.in_use";

        /// <summary>Counter of worker session lifecycle events. Labels: event.</summary>
        public const string WorkerSessions = "tempo.worker.sessions";

        /// <summary>Counter of worker protocol frames. Labels: direction, frame.</summary>
        public const string WorkerFrames = "tempo.worker.frames";

        /// <summary>Counter of assignments received by a worker. Labels: outcome.</summary>
        public const string WorkerAssignments = "tempo.worker.assignments";

        #endregion

        #region Capacity-and-Cache-Metrics

        /// <summary>Up/down counter of external runtime process slots in use.</summary>
        public const string CapacityInUse = "tempo.capacity.in_use";

        /// <summary>Up/down counter of step runs waiting for an external runtime process slot.</summary>
        public const string CapacityQueued = "tempo.capacity.queued";

        /// <summary>Histogram of time spent waiting for an external runtime process slot, in seconds. Labels: outcome.</summary>
        public const string CapacityWait = "tempo.capacity.wait.duration";

        /// <summary>Counter of external runtime processes killed (timeout or cancellation). Labels: reason.</summary>
        public const string ProcessKills = "tempo.process.kills";

        /// <summary>Histogram of time spent waiting on an internal limiter (mutex or semaphore), in seconds. Labels: limiter.</summary>
        public const string LimiterWait = "tempo.limiter.wait.duration";

        /// <summary>Counter of cache lookups. Labels: cache, outcome.</summary>
        public const string CacheLookups = "tempo.cache.lookups";

        /// <summary>Histogram of cache preparation (lookup plus fill on miss) duration in seconds. Labels: cache, outcome.</summary>
        public const string CacheDuration = "tempo.cache.duration";

        #endregion

        #region Integration-Metrics

        /// <summary>Counter of outbound integration calls. Labels: service, operation, outcome.</summary>
        public const string IntegrationRequests = "tempo.integration.requests";

        /// <summary>Histogram of outbound integration call duration in seconds. Labels: service, operation, outcome.</summary>
        public const string IntegrationDuration = "tempo.integration.duration";

        #endregion

        #region Background-Task-Metrics

        /// <summary>Counter of background task iterations. Labels: task, outcome.</summary>
        public const string TaskRuns = "tempo.task.runs";

        /// <summary>Histogram of background task iteration duration in seconds. Labels: task, outcome.</summary>
        public const string TaskDuration = "tempo.task.duration";

        /// <summary>Up/down counter of fire-and-forget background work items queued but not yet finished. Labels: task.</summary>
        public const string TaskPending = "tempo.task.pending";

        /// <summary>Counter of items processed by background tasks. Labels: task, action.</summary>
        public const string TaskItems = "tempo.task.items";

        #endregion

        #region Security-Metrics

        /// <summary>Counter of authentication attempts. Labels: method, outcome.</summary>
        public const string AuthAttempts = "tempo.auth.attempts";

        /// <summary>Counter of authorization decisions. Labels: outcome.</summary>
        public const string AuthzDecisions = "tempo.authz.decisions";

        #endregion

        #region Mcp-Metrics

        /// <summary>Counter of MCP tool invocations. Labels: tool, outcome.</summary>
        public const string McpToolCalls = "tempo.mcp.tool.calls";

        /// <summary>Histogram of MCP tool invocation duration in seconds. Labels: tool, outcome.</summary>
        public const string McpToolDuration = "tempo.mcp.tool.duration";

        #endregion

        #region Health-Metrics

        /// <summary>Counter of handled errors. Labels: component, error.type.</summary>
        public const string Errors = "tempo.errors";

        /// <summary>Gauge that is always 1, labeled with build metadata. Labels: version, component.</summary>
        public const string BuildInfo = "tempo.build.info";

        /// <summary>Gauge of safe, non-secret configuration values. Labels: setting.</summary>
        public const string ConfigSetting = "tempo.config.setting";

        #endregion

        #region Span-Names

        /// <summary>Span covering one data flow run.</summary>
        public const string SpanFlowRun = "tempo.flow.run";

        /// <summary>Prefix of the span covering one step execution; the step identifier follows.</summary>
        public const string SpanStepPrefix = "step:";

        /// <summary>Prefix of the span covering one pipeline stage; the stage name follows.</summary>
        public const string SpanStagePrefix = "stage:";

        /// <summary>Prefix of the root span covering one background task iteration; the task name follows.</summary>
        public const string SpanTaskPrefix = "task:";

        /// <summary>Span covering a flow-run enqueue request.</summary>
        public const string SpanDispatchEnqueue = "tempo.dispatch.enqueue";

        /// <summary>Span covering one scheduling decision for a queued flow run.</summary>
        public const string SpanDispatchSchedule = "tempo.dispatch.schedule";

        /// <summary>Span covering the coordinator applying a run completion.</summary>
        public const string SpanDispatchComplete = "tempo.dispatch.complete";

        /// <summary>Span covering the server sending an assignment to a remote worker.</summary>
        public const string SpanWorkerAssignSend = "tempo.worker.assign send";

        /// <summary>Span covering a worker executing one assignment.</summary>
        public const string SpanWorkerAssignment = "tempo.worker.assignment";

        /// <summary>Span covering a worker websocket session registration.</summary>
        public const string SpanWorkerRegister = "tempo.worker.register";

        /// <summary>Span covering a wait for an external runtime process slot.</summary>
        public const string SpanCapacityAcquire = "tempo.capacity.acquire";

        /// <summary>Prefix of the span covering one MCP tool call; the tool name follows.</summary>
        public const string SpanMcpToolPrefix = "mcp.tool ";

        #endregion

        #region Attribute-Keys

        /// <summary>Bounded outcome of an operation (for example success, error, exception, timeout).</summary>
        public const string AttrOutcome = "outcome";

        /// <summary>Flow or step runner kind (for example registry, code, rest, artifact_python).</summary>
        public const string AttrRunner = "runner";

        /// <summary>Pipeline name (dispatch, flow, worker).</summary>
        public const string AttrPipeline = "pipeline";

        /// <summary>Pipeline stage name.</summary>
        public const string AttrStage = "stage";

        /// <summary>Background task name.</summary>
        public const string AttrTask = "task";

        /// <summary>Background task item action (for example deleted, marked).</summary>
        public const string AttrAction = "action";

        /// <summary>Integration service name (for example sqlite, http, tempo-server, process).</summary>
        public const string AttrService = "service";

        /// <summary>Integration operation name (for example select, GET, artifact_download).</summary>
        public const string AttrOperation = "operation";

        /// <summary>Step transition path taken (on_success, on_failure, on_exception, terminal).</summary>
        public const string AttrPath = "path";

        /// <summary>Flow-run enqueue source (api, trigger).</summary>
        public const string AttrSource = "source";

        /// <summary>Executor node kind (server, worker).</summary>
        public const string AttrNodeKind = "node_kind";

        /// <summary>Flow run terminal state.</summary>
        public const string AttrState = "state";

        /// <summary>Reason for a recovery, kill, or disconnect.</summary>
        public const string AttrReason = "reason";

        /// <summary>Lifecycle event name.</summary>
        public const string AttrEvent = "event";

        /// <summary>Frame direction (in, out).</summary>
        public const string AttrDirection = "direction";

        /// <summary>Worker protocol frame type.</summary>
        public const string AttrFrame = "frame";

        /// <summary>Internal limiter name (for example dispatch_gate, sqlite_write).</summary>
        public const string AttrLimiter = "limiter";

        /// <summary>Cache name.</summary>
        public const string AttrCache = "cache";

        /// <summary>Authentication method (token, api_key, access_key, password, worker_token, none).</summary>
        public const string AttrMethod = "method";

        /// <summary>MCP tool name.</summary>
        public const string AttrTool = "tool";

        /// <summary>Component that observed an error (for example dispatch, worker, flow).</summary>
        public const string AttrComponent = "component";

        /// <summary>OpenTelemetry semantic-convention error type (exception type name or bounded code).</summary>
        public const string AttrErrorType = "error.type";

        /// <summary>Build version.</summary>
        public const string AttrVersion = "version";

        /// <summary>Configuration setting name.</summary>
        public const string AttrSetting = "setting";

        /// <summary>Span attribute: tenant identifier (spans only, never a metric label).</summary>
        public const string AttrTenantId = "tempo.tenant.id";

        /// <summary>Span attribute: data flow identifier (spans only).</summary>
        public const string AttrDataFlowId = "tempo.dataflow.id";

        /// <summary>Span attribute: flow run identifier (spans only).</summary>
        public const string AttrFlowRunId = "tempo.flow_run.id";

        /// <summary>Span attribute: step identifier (spans only).</summary>
        public const string AttrStepId = "tempo.step.id";

        /// <summary>Span attribute: step run identifier (spans only).</summary>
        public const string AttrStepRunId = "tempo.step_run.id";

        /// <summary>Span attribute: step result (spans only).</summary>
        public const string AttrStepResult = "tempo.step.result";

        /// <summary>Span attribute: next step selected by the transition (spans only).</summary>
        public const string AttrNextStepId = "tempo.step.next_id";

        /// <summary>Span attribute: run assignment identifier (spans only).</summary>
        public const string AttrAssignmentId = "tempo.assignment.id";

        /// <summary>Span attribute: dispatch attempt number (spans only).</summary>
        public const string AttrAttempt = "tempo.assignment.attempt";

        /// <summary>Span attribute: worker identifier (spans only).</summary>
        public const string AttrWorkerId = "tempo.worker.id";

        /// <summary>Span attribute: worker session identifier (spans only).</summary>
        public const string AttrWorkerSessionId = "tempo.worker.session_id";

        /// <summary>Span attribute: whether the capacity request queued behind the per-tenant limit (spans only).</summary>
        public const string AttrCapacityTenantQueued = "tempo.capacity.tenant_queued";

        /// <summary>Span attribute: whether the capacity request queued behind the server-wide limit (spans only).</summary>
        public const string AttrCapacityServerQueued = "tempo.capacity.server_queued";

        /// <summary>Span attribute: OpenTelemetry database system name.</summary>
        public const string AttrDbSystem = "db.system.name";

        /// <summary>Span attribute: OpenTelemetry database operation name.</summary>
        public const string AttrDbOperation = "db.operation.name";

        /// <summary>Span attribute: OpenTelemetry HTTP request method.</summary>
        public const string AttrHttpMethod = "http.request.method";

        /// <summary>Span attribute: OpenTelemetry HTTP response status code.</summary>
        public const string AttrHttpStatusCode = "http.response.status_code";

        /// <summary>Span attribute: OpenTelemetry server address (host only, never a full URL).</summary>
        public const string AttrServerAddress = "server.address";

        /// <summary>Span attribute: OpenTelemetry server port.</summary>
        public const string AttrServerPort = "server.port";

        /// <summary>Span attribute: OpenTelemetry process exit code.</summary>
        public const string AttrProcessExitCode = "process.exit.code";

        #endregion

        #region Propagation

        /// <summary>Environment variable carrying the W3C traceparent into child processes (OpenTelemetry environment-carrier convention).</summary>
        public const string EnvTraceParent = "TRACEPARENT";

        /// <summary>Environment variable carrying the W3C tracestate into child processes.</summary>
        public const string EnvTraceState = "TRACESTATE";

        #endregion

        #region Outcomes

        /// <summary>Outcome: the operation succeeded.</summary>
        public const string OutcomeSuccess = "success";

        /// <summary>Outcome: the operation returned an expected failure.</summary>
        public const string OutcomeError = "error";

        /// <summary>Outcome: the operation threw or returned an unexpected failure.</summary>
        public const string OutcomeException = "exception";

        /// <summary>Outcome: the operation exceeded its time budget.</summary>
        public const string OutcomeTimeout = "timeout";

        /// <summary>Outcome: the operation was cancelled.</summary>
        public const string OutcomeCancelled = "cancelled";

        #endregion
    }
}
