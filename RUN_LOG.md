# Run Log Plan

Status: Proposed
Owner:
Last Updated: 2026-04-22

## Purpose

Implement durable, file-backed run logs for Tempo flow executions so operators and developers can:

- inspect detailed logs for a specific flow run across container restarts
- correlate logs to the run, assignment attempt, worker, and step
- view execution history and detailed logs from the `Runs` experience in the dashboard
- access the same surface through REST, OpenAPI, MCP, Postman, and documented SDK patterns
- retain logs for a bounded period, with a default retention of 7 days

This document is an execution handoff. A developer should be able to pick it up, annotate progress inline, and ship the feature without re-discovering the runtime constraints.

## Decision Summary

- Detailed run-log bodies should be file-backed, not stored in database tables.
- Existing database tables should remain the system of record for run and assignment history:
  - `flow_runs`
  - `run_assignments`
  - `worker_activity`
- v1 should not add a `run_log_contents` or equivalent blob table.
- `stdout` must remain reserved for the protocol `StepResult` for external runtimes. User logs cannot go to `stdout`.
- Run logs should live under a deterministic per-run directory keyed by `flowRunId`, with attempt- and step-specific files underneath it.
- The server should own retention and cleanup. Default retention should be 7 days.
- Run-log viewing should be tenant-scoped like existing run APIs, not admin-only like the global server/worker log viewer.
- The existing global `Logs` page should remain focused on server and worker service logs. Run logs should be surfaced from `Runs`, not jammed into the global source picker.

## Progress Tracking

- [ ] Phase 0: Confirm final design and naming
- [ ] Phase 1: Add shared run-log storage and settings
- [ ] Phase 2: Emit run logs from worker, server-local execution, and SDK/runtime shims
- [ ] Phase 3: Add server-side run history and run-log APIs
- [ ] Phase 4: Add dashboard run activity and run-log UX
- [ ] Phase 5: Add MCP, Postman, docs, and SDK documentation
- [ ] Phase 6: Add backend, dashboard, and SDK test coverage
- [ ] Phase 7: Final validation in local and Docker deployments

## Scope

### In Scope

- per-run log files for Tempo flow executions
- correlation to run, assignment, worker, step, and attempt
- file-backed retention and deletion
- dashboard run history and run-log viewer
- tenant-scoped REST/OpenAPI routes for run activity and run logs
- MCP tools for run-log discovery and reads
- Postman coverage
- SDK updates and docs for C#, JavaScript, and Python artifact handlers
- Docker, factory, and reset asset updates for persistent run-log storage
- strong targeted coverage for the new slice

### Out of Scope for v1

- storing full run-log bodies in the database
- live streaming over SSE/WebSocket
- cross-node centralized aggregation beyond shared storage
- full-text indexing/search across all run logs
- role modeling beyond existing tenant-scoped run authorization
- non-file-backed storage backends

## Current Repo Context

Relevant existing files and patterns:

- Run models and persisted summary state:
  - [src/Tempo.Core/Models/FlowRun.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Models/FlowRun.cs)
  - [src/Tempo.Core/Models/RunAssignmentRecord.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Models/RunAssignmentRecord.cs)
  - [src/Tempo.Core/Models/WorkerActivityRecord.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Models/WorkerActivityRecord.cs)
- Existing run persistence and activity writes:
  - [src/Tempo.Core/Database/Common/Implementations/FlowRunMethods.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Database/Common/Implementations/FlowRunMethods.cs)
  - [src/Tempo.Server/Services/RunAssignmentStore.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Services/RunAssignmentStore.cs)
  - [src/Tempo.Server/Services/RunDispatchCoordinator.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Services/RunDispatchCoordinator.cs)
- Run routes and dashboard surface:
  - [src/Tempo.Server/Routes/FlowRunRoutes.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Routes/FlowRunRoutes.cs)
  - [dashboard/src/views/RunsView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/RunsView.jsx)
