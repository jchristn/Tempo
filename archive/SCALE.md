# Tempo v0.3.0 Distributed Execution Plan

## Status Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked or requires a decision

## How To Use This File

- Update the checkbox on every task as work progresses.
- Add `Owner:`, `Started:`, `Completed:`, and `Notes:` directly below any task that needs context.
- If scope changes, update `Locked Decisions` or `Deferred Scope` in the same PR.
- Do not treat this document as a wish list. If a task is not listed here, it is out of scope for v0.3.0 unless this file is amended first.

## Release Goal

Tempo v0.3.0 must close the biggest capability, manageability, and scale gap with Airflow without attempting full Airflow parity in one release.

This release is complete only when Tempo can:

1. Run with a clear control-plane and execution-plane split.
2. Add and remove worker nodes dynamically.
3. Route whole flow runs onto eligible workers.
4. Recover cleanly from worker loss without leaving runs stuck.
5. Show worker state and run placement to operators.

## Current Code Constraints

These constraints are the reason this plan is shaped the way it is:

- [`src/Tempo.Server/TempoServer.cs`](src/Tempo.Server/TempoServer.cs) starts the web host and today also starts local execution.
- [`src/Tempo.Core/Services/FlowQueueWorker.cs`](src/Tempo.Core/Services/FlowQueueWorker.cs) polls `flow_runs`, claims work, and executes it locally.
- [`src/Tempo.Core/Database/Common/Implementations/FlowRunMethods.cs`](src/Tempo.Core/Database/Common/Implementations/FlowRunMethods.cs) contains the current queue-claim path that must be retired.
- [`src/Tempo.Server/Routes/FlowRunRoutes.cs`](src/Tempo.Server/Routes/FlowRunRoutes.cs) currently cancels queued runs directly.
- [`src/Tempo.Server/Routes/TriggerRoutes.cs`](src/Tempo.Server/Routes/TriggerRoutes.cs) assumes `Queued` and `Running` are the only non-terminal `FlowRun.State` values.
- [`src/Tempo.Core/Runtime/RegistryDataFlowRunner.cs`](src/Tempo.Core/Runtime/RegistryDataFlowRunner.cs) executes an entire flow run inside one process.
- [`src/Tempo.Core/Runtime/DatabaseStepExecutionResolver.cs`](src/Tempo.Core/Runtime/DatabaseStepExecutionResolver.cs) resolves step implementations from server-managed state and cannot be carried into a remote worker unchanged.
- [`src/Tempo.Core/Database/DatabaseDriverBase.cs`](src/Tempo.Core/Database/DatabaseDriverBase.cs) exposes raw SQL execution returning `DataTable` results across all providers, so v0.3.0 must avoid provider-specific CAS SQL as a dependency.

## Locked Decisions

These decisions are settled for v0.3.0. Do not reopen them unless implementation exposes a blocker that cannot be resolved within the current release.

- `[x]` Tempo dispatches whole `flow_runs`, not individual steps, in v0.3.0.
- `[x]` v0.3.0 is a single-scheduler release. Multi-scheduler HA and leader election are out of scope.
- `[x]` `Tempo.Server` remains the sole database writer. Workers do not connect to the Tempo database.
- `[x]` The current queue worker path is retired in Phase 1. `FlowQueueWorker` and `ClaimNextQueuedAsync` do not remain as fallback modes.
- `[x]` One in-process `IRunDispatchCoordinator` owns scheduling, queued cancel, assignment persistence, completion handling, and lease recovery.
- `[x]` Server-local execution runs through `LocalServerRunExecutor` as a pseudo-worker using the same execution plan as remote workers.
- `[x]` `FlowRun.State` stays coarse and backward-compatible. New dispatch transitions live on `flow_runs.dispatch_state` and `run_assignments`.
- `[x]` Delivery semantics are at-least-once. Idempotency is keyed by `runAssignmentId`, `leaseToken`, and `workerSessionId`.
- `[x]` The first-party worker daemon is C# only in v0.3.0.
- `[x]` Worker artifacts are fetched over authenticated HTTP. Large artifact payloads do not travel over the worker WebSocket.
- `[x]` Scheduling settings stay under [`src/Tempo.Core/Settings/EngineSettings.cs`](src/Tempo.Core/Settings/EngineSettings.cs) and are treated as reboot-required.
- `[x]` Capability matching is per-step, not per-worker-global, using `{executionKey, tenantScope, sourceKind, signatureHash}`.
- `[x]` `worker_sessions` remains a small table. Historical capability snapshots are stored in `worker_activity`, not denormalized across session rows.
- `[x]` v0.3.0 ships exactly two placement strategies: `LeastLoaded` and `LabelPinned`.
- `[x]` Split-brain protection is fail-closed for scheduling. A second live server starts in API-only mode unless an explicit unsupported override is supplied.
- `[x]` In-flight cancel for already-running remote work is deferred.

