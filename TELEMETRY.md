# Tempo Telemetry

Tempo ships with metrics, traces, and logs built in. An on-call engineer can use the Grafana dashboards and traces described here to see where time went and what failed, across the server, every worker, the MCP server, artifact subprocesses, and outbound calls, without reading the source.

- **Emit side:** every Tempo assembly records into one `System.Diagnostics.Metrics.Meter` and one `System.Diagnostics.ActivitySource`, both named **`Tempo`**. Nothing is exported until a host subscribes, and unobserved instruments cost a few nanoseconds. The `Tempo` and `Tempo.Core` NuGet packages carry no exporter or SDK dependency.
- **HTTP layer:** Watson 7.2 emits the HTTP server metrics and one server span per request on the **`Watson`** meter and activity source. Tempo confirms `Settings.Telemetry` is on and does not duplicate it.
- **Host side:** `Tempo.Server`, `Tempo.Worker`, and `Tempo.McpServer` each start one [Radiant](https://www.nuget.org/packages/Radiant) host (`Radiant` 0.1.2, package reference only) at the composition root. It subscribes to `Tempo`, `Watson`, and the .NET `System.Net.Http` activity source. It pushes OTLP, can serve Prometheus in-process, adds .NET runtime metrics, and forwards log lines for the server and workers.
- **Stack:** `docker/compose.yaml` brings up an OpenTelemetry Collector, Prometheus, Grafana Tempo, Loki, and Grafana with datasources and seven dashboards provisioned as code.

---

## Contents

1. [Sources and names](#sources-and-names)
2. [Enabling and configuring](#enabling-and-configuring)
3. [Subscribing from your own host (library consumers)](#subscribing-from-your-own-host-library-consumers)
4. [Metrics catalog](#metrics-catalog)
5. [Spans catalog](#spans-catalog)
6. [Context propagation](#context-propagation)
7. [Logs](#logs)
8. [Coverage map](#coverage-map)
9. [The observability stack](#the-observability-stack)
10. [Dashboards](#dashboards)
11. [Recommended alerts (PromQL)](#recommended-alerts-promql)
12. [Cardinality, privacy, and cost](#cardinality-privacy-and-cost)
13. [Known limitations](#known-limitations)
14. [Testing telemetry](#testing-telemetry)

---

## Sources and names

| Name | Kind | Emitted by | Notes |
|---|---|---|---|
| `Tempo` | Meter and ActivitySource | `Tempo` (engine), `Tempo.Core`, `Tempo.Server`, `Tempo.Worker`, `Tempo.McpServer` | Version = the `Tempo` assembly version. |
| `Watson` | Meter and ActivitySource | Watson webserver inside `Tempo.Server` and (through Voltaic) `Tempo.McpServer` | See Watson's `TELEMETRY.md`. |
| `System.Net.Http` | ActivitySource | .NET `HttpClient` | Subscribed so the client span HttpClient inserts under Tempo's own client spans is exported and traces stay gap-free. Its meter is not subscribed (REST step hosts are tenant-defined and would be an unbounded label). |

Every name (meter, source, instrument, span, attribute key, environment carrier) is defined once in `src/Tempo/Telemetry/TelemetryConstants.cs`. Emission helpers live in `src/Tempo/Telemetry/TempoTelemetry.cs`. Treat these strings as a public contract: dashboards and alerts depend on them.

Process roles are reported on `tempo.build.info` as `component` = `server`, `worker`, `mcp`, or `library`. The OpenTelemetry `service.name` is `tempo-server`, `tempo-worker`, or `tempo-mcp`. With the bundled collector it becomes the Prometheus `job` label, and `service.instance.id` becomes `instance`.

## Enabling and configuring

Telemetry export is **on by default** in all three services, pushing OTLP gRPC to `http://127.0.0.1:4317`. Each service has a `telemetry` block in its settings file (`tempo.json`, `tempo.worker.json`, `tempo.mcp.json`), and every key can be overridden by an environment variable.

| Setting (`telemetry.*`) | Environment variable | Default | Meaning |
|---|---|---|---|
| `enabled` | `TEMPO_TELEMETRY_ENABLED` | `true` | Master switch for export. When false nothing is subscribed; emission stays a no-op. |
| `serviceName` | `TEMPO_TELEMETRY_SERVICE_NAME` | process default | `tempo-server`, `tempo-worker`, or `tempo-mcp` when unset. |
| `otlpEnabled` | `TEMPO_TELEMETRY_OTLP_ENABLED` | `true` | Push metrics, traces, and logs over OTLP. |
| `otlpEndpoint` | `TEMPO_TELEMETRY_OTLP_ENDPOINT` | `http://127.0.0.1:4317` | Collector or OTLP backend. Must be an absolute URI. |
| `otlpProtocol` | `TEMPO_TELEMETRY_OTLP_PROTOCOL` | `grpc` | `grpc` or `httpprotobuf`. Prefer `grpc`; see [Known limitations](#known-limitations). |
| `prometheusEnabled` | `TEMPO_TELEMETRY_PROMETHEUS_ENABLED` | `false` | Serve an in-process `/metrics` endpoint covering every subscribed meter. Use it when no collector is deployed. |
| `prometheusHostname` | `TEMPO_TELEMETRY_PROMETHEUS_HOSTNAME` | `127.0.0.1` | Use `*` inside a container. Never expose it publicly. |
| `prometheusPort` | `TEMPO_TELEMETRY_PROMETHEUS_PORT` | `9464` | 1 to 65535. Each process on one host needs its own port. |
| `logsEnabled` | `TEMPO_TELEMETRY_LOGS_ENABLED` | `true` | Forward log lines as OpenTelemetry logs. Always off for `Tempo.McpServer`, which runs no background work. |
| `logsMinimumSeverity` | none | `2` | 0 verbose, 1 debug, 2 information, 3 warning, 4 error, 5 critical, 7 none. |
| `lokiEnabled` | `TEMPO_TELEMETRY_LOKI_ENABLED` | `false` | Push logs straight to Loki (no collector). |
| `lokiEndpoint` | `TEMPO_TELEMETRY_LOKI_ENDPOINT` | `http://127.0.0.1:3100/otlp` | Loki OTLP base URL. |
| `samplingRatio` | `TEMPO_TELEMETRY_SAMPLING_RATIO` | `1.0` | Parent-based trace sampling, 0.0 to 1.0. |
| `metricsExportIntervalMs` | none | `15000` | OTLP metric push interval, clamped to 1000 to 300000. |
| `includeRuntimeMetrics` | none | `true` | .NET runtime and process metrics. |

One related engine setting: `engine.queueDepthSampleIntervalMs` (default 15000, range 1000 to 600000) controls how often the scheduler runs one `COUNT` query to publish `tempo.dispatch.queue.depth`.

Telemetry is best-effort everywhere. Every recording helper swallows its own failures. If the host cannot start (bad endpoint, port in use), the service logs one warning and keeps running without export.

## Subscribing from your own host (library consumers)

If you embed the `Tempo` engine (`DataFlowRunner`, `StepManager`, `RestStepRunner`) or `Tempo.Core` in your own application, subscribe to the two names. With Radiant:

```csharp
RadiantSettings settings = new RadiantSettings("my-app");
settings.Sources.AddMeter("Tempo");
settings.Sources.AddActivitySource("Tempo");
settings.Sources.AddActivitySource("System.Net.Http");   // optional: gap-free outbound HTTP traces
using (RadiantHost host = RadiantHost.Start(settings))
{
    // run flows
}
```

With the OpenTelemetry SDK directly:

```csharp
using MeterProvider meters = Sdk.CreateMeterProviderBuilder().AddMeter("Tempo").AddOtlpExporter().Build();
using TracerProvider traces = Sdk.CreateTracerProviderBuilder().AddSource("Tempo").AddOtlpExporter().Build();
```

`dotnet-counters monitor --counters Tempo -n <process>` also works with no code at all.

## Metrics catalog

All Tempo instruments are on the `Tempo` meter. Durations are histograms in seconds with explicit bucket advice (flow, step, stage, task, and capacity: 5 ms to 600 s; integration, limiter, and MCP: 0.5 ms to 10 s). Prometheus names are what the bundled collector exposes (counters get `_total`, second-unit instruments get `_seconds`, and histograms get `_bucket`, `_sum`, and `_count`).

### Flows and steps (engine and platform)

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `tempo.flow.runs` | `tempo_flow_runs_total` | Counter | `{run}` | `runner`, `outcome` | Completed data flow runs. `runner` = `engine` (`DataFlowRunner`) or `registry` (server-local and worker execution). |
| `tempo.flow.duration` | `tempo_flow_duration_seconds` | Histogram | s | `runner`, `outcome` | End-to-end flow run time. |
| `tempo.flow.active` | `tempo_flow_active` | UpDownCounter | `{run}` | `runner` | Flow runs executing in this process now. |
| `tempo.step.executions` | `tempo_step_executions_total` | Counter | `{execution}` | `runner`, `outcome` | Step executions. `runner` is the runner type in snake case: `code`, `code_attribute`, `rest`, `artifact_process`, `artifact_python`, `artifact_java_script`, `artifact_dotnet_process`, `host_executable`. |
| `tempo.step.duration` | `tempo_step_duration_seconds` | Histogram | s | `runner`, `outcome` | Step execution time, including timeout enforcement. |
| `tempo.step.transitions` | `tempo_step_transitions_total` | Counter | `{transition}` | `path` | Transitions taken: `on_success`, `on_failure`, `on_exception`, `terminal`. |

`outcome` for flows and steps is `success`, `error`, `exception`, `timeout`, `max_iterations_exceeded`, or (flows only) `cancelled`.

### Pipelines (dispatch, flow, worker)

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `tempo.pipeline.stage.events` | `tempo_pipeline_stage_events_total` | Counter | `{event}` | `pipeline`, `stage`, `outcome` | One event per stage execution. |
| `tempo.pipeline.stage.duration` | `tempo_pipeline_stage_duration_seconds` | Histogram | s | `pipeline`, `stage`, `outcome` | Per-stage latency. |
| `tempo.last_success.timestamp` | `tempo_last_success_timestamp_seconds` | Gauge | s | `task` | Unix time of the last success: `dispatch`, `flow_run`, `worker_assignment`, and every background `task`. |

Stages by pipeline:

- `dispatch` (server): `queued` (enqueue to assignment), `plan`, `assign`, `execute` (server-local run), `handoff` (websocket send to a worker), `complete`.
- `flow` (wherever a registry flow runs): `resolve` (step lookup), `validate` (runtime config and input contract), `prepare` (runner creation, including artifact download and cache fill).
- `worker` (worker): `execute` (assignment accepted to completion; `outcome` is the terminal state).

### Dispatch, workers, and protocol

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `tempo.dispatch.enqueued` | `tempo_dispatch_enqueued_total` | Counter | `{run}` | `source` (`api`, `trigger`), `outcome` (`accepted`, `rejected`) | Enqueue requests. |
| `tempo.dispatch.assignments` | `tempo_dispatch_assignments_total` | Counter | `{decision}` | `node_kind` (`server`, `worker`, `none`), `outcome` | `assigned`, `no_executor` (all busy, run stays queued), `no_eligible_worker` and `plan_failed` (run fails), `assign_failed`, `send_failed`. |
| `tempo.dispatch.completions` | `tempo_dispatch_completions_total` | Counter | `{run}` | `node_kind`, `state`, `outcome` (`applied`, `stale`, `failed`) | Completions reported to the coordinator. |
| `tempo.dispatch.recoveries` | `tempo_dispatch_recoveries_total` | Counter | `{assignment}` | `reason` (`lease_expired`, `heartbeat_timeout`, `superseded_session`, `disconnected`) | Assignments recovered and requeued. |
| `tempo.dispatch.queue.depth` | `tempo_dispatch_queue_depth` | Gauge | `{run}` | none | Pending runs (sampled). |
| `tempo.dispatch.scheduler.active` | `tempo_dispatch_scheduler_active` | Gauge | `{state}` | none | 1 when this server owns scheduling, 0 when suppressed by another server. |
| `tempo.workers.connected` | `tempo_workers_connected` | Gauge | `{executor}` | `node_kind` | Executors that can accept work (enabled, not draining). |
| `tempo.workers.capacity` | `tempo_workers_capacity` | Gauge | `{slot}` | `node_kind` | Run slots offered. |
| `tempo.workers.in_use` | `tempo_workers_in_use` | Gauge | `{slot}` | `node_kind` | Run slots in use. |
| `tempo.worker.sessions` | `tempo_worker_sessions_total` | Counter | `{event}` | `event` | Server: `connected`, `disconnected`, `heartbeat_timeout`, `superseded`, `auth_failed`, `protocol_error`. Worker: `connected`, `disconnected`. |
| `tempo.worker.frames` | `tempo_worker_frames_total` | Counter | `{frame}` | `direction` (`in`, `out`), `frame` | Protocol frames by type (`hello`, `hello-ack`, `heartbeat`, `assign`, `assign-ack`, `run-completed`, `drain`, `resume`). |
| `tempo.worker.assignments` | `tempo_worker_assignments_total` | Counter | `{assignment}` | `outcome` | Worker side: `accepted`, `rejected_session`, `rejected_draining`, `rejected_capabilities`, `rejected_capacity`. |

### Capacity, limiters, and caches

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `tempo.capacity.in_use` | `tempo_capacity_in_use` | UpDownCounter | `{slot}` | none | External-runtime process slots in use. |
| `tempo.capacity.queued` | `tempo_capacity_queued` | UpDownCounter | `{request}` | none | Step runs waiting for a process slot. |
| `tempo.capacity.wait.duration` | `tempo_capacity_wait_duration_seconds` | Histogram | s | `outcome` (`acquired`, `cancelled`, `exception`) | Time waiting for a slot. Limits appear on `tempo.config.setting`. |
| `tempo.process.kills` | `tempo_process_kills_total` | Counter | `{process}` | `reason` (`timeout`, `cancelled`) | Artifact processes killed. |
| `tempo.limiter.wait.duration` | `tempo_limiter_wait_duration_seconds` | Histogram | s | `limiter` (`dispatch_gate`, `sqlite_write`) | Wait on internal mutexes: the coordinator gate and the SQLite write lock. |
| `tempo.cache.lookups` | `tempo_cache_lookups_total` | Counter | `{lookup}` | `cache` (`artifact_package`, `python_env`), `outcome` (`hit`, `miss`, `error`) | Cache lookups. |
| `tempo.cache.duration` | `tempo_cache_duration_seconds` | Histogram | s | `cache`, `outcome` | Lookup plus fill time (download and extract, or venv build and pip install). |

### Integrations

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `tempo.integration.requests` | `tempo_integration_requests_total` | Counter | `{request}` | `service`, `operation`, `outcome` | Every outbound call. |
| `tempo.integration.duration` | `tempo_integration_duration_seconds` | Histogram | s | `service`, `operation`, `outcome` | Outbound call latency. |

| `service` | `operation` | Where |
|---|---|---|
| `sqlite`, `postgresql`, `mysql`, `sqlserver` | leading SQL verb: `select`, `insert`, `update`, `delete`, `create`, `alter`, `drop`, `begin`, `pragma`, `with`, `other`, or `batch` | every database call, all four drivers |
| `http` | HTTP method | REST steps (`RestStepRunner`) |
| `process` | runner label (`artifact_process`, `artifact_python`, ...), `dotnet_publish` | artifact subprocesses; C# source-step packaging |
| `tempo-server` | `artifact_download` | worker downloads of artifact packages (time to response headers) |
| `tempo-server` | HTTP method | MCP server calls to the Tempo REST API |

`outcome` is `success`, `error` (4xx or expected failure), `exception` (5xx, network, or thrown), `timeout`, `cancelled`, or `not_found`.

### Background tasks

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `tempo.task.runs` | `tempo_task_runs_total` | Counter | `{run}` | `task`, `outcome` | One per iteration. |
| `tempo.task.duration` | `tempo_task_duration_seconds` | Histogram | s | `task`, `outcome` | Iteration time. |
| `tempo.task.items` | `tempo_task_items_total` | Counter | `{item}` | `task`, `action` | Items processed (`deleted`, `marked`, `scanned`, `blobs_deleted`, `errors`, `flows_updated`, `ambiguous`, `orphaned`). |
| `tempo.task.pending` | `tempo_task_pending` | UpDownCounter | `{item}` | `task` | Fire-and-forget work queued but not finished (request-history capture). |

Tasks: `request_history_prune`, `artifact_gc`, `run_log_prune` (periodic), `request_history_capture` (per request), and the startup tasks `database_init`, `hydration`, `inline_rest_migration`, `builtin_reconciliation`.

### Security, MCP, errors, build, and config

| Instrument | Prometheus | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `tempo.auth.attempts` | `tempo_auth_attempts_total` | Counter | `{attempt}` | `method` (`token`, `api_key`, `access_key`, `password`, `worker_token`, `none`), `outcome` | Authentication results (`success`, `notfound`, `inactive`, `invalid`, `expired`, `none`). |
| `tempo.authz.decisions` | `tempo_authz_decisions_total` | Counter | `{decision}` | `outcome` (`permitted`, `deniedimplicit`, `deniedexplicit`) | Permission-checked authorization decisions. |
| `tempo.mcp.tool.calls` | `tempo_mcp_tool_calls_total` | Counter | `{call}` | `tool`, `outcome` | MCP tool invocations (tool names are the fixed registry). |
| `tempo.mcp.tool.duration` | `tempo_mcp_tool_duration_seconds` | Histogram | s | `tool`, `outcome` | MCP tool latency. |
| `tempo.errors` | `tempo_errors_total` | Counter | `{error}` | `component`, `error.type` | Handled errors by component (`flow`, `database`, `dispatch`, `http`, `worker_session`, `worker_connection`, `worker_heartbeat`, `worker_assignment`, `mcp`, and task names) and exception type. |
| `tempo.build.info` | `tempo_build_info` | Gauge | `{info}` | `version`, `component` | Always 1. One series per process. |
| `tempo.config.setting` | `tempo_config_setting` | Gauge | `{value}` | `setting` | Safe numeric configuration: `engine.*` (concurrency, poll interval, lease, heartbeat timeout, attempts, queue enabled, server-local execution), `external.max_processes_*`, `request_history.*`, `run_logs.*`, `telemetry.sampling_ratio`, `worker.*`. No secrets. |

### Watson (HTTP) and runtime

Watson emits `http_server_request_duration_seconds` (labels `http_request_method`, `http_route` as the route template, `http_response_status_code`), `http_server_active_requests`, the body-size histograms, and `watson_*` server metrics. The up gauge is exported as `watson_server_up_ratio`. The telemetry host applies seconds buckets (1 ms to 30 s) to `http.server.request.duration`, because the SDK's defaults are millisecond-shaped.

Runtime instrumentation adds `dotnet_*` (GC, JIT, thread pool, exceptions, working set, CPU) and `process_*` series for every process.

## Spans catalog

All Tempo spans are on the `Tempo` activity source. Span status is set explicitly (`Ok` or `Error`). Failures set `error.type`, and exceptions are recorded as OpenTelemetry `exception` events (type and message only).

| Span name | Kind | Emitted by | Parent | Key attributes |
|---|---|---|---|---|
| `{METHOD} {route}` | Server | Watson | inbound `traceparent` | `http.*`, `url.*` (Watson) |
| `tempo.dispatch.enqueue` | Producer | server | Watson request span | `tempo.tenant.id`, `tempo.dataflow.id`, `tempo.flow_run.id`, `source` |
| `tempo.dispatch.schedule` | Internal | server scheduler | the run's enqueue span (in-memory hand-off) or a new root | `tempo.flow_run.id`, `node_kind`, `tempo.worker.id`, `tempo.assignment.id` |
| `stage:plan`, `stage:select` | Internal | server | schedule | `pipeline`, `stage`. Emitted retroactively only for terminal scheduling decisions, so a saturated queue does not emit a span per poll. |
| `stage:assign` | Internal | server | schedule | `tempo.assignment.id`, `tempo.assignment.attempt` |
| `stage:execute` / `stage:handoff` | Internal | server | schedule | `tempo.assignment.id`, `node_kind` |
| `tempo.worker.assign send` | Producer | server | handoff stage | `tempo.assignment.id`, `tempo.worker.id`, `tempo.worker.session_id` |
| `tempo.worker.assignment` | Consumer | worker | **assign send span, across the websocket** | `tempo.assignment.id`, `tempo.flow_run.id`, `state` |
| `tempo.dispatch.complete` | Consumer | server | **worker assignment span** (remote) or the execute stage (local) | `tempo.flow_run.id`, `node_kind`, `state` |
| `tempo.worker.register` | Internal | server | worker websocket upgrade span | `tempo.worker.id`, `tempo.worker.session_id` |
| `tempo.flow.run` | Internal | engine and registry runners | execute stage or worker assignment | `runner`, `tempo.tenant.id`, `tempo.dataflow.id`, `tempo.flow_run.id` |
| `stage:resolve`, `stage:validate`, `stage:prepare` | Internal | registry runner | flow | `tempo.step.id` |
| `step:{stepId}` | Internal | every step runner | flow | `tempo.step.id`, `runner`, `tempo.step_run.id`, `tempo.step.result` |
| `http {METHOD}` | Client | REST steps | step | `http.request.method`, `server.address`, `server.port`, `http.response.status_code` (never the URL) |
| `process {runner}` | Client | artifact process runners | step | `process.exit.code` |
| `process dotnet_publish` | Client | C# source-step packaging | Watson request | `process.exit.code` |
| `tempo.capacity.acquire` | Internal | capacity manager | step | `tempo.step_run.id`, `tempo.capacity.tenant_queued`, `tempo.capacity.server_queued` |
| `artifact_package hit` / `artifact_package fill` | Internal | artifact runtime plan | prepare stage | `cache`, `outcome` |
| `python_env fill` | Internal | Python environment cache | prepare stage | `cache` |
| `tempo-server artifact_download` | Client | worker | prepare stage | `http.response.status_code` (the lease token in the URL is never recorded) |
| `{db} {operation}` (for example `sqlite select`) | Client | all database drivers | any Tempo span; **never a root** | `db.system.name`, `db.operation.name` (no SQL text) |
| `mcp.tool {tool}` | Server | MCP server | Watson span (HTTP transport) or root (TCP and WebSocket) | `tool` |
| `tempo-server {METHOD}` | Client | MCP API client | MCP tool span | `http.request.method`, `server.address`, `http.response.status_code` |
| `task:{task}` | Internal (root) | server maintenance loops and startup | none | `task` |

## Context propagation

One trace covers a run from the HTTP request that enqueued it to the outbound call that failed, across processes:

| Boundary | Carrier |
|---|---|
| Client to server or MCP | W3C `traceparent` header, adopted by Watson (`PropagateContext = true`) |
| Enqueue to scheduler loop | in-memory map of run id to traceparent, bounded at 10,000 pending runs; lost on server restart (scheduling then starts a new root) |
| Server to worker (websocket `assign` frame) | `traceParent` / `traceState` fields on `WorkerAssignMessage`, omitted when not tracing |
| Worker to server (websocket `run-completed` frame) | `traceParent` / `traceState` fields on `RunCompletionReport` |
| Tempo to REST targets, MCP to server, worker to server | `traceparent` header injected by `HttpClient` |
| Tempo to artifact subprocesses | `TRACEPARENT` / `TRACESTATE` environment variables (OpenTelemetry environment-carrier convention) |

The new frame fields are optional and additive. Workers and servers that predate them ignore them.

## Logs

`Tempo.Server` and `Tempo.Worker` run background work (scheduling, leases, heartbeats, maintenance), so their existing log lines are also exported as OpenTelemetry logs. The host bridges `SyslogLogging`'s `MessageLogged` event, which fires on the logging thread, so each record carries the active `trace_id` and `span_id`. Grafana links a Loki line to its trace and a trace to its lines.

- The server attaches the bridge **after** start-up hydration, so the default-credential line printed on first run never leaves the host's console and log file.
- Records at `information` and above are exported by default. Debug-level request lines include query strings. Keep `logsMinimumSeverity` at 2 or higher in shared deployments.
- `Tempo.McpServer` does not export logs.

## Coverage map

| Area | Item (path) | Coverage |
|---|---|---|
| Library API | `DataFlowRunner` (`src/Tempo/Runners/DataFlowRunner.cs`) | `tempo.flow.run` span; flow runs, duration, active; transitions |
| Library API | `StepRunner.Execute` (`src/Tempo/Runners/StepRunner.cs`), base of every runner | `step:*` span; step executions and duration; error, exception, and timeout paths |
| Outbound | `RestStepRunner` (`src/Tempo/Runners/RestStepRunner.cs`) | `http {METHOD}` client span; integration metrics; traceparent |
| Service API | Watson HTTP routes (`src/Tempo.Server/Routes/*`) | Watson metrics and server span; `tempo.auth.attempts` (`TempoServer.AuthenticateRequestAsync`); `tempo.authz.decisions` (`AuthorizationService`); `tempo.errors{component=http}` |
| Service API | Worker websocket (`src/Tempo.Server/Routes/WorkerRoutes.cs`) | sessions, frames, worker-token auth, protocol errors |
| Service API | MCP tools over HTTP, TCP, and WebSocket (`src/Tempo.McpServer/Tools/TempoToolRegistrar.cs`) | `mcp.tool *` span; tool calls and duration |
| Outbound | MCP API client (`src/Tempo.McpServer/Services/TempoApiClient.cs`) | client span; integration metrics |
| Outbound | Database drivers, all four (`src/Tempo.Core/Database/*/…DatabaseDriver.cs`, `DatabaseTelemetry.cs`) | `{db} {op}` spans; integration metrics; SQLite write-lock waits |
| Outbound | Artifact subprocesses (`src/Tempo.Core/Runtime/ArtifactProcessStepRunner.cs`) | `process *` span with exit code; integration metrics; kills; `TRACEPARENT` |
| Outbound | `dotnet publish` for C# source steps (`src/Tempo.Core/Services/SourceStepPackageService.cs`) | `process dotnet_publish` span; integration metrics |
| Outbound | Worker artifact download (`src/Tempo.Worker/RemoteArtifactBlobStore.cs`) | client span; integration metrics |
| Pipeline | Dispatch (`src/Tempo.Server/Services/RunDispatchCoordinator.cs`, `RemoteWorkerRunExecutor.cs`) | enqueue, schedule, and stage spans; enqueue, decisions, completions, and recoveries; per-stage histograms including `queued`; queue depth; last success |
| Pipeline | Registry flow execution (`src/Tempo.Core/Runtime/RegistryDataFlowRunner.cs`) | `resolve`, `validate`, and `prepare` stage spans and metrics |
| Worker | Worker daemon (`src/Tempo.Worker/WorkerNode.cs`) | assignment consumer span; assignment outcomes; worker execute stage; sessions, frames, errors |
| Pool, limiter | External-runtime capacity (`src/Tempo.Core/Runtime/ExternalRuntimeCapacityManager.cs`) | in use, queued, wait, acquire span |
| Limiter | Coordinator gate; SQLite write lock | `tempo.limiter.wait.duration` |
| Pool | Executor pool (local pseudo-worker and remote workers) | connected, capacity, and in-use gauges by node kind |
| Cache | Artifact package cache (`ArtifactRuntimePlan.cs`); Python venv cache (`PythonEnvironmentCache.cs`) | hit, miss, and error; fill time; spans |
| Queue | Pending flow runs; fire-and-forget request-history capture | queue-depth gauge; `tempo.task.pending` |
| Background | Request-history prune, artifact GC, and run-log prune loops (`src/Tempo.Server/TempoServer.cs`) | `task:*` root spans; runs, duration, items, last success |
| Lifecycle | Start-up (`src/Tempo.Server/Bootstrapper.cs`): database init, hydration, migrations, reconciliation | `task:*` spans and task metrics |
| Lifecycle, config | All three hosts | `tempo.build.info`, `tempo.config.setting`, runtime metrics, `service.name` and instance |

## The observability stack

`docker compose -f docker/compose.yaml up -d` starts Tempo plus:

| Service | Image | Host port | Role |
|---|---|---|---|
| `otel-collector` | `otel/opentelemetry-collector-contrib:0.109.0` | 4317, 4318 | Receives OTLP from every Tempo process. Metrics go to its Prometheus exporter (`:8889`), traces to Grafana Tempo, logs to Loki. |
| `prometheus` | `prom/prometheus:v3.5.4` | 9090 | Scrapes the collector with `honor_labels`, so `job` is the service name. |
| `grafana-tempo` | `grafana/tempo:2.6.1` | 3200 | Trace storage (24 h retention). |
| `loki` | `grafana/loki:3.2.1` | 3100 | Log storage (native OTLP). |
| `grafana` | `grafana/grafana-oss:13.0.2` | **3001** | Datasources (UIDs `prometheus`, `tempo`, `loki`) and the `Tempo` dashboard folder. Port 3000 belongs to the Tempo dashboard. |

Every Tempo service gets `TEMPO_TELEMETRY_OTLP_ENDPOINT=http://otel-collector:4317` and a service name. Startup is ordered with `depends_on`: Grafana Tempo and Loki (healthy), then the collector, then the Tempo services, and Grafana waits on Prometheus, Grafana Tempo, and Loki being healthy. Configuration lives in `docker/otel-collector.yaml`, `docker/prometheus.yaml`, `docker/grafana-tempo.yaml`, and `docker/grafana/provisioning/`. Dashboard JSON is in `assets/grafana/`.

Grafana's admin login defaults to `admin` / `admin` for local development only. Set `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` in the environment for anything shared, and do not publish Prometheus, Loki, Grafana Tempo, the collector, or an in-process `/metrics` port on a public interface. The Tempo dashboard's home page carries an **External services** card with these URLs and credentials.

To export somewhere else (Grafana Cloud, Honeycomb, Datadog), point `TEMPO_TELEMETRY_OTLP_ENDPOINT` at that collector or gateway. No code changes are needed.

## Dashboards

All dashboards are in the Grafana folder **Tempo**, share a `Service` (`job`) variable, and link to each other.

| Dashboard | UID | Answers |
|---|---|---|
| Tempo / Overview | `tempo-overview` | Is the server up? Are workers connected? Is work flowing and succeeding? HTTP 5xx share, error breakdown, running processes. Start here. |
| Tempo / HTTP | `tempo-http` | Which route is slow or failing (rate, status class, p50/p95/p99, top routes by p95)? Authentication and authorization outcomes, connections, websocket sessions. |
| Tempo / Dispatch Pipeline | `tempo-dispatch` | Where is a run stuck: queued, plan, assign, execute, handoff, or complete? Scheduling decisions, completions, recoveries, queue depth, gate waits. |
| Tempo / Flows & Steps | `tempo-flows` | Which runner or step type is slow or failing? Error-branch transitions, flows in flight, per-step resolve, validate, and prepare time. |
| Tempo / Workers & Capacity | `tempo-workers` | Fleet size and utilization, session drops, rejected assignments, protocol frames, process slots, capacity waits, kills. |
| Tempo / Integrations | `tempo-integrations` | Is a dependency the cause? Calls, failures, and p95 by service and operation; database by operation; cache hit ratio and fill time; MCP tools. |
| Tempo / Background Tasks & Runtime | `tempo-runtime` | Are maintenance tasks running and succeeding? Handled errors, safe configuration, build info, memory, CPU, GC, thread pool. |

From any panel, use Grafana Explore on the `Traces` datasource (for example `{ name = "tempo.dispatch.schedule" && status = error }`) to jump to a representative trace, then to its logs.

## Recommended alerts (PromQL)

| Alert | Expression | For |
|---|---|---|
| Server down | `max(watson_server_up_ratio{job="tempo-server"}) < 1 or absent(watson_server_up_ratio{job="tempo-server"})` | 2m |
| No workers (when the server cannot run work itself) | `sum(tempo_workers_connected{node_kind="worker"}) == 0 and max(tempo_config_setting{setting="engine.server_can_execute_workload"}) == 0` | 5m |
| Queue backing up | `sum(tempo_dispatch_queue_depth) > 100` | 10m |
| Runs waiting too long | `histogram_quantile(0.95, sum by (le) (rate(tempo_pipeline_stage_duration_seconds_bucket{pipeline="dispatch",stage="queued"}[10m]))) > 60` | 10m |
| Flow failure ratio high | `sum(rate(tempo_flow_runs_total{outcome!="success"}[15m])) / sum(rate(tempo_flow_runs_total[15m])) > 0.2` | 15m |
| Runs failing to schedule | `sum(rate(tempo_dispatch_assignments_total{outcome=~"no_eligible_worker\|plan_failed\|assign_failed\|send_failed"}[10m])) > 0` | 10m |
| Assignments being recovered | `sum(increase(tempo_dispatch_recoveries_total[15m])) > 5` | 0m |
| Worker sessions flapping | `sum(increase(tempo_worker_sessions_total{job="tempo-server",event=~"heartbeat_timeout\|disconnected"}[15m])) > 5` | 0m |
| Dependency failing | `sum by (service) (rate(tempo_integration_requests_total{outcome=~"exception\|timeout"}[5m])) / sum by (service) (rate(tempo_integration_requests_total[5m])) > 0.1` | 10m |
| Database slow | `histogram_quantile(0.95, sum by (le, service) (rate(tempo_integration_duration_seconds_bucket{service=~"sqlite\|postgresql\|mysql\|sqlserver"}[5m]))) > 0.5` | 10m |
| Process capacity saturated | `sum(tempo_capacity_queued) > 0 and histogram_quantile(0.95, sum by (le) (rate(tempo_capacity_wait_duration_seconds_bucket[10m]))) > 30` | 10m |
| HTTP 5xx | `sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])) / sum(rate(http_server_request_duration_seconds_count[5m])) > 0.05` | 5m |
| Maintenance stalled | `time() - max by (task) (tempo_last_success_timestamp_seconds{task=~"artifact_gc\|run_log_prune\|request_history_prune"}) > 3 * 3600` | 0m |
| Nothing succeeding | `time() - max(tempo_last_success_timestamp_seconds{task="dispatch"}) > 3600 and sum(increase(tempo_dispatch_enqueued_total{outcome="accepted"}[1h])) > 0` | 0m |

Tune the thresholds to your traffic. The maintenance-stalled window assumes the default prune and GC intervals.

## Cardinality, privacy, and cost

- **Bounded labels only.** Metric labels are fixed vocabularies (outcomes, stages, runner and service names, frame types, task names) or values bounded by configuration (tool names, setting names). Tenant, flow, run, step, assignment, and worker identifiers, URLs, and messages go on spans only. A test (`MetricLabelsStayBounded`) fails the build if a metric label carries an identifier, free text, or an undocumented key.
- **No secrets or payloads.** SQL text, request and response bodies, step input and output, URLs (REST targets may embed tokens; artifact downloads carry a lease token), header values, and credentials are never recorded. Exception events carry type and message only.
- **Cost.** Unobserved instruments cost single-digit nanoseconds. Database spans exist only inside an existing trace, so background polling and heartbeats produce metrics but no orphan traces. Scheduling spans are emitted only for terminal decisions. Worker heartbeat handling is detached from the long-lived websocket request span. Reduce trace volume with `samplingRatio`.

## Known limitations

- **OTLP over HTTP:** with `otlpProtocol = httpprotobuf`, the exporter posts every signal to the configured endpoint verbatim. It does not append `/v1/traces`, `/v1/metrics`, or `/v1/logs`. Use the default `grpc` against a collector, or point HTTP export at a gateway that accepts all signals at one path.
- **Collector health:** the collector image has no shell or HTTP client, so compose cannot healthcheck it. Dependents wait for `service_started`, and its `health_check` extension listens on `:13133` inside the network.
- **Scheduler hand-off:** the enqueue-to-schedule trace link is in memory. A server restart between enqueue and assignment starts a fresh trace for that run, though metrics are unaffected.
- **Artifact download timing** covers the time to response headers. The streamed body read is part of the `artifact_package fill` cache span.

## Testing telemetry

`src/Test.Shared/Suites/TelemetrySuite.cs` runs through the Touchstone CLI (`src/Test.Automated`), xUnit, and NUnit. It uses plain BCL `MeterListener` and `ActivityListener` capture (`TelemetryCapture`) plus a stand-in OTLP endpoint (`OtlpRecordingEndpoint`), and covers:

- no-listener safety and null inputs
- engine flows and steps, plus error, exception, and timeout paths
- REST client spans and `traceparent`, including unreachable targets
- database spans without SQL text, failures, and limiter waits
- capacity waits, including cancellation, with balanced gauges
- artifact processes, including exit code, cache, and the child receiving `TRACEPARENT`
- the server-local dispatch pipeline as one trace with every stage
- a remote worker joining the trace across the websocket in both directions
- rejected enqueues
- MCP tool calls joining the server's Watson span, plus failures
- authentication and authorization outcomes
- background-task runs and last-success gauges
- build-info and config gauges
- telemetry-host export (Prometheus scrape, plus OTLP traces and bridged, trace-correlated logs)
- a disabled or failing host staying inert
- settings validation and environment overrides
- bounded metric labels