- Existing service-log viewer implementation:
  - [src/Tempo.Server/Services/LogFileService.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Services/LogFileService.cs)
  - [dashboard/src/views/LogsView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/LogsView.jsx)
- Local and remote execution entry points:
  - [src/Tempo.Server/Services/LocalServerRunExecutor.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Services/LocalServerRunExecutor.cs)
  - [src/Tempo.Worker/WorkerNode.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerNode.cs)
  - [src/Tempo.Core/Runtime/RegistryDataFlowRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/RegistryDataFlowRunner.cs)
  - [src/Tempo.Core/Runtime/ArtifactProcessStepRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/ArtifactProcessStepRunner.cs)
  - [src/Tempo.Core/Runtime/ArtifactPythonStepRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/ArtifactPythonStepRunner.cs)
  - [src/Tempo.Core/Runtime/ArtifactJavaScriptStepRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/ArtifactJavaScriptStepRunner.cs)
- SDKs that need a logging surface:
  - [sdk/csharp/Tempo.Sdk/ITempoStepHandler.cs](/abs/path/C:/Code/Tempo/Tempo/sdk/csharp/Tempo.Sdk/ITempoStepHandler.cs)
  - [sdk/csharp/Tempo.Sdk/TempoStepHost.cs](/abs/path/C:/Code/Tempo/Tempo/sdk/csharp/Tempo.Sdk/TempoStepHost.cs)
  - [sdk/js/src/index.js](/abs/path/C:/Code/Tempo/Tempo/sdk/js/src/index.js)
  - [sdk/python/tempo_sdk/__init__.py](/abs/path/C:/Code/Tempo/Tempo/sdk/python/tempo_sdk/__init__.py)
- Settings and deployment:
  - [src/Tempo.Core/Settings/Settings.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Settings/Settings.cs)
  - [src/Tempo.Worker/WorkerSettings.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerSettings.cs)
  - [src/Tempo.Worker/WorkerSettingsLoader.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerSettingsLoader.cs)
  - [docker/compose.yaml](/abs/path/C:/Code/Tempo/Tempo/docker/compose.yaml)
  - [docker/factory/reset.bat](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.bat)
  - [docker/factory/reset.sh](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.sh)
- Docs and machine-readable surfaces:
  - [src/Tempo.McpServer/Tools/TempoToolRegistrar.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.McpServer/Tools/TempoToolRegistrar.cs)
  - [docs/REST_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/REST_API.md)
  - [docs/MCP_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/MCP_API.md)
  - [docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md](/abs/path/C:/Code/Tempo/Tempo/docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md)
  - [docs/EXTERNAL_EXECUTION_OPERATOR_GUIDE.md](/abs/path/C:/Code/Tempo/Tempo/docs/EXTERNAL_EXECUTION_OPERATOR_GUIDE.md)
  - [docs/WORKER_PROTOCOL.md](/abs/path/C:/Code/Tempo/Tempo/docs/WORKER_PROTOCOL.md)
  - [sdk/csharp/README.md](/abs/path/C:/Code/Tempo/Tempo/sdk/csharp/README.md)
  - [sdk/js/README.md](/abs/path/C:/Code/Tempo/Tempo/sdk/js/README.md)
  - [sdk/python/README.md](/abs/path/C:/Code/Tempo/Tempo/sdk/python/README.md)
  - [README.md](/abs/path/C:/Code/Tempo/Tempo/README.md)
  - [Tempo.postman_collection.json](/abs/path/C:/Code/Tempo/Tempo/Tempo.postman_collection.json)

## Recommended Design

### 1. Storage Model

Use a dedicated shared run-log root, separate from the existing service-log roots.

Recommended conceptual layout:

```text
<run-log-root>/
  run_abcd1234/
    manifest.json
    run.log
    attempt-001-ras_xyz/
      worker.log
      host.log
      step-001-sru_aaa-my.step.log
      step-001-sru_aaa-my.step.stderr.log
      step-002-sru_bbb-other.step.log
```

