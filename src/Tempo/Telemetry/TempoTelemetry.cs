namespace Tempo.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using Tempo.Enums;

    /// <summary>
    /// Tempo's telemetry emitter. Owns the process-wide <see cref="System.Diagnostics.Metrics.Meter"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/> named <see cref="TelemetryConstants.MeterName"/>, and exposes typed,
    /// bounded-cardinality recording methods for every Tempo component.
    /// <para>
    /// Emission rides the .NET base class library only. Nothing is exported until a host subscribes to the
    /// <c>Tempo</c> meter and activity source (for example with Radiant, the OpenTelemetry SDK, or
    /// <c>dotnet-counters</c>); unobserved instruments cost a few nanoseconds and allocate nothing.
    /// </para>
    /// <para>
    /// All members are thread-safe. Every recording method is best-effort: it never throws, so instrumentation
    /// can never break the code path it observes. Identifiers and free-form text are only ever placed on spans,
    /// never on metric labels.
    /// </para>
    /// </summary>
    public static class TempoTelemetry
    {
        #region Public-Members

        /// <summary>The meter every Tempo instrument is created on. Never null.</summary>
        public static Meter Meter => _Meter;

        /// <summary>The activity source every Tempo span is started on. Never null.</summary>
        public static ActivitySource ActivitySource => _ActivitySource;

        /// <summary>The Tempo library version reported on <see cref="TelemetryConstants.BuildInfo"/> and on the meter. Never null.</summary>
        public static string Version => _Version;

        /// <summary>
        /// The process role reported on <see cref="TelemetryConstants.BuildInfo"/> (for example server, worker, mcp).
        /// Default: <c>library</c>. Null or whitespace resets the default. Bounded: hosts set one fixed value at startup.
        /// </summary>
        public static string Component
        {
            get => _Component;
            set => _Component = String.IsNullOrWhiteSpace(value) ? "library" : value.Trim();
        }

        #endregion

        #region Private-Members

        private static readonly string _Version = ResolveVersion();
        private static readonly Meter _Meter = new Meter(TelemetryConstants.MeterName, _Version);
        private static readonly ActivitySource _ActivitySource = new ActivitySource(TelemetryConstants.ActivitySourceName, _Version);
        private static string _Component = "library";

        private static readonly double[] _LongBuckets = new double[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600 };
        private static readonly double[] _FastBuckets = new double[] { 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 };

        private static readonly Counter<long> _FlowRuns = _Meter.CreateCounter<long>(TelemetryConstants.FlowRuns, "{run}", "Completed data flow runs by runner and outcome.");
        private static readonly Histogram<double> _FlowDuration = CreateHistogram(TelemetryConstants.FlowDuration, "Data flow run duration by runner and outcome.", _LongBuckets);
        private static readonly UpDownCounter<long> _FlowActive = _Meter.CreateUpDownCounter<long>(TelemetryConstants.FlowActive, "{run}", "Data flow runs currently executing in this process.");
        private static readonly Counter<long> _StepExecutions = _Meter.CreateCounter<long>(TelemetryConstants.StepExecutions, "{execution}", "Step executions by runner and outcome.");
        private static readonly Histogram<double> _StepDuration = CreateHistogram(TelemetryConstants.StepDuration, "Step execution duration by runner and outcome.", _LongBuckets);
        private static readonly Counter<long> _StepTransitions = _Meter.CreateCounter<long>(TelemetryConstants.StepTransitions, "{transition}", "Step transitions taken by the flow state machine.");

        private static readonly Counter<long> _StageEvents = _Meter.CreateCounter<long>(TelemetryConstants.StageEvents, "{event}", "Pipeline stage events by pipeline, stage, and outcome.");
        private static readonly Histogram<double> _StageDuration = CreateHistogram(TelemetryConstants.StageDuration, "Pipeline stage duration by pipeline, stage, and outcome.", _LongBuckets);

        private static readonly Counter<long> _DispatchEnqueued = _Meter.CreateCounter<long>(TelemetryConstants.DispatchEnqueued, "{run}", "Flow-run enqueue requests by source and outcome.");
        private static readonly Counter<long> _DispatchAssignments = _Meter.CreateCounter<long>(TelemetryConstants.DispatchAssignments, "{decision}", "Scheduling decisions by executor node kind and outcome.");
        private static readonly Counter<long> _DispatchCompletions = _Meter.CreateCounter<long>(TelemetryConstants.DispatchCompletions, "{run}", "Run completions by node kind, terminal state, and outcome.");
        private static readonly Counter<long> _DispatchRecoveries = _Meter.CreateCounter<long>(TelemetryConstants.DispatchRecoveries, "{assignment}", "Assignments recovered and requeued by reason.");
        private static readonly Counter<long> _WorkerSessions = _Meter.CreateCounter<long>(TelemetryConstants.WorkerSessions, "{event}", "Worker session lifecycle events.");
        private static readonly Counter<long> _WorkerFrames = _Meter.CreateCounter<long>(TelemetryConstants.WorkerFrames, "{frame}", "Worker protocol frames by direction and type.");
        private static readonly Counter<long> _WorkerAssignments = _Meter.CreateCounter<long>(TelemetryConstants.WorkerAssignments, "{assignment}", "Assignments received by a worker by outcome.");

        private static readonly UpDownCounter<long> _CapacityInUse = _Meter.CreateUpDownCounter<long>(TelemetryConstants.CapacityInUse, "{slot}", "External runtime process slots in use.");
        private static readonly UpDownCounter<long> _CapacityQueued = _Meter.CreateUpDownCounter<long>(TelemetryConstants.CapacityQueued, "{request}", "Step runs waiting for an external runtime process slot.");
        private static readonly Histogram<double> _CapacityWait = CreateHistogram(TelemetryConstants.CapacityWait, "Time spent waiting for an external runtime process slot.", _LongBuckets);
        private static readonly Counter<long> _ProcessKills = _Meter.CreateCounter<long>(TelemetryConstants.ProcessKills, "{process}", "External runtime processes killed by reason.");
        private static readonly Histogram<double> _LimiterWait = CreateHistogram(TelemetryConstants.LimiterWait, "Time spent waiting on an internal limiter.", _FastBuckets);
        private static readonly Counter<long> _CacheLookups = _Meter.CreateCounter<long>(TelemetryConstants.CacheLookups, "{lookup}", "Cache lookups by cache and outcome.");
        private static readonly Histogram<double> _CacheDuration = CreateHistogram(TelemetryConstants.CacheDuration, "Cache preparation duration (lookup plus fill on miss).", _LongBuckets);

        private static readonly Counter<long> _IntegrationRequests = _Meter.CreateCounter<long>(TelemetryConstants.IntegrationRequests, "{request}", "Outbound integration calls by service, operation, and outcome.");
        private static readonly Histogram<double> _IntegrationDuration = CreateHistogram(TelemetryConstants.IntegrationDuration, "Outbound integration call duration by service, operation, and outcome.", _FastBuckets);

        private static readonly Counter<long> _TaskRuns = _Meter.CreateCounter<long>(TelemetryConstants.TaskRuns, "{run}", "Background task iterations by task and outcome.");
        private static readonly Histogram<double> _TaskDuration = CreateHistogram(TelemetryConstants.TaskDuration, "Background task iteration duration by task and outcome.", _LongBuckets);
        private static readonly UpDownCounter<long> _TaskPending = _Meter.CreateUpDownCounter<long>(TelemetryConstants.TaskPending, "{item}", "Fire-and-forget background work items queued but not yet finished.");
        private static readonly Counter<long> _TaskItems = _Meter.CreateCounter<long>(TelemetryConstants.TaskItems, "{item}", "Items processed by background tasks by task and action.");

        private static readonly Counter<long> _AuthAttempts = _Meter.CreateCounter<long>(TelemetryConstants.AuthAttempts, "{attempt}", "Authentication attempts by method and outcome.");
        private static readonly Counter<long> _AuthzDecisions = _Meter.CreateCounter<long>(TelemetryConstants.AuthzDecisions, "{decision}", "Authorization decisions by outcome.");

        private static readonly Counter<long> _McpToolCalls = _Meter.CreateCounter<long>(TelemetryConstants.McpToolCalls, "{call}", "MCP tool invocations by tool and outcome.");
        private static readonly Histogram<double> _McpToolDuration = CreateHistogram(TelemetryConstants.McpToolDuration, "MCP tool invocation duration by tool and outcome.", _FastBuckets);

        private static readonly Counter<long> _Errors = _Meter.CreateCounter<long>(TelemetryConstants.Errors, "{error}", "Handled errors by component and error type.");

        private static readonly ConcurrentDictionary<string, double> _LastSuccess = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, double> _Config = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, long[]> _WorkerPool = new ConcurrentDictionary<string, long[]>(StringComparer.Ordinal);
        private static long _QueueDepth = -1;
        private static int _SchedulerActive = -1;

        private static readonly ObservableGauge<double> _LastSuccessGauge = _Meter.CreateObservableGauge<double>(
            TelemetryConstants.LastSuccess, ObserveLastSuccess, "s", "Unix time of the last successful completion per pipeline or task.");
        private static readonly ObservableGauge<long> _BuildInfoGauge = _Meter.CreateObservableGauge<long>(
            TelemetryConstants.BuildInfo, ObserveBuildInfo, "{info}", "Always 1; labels carry the build version and process role.");
        private static readonly ObservableGauge<double> _ConfigGauge = _Meter.CreateObservableGauge<double>(
            TelemetryConstants.ConfigSetting, ObserveConfig, "{value}", "Safe, non-secret configuration values.");
        private static readonly ObservableGauge<long> _QueueDepthGauge = _Meter.CreateObservableGauge<long>(
            TelemetryConstants.DispatchQueueDepth, ObserveQueueDepth, "{run}", "Flow runs waiting in the dispatch queue (sampled by the scheduler loop).");
        private static readonly ObservableGauge<int> _SchedulerActiveGauge = _Meter.CreateObservableGauge<int>(
            TelemetryConstants.DispatchSchedulerActive, ObserveSchedulerActive, "{state}", "1 when this server owns scheduling, 0 when suppressed.");
        private static readonly ObservableGauge<long> _WorkersConnectedGauge = _Meter.CreateObservableGauge<long>(
            TelemetryConstants.WorkersConnected, () => ObserveWorkerPool(0), "{executor}", "Executors that can accept work by node kind.");
        private static readonly ObservableGauge<long> _WorkersCapacityGauge = _Meter.CreateObservableGauge<long>(
            TelemetryConstants.WorkersCapacity, () => ObserveWorkerPool(1), "{slot}", "Run slots offered by connected executors by node kind.");
        private static readonly ObservableGauge<long> _WorkersInUseGauge = _Meter.CreateObservableGauge<long>(
            TelemetryConstants.WorkersInUse, () => ObserveWorkerPool(2), "{slot}", "Run slots in use on connected executors by node kind.");

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start a span on the Tempo activity source. Returns null when no listener samples it (the common,
        /// unobserved case) or when span creation fails, so callers use the null-conditional operator.
        /// </summary>
        /// <param name="name">Span name. Must be low-cardinality (identifiers belong in tags). Null returns null.</param>
        /// <param name="kind">Span kind. Default: <see cref="ActivityKind.Internal"/>.</param>
        /// <param name="parentTraceParent">Optional W3C traceparent to parent the span on (used across process and queue boundaries). Null or invalid values fall back to <see cref="Activity.Current"/>.</param>
        /// <param name="parentTraceState">Optional W3C tracestate accompanying <paramref name="parentTraceParent"/>.</param>
        /// <returns>The started span, or null.</returns>
        public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, string? parentTraceParent = null, string? parentTraceState = null)
        {
            if (String.IsNullOrEmpty(name)) return null;
            try
            {
                if (!String.IsNullOrWhiteSpace(parentTraceParent) &&
                    ActivityContext.TryParse(parentTraceParent, parentTraceState, out ActivityContext parent))
                {
                    return _ActivitySource.StartActivity(name, kind, parent);
                }

                return _ActivitySource.StartActivity(name, kind);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Start a span with an explicit start time, for work whose span is only worth emitting once its outcome is
        /// known (for example a scheduling attempt that may simply be retried). Pair with <see cref="StopActivityAt"/>.
        /// Returns null when unobserved or on failure.
        /// </summary>
        /// <param name="name">Span name. Must be low-cardinality.</param>
        /// <param name="startTime">The span start time.</param>
        /// <param name="kind">Span kind. Default: <see cref="ActivityKind.Internal"/>.</param>
        /// <param name="parentTraceParent">Optional W3C traceparent to parent on; falls back to <see cref="Activity.Current"/>.</param>
        /// <param name="parentTraceState">Optional W3C tracestate.</param>
        /// <returns>The started span, or null.</returns>
        public static Activity? StartActivityAt(string name, DateTimeOffset startTime, ActivityKind kind = ActivityKind.Internal, string? parentTraceParent = null, string? parentTraceState = null)
        {
            if (String.IsNullOrEmpty(name)) return null;
            try
            {
                ActivityContext parent = default;
                if (String.IsNullOrWhiteSpace(parentTraceParent) ||
                    !ActivityContext.TryParse(parentTraceParent, parentTraceState, out parent))
                {
                    parent = Activity.Current?.Context ?? default;
                }

                return _ActivitySource.StartActivity(name, kind, parent, null, null, startTime);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Stop a span at an explicit end time. Null-safe and best-effort.</summary>
        /// <param name="activity">Span. May be null.</param>
        /// <param name="endTime">The span end time.</param>
        public static void StopActivityAt(Activity? activity, DateTimeOffset endTime)
        {
            if (activity == null) return;
            try
            {
                activity.SetEndTime(endTime.UtcDateTime);
                activity.Stop();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Start a new root span that does not inherit <see cref="Activity.Current"/>, for background work that
        /// begins its own unit of work (scheduler passes, maintenance tasks). Returns null when unobserved.
        /// </summary>
        /// <param name="name">Span name. Must be low-cardinality.</param>
        /// <param name="kind">Span kind. Default: <see cref="ActivityKind.Internal"/>.</param>
        /// <returns>The started span, or null.</returns>
        public static Activity? StartRootActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            if (String.IsNullOrEmpty(name)) return null;
            Activity? previous = Activity.Current;
            try
            {
                Activity.Current = null;
                return _ActivitySource.StartActivity(name, kind);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (Activity.Current == null) Activity.Current = previous;
            }
        }

        /// <summary>
        /// Start a client span for an outbound integration call, named <c>"&lt;service&gt; &lt;operation&gt;"</c>.
        /// When <paramref name="requireParent"/> is true the span is only created inside an existing trace, which
        /// keeps chatty low-level calls (database polling, heartbeats) from producing orphan root traces.
        /// </summary>
        /// <param name="service">Bounded integration service name (for example sqlite, http, tempo-server).</param>
        /// <param name="operation">Bounded operation name (for example select, GET, artifact_download).</param>
        /// <param name="requireParent">Only create the span when <see cref="Activity.Current"/> is set. Default: false.</param>
        /// <param name="kind">Span kind. Default: <see cref="ActivityKind.Client"/>.</param>
        /// <returns>The started span, or null.</returns>
        public static Activity? StartIntegration(string service, string operation, bool requireParent = false, ActivityKind kind = ActivityKind.Client)
        {
            if (requireParent && Activity.Current == null) return null;
            Activity? activity = StartActivity(Safe(service) + " " + Safe(operation), kind);
            activity?.SetTag(TelemetryConstants.AttrService, Safe(service));
            activity?.SetTag(TelemetryConstants.AttrOperation, Safe(operation));
            return activity;
        }

        /// <summary>Start a span for one pipeline stage, named <c>"stage:&lt;stage&gt;"</c>. Returns null when unobserved.</summary>
        /// <param name="pipeline">Bounded pipeline name (dispatch, flow, worker).</param>
        /// <param name="stage">Bounded stage name.</param>
        /// <returns>The started span, or null.</returns>
        public static Activity? StartStage(string pipeline, string stage)
        {
            Activity? activity = StartActivity(TelemetryConstants.SpanStagePrefix + Safe(stage));
            activity?.SetTag(TelemetryConstants.AttrPipeline, Safe(pipeline));
            activity?.SetTag(TelemetryConstants.AttrStage, Safe(stage));
            return activity;
        }

        /// <summary>Start the root span for one background task iteration, named <c>"task:&lt;task&gt;"</c>. Returns null when unobserved.</summary>
        /// <param name="task">Bounded task name.</param>
        /// <returns>The started span, or null.</returns>
        public static Activity? StartTask(string task)
        {
            Activity? activity = StartRootActivity(TelemetryConstants.SpanTaskPrefix + Safe(task));
            activity?.SetTag(TelemetryConstants.AttrTask, Safe(task));
            return activity;
        }

        /// <summary>The W3C traceparent of <see cref="Activity.Current"/>, for propagation across queues and process boundaries. Null when no W3C activity is current.</summary>
        /// <returns>The traceparent string, or null.</returns>
        public static string? CurrentTraceParent()
        {
            return TraceParentOf(Activity.Current);
        }

        /// <summary>The W3C traceparent of the supplied activity. Null when the activity is null or not in W3C format.</summary>
        /// <param name="activity">Activity. May be null.</param>
        /// <returns>The traceparent string, or null.</returns>
        public static string? TraceParentOf(Activity? activity)
        {
            if (activity == null || activity.IdFormat != ActivityIdFormat.W3C) return null;
            return activity.Id;
        }

        /// <summary>The W3C tracestate of <see cref="Activity.Current"/>, or null.</summary>
        /// <returns>The tracestate string, or null.</returns>
        public static string? CurrentTraceState()
        {
            return Activity.Current?.TraceStateString;
        }

        /// <summary>Mark a span successful. Null-safe and best-effort.</summary>
        /// <param name="activity">Span. May be null.</param>
        public static void SetOk(Activity? activity)
        {
            try { activity?.SetStatus(ActivityStatusCode.Ok); } catch (Exception) { }
        }

        /// <summary>
        /// Mark a span failed with a bounded error type and an optional description. Null-safe and best-effort.
        /// The description is placed on the span only; it must not contain secrets or payloads.
        /// </summary>
        /// <param name="activity">Span. May be null.</param>
        /// <param name="errorType">Bounded error type (exception type name or a code such as timeout).</param>
        /// <param name="description">Optional human-readable description.</param>
        public static void SetError(Activity? activity, string errorType, string? description = null)
        {
            if (activity == null) return;
            try
            {
                activity.SetTag(TelemetryConstants.AttrErrorType, Safe(errorType));
                activity.SetStatus(ActivityStatusCode.Error, description);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Record an exception on a span as an OpenTelemetry <c>exception</c> event and mark the span failed.
        /// Null-safe and best-effort. Only the exception type and message are recorded (no stack trace payloads beyond the message).
        /// </summary>
        /// <param name="activity">Span. May be null.</param>
        /// <param name="exception">Exception. May be null (then only the status is set).</param>
        public static void RecordException(Activity? activity, Exception? exception)
        {
            if (activity == null) return;
            try
            {
                if (exception != null)
                {
                    ActivityTagsCollection tags = new ActivityTagsCollection
                    {
                        { "exception.type", exception.GetType().FullName },
                        { "exception.message", exception.Message }
                    };
                    activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
                }

                SetError(activity, exception == null ? "error" : exception.GetType().Name, exception?.Message);
            }
            catch (Exception) { }
        }

        /// <summary>Map a step result to a bounded outcome label.</summary>
        /// <param name="result">Step result type.</param>
        /// <returns>success, error, exception, timeout, or max_iterations_exceeded.</returns>
        public static string OutcomeOf(StepResultTypeEnum result)
        {
            switch (result)
            {
                case StepResultTypeEnum.Success: return TelemetryConstants.OutcomeSuccess;
                case StepResultTypeEnum.Error: return TelemetryConstants.OutcomeError;
                case StepResultTypeEnum.Exception: return TelemetryConstants.OutcomeException;
                case StepResultTypeEnum.Timeout: return TelemetryConstants.OutcomeTimeout;
                case StepResultTypeEnum.MaxIterationsExceeded: return "max_iterations_exceeded";
                default: return "unknown";
            }
        }

        /// <summary>
        /// Map a runner type to a bounded runner label: the type name with any <c>StepRunner</c> suffix removed, in snake case
        /// (for example <c>RestStepRunner</c> becomes <c>rest</c> and <c>ArtifactPythonStepRunner</c> becomes <c>artifact_python</c>).
        /// </summary>
        /// <param name="runnerType">Runner type. Null returns <c>unknown</c>.</param>
        /// <returns>The runner label.</returns>
        public static string RunnerLabelOf(Type? runnerType)
        {
            if (runnerType == null) return "unknown";
            return _RunnerLabels.GetOrAdd(runnerType, t => ToSnakeCase(TrimSuffix(t.Name, "StepRunner")));
        }

        /// <summary>Record a completed data flow run. Best-effort.</summary>
        /// <param name="runner">Bounded flow runner label (engine, registry).</param>
        /// <param name="outcome">Bounded outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordFlowRun(string runner, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList { { TelemetryConstants.AttrRunner, Safe(runner) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } };
                _FlowRuns.Add(1, tags);
                _FlowDuration.Record(Math.Max(0, seconds), tags);
                if (String.Equals(outcome, TelemetryConstants.OutcomeSuccess, StringComparison.Ordinal)) MarkSuccess("flow_run");
            }
            catch (Exception) { }
        }

        /// <summary>Adjust the count of in-flight flow runs. Best-effort.</summary>
        /// <param name="runner">Bounded flow runner label.</param>
        /// <param name="delta">+1 when a run starts, -1 when it ends.</param>
        public static void AddActiveFlow(string runner, int delta)
        {
            try { _FlowActive.Add(delta, new TagList { { TelemetryConstants.AttrRunner, Safe(runner) } }); } catch (Exception) { }
        }

        /// <summary>Record a completed step execution. Best-effort.</summary>
        /// <param name="runner">Bounded step runner label.</param>
        /// <param name="outcome">Bounded outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordStep(string runner, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList { { TelemetryConstants.AttrRunner, Safe(runner) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } };
                _StepExecutions.Add(1, tags);
                _StepDuration.Record(Math.Max(0, seconds), tags);
            }
            catch (Exception) { }
        }

        /// <summary>Record a step transition. Best-effort.</summary>
        /// <param name="path">on_success, on_failure, on_exception, or terminal.</param>
        public static void RecordTransition(string path)
        {
            try { _StepTransitions.Add(1, new TagList { { TelemetryConstants.AttrPath, Safe(path) } }); } catch (Exception) { }
        }

        /// <summary>Record the transition the flow state machine took after a step. Best-effort.</summary>
        /// <param name="result">The step result type.</param>
        /// <param name="isException">Whether the step was routed down the exception path.</param>
        /// <param name="nextStepId">The next step identifier; null or empty means the flow terminated.</param>
        public static void RecordTransition(StepResultTypeEnum result, bool isException, string? nextStepId)
        {
            string path;
            if (String.IsNullOrEmpty(nextStepId)) path = "terminal";
            else if (isException || result == StepResultTypeEnum.Exception || result == StepResultTypeEnum.Timeout) path = "on_exception";
            else if (result == StepResultTypeEnum.Success) path = "on_success";
            else path = "on_failure";
            RecordTransition(path);
        }

        /// <summary>Record one pipeline stage. Best-effort.</summary>
        /// <param name="pipeline">Bounded pipeline name (dispatch, flow, worker).</param>
        /// <param name="stage">Bounded stage name (for example queued, plan, assign, execute, complete).</param>
        /// <param name="outcome">Bounded outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordStage(string pipeline, string stage, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryConstants.AttrPipeline, Safe(pipeline) },
                    { TelemetryConstants.AttrStage, Safe(stage) },
                    { TelemetryConstants.AttrOutcome, Safe(outcome) }
                };
                _StageEvents.Add(1, tags);
                _StageDuration.Record(Math.Max(0, seconds), tags);
            }
            catch (Exception) { }
        }

        /// <summary>Record an outbound integration call. Best-effort.</summary>
        /// <param name="service">Bounded service name.</param>
        /// <param name="operation">Bounded operation name.</param>
        /// <param name="outcome">Bounded outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordIntegration(string service, string operation, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryConstants.AttrService, Safe(service) },
                    { TelemetryConstants.AttrOperation, Safe(operation) },
                    { TelemetryConstants.AttrOutcome, Safe(outcome) }
                };
                _IntegrationRequests.Add(1, tags);
                _IntegrationDuration.Record(Math.Max(0, seconds), tags);
            }
            catch (Exception) { }
        }

        /// <summary>Record one background task iteration and, on success, stamp its last-success time. Best-effort.</summary>
        /// <param name="task">Bounded task name.</param>
        /// <param name="outcome">Bounded outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordTask(string task, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList { { TelemetryConstants.AttrTask, Safe(task) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } };
                _TaskRuns.Add(1, tags);
                _TaskDuration.Record(Math.Max(0, seconds), tags);
                if (String.Equals(outcome, TelemetryConstants.OutcomeSuccess, StringComparison.Ordinal)) MarkSuccess(task);
            }
            catch (Exception) { }
        }

        /// <summary>Adjust the count of pending fire-and-forget work items for a background task. Best-effort.</summary>
        /// <param name="task">Bounded task name.</param>
        /// <param name="delta">+1 when an item is queued, -1 when it finishes.</param>
        public static void AddTaskPending(string task, int delta)
        {
            try { _TaskPending.Add(delta, new TagList { { TelemetryConstants.AttrTask, Safe(task) } }); } catch (Exception) { }
        }

        /// <summary>Record items processed by a background task. Zero or negative counts are ignored. Best-effort.</summary>
        /// <param name="task">Bounded task name.</param>
        /// <param name="action">Bounded action (for example deleted, marked).</param>
        /// <param name="count">Item count.</param>
        public static void RecordTaskItems(string task, string action, long count)
        {
            if (count <= 0) return;
            try { _TaskItems.Add(count, new TagList { { TelemetryConstants.AttrTask, Safe(task) }, { TelemetryConstants.AttrAction, Safe(action) } }); } catch (Exception) { }
        }

        /// <summary>Stamp the last-success time of a pipeline or task to now. Best-effort.</summary>
        /// <param name="task">Bounded pipeline or task name.</param>
        public static void MarkSuccess(string task)
        {
            try { _LastSuccess[Safe(task)] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0; } catch (Exception) { }
        }

        /// <summary>Record a flow-run enqueue request. Best-effort.</summary>
        /// <param name="source">api or trigger.</param>
        /// <param name="outcome">accepted or rejected.</param>
        public static void RecordEnqueue(string source, string outcome)
        {
            try { _DispatchEnqueued.Add(1, new TagList { { TelemetryConstants.AttrSource, Safe(source) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } }); } catch (Exception) { }
        }

        /// <summary>Record a scheduling decision. Best-effort.</summary>
        /// <param name="nodeKind">server, worker, or none.</param>
        /// <param name="outcome">assigned, no_executor, no_eligible_worker, plan_failed, or send_failed.</param>
        public static void RecordAssignment(string nodeKind, string outcome)
        {
            try { _DispatchAssignments.Add(1, new TagList { { TelemetryConstants.AttrNodeKind, Safe(nodeKind) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } }); } catch (Exception) { }
        }

        /// <summary>Record a run completion reported to the coordinator. Best-effort.</summary>
        /// <param name="nodeKind">server, worker, or unknown.</param>
        /// <param name="state">Terminal flow-run state (lowercase).</param>
        /// <param name="outcome">applied or stale.</param>
        public static void RecordCompletion(string nodeKind, string state, string outcome)
        {
            try
            {
                _DispatchCompletions.Add(1, new TagList
                {
                    { TelemetryConstants.AttrNodeKind, Safe(nodeKind) },
                    { TelemetryConstants.AttrState, Safe(state) },
                    { TelemetryConstants.AttrOutcome, Safe(outcome) }
                });
            }
            catch (Exception) { }
        }

        /// <summary>Record assignments recovered and requeued. Zero or negative counts are ignored. Best-effort.</summary>
        /// <param name="reason">lease_expired, heartbeat_timeout, superseded_session, or disconnected.</param>
        /// <param name="count">Recovered assignment count.</param>
        public static void RecordRecoveries(string reason, long count)
        {
            if (count <= 0) return;
            try { _DispatchRecoveries.Add(count, new TagList { { TelemetryConstants.AttrReason, Safe(reason) } }); } catch (Exception) { }
        }

        /// <summary>Publish the sampled dispatch queue depth. Negative values hide the series. Best-effort.</summary>
        /// <param name="depth">Queued flow runs.</param>
        public static void SetQueueDepth(long depth)
        {
            Interlocked.Exchange(ref _QueueDepth, depth);
        }

        /// <summary>Publish whether this server currently owns scheduling. Best-effort.</summary>
        /// <param name="active">True when scheduling is active.</param>
        public static void SetSchedulerActive(bool active)
        {
            Interlocked.Exchange(ref _SchedulerActive, active ? 1 : 0);
        }

        /// <summary>Publish the executor pool for one node kind. Best-effort.</summary>
        /// <param name="nodeKind">server or worker.</param>
        /// <param name="connected">Executors that can accept work.</param>
        /// <param name="capacity">Run slots offered.</param>
        /// <param name="inUse">Run slots in use.</param>
        public static void SetWorkerPool(string nodeKind, long connected, long capacity, long inUse)
        {
            try { _WorkerPool[Safe(nodeKind)] = new long[] { connected, capacity, inUse }; } catch (Exception) { }
        }

        /// <summary>Record a worker session lifecycle event. Best-effort.</summary>
        /// <param name="lifecycleEvent">connected, disconnected, heartbeat_timeout, superseded, auth_failed, or protocol_error.</param>
        public static void RecordWorkerSession(string lifecycleEvent)
        {
            try { _WorkerSessions.Add(1, new TagList { { TelemetryConstants.AttrEvent, Safe(lifecycleEvent) } }); } catch (Exception) { }
        }

        /// <summary>Record a worker protocol frame. Best-effort.</summary>
        /// <param name="direction">in or out, from the recording process's point of view.</param>
        /// <param name="frameType">Protocol frame type (bounded by the protocol).</param>
        public static void RecordWorkerFrame(string direction, string frameType)
        {
            try { _WorkerFrames.Add(1, new TagList { { TelemetryConstants.AttrDirection, Safe(direction) }, { TelemetryConstants.AttrFrame, Safe(frameType) } }); } catch (Exception) { }
        }

        /// <summary>Record an assignment received by a worker. Best-effort.</summary>
        /// <param name="outcome">accepted, rejected_session, rejected_draining, rejected_capabilities, or rejected_capacity.</param>
        public static void RecordWorkerAssignment(string outcome)
        {
            try { _WorkerAssignments.Add(1, new TagList { { TelemetryConstants.AttrOutcome, Safe(outcome) } }); } catch (Exception) { }
        }

        /// <summary>Adjust external runtime capacity counters. Best-effort.</summary>
        /// <param name="inUseDelta">Change in slots in use.</param>
        /// <param name="queuedDelta">Change in queued requests.</param>
        public static void AddCapacity(int inUseDelta, int queuedDelta)
        {
            try
            {
                if (inUseDelta != 0) _CapacityInUse.Add(inUseDelta);
                if (queuedDelta != 0) _CapacityQueued.Add(queuedDelta);
            }
            catch (Exception) { }
        }

        /// <summary>Record a wait for an external runtime process slot. Best-effort.</summary>
        /// <param name="outcome">acquired or cancelled.</param>
        /// <param name="seconds">Wait duration in seconds.</param>
        public static void RecordCapacityWait(string outcome, double seconds)
        {
            try { _CapacityWait.Record(Math.Max(0, seconds), new TagList { { TelemetryConstants.AttrOutcome, Safe(outcome) } }); } catch (Exception) { }
        }

        /// <summary>Record an external runtime process kill. Best-effort.</summary>
        /// <param name="reason">timeout or cancelled.</param>
        public static void RecordProcessKill(string reason)
        {
            try { _ProcessKills.Add(1, new TagList { { TelemetryConstants.AttrReason, Safe(reason) } }); } catch (Exception) { }
        }

        /// <summary>Record time spent waiting on an internal limiter (mutex or semaphore). Best-effort.</summary>
        /// <param name="limiter">Bounded limiter name (for example dispatch_gate, sqlite_write).</param>
        /// <param name="seconds">Wait duration in seconds.</param>
        public static void RecordLimiterWait(string limiter, double seconds)
        {
            try { _LimiterWait.Record(Math.Max(0, seconds), new TagList { { TelemetryConstants.AttrLimiter, Safe(limiter) } }); } catch (Exception) { }
        }

        /// <summary>Record a cache lookup and its preparation time. Best-effort.</summary>
        /// <param name="cache">Bounded cache name (for example artifact_package, python_env).</param>
        /// <param name="outcome">hit, miss, or error.</param>
        /// <param name="seconds">Lookup plus fill duration in seconds.</param>
        public static void RecordCache(string cache, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList { { TelemetryConstants.AttrCache, Safe(cache) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } };
                _CacheLookups.Add(1, tags);
                _CacheDuration.Record(Math.Max(0, seconds), tags);
            }
            catch (Exception) { }
        }

        /// <summary>Record an authentication attempt. Best-effort.</summary>
        /// <param name="method">token, api_key, access_key, password, worker_token, or none.</param>
        /// <param name="outcome">Authentication result (lowercase).</param>
        public static void RecordAuthentication(string method, string outcome)
        {
            try { _AuthAttempts.Add(1, new TagList { { TelemetryConstants.AttrMethod, Safe(method) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } }); } catch (Exception) { }
        }

        /// <summary>Record an authorization decision. Best-effort.</summary>
        /// <param name="outcome">Authorization result (lowercase).</param>
        public static void RecordAuthorization(string outcome)
        {
            try { _AuthzDecisions.Add(1, new TagList { { TelemetryConstants.AttrOutcome, Safe(outcome) } }); } catch (Exception) { }
        }

        /// <summary>Record an MCP tool invocation. Best-effort.</summary>
        /// <param name="tool">Registered tool name (bounded by the tool registry).</param>
        /// <param name="outcome">Bounded outcome.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordMcpTool(string tool, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList { { TelemetryConstants.AttrTool, Safe(tool) }, { TelemetryConstants.AttrOutcome, Safe(outcome) } };
                _McpToolCalls.Add(1, tags);
                _McpToolDuration.Record(Math.Max(0, seconds), tags);
            }
            catch (Exception) { }
        }

        /// <summary>Record a handled error. Best-effort.</summary>
        /// <param name="component">Bounded component name.</param>
        /// <param name="errorType">Bounded error type (exception type name or a code).</param>
        public static void RecordError(string component, string errorType)
        {
            try { _Errors.Add(1, new TagList { { TelemetryConstants.AttrComponent, Safe(component) }, { TelemetryConstants.AttrErrorType, Safe(errorType) } }); } catch (Exception) { }
        }

        /// <summary>Record a handled exception by its type name. Null exceptions are recorded as <c>error</c>. Best-effort.</summary>
        /// <param name="component">Bounded component name.</param>
        /// <param name="exception">Exception. May be null.</param>
        public static void RecordError(string component, Exception? exception)
        {
            RecordError(component, exception == null ? "error" : exception.GetType().Name);
        }

        /// <summary>
        /// Publish a safe, non-secret configuration value on <see cref="TelemetryConstants.ConfigSetting"/>.
        /// Hosts call this once at startup for a fixed set of settings. Best-effort.
        /// </summary>
        /// <param name="setting">Bounded setting name.</param>
        /// <param name="value">Numeric value (booleans as 0 or 1).</param>
        public static void SetConfig(string setting, double value)
        {
            try { _Config[Safe(setting)] = value; } catch (Exception) { }
        }

        /// <summary>Elapsed seconds since a <see cref="Stopwatch.GetTimestamp"/> value.</summary>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Elapsed seconds.</returns>
        public static double SecondsSince(long startTimestamp)
        {
            return Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        }

        #endregion

        #region Private-Methods

        private static readonly ConcurrentDictionary<Type, string> _RunnerLabels = new ConcurrentDictionary<Type, string>();

        private static Histogram<double> CreateHistogram(string name, string description, double[] buckets)
        {
            return _Meter.CreateHistogram<double>(
                name,
                "s",
                description,
                tags: null,
                advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = buckets });
        }

        private static string Safe(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string TrimSuffix(string value, string suffix)
        {
            if (value.Length > suffix.Length && value.EndsWith(suffix, StringComparison.Ordinal)) return value.Substring(0, value.Length - suffix.Length);
            return value;
        }

        private static string ToSnakeCase(string value)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (Char.IsUpper(c))
                {
                    if (i > 0 && (Char.IsLower(value[i - 1]) || (i + 1 < value.Length && Char.IsLower(value[i + 1])))) sb.Append('_');
                    sb.Append(Char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(TempoTelemetry).Assembly;
                string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!String.IsNullOrWhiteSpace(informational))
                {
                    int plus = informational.IndexOf('+');
                    return plus > 0 ? informational.Substring(0, plus) : informational;
                }

                return assembly.GetName().Version?.ToString() ?? "0.0.0";
            }
            catch (Exception)
            {
                return "0.0.0";
            }
        }

        private static IEnumerable<Measurement<double>> ObserveLastSuccess()
        {
            List<Measurement<double>> measurements = new List<Measurement<double>>();
            foreach (KeyValuePair<string, double> entry in _LastSuccess)
            {
                measurements.Add(new Measurement<double>(entry.Value, new KeyValuePair<string, object?>(TelemetryConstants.AttrTask, entry.Key)));
            }

            return measurements;
        }

        private static Measurement<long> ObserveBuildInfo()
        {
            return new Measurement<long>(
                1,
                new KeyValuePair<string, object?>(TelemetryConstants.AttrVersion, _Version),
                new KeyValuePair<string, object?>(TelemetryConstants.AttrComponent, _Component));
        }

        private static IEnumerable<Measurement<double>> ObserveConfig()
        {
            List<Measurement<double>> measurements = new List<Measurement<double>>();
            foreach (KeyValuePair<string, double> entry in _Config)
            {
                measurements.Add(new Measurement<double>(entry.Value, new KeyValuePair<string, object?>(TelemetryConstants.AttrSetting, entry.Key)));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<long>> ObserveQueueDepth()
        {
            long depth = Interlocked.Read(ref _QueueDepth);
            if (depth < 0) return Array.Empty<Measurement<long>>();
            return new[] { new Measurement<long>(depth) };
        }

        private static IEnumerable<Measurement<int>> ObserveSchedulerActive()
        {
            int active = Volatile.Read(ref _SchedulerActive);
            if (active < 0) return Array.Empty<Measurement<int>>();
            return new[] { new Measurement<int>(active) };
        }

        private static IEnumerable<Measurement<long>> ObserveWorkerPool(int index)
        {
            List<Measurement<long>> measurements = new List<Measurement<long>>();
            foreach (KeyValuePair<string, long[]> entry in _WorkerPool)
            {
                measurements.Add(new Measurement<long>(entry.Value[index], new KeyValuePair<string, object?>(TelemetryConstants.AttrNodeKind, entry.Key)));
            }

            return measurements;
        }

        #endregion
    }
}