## Deferred Scope

The following items are explicitly not part of v0.3.0:

- Step-level cross-worker execution inside a single flow run.
- Multi-scheduler HA, leader election, or active/active scheduling.
- `RoundRobin`, `WeightedRoundRobin`, `WeightedLeastLoaded`, `TenantAffinity`, or other additional load-balancing strategies.
- JS or Python worker-protocol implementations.
- A `worker_telemetry_samples` time-series table.
- Per-worker histogram dashboards, home-page worker tiles, or runtime parity dashboards.
- In-flight cancel for running remote assignments.
- Push-based trigger completion or other replacement of `TriggerRoutes` polling.
- `step_runs` worker placement denormalization unless Phase 3 proves it is required after run-level placement lands.

## Target Architecture

### Control Plane and Execution Plane

- `Tempo.Server` becomes the control plane.
- `Tempo.Worker` becomes the execution plane.
- `Tempo.Server` handles REST, OpenAPI, MCP-backed APIs, worker management, scheduling, persistence, and artifact download.
- `Tempo.Worker` maintains an authenticated WebSocket session, advertises capabilities, receives run assignments, executes runs, reports lifecycle frames, and fetches artifacts over HTTP.
- The server may optionally participate in execution through a pseudo-worker controlled by `engine.serverCanExecuteWorkload`.

### Execution Unit

- A single `flow_run` is assigned to one worker for the duration of that attempt.
- `RegistryDataFlowRunner` remains the whole-flow executor.
- Step-level handoff, remote checkpoints, and mid-run migration are deferred.

### Single Authoritative Coordinator

- `IRunDispatchCoordinator` is the only in-process authority for:
  - enqueue-to-assignment transitions
  - queued cancel
  - remote completion handling
  - lease-expiry recovery
  - worker drain admission decisions
- `FlowRunRoutes` and any worker-management surface call into the coordinator rather than mutating `flow_runs` directly.
- The coordinator owns an internal `SemaphoreSlim` so v0.3.0 has one authoritative scheduler inside the process.

### Execution Plan Contract

Before any assignment is made, the server creates a serializable execution plan that both local and remote executors consume.

`FlowRunExecutionPlan` must contain:

- `flowRunId`
- `tenantId`
- `triggerContext`
- resolved flow graph
- resolved step map
- required capability set
- initial input payload
- execution budget and lease metadata

Each resolved step snapshot must contain:

- `executionKey`
- `tenantScope`
- `sourceKind`
- `signatureHash`
- inline config when the step is inline
- artifact reference when the step is registry-backed

This keeps the worker out of the step-catalog database and ensures the local pseudo-worker and remote worker follow one execution path.

### Persistence Rules

- All authoritative writes remain on the server.
- Assignment creation and dispatch-state updates run through `ExecuteQueriesAsync(isTransaction: true)`.
- Because the DB abstraction returns `DataTable` rather than affected-row counts, any guarded `UPDATE` that must be verified is followed by a `SELECT` inside the same transaction.
- v0.3.0 does not depend on provider-specific `RETURNING`, `OUTPUT`, or similar SQL dialect features.

## Required Data Model

Add only the minimum schema required for reliable distributed execution.

### New Tables

- `workers`
  - Required fields:
    - `id`
    - `name`
    - `kind`
    - `state`
    - `enabled`
    - `drain_mode`
    - `version`
    - `host_name`
    - `labels_json`
    - `max_concurrent_runs`
    - `last_heartbeat_utc`
    - `created_utc`

- `worker_sessions`
  - Required fields:
    - `id`
    - `worker_id`
    - `connected_utc`
    - `disconnected_utc`
    - `disconnect_reason`
    - `protocol_version`
  - Notes:
    - keep this minimal
    - full `hello` snapshots belong in `worker_activity`