Rules:

- the top-level directory name must include the `flowRunId`
- retries and recovery must create a separate attempt directory keyed by `attemptNumber` and `runAssignmentId`
- each step should have a dedicated primary log file
- host/runtime failures should go to `host.log`
- `manifest.json` should provide a cheap index for the UI and APIs:
  - run id
  - tenant id
  - flow id
  - run assignment ids
  - worker ids
  - file list with step ids, step run ids, and kinds

Rationale:

- deterministic paths make it easy to locate logs by run id
- separate attempt directories handle retries, lease recovery, and worker changes cleanly
- separate step files avoid interleaving unrelated output and make troubleshooting specific steps easier

### 2. Database Model

Do not store run-log content in database tables in v1.

Use the existing tables as the source of truth for summary/history:

- `flow_runs`
  - run state
  - source IP
  - timestamps
  - assigned worker
  - overall output/error
- `run_assignments`
  - assignment attempts
  - worker placement
  - attempt timestamps
- `worker_activity`
  - append-only worker-side and coordinator-side events

Database work still required:

- add server-side query methods for `run_assignments` by `flow_run_id`
- add server-side query methods for `worker_activity` by `flow_run_id`
- expand `worker_activity` writes so the timeline is actually useful:
  - assigned
  - assignment accepted
  - assignment rejected
  - execution started
  - execution completed
  - timeout
  - disconnect recovery
  - orphan completion

Decision:

- no schema migration is required for v1 run-log bodies if the file layout stays deterministic
- if future scale proves that filesystem enumeration is too expensive, a later `run_log_index` table can be added, but that is not part of this plan

### 3. Settings Model

Add a dedicated shared settings section for run logs, for both server and worker consumption.

Recommended shape:

```json
"runLogs": {
  "enabled": true,
  "rootPath": "./run-logs",
  "retentionDays": 7,
  "pruneIntervalMinutes": 60,
  "defaultTailLines": 400,
  "defaultMaxBytes": 262144,
  "maxTailLines": 5000,
  "maxReadBytes": 1048576
}
```

Recommended environment variables:

- `TEMPO_RUN_LOG_ENABLED`
- `TEMPO_RUN_LOG_ROOT`
- `TEMPO_RUN_LOG_RETENTION_DAYS`
- `TEMPO_RUN_LOG_PRUNE_INTERVAL_MINUTES`

Notes:

- the server needs full read/write access to the run-log root
- workers need write access
- the same root must be visible to all workers and the server in Docker deployments

### 4. Runtime and SDK Emission Model

This is the most important design point.

External runtime protocol today is strict:

- stdin carries the request JSON
- stdout must contain only the final `StepResult` JSON

Therefore:

- user logs must never be emitted to stdout
- runtime shims and SDKs must write logs to files or stderr, not stdout

Recommended runtime responsibilities:

- `RegistryDataFlowRunner`
  - create a run-log scope for the flow run
  - emit step start/complete boundaries
  - emit step result and runtime summaries
- `LocalServerRunExecutor`
  - emit assignment-level summary events into `run.log`
- `WorkerNode`
  - emit assignment accepted/started/completed events into the run directory
  - continue writing coarse service logs to the existing worker service log
- `ArtifactProcessStepRunner`
  - create step-specific paths
  - capture child `stderr` to a dedicated file
  - write host protocol/timeout/exit-code messages to `host.log`
  - pass log-path environment variables into the child process
- `ArtifactPythonStepRunner`
  - generated shim should redirect `print(...)` and standard `logging` output to the step log file
- `ArtifactJavaScriptStepRunner`
  - generated shim should redirect `console.*` and `process.stderr` to the step log file
- `Artifact.DotnetProcess` / C# SDK
  - `TempoStepHost.RunAsync` should install a file-backed logger before invoking the handler

Recommended child-process environment variables:

- `TEMPO_RUN_LOG_DIR`
- `TEMPO_RUN_LOG_FILE`
- `TEMPO_RUN_LOG_KIND`
- `TEMPO_FLOW_RUN_ID`
- `TEMPO_RUN_ASSIGNMENT_ID`
- `TEMPO_STEP_RUN_ID`
- `TEMPO_STEP_ID`
- `TEMPO_WORKER_ID`

### 5. SDK Surface

The SDK surface should be additive, not a breaking rewrite.

Recommended C# design:

- add `ITempoStepLogger`
- add `TempoExecutionContext.Current` or equivalent `AsyncLocal` ambient context
- expose `TempoExecutionContext.Current.Logger`
- add an optional convenience base class or helper, but do not require inheritance
- keep `ITempoStepHandler.RunAsync(StepRequest request, CancellationToken token)` working

Recommended JavaScript design:

- export a logger helper from `sdk/js`
- make `TempoStepHost.run` install a current logger for the duration of the handler
- route `console.log`, `console.warn`, `console.error`, and friends to the file-backed sink

Recommended Python design:

- expose a logger helper from `tempo_sdk`
- make `TempoStepHost.run` install the current logger
- route `print` and root `logging` handlers to the file-backed sink

Recommended behavior for all SDKs:

- timestamp each line
- include severity
- include `flowRunId`, `stepRunId`, and `stepId` when available
- flush on each write to reduce debugging surprises during running executions

### 6. API Model

Run history and run logs should be tenant-scoped, because the content is tied to tenant flow executions.

Recommended routes:

- `GET /v1.0/tenants/{tenantId}/runs/{id}/activity`
  - returns:
    - the run summary
    - assignment attempts from `run_assignments`
    - activity events from `worker_activity`
- `GET /v1.0/tenants/{tenantId}/runs/{id}/logs`
  - lists log files visible for one run
- `GET /v1.0/tenants/{tenantId}/runs/{id}/logs/content?path=&tailLines=&maxBytes=`
  - reads a bounded tail from one run-log file
- `GET /v1.0/tenants/{tenantId}/runs/{id}/logs/download?path=`
  - downloads the complete log file
- `DELETE /v1.0/tenants/{tenantId}/runs/{id}/logs/content?path=`
  - deletes one archived run-log file
- `DELETE /v1.0/tenants/{tenantId}/runs/{id}/logs`
  - optional bulk purge for a completed run's log directory

Recommended response fields for `GET /logs`:

- `path`
- `fileName`
- `kind`
- `attemptNumber`
- `runAssignmentId`
- `workerId`
- `stepId`
- `stepRunId`
- `byteLength`
- `lastModifiedUtc`
- `active`
- `deleteAllowed`

Delete policy:

- reject delete of active files for a currently running run
- allow deletion only when the run is terminal, or when the file is clearly archived and not current

### 7. Dashboard Model

Keep the existing global `Logs` page for service logs.

Add run-specific UX to `Runs`:

- row action: `View logs`
- run modal or drawer sections:
  - `Summary`
  - `Activity`
  - `Logs`

Recommended UX behavior:

- `Activity` shows:
  - flow
  - source IP
  - queued/assigned/started/completed times
  - worker id
  - assignment attempts
  - total runtime
  - success/failure/exception/cancelled
  - worker activity timeline
- `Logs` shows:
  - file list for the selected run
  - bounded viewer pane
  - download
  - delete when allowed
  - deep links to a specific log file for a run

Implementation preference:

- factor the viewer pane in `LogsView` into a reusable component
- avoid making operators browse the global service-log page just to find a single run

### 8. Retention and Cleanup

Retention should be server-owned and automatic.

Recommended behavior:

- default retention: 7 days
- background prune loop on server startup
- prune completed-run directories older than the configured retention cutoff
- delete run-log directories when `docker/factory/reset.*` is executed
- optionally delete a completed run's log directory when the run is explicitly deleted