- `run_assignments`
  - Required fields:
    - `id`
    - `flow_run_id`
    - `worker_id`
    - `worker_session_id`
    - `attempt_number`
    - `state`
    - `lease_token`
    - `lease_expires_utc`
    - `assigned_utc`
    - `completed_utc`
  - Notes:
    - `worker_session_id` is nullable for `LocalServerRunExecutor`

- `worker_activity`
  - Required fields:
    - `id`
    - `worker_id`
    - `worker_session_id`
    - `flow_run_id`
    - `run_assignment_id`
    - `event_type`
    - `severity`
    - `message`
    - `payload_json`
    - `created_utc`
  - Notes:
    - store the full `hello` capability snapshot in the connect event payload
    - late or stale completions are recorded here as `orphan_completion`

- `server_instances`
  - Required fields:
    - `id`
    - `started_utc`
    - `last_heartbeat_utc`
    - `version`

### `flow_runs` Extensions

Add the following fields to `flow_runs`:

- `dispatch_state`
- `dispatch_attempt`
- `assigned_worker_id`
- `run_assignment_id`
- `queue_wait_ms`
- `assigned_utc`
- `lease_expires_utc`
- `execution_node_kind`

### Explicit Non-Adds

- Do not add `worker_telemetry_samples` in v0.3.0.
- Do not extend `FlowRun.State`.
- Do not add `assigned_worker_name` or other join-derived fields unless Phase 3 proves they are needed.

## Phase 1 - Foundation

Phase goal: replace the existing local queue-claim path with a single authoritative coordinator and keep single-node behavior working through the new architecture before remote workers exist.

- `[x]` Add the additive schema and indexes required for distributed execution.
  - Update provider schema files:
    - [`src/Tempo.Core/Database/Sqlite/Queries/SchemaQueries.cs`](src/Tempo.Core/Database/Sqlite/Queries/SchemaQueries.cs)
    - [`src/Tempo.Core/Database/SqlServer/SqlServerSchema.cs`](src/Tempo.Core/Database/SqlServer/SqlServerSchema.cs)
    - [`src/Tempo.Core/Database/Postgresql/PostgresqlSchema.cs`](src/Tempo.Core/Database/Postgresql/PostgresqlSchema.cs)
    - [`src/Tempo.Core/Database/Mysql/MysqlSchema.cs`](src/Tempo.Core/Database/Mysql/MysqlSchema.cs)
  - Add indexes for:
    - online worker lookup
    - stale session scans
    - stale lease recovery
    - worker activity lookups by worker and run
    - duplicate-scheduler heartbeat checks

- `[x]` Add the core scheduling abstractions.
  - Add or update code under:
    - [`src/Tempo.Core`](src/Tempo.Core)
    - [`src/Tempo.Server`](src/Tempo.Server)
  - Required interfaces:
    - `IRunDispatchCoordinator`
    - `IRunAssignmentStore`
    - `IRunScheduler`
    - `ILoadBalancer`
    - `IRunExecutor`
  - Required coordinator methods:
    - `EnqueueAsync`
    - `CancelQueuedAsync`
    - `HandleCompletionAsync`
    - `HandleLeaseExpiryAsync`

- `[x]` Introduce `FlowRunExecutionPlan`.
  - Update or add code near:
    - [`src/Tempo.Core/Runtime`](src/Tempo.Core/Runtime)
  - Required behavior:
    - resolve steps on the server
    - compute the run's required capability set
    - package inline step data and artifact references
    - make the plan serializable for remote delivery

- `[x]` Implement `LocalServerRunExecutor`.
  - Update startup wiring in:
    - [`src/Tempo.Server/TempoServer.cs`](src/Tempo.Server/TempoServer.cs)
  - Required behavior:
    - register a pseudo-worker only when `engine.serverCanExecuteWorkload = true`
    - consume the same execution plan as remote workers
    - never bypass the coordinator

- `[x]` Move queued cancel into the coordinator.
  - Update:
    - [`src/Tempo.Server/Routes/FlowRunRoutes.cs`](src/Tempo.Server/Routes/FlowRunRoutes.cs)
  - Required behavior:
    - queued cancel no longer writes state directly
    - all run-state and dispatch-state transitions route through one authority

- `[x]` Retire the old queue-claim path.
  - Remove or stop using:
    - [`src/Tempo.Core/Services/FlowQueueWorker.cs`](src/Tempo.Core/Services/FlowQueueWorker.cs)
    - queue-claim usage in [`src/Tempo.Core/Database/Common/Implementations/FlowRunMethods.cs`](src/Tempo.Core/Database/Common/Implementations/FlowRunMethods.cs)
  - Required result:
    - no code path claims work using the old `SELECT -> UPDATE -> verify` flow

- `[x]` Keep API behavior backward-compatible.
  - Verify behavior in:
    - [`src/Tempo.Server/Routes/TriggerRoutes.cs`](src/Tempo.Server/Routes/TriggerRoutes.cs)
  - Required behavior:
    - `FlowRun.State` remains unchanged
    - `TriggerRoutes` still returns final output on success
    - when no executor is available before the wait budget, `202` still returns with run headers

- `[x]` Add Phase 1 tests.
  - Update or add tests under:
    - [`src/Test.Shared`](src/Test.Shared)
    - [`src/Test.Xunit`](src/Test.Xunit)
    - [`src/Test.Nunit`](src/Test.Nunit)
    - [`src/Test.Automated`](src/Test.Automated)
  - Minimum coverage:
    - coordinator owns queued cancel
    - server-local execution flows through the new path
    - no remote worker required for baseline execution
    - `FlowRun.State` semantics unchanged

### Phase 1 Exit Criteria

- `[x]` `FlowQueueWorker` is retired and not used anywhere in the runtime path.
- `[x]` `ClaimNextQueuedAsync` is retired as a scheduling primitive.
- `[x]` Zero-remote-worker execution succeeds through the new coordinator path.
- `[x]` Existing trigger semantics remain backward-compatible.
- `[x]` Baseline automated tests are green.

## Phase 2 - Remote Worker Connectivity and Reliability

Phase goal: add a first-party remote worker, authenticated worker sessions, reliable assignment recovery, and capability-aware placement.

- `[x]` Add the new worker project.
  - Add:
    - `src/Tempo.Worker/Tempo.Worker.csproj`
  - Required behavior:
    - config load
    - logging bootstrap
    - WebSocket connect and reconnect loop
    - graceful drain and shutdown

- `[x]` Write the worker protocol.
  - Add:
    - [`docs/WORKER_PROTOCOL.md`](docs/WORKER_PROTOCOL.md)
  - Required frames:
    - `hello`
    - `hello-ack`
    - `heartbeat`
    - `assign`
    - `assign-ack`
    - `run-completed`
    - `drain`
    - `resume`
  - Required protocol rules:
    - at-least-once delivery
    - no in-flight cancel in v0.3.0
    - late completion is accepted only when assignment id, lease token, and session id match the current assignment
    - invalid late completion is persisted as `orphan_completion`

- `[x]` Implement worker session management on the server.
  - Update or add code under:
    - [`src/Tempo.Server`](src/Tempo.Server)
  - Required behavior:
    - create `worker_sessions` on connect
    - persist full `hello` capability snapshots to `worker_activity`
    - mark disconnects explicitly
    - keep `run_assignments.worker_session_id` nullable for local execution

- `[x]` Implement worker authentication and token rotation.
  - Update server routes and services under:
    - [`src/Tempo.Server`](src/Tempo.Server)
  - Required behavior:
    - per-worker token issuance
    - worker-authenticated connection path
    - `POST /v1.0/workers/{id}/rotate-token`
    - do not reuse end-user bearer tokens

- `[x]` Add authenticated worker artifact download.
  - Update:
    - [`src/Tempo.Server/Routes/ArtifactRoutes.cs`](src/Tempo.Server/Routes/ArtifactRoutes.cs)
  - Required behavior:
    - workers fetch artifact bytes through HTTP
    - plan payload carries artifact references, not artifact bodies
    - download authorization is worker-scoped

- `[x]` Implement assignment recovery and lease expiry handling.
  - Required behavior:
    - detect stale heartbeats
    - detect expired leases
    - requeue eligible runs
    - produce exactly one terminal run outcome
    - cap retries using engine settings

- `[x]` Implement split-brain protection.
  - Update startup logic in:
    - [`src/Tempo.Server/TempoServer.cs`](src/Tempo.Server/TempoServer.cs)
  - Required behavior:
    - maintain a `server_instances` heartbeat
    - if another live scheduler is detected, start in API-only mode
    - expose a clear scheduling-disabled error on enqueue
    - keep any duplicate-scheduler override explicitly unsupported

- `[x]` Implement capability-aware worker eligibility.
  - Required behavior:
    - worker capabilities are advertised as `{executionKey, tenantScope, sourceKind, signatureHash}`
    - tenant-local built-ins and global built-ins follow the same resolution precedence as the server
    - capability mismatch fails fast with a logged `no_eligible_worker` decision

- `[x]` Implement the two shipped placement strategies.
  - Required strategies:
    - `LeastLoaded`
    - `LabelPinned`
  - Required notes:
    - if there is no existing routing-hint field, add the smallest one needed to support label pinning
    - do not add preferred labels, strategy overrides, queues, or weighted strategies in v0.3.0

- `[x]` Add C# worker-protocol DTOs only.
  - Update:
    - [`sdk/csharp`](sdk/csharp)
  - Required behavior:
    - C# protocol models match `WORKER_PROTOCOL.md`
    - JS and Python SDKs remain unchanged for v0.3.0

- `[x]` Add Phase 2 tests and fault injection.
  - Minimum coverage:
    - one server plus one worker
    - one server plus multiple workers
    - `serverCanExecuteWorkload = false`
    - stale heartbeat requeue
    - worker dies before `assign-ack`
    - worker dies mid-run
    - duplicate completion frames
    - capability mismatch rejection

### Phase 2 Exit Criteria

- `[ ]` One server plus three workers can execute at least 10,000 runs.
- `[x]` A worker killed mid-run results in requeue and exactly one terminal run outcome.
- `[x]` No run remains stuck because of a dead worker or stale lease.
- `[x]` Capability mismatch fails fast rather than stalling.
- `[x]` Split-brain detection disables scheduling on the second live server.

## Phase 3 - Operator Surface

Phase goal: expose worker state, worker control, and run placement to operators through REST, OpenAPI, the dashboard, MCP, and docs.

- `[x]` Add the worker REST surface.
  - Update routes and models under:
    - [`src/Tempo.Server/Routes`](src/Tempo.Server/Routes)
    - [`src/Tempo.Server/Routes/OpenApiSchemaCatalog.cs`](src/Tempo.Server/Routes/OpenApiSchemaCatalog.cs)
  - Minimum endpoints:
    - `GET /v1.0/workers`
    - `GET /v1.0/workers/{id}`
    - `POST /v1.0/workers/{id}/drain`
    - `POST /v1.0/workers/{id}/resume`
    - `POST /v1.0/workers/{id}/rotate-token`
  - Required behavior:
    - extend run read responses with assignment and worker metadata
    - add standalone assignment endpoints only if the UI needs them

- `[x]` Add the minimum dashboard support.
  - Add or update:
    - [`dashboard/src/views/WorkersView.jsx`](dashboard/src/views/WorkersView.jsx)
    - [`dashboard/src/views/RunsView.jsx`](dashboard/src/views/RunsView.jsx)
    - [`dashboard/src/views/SettingsView.jsx`](dashboard/src/views/SettingsView.jsx)
    - [`dashboard/src/components/ActivityChart.jsx`](dashboard/src/components/ActivityChart.jsx)
    - [`dashboard/src/utils/api.js`](dashboard/src/utils/api.js)
  - Required UI surface:
    - worker list
    - filters
    - drain and resume actions
    - worker detail drawer
    - one generalized activity chart
    - assigned worker column on runs
    - engine scheduling settings editor
  - Explicit non-goals:
    - no home-page worker tiles
    - no per-worker histogram dashboard
    - no runtime capability parity page in v0.3.0

- `[x]` Add the minimal MCP surface.
  - Update:
    - [`src/Tempo.McpServer/Tools/TempoToolRegistrar.cs`](src/Tempo.McpServer/Tools/TempoToolRegistrar.cs)
  - Required tools:
    - `listWorkers`
    - `readWorker`
    - `drainWorker`
    - `resumeWorker`

- `[x]` Mark engine scheduling settings as reboot-required.
  - Update:
    - [`src/Tempo.Core/Settings/EngineSettings.cs`](src/Tempo.Core/Settings/EngineSettings.cs)
    - [`src/Tempo.Core/Settings/Settings.cs`](src/Tempo.Core/Settings/Settings.cs)
    - [`src/Tempo.Server/Services/SettingsStore.cs`](src/Tempo.Server/Services/SettingsStore.cs)
    - [`src/Tempo.Server/tempo.json`](src/Tempo.Server/tempo.json)
  - Required settings:
    - `serverCanExecuteWorkload`
    - `loadBalancingStrategy`
    - `workerHeartbeatTimeoutMs`
    - `leaseDurationMs`
    - `maxAssignmentAttempts`
    - `allowDuplicateScheduler`