Retention should use database state where possible:

- use `flow_runs.completed_utc` to decide whether a run is safely pruneable
- do not prune directories for runs that are still `Queued` or `Running`

## Phase Plan

## Phase 0: Confirm Final Design and Naming

Goal: lock the v1 choices before implementation spreads across runtime, dashboard, and SDKs.

Checklist:

- [ ] Confirm the dedicated `runLogs` settings section and env var names
- [ ] Confirm the per-run directory layout and file naming
- [ ] Confirm tenant-scoped authorization model for run-log reads
- [ ] Confirm delete policy for active vs terminal runs
- [ ] Confirm retention default of 7 days
- [ ] Confirm whether bulk purge route ships in v1

Developer notes:

-

## Phase 1: Add Shared Run-Log Storage and Settings

Goal: create stable storage that survives container restarts and is visible to both workers and server.

Checklist:

- [ ] Add `RunLogSettings` to shared settings
- [ ] Load worker env overrides for the run-log root
- [ ] Add a shared named Docker volume for run logs
- [ ] Mount the run-log volume into server and workers
- [ ] Update factory/reset scripts to clear the run-log storage
- [ ] Update factory/operator docs to describe the new layout

Expected file touches:

- [ ] [src/Tempo.Core/Settings/Settings.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Settings/Settings.cs)
- [ ] new shared settings model file under `src/Tempo.Core/Settings`
- [ ] [src/Tempo.Worker/WorkerSettings.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerSettings.cs)
- [ ] [src/Tempo.Worker/WorkerSettingsLoader.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerSettingsLoader.cs)
- [ ] [docker/compose.yaml](/abs/path/C:/Code/Tempo/Tempo/docker/compose.yaml)
- [ ] [docker/factory/reset.bat](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.bat)
- [ ] [docker/factory/reset.sh](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.sh)
- [ ] [docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md](/abs/path/C:/Code/Tempo/Tempo/docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md)

Acceptance criteria:

- Docker restart preserves run-log files
- scaled workers write into the same shared root without path collisions
- the server can read and prune the same files without container execs

Developer notes:

-

## Phase 2: Emit Run Logs from Worker, Server, and SDK/Runtime Shims

Goal: make actual run logs exist, with deterministic correlation to runs and steps.

Checklist:

- [ ] Add a shared run-log writer/service in core runtime code
- [ ] Emit run boundary events from `RegistryDataFlowRunner`
- [ ] Emit assignment boundary events from `LocalServerRunExecutor`
- [ ] Emit assignment boundary events from `WorkerNode`
- [ ] Add step file creation and host/stderr capture to `ArtifactProcessStepRunner`
- [ ] Update the generated Python shim to redirect `print` and `logging`
- [ ] Update the generated JavaScript shim to redirect `console.*`
- [ ] Add C# SDK file-backed logger support
- [ ] Add JS SDK logger support
- [ ] Add Python SDK logger support
- [ ] Pass run-log environment variables into child processes
- [ ] Ensure `stdout` remains valid protocol JSON

Suggested implementation files:

- [ ] new run-log service under `src/Tempo.Core/Runtime` or `src/Tempo.Server/Services`
- [ ] [src/Tempo.Core/Runtime/RegistryDataFlowRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/RegistryDataFlowRunner.cs)
- [ ] [src/Tempo.Server/Services/LocalServerRunExecutor.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Services/LocalServerRunExecutor.cs)
- [ ] [src/Tempo.Worker/WorkerNode.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerNode.cs)
- [ ] [src/Tempo.Core/Runtime/ArtifactProcessStepRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/ArtifactProcessStepRunner.cs)
- [ ] [src/Tempo.Core/Runtime/ArtifactPythonStepRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/ArtifactPythonStepRunner.cs)
- [ ] [src/Tempo.Core/Runtime/ArtifactJavaScriptStepRunner.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Runtime/ArtifactJavaScriptStepRunner.cs)
- [ ] [sdk/csharp/Tempo.Sdk/TempoStepHost.cs](/abs/path/C:/Code/Tempo/Tempo/sdk/csharp/Tempo.Sdk/TempoStepHost.cs)
- [ ] [sdk/js/src/index.js](/abs/path/C:/Code/Tempo/Tempo/sdk/js/src/index.js)
- [ ] [sdk/python/tempo_sdk/__init__.py](/abs/path/C:/Code/Tempo/Tempo/sdk/python/tempo_sdk/__init__.py)

Acceptance criteria:

- a local server-executed run writes run-log files
- a remote worker-executed run writes run-log files
- step logs from C#, JS, and Python are captured without corrupting protocol stdout
- child stderr is preserved in run-log files

Developer notes:

-

## Phase 3: Add Server-Side Run History and Run-Log APIs

Goal: expose the run history and run-log surface through supported REST/OpenAPI routes.

Checklist:

- [ ] Add DAO/query methods for run assignments by run id
- [ ] Add DAO/query methods for worker activity by run id
- [ ] Add a `RunLogService` or equivalent server-side file service
- [ ] Add bounded read and safe delete support for run-log files
- [ ] Add a run-activity route
- [ ] Add run-log list/read/download/delete routes
- [ ] Add OpenAPI schemas and route metadata
- [ ] Add server-side retention/prune loop for run-log directories

Expected file touches:

- [ ] [src/Tempo.Server/Routes/FlowRunRoutes.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Routes/FlowRunRoutes.cs) or new `RunLogRoutes.cs`
- [ ] [src/Tempo.Server/Services/RunAssignmentStore.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Services/RunAssignmentStore.cs)
- [ ] new run-log server service under `src/Tempo.Server/Services`
- [ ] `src/Tempo.Server/Routes/OpenApiSchemaCatalog.cs`
- [ ] any required response DTOs under `src/Tempo.Core/Responses`

Route behavior requirements:

- run-activity route returns:
  - flow-run record
  - assignment attempt records
  - worker activity records
- run-log list route returns only files under the selected run directory
- read route performs bounded reads and rejects traversal
- download route streams one file
- delete route rejects active/current files for active runs

Acceptance criteria:

- OpenAPI includes the new routes
- tenant-scoped auth works like existing run routes
- one run's logs cannot escape into another run's directory
- completed runs can be listed, read, downloaded, and cleaned up safely

Developer notes:

-

## Phase 4: Add Dashboard Run Activity and Run-Log UX

Goal: make run history and run-log access first-class in the dashboard.

Checklist:

- [ ] Add API client methods for run activity and run logs
- [ ] Add a run activity panel or tab to `RunsView`
- [ ] Add a run logs panel or tab to `RunsView`
- [ ] Reuse or extract the existing log viewer UI from `LogsView`
- [ ] Add direct row action entry points from the runs table
- [ ] Add deep linking to a run log file
- [ ] Ensure every new label, column, control, and action has a hover tooltip

Expected file touches:

- [ ] [dashboard/src/utils/api.js](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/utils/api.js)
- [ ] [dashboard/src/views/RunsView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/RunsView.jsx)
- [ ] [dashboard/src/views/LogsView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/LogsView.jsx)
- [ ] new shared viewer component under `dashboard/src/components`
- [ ] any shared CSS in `dashboard/src/App.css`

UX requirements:

- runs should expose activity and logs without leaving the run context
- activity should show assignment attempts and worker selection clearly
- log files should show attempt/step metadata, not only raw file names
- empty and running states should be handled explicitly

Acceptance criteria:

- from the runs table, a user can open a run and see its history and logs
- the viewer supports bounded reads, download, and delete when allowed
- the dashboard does not require use of the global service-log page to inspect run logs

Developer notes:

-

## Phase 5: Add MCP, Postman, Docs, and SDK Documentation