- `[x]` Update operator-facing docs and examples.
  - Update:
    - [`README.md`](README.md)
    - [`docs/REST_API.md`](docs/REST_API.md)
    - [`docs/MCP_API.md`](docs/MCP_API.md)
    - [`docs/BEST_PRACTICES.md`](docs/BEST_PRACTICES.md)
    - [`Tempo.postman_collection.json`](Tempo.postman_collection.json)
  - Add:
    - [`docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md`](docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md)

- `[x]` Add worker packaging and local deployment support.
  - Update or add:
    - `build-worker.bat`
    - `docker/compose.yaml`
    - worker Dockerfile under `docker/`
  - Required behavior:
    - local compose example includes at least one worker
    - server-only scheduling mode is demonstrated

### Phase 3 Exit Criteria

- `[x]` Operators can list workers, inspect one worker, and drain or resume a worker end to end.
- `[x]` The dashboard shows current worker state and assigned worker placement on runs.
- `[x]` Engine scheduling settings are visible and clearly marked as reboot-required.
- `[x]` MCP tools work against the shipped worker-management surface.
- `[x]` Operator docs and Postman examples match the implemented API.

## Phase 4 - Hardening and Release

Phase goal: prove reliability under load, validate provider coverage, and close the release with clear operational guidance.

- `[~]` Run soak and recovery testing.
  - Minimum scenarios:
    - long-running mixed workloads
    - repeated worker disconnect and reconnect
    - duplicate completion frame injection
    - scheduler-disabled startup because of duplicate server heartbeat

- `[ ]` Validate schema behavior across all supported providers.
  - Providers:
    - SQLite
    - PostgreSQL
    - MySQL
    - SQL Server
  - Required result:
    - migrations apply cleanly
    - schema reset remains possible

- `[~]` Verify release packaging.
  - Required artifacts:
    - server build
    - worker build
    - dashboard build
    - updated container assets

- `[x]` Close release docs.
  - Update:
    - [`CHANGELOG.md`](CHANGELOG.md)
  - Required result:
    - release notes explain the new execution model
    - operator upgrade notes call out reboot-required engine settings

### Phase 4 Exit Criteria

- `[ ]` All acceptance criteria in this file are checked off.
- `[ ]` No open blocker remains in the release scope.
- `[ ]` Release artifacts and docs are ready to ship.

## Acceptance Criteria

The release is not done until every item below is true.

- `[ ]` One server plus three workers executes at least 10,000 runs with P99 dispatch latency below 500 ms.
- `[x]` Killing a worker mid-run causes recovery within `workerHeartbeatTimeoutMs + leaseDurationMs` and still yields exactly one terminal run outcome.
- `[x]` With `serverCanExecuteWorkload = false` and zero workers connected, new runs remain queued with `dispatch_state = Pending`, and trigger wait still returns `202` after the wait budget.
- `[x]` Capability mismatch fails fast with a logged `no_eligible_worker` decision and never becomes a silent stall.
- `[x]` Operators can see live worker state, drain or resume workers, and identify the assigned worker on a run.
- `[ ]` Schema changes apply cleanly across SQLite, PostgreSQL, MySQL, and SQL Server.
- `[x]` `FlowRun.State`, trigger wait behavior, and existing run-read contracts remain backward-compatible with v0.2.x.

## Progress Notes

Use this section for short chronological updates during implementation.

- `[x]` 2026-04-21: Phase 1 foundation landed. Added distributed-execution schema, dispatch metadata on `flow_runs`, coordinator/scheduler abstractions, `FlowRunExecutionPlan`, `LocalServerRunExecutor`, coordinator-owned queued cancel, and removed the legacy queue-claim worker path. `dotnet run --project src/Test.Automated -- --results artifacts/phase1-test-results.json` passed with 186/186 tests green.
- `[x]` 2026-04-21: Phase 2 and Phase 3 implementation landed. Added `Tempo.Worker`, worker protocol/contracts, authenticated worker management and artifact download, capability-aware placement, recovery paths, dashboard worker views, MCP worker tools, compose packaging, operator docs, and distributed execution coverage for remote execution, stale heartbeat recovery, duplicate completion, capability mismatch, worker drain/resume, and duplicate-scheduler suppression.