Goal: keep every supported surface aligned with the implementation.

Checklist:

- [ ] Add typed MCP tools for run activity and run-log access
- [ ] Add a Postman folder for run logs
- [ ] Document the REST routes and response models
- [ ] Document the dashboard run activity/log UX
- [ ] Document the shared storage layout and retention behavior
- [ ] Document SDK logging usage for C#, JS, and Python
- [ ] Update worker/external runtime docs with the stdout-vs-log-file rule

Expected file touches:

- [ ] [src/Tempo.McpServer/Tools/TempoToolRegistrar.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.McpServer/Tools/TempoToolRegistrar.cs)
- [ ] [Tempo.postman_collection.json](/abs/path/C:/Code/Tempo/Tempo/Tempo.postman_collection.json)
- [ ] [docs/REST_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/REST_API.md)
- [ ] [docs/MCP_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/MCP_API.md)
- [ ] [docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md](/abs/path/C:/Code/Tempo/Tempo/docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md)
- [ ] [docs/EXTERNAL_EXECUTION_OPERATOR_GUIDE.md](/abs/path/C:/Code/Tempo/Tempo/docs/EXTERNAL_EXECUTION_OPERATOR_GUIDE.md)
- [ ] [docs/WORKER_PROTOCOL.md](/abs/path/C:/Code/Tempo/Tempo/docs/WORKER_PROTOCOL.md)
- [ ] [sdk/csharp/README.md](/abs/path/C:/Code/Tempo/Tempo/sdk/csharp/README.md)
- [ ] [sdk/js/README.md](/abs/path/C:/Code/Tempo/Tempo/sdk/js/README.md)
- [ ] [sdk/python/README.md](/abs/path/C:/Code/Tempo/Tempo/sdk/python/README.md)
- [ ] [README.md](/abs/path/C:/Code/Tempo/Tempo/README.md)

Suggested MCP tool names:

- `run_activity`
- `run_logs_list`
- `run_logs_read`
- `run_logs_download`
- `run_logs_delete`

Acceptance criteria:

- REST, MCP, dashboard, and Postman all describe the same route and file model
- SDK READMEs show how to write logs without using stdout
- operator docs explain where logs live in Docker deployments

Developer notes:

-

## Phase 6: Add Backend, Dashboard, and SDK Coverage

Goal: make the feature defensible and keep regressions out.

Checklist:

- [ ] Add backend tests for run activity queries and routes
- [ ] Add backend tests for run-log list/read/download/delete
- [ ] Add retention/prune tests
- [ ] Add distributed-worker integration tests
- [ ] Add dashboard tests for activity and log panels
- [ ] Add SDK tests for C#, JS, and Python logging helpers
- [ ] Run a focused coverage pass and close the gaps on the touched slice

Suggested backend test targets:

- [ ] run-activity route returns assignments and worker activity
- [ ] local server run produces run-log files
- [ ] remote worker run produces run-log files
- [ ] retry/recovery creates distinct attempt directories
- [ ] bounded reads respect tail and byte limits
- [ ] traversal is rejected
- [ ] delete rejects active files
- [ ] prune removes completed-run directories older than retention
- [ ] Python `print` and `logging` are captured
- [ ] JS `console.log` is captured
- [ ] C# SDK logger writes to the run-log file
- [ ] protocol stdout remains valid after logging changes

Suggested file touches:

- [ ] new `RunLogSuite` in `src/Test.Shared/Suites`
- [ ] [src/Test.Shared/TempoSuites.cs](/abs/path/C:/Code/Tempo/Tempo/src/Test.Shared/TempoSuites.cs)
- [ ] existing distributed/artifact suites as needed
- [ ] dashboard `*.test.jsx` files around `RunsView`
- [ ] SDK test apps under `sdk/csharp`, `sdk/js`, and `sdk/python`

Coverage guidance:

- aim for near-100% coverage on the new run-log slice and every directly touched path
- do not claim repo-wide 100% unless that larger effort is actually completed

Developer notes:

-

## Phase 7: Final Validation

Goal: verify the feature end to end in local and Docker-backed environments.

Checklist:

- [ ] local build passes
- [ ] automated suites pass
- [ ] dashboard build and tests pass
- [ ] SDK test apps pass
- [ ] Docker deployment starts cleanly
- [ ] local server runs emit run logs
- [ ] remote worker runs emit run logs
- [ ] run activity appears in the dashboard
- [ ] run logs appear in the dashboard
- [ ] run-log REST routes work in Postman
- [ ] run-log MCP tools work against a live server
- [ ] retention/prune removes old completed-run directories

Suggested verification commands:

```powershell
dotnet build .\src\Tempo.sln
dotnet run --no-build --project .\src\Test.Automated
npm.cmd --prefix .\dashboard run test
npm.cmd --prefix .\dashboard run build
dotnet run --project .\sdk\csharp\Tempo.Sdk.TestApp\Tempo.Sdk.TestApp.csproj
python .\sdk\python\test_app\test_sdk.py
node .\sdk\js\test-app\test.js
docker compose -f .\docker\compose.yaml up -d
```

Manual checks:

- create a flow that writes logs from step code
- run it locally and through a worker
- open the run in the dashboard
- inspect the activity timeline
- inspect the step log file
- download the log file
- confirm the same run still has logs after a container restart
- confirm logs disappear after factory reset

Developer notes:

-

## Acceptance Summary

The feature is complete when all of the following are true:

- [ ] each flow run produces deterministic file-backed logs correlated to the run id
- [ ] retries and recoveries create distinct attempt-scoped log directories
- [ ] C#, JS, and Python step code can write logs without breaking protocol stdout
- [ ] the server exposes tenant-scoped run-activity and run-log routes
- [ ] the dashboard `Runs` experience shows both activity and logs
- [ ] MCP and Postman support the same run-log surface
- [ ] docs and SDK READMEs describe the feature accurately
- [ ] run logs survive container restarts and are removed by retention/factory reset

## Risks and Hard Parts

### Risk: Protocol corruption from stdout logging

Impact:

- external runtimes return invalid `StepResult` JSON
- runs fail for reasons unrelated to user business logic

Mitigation:

- keep stdout strictly protocol-only
- route logs to files and stderr
- add explicit tests that parse the emitted result after logging

### Risk: Shared-storage assumptions break on scaled workers

Impact:

- workers emit logs the server cannot see
- dashboard shows incomplete or misleading data

Mitigation:

- solve the Docker/shared-root story first
- validate with multiple workers in Compose

### Risk: Generic `Artifact.Process` steps are not SDK-based

Impact:

- custom processes do not automatically adopt the new SDK logger helpers

Mitigation:

- always capture stderr
- always pass log-path env vars
- document the contract for custom processes clearly

### Risk: Overloading the existing global Logs page

Impact:

- poor UX
- unbounded source lists
- confusing separation between service logs and run logs

Mitigation:

- keep server/worker logs in the global `Logs` page
- add run logs to the `Runs` experience

### Risk: Secrets appear in run logs

Impact:

- sensitive data may be exposed to any principal who can read run logs

Mitigation:

- reuse existing tenant-scoped run authorization
- document the risk in operator and SDK docs
- avoid automatic body/header capture beyond what step code intentionally writes

## Open Questions

- [ ] Should delete permissions for run logs match any tenant principal who can read runs, or be limited to tenant admins? Recommended: restrict delete to tenant admins while allowing read to existing run viewers.
- [ ] Should bulk purge for a run ship in v1, or should v1 support only single-file delete plus background retention? Recommended: single-file delete plus background retention is enough for the first pass.

## Completion Notes

Use this section when implementation is done.

- Implementation PR:
- Validation summary:
- Follow-up work:
