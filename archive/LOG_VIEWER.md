# Log Viewer Plan

Status: Implemented
Owner:
Last Updated: 2026-04-21

## Purpose

Implement a low-risk, file-backed log viewer for Tempo that allows administrators to:

- enumerate available log sources
- list available log files per source
- read bounded content from a selected log file
- delete eligible log files
- view server and worker logs from the dashboard
- access the same surface through REST, OpenAPI, MCP, and Postman

This document is intentionally written as an execution handoff. A developer should be able to work through the checklist, update progress inline, and leave notes under each phase.

## Progress Tracking

Update this section as work lands.

- [x] Phase 0: Confirm scope and storage layout
- [x] Phase 1: Normalize Docker log storage
- [x] Phase 2: Add server-side log catalog and REST routes
- [x] Phase 3: Add dashboard log viewer
- [x] Phase 4: Add MCP support
- [x] Phase 5: Update Postman and docs
- [x] Phase 6: Add tests and coverage pass
- [x] Phase 7: Final validation

## Scope

### In Scope

- `Tempo.Server` logs
- `Tempo.Worker` logs
- admin-only REST endpoints for log enumeration, read, and delete
- OpenAPI metadata for the new log routes
- dashboard UI for browsing and reading logs
- worker-context entry points in the dashboard
- MCP tools for the same operations
- Postman coverage
- docs updates
- Docker and factory/reset asset updates required to make worker logs file-backed and discoverable
- targeted test coverage for the new logging slice

### Out of Scope for v1

- dashboard, MCP, or REST support for container stdout/stderr via `docker logs`
- WebSocket or SSE live tail streaming
- in-browser full-text indexing
- cross-node log aggregation beyond what is visible on shared/mounted storage
- role-based access finer than existing admin-only worker-management style authorization
- adding a general-purpose admin REST SDK package unless explicitly requested later

## Constraints

- Keep the feature file-backed and deterministic.
- Do not allow arbitrary path traversal or arbitrary filesystem reads.
- Do not shell out to Docker from the server.
- Do not expose logs to non-admin users in v1.
- Prefer bounded reads by line count and byte count over full-file reads.
- Prefer read-only server access to worker log storage.
- Keep delete behavior conservative. It is acceptable for v1 to reject deleting currently active log files.

## Current Repo Context

Relevant existing files and patterns:

- Server bootstrap and route registration:
  - [src/Tempo.Server/TempoServer.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/TempoServer.cs)
- Existing file-backed route pattern:
  - [src/Tempo.Server/Routes/ArtifactRoutes.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Routes/ArtifactRoutes.cs)
- Existing admin-only route pattern:
  - [src/Tempo.Server/Routes/WorkerRoutes.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Routes/WorkerRoutes.cs)
- Existing request-history route pattern:
  - [src/Tempo.Server/Routes/RequestHistoryRoutes.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/Routes/RequestHistoryRoutes.cs)
- Logging settings:
  - [src/Tempo.Core/Settings/LoggingSettings.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Core/Settings/LoggingSettings.cs)
- Worker settings and environment overrides:
  - [src/Tempo.Worker/WorkerSettings.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerSettings.cs)
  - [src/Tempo.Worker/WorkerSettingsLoader.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerSettingsLoader.cs)
- Compose and factory assets:
  - [docker/compose.yaml](/abs/path/C:/Code/Tempo/Tempo/docker/compose.yaml)
  - [docker/factory/reset.bat](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.bat)
  - [docker/factory/reset.sh](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.sh)
  - [docker/factory/README.md](/abs/path/C:/Code/Tempo/Tempo/docker/factory/README.md)
- Dashboard navigation and view wiring:
  - [dashboard/src/components/Sidebar.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/components/Sidebar.jsx)
  - [dashboard/src/components/Dashboard.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/components/Dashboard.jsx)
  - [dashboard/src/utils/api.js](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/utils/api.js)
  - [dashboard/src/views/WorkersView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/WorkersView.jsx)
  - [dashboard/src/views/HomeView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/HomeView.jsx)
  - [dashboard/src/views/ArtifactsView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/ArtifactsView.jsx)
- MCP:
  - [src/Tempo.McpServer/Services/TempoApiClient.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.McpServer/Services/TempoApiClient.cs)
  - [src/Tempo.McpServer/Tools/TempoToolRegistrar.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.McpServer/Tools/TempoToolRegistrar.cs)
- Docs and machine-consumable surfaces:
  - [docs/REST_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/REST_API.md)
  - [docs/MCP_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/MCP_API.md)
  - [docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md](/abs/path/C:/Code/Tempo/Tempo/docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md)
  - [README.md](/abs/path/C:/Code/Tempo/Tempo/README.md)
  - [Tempo.postman_collection.json](/abs/path/C:/Code/Tempo/Tempo/Tempo.postman_collection.json)

## Recommended Design

### v1 Source Model

Use a small fixed catalog of source kinds:

- `server`
- `worker`

Source identifiers:

- `server`: fixed source id such as `server`
- `worker`: Tempo worker ID, for example `wrk_docker_1`

### Storage Model

Server logs are already file-backed and mounted through a stable root.

Worker logs need to be normalized so `Tempo.Server` can enumerate them without reaching into containers. The recommended layout is:

- shared worker log root
- one subdirectory per worker ID
- each worker writes its log file into its own subdirectory
- `Tempo.Server` mounts the worker log root read-only

Example conceptual layout:

```text
/var/lib/tempo-server/logs/tempo.log
/var/lib/tempo-worker/logs/wrk_docker_1/tempo.worker.log
/var/lib/tempo-worker/logs/wrk_docker_2/tempo.worker.log
/var/lib/tempo-worker/logs/wrk_docker_3/tempo.worker.log
```

This should be worker-ID based, not worker-name based, so renaming a worker does not break log discovery.

### API Model

Admin-only routes under `/v1.0/logs`:

- `GET /v1.0/logs/sources`
- `GET /v1.0/logs/files?sourceKind=&sourceId=`
- `GET /v1.0/logs/files/content?sourceKind=&sourceId=&path=&tailLines=&maxBytes=`
- `DELETE /v1.0/logs/files/content?sourceKind=&sourceId=&path=`

Response model should expose:

- source metadata
- file metadata
- bounded text content
- delete eligibility
- whether the file is likely current/active

### Dashboard Model

Add a dedicated `Logs` page under `System`, plus worker-specific shortcuts from `Workers`.

The `Logs` page should provide:

- source picker
- file list
- log viewer pane
- refresh and optional auto-refresh
- copy content
- delete action when allowed

The first implementation should use polling and bounded reads. No streaming transport is required.

### MCP Model

Add typed MCP tools:

- `listLogSources`
- `listLogFiles`
- `readLogFile`
- `deleteLogFile`

Keep the existing `tempo_request` escape hatch unchanged.

## Phase Plan

## Phase 0: Confirm Scope and Storage Layout

Goal: Lock the v1 behavior and remove ambiguity before code changes spread across server, dashboard, and Docker.

Checklist:

- [ ] Confirm v1 source kinds are only `server` and `worker`
- [ ] Confirm authorization model is admin-only, matching worker routes
- [ ] Confirm delete policy for active/current files
- [ ] Confirm worker log storage layout and mount strategy
- [ ] Confirm bounded read defaults for `tailLines` and `maxBytes`
- [ ] Confirm response schema shape before touching OpenAPI

Implementation notes:

- Prefer reusing the existing admin-only pattern from `WorkerRoutes`.
- Do not add a new `ResourceTypeEnum` value for logs in v1 unless there is a clear product requirement for non-admin RBAC later.
- Record any scope changes here before implementation begins.

Developer notes:

-

## Phase 1: Normalize Docker Log Storage

Goal: Make worker logs visible on stable storage so the server can read them safely.

Checklist:

- [ ] Add worker log directory environment override to `Tempo.Worker`
- [ ] Update Compose so each worker writes to a per-worker directory under a stable shared root
- [ ] Mount the worker log root into `Tempo.Server` as read-only
- [ ] Add any required server-side config setting for the worker log root
- [ ] Update factory/reset assets to recreate the worker log root
- [ ] Update factory README to describe the new volume/layout

Expected file touches:

- [ ] [src/Tempo.Worker/WorkerSettingsLoader.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Worker/WorkerSettingsLoader.cs)
- [ ] [docker/compose.yaml](/abs/path/C:/Code/Tempo/Tempo/docker/compose.yaml)
- [ ] [docker/factory/reset.bat](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.bat)
- [ ] [docker/factory/reset.sh](/abs/path/C:/Code/Tempo/Tempo/docker/factory/reset.sh)
- [ ] [docker/factory/README.md](/abs/path/C:/Code/Tempo/Tempo/docker/factory/README.md)
- [ ] `docker/factory/tempo_worker_logs`

Acceptance criteria:

- `docker compose up -d` creates a server-visible worker log root
- each worker writes to a distinct subdirectory keyed by worker ID
- `Tempo.Server` can enumerate worker log files from mounted storage without shelling into containers
- reset scripts restore the deployment to a clean state that still supports the log viewer

Developer notes:

-

## Phase 2: Add Server-Side Log Catalog and REST Routes

Goal: Add the core file-backed log surface in `Tempo.Server`.

Checklist:

- [ ] Add settings or resolved paths for server and worker log roots
- [ ] Add a small service layer for log source resolution and file enumeration
- [ ] Add bounded log file read support
- [ ] Add safe delete support
- [ ] Add `LogRoutes` and register them
- [ ] Add OpenAPI metadata and schemas
- [ ] Ensure admin-only authorization
- [ ] Ensure path traversal and invalid source errors are handled cleanly

Suggested components:

- [ ] `src/Tempo.Server/Services/LogSourceCatalog.cs`
- [ ] `src/Tempo.Server/Services/LogFileService.cs`
- [ ] `src/Tempo.Server/Routes/LogRoutes.cs`

Potential supporting model files:

- [ ] `src/Tempo.Core/Responses/LogSourceSummaryResponse.cs`
- [ ] `src/Tempo.Core/Responses/LogFileSummaryResponse.cs`
- [ ] `src/Tempo.Core/Responses/LogFileReadResponse.cs`

Expected file touches:

- [ ] [src/Tempo.Server/TempoServer.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.Server/TempoServer.cs)
- [ ] `src/Tempo.Server/Routes/OpenApiSchemaCatalog.cs`
- [ ] new server services and route files

Route behavior requirements:

- `GET /v1.0/logs/sources`
  - returns fixed source kinds and currently available source IDs
- `GET /v1.0/logs/files`
  - lists files for a selected source
  - rejects unknown source kind or source id
- `GET /v1.0/logs/files/content`
  - returns bounded text content
  - defaults to tail behavior
  - does not attempt to deserialize logs as JSON
- `DELETE /v1.0/logs/files/content`
  - only deletes eligible files
  - rejects current/active file deletion if the safety policy says no

Safety requirements:

- normalize every path to a canonical full path before use
- ensure the final file path remains inside the configured source root
- reject absolute paths from clients
- reject `..` traversal segments
- do not expose directories outside known roots

Acceptance criteria:

- REST routes are visible in `/openapi.json`
- admin auth works the same way as worker routes
- listing, reading, and deletion work for server logs
- listing and reading work for worker logs from mounted storage
- invalid path inputs fail cleanly

Developer notes:

-

## Phase 3: Add Dashboard Log Viewer

Goal: Give operators a first-class UI for browsing server and worker logs.

Checklist:

- [ ] Add a `Logs` section to the sidebar under `System`
- [ ] Wire a `LogsView` into dashboard routing
- [ ] Add typed API client methods for log routes
- [ ] Implement source picker, file list, and viewer pane
- [ ] Add refresh and optional auto-refresh
- [ ] Add delete action for eligible files
- [ ] Add worker-context quick links from `Workers`
- [ ] Add at least one home-page shortcut to server logs
- [ ] Ensure every new control, field, and column has a hover title

Expected file touches:

- [ ] [dashboard/src/components/Sidebar.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/components/Sidebar.jsx)
- [ ] [dashboard/src/components/Dashboard.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/components/Dashboard.jsx)
- [ ] [dashboard/src/utils/api.js](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/utils/api.js)
- [ ] [dashboard/src/views/WorkersView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/WorkersView.jsx)
- [ ] [dashboard/src/views/HomeView.jsx](/abs/path/C:/Code/Tempo/Tempo/dashboard/src/views/HomeView.jsx)
- [ ] `dashboard/src/views/LogsView.jsx`
- [ ] any required shared components or CSS in `dashboard/src/App.css`

UX requirements:

- default to server logs when opening the page with no query params
- support deep links like `/dashboard/logs?sourceKind=worker&sourceId=wrk_...`
- show file metadata such as size and modified time
- clearly indicate when content is truncated due to bounds
- use monospace rendering for log content and file paths
- keep delete confirmation explicit and conservative

Acceptance criteria:

- operators can browse to a dedicated Logs page
- operators can open worker logs directly from the Workers page
- operators can switch sources and files without losing context
- the dashboard handles empty states, missing files, and authorization failures cleanly

Developer notes:

-

## Phase 4: Add MCP Support

Goal: Expose the same log surface to MCP clients without requiring generic REST calls.

Checklist:

- [ ] Add typed MCP tools for source list, file list, read, and delete
- [ ] Add input schemas and descriptions
- [ ] Ensure tool names are consistent with current MCP naming style
- [ ] Document header and body expectations in MCP docs

Expected file touches:

- [ ] [src/Tempo.McpServer/Tools/TempoToolRegistrar.cs](/abs/path/C:/Code/Tempo/Tempo/src/Tempo.McpServer/Tools/TempoToolRegistrar.cs)
- [ ] [docs/MCP_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/MCP_API.md)

Suggested tool names:

- `listLogSources`
- `listLogFiles`
- `readLogFile`
- `deleteLogFile`

Acceptance criteria:

- an MCP client authenticated as admin can enumerate sources and files
- bounded reads return usable text responses
- delete behavior matches REST behavior

Developer notes:

-

## Phase 5: Update Postman and Docs

Goal: Keep human and machine-facing docs aligned with the implementation.

Checklist:

- [ ] Add a `Logs` folder to Postman
- [ ] Add collection variables for source kind, source id, and file path
- [ ] Document the new REST routes and response shapes
- [ ] Document the dashboard logs page and worker quick links
- [ ] Document the Docker storage expectation for worker logs
- [ ] Update README summary material where appropriate

Expected file touches:

- [ ] [Tempo.postman_collection.json](/abs/path/C:/Code/Tempo/Tempo/Tempo.postman_collection.json)
- [ ] [docs/REST_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/REST_API.md)
- [ ] [docs/MCP_API.md](/abs/path/C:/Code/Tempo/Tempo/docs/MCP_API.md)
- [ ] [docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md](/abs/path/C:/Code/Tempo/Tempo/docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md)
- [ ] [README.md](/abs/path/C:/Code/Tempo/Tempo/README.md)
- [ ] [docker/factory/README.md](/abs/path/C:/Code/Tempo/Tempo/docker/factory/README.md)

Documentation requirements:

- describe the admin-only authorization model
- describe safe bounded read semantics
- describe delete restrictions
- show at least one REST example and one MCP example
- describe how worker log visibility depends on the Docker storage layout

Developer notes:

-

## Phase 6: Add Tests and Coverage Pass

Goal: Add enough coverage that this feature is defensible and maintainable.

Checklist:

- [ ] Add a backend suite for log management
- [ ] Cover route auth, happy path, and failure modes
- [ ] Cover path traversal rejection
- [ ] Cover missing source and missing file behavior
- [ ] Cover delete allowed and delete denied cases
- [ ] Cover OpenAPI registration for the new routes
- [ ] Add a frontend test harness for the dashboard if one is still missing
- [ ] Add UI tests for `LogsView`
- [ ] Add MCP tests or focused route/schema assertions for MCP tools
- [ ] Run a targeted coverage pass and document gaps

Backend test targets:

- [ ] admin required
- [ ] list sources
- [ ] list files for server source
- [ ] list files for worker source
- [ ] read bounded content
- [ ] reject traversal and invalid path values
- [ ] reject unknown source
- [ ] delete non-current eligible file
- [ ] reject delete of current/active file if blocked by policy

Frontend harness work:

- [ ] add `vitest`
- [ ] add `@testing-library/react`
- [ ] add `jsdom`

Frontend test targets:

- [ ] `LogsView` initial render
- [ ] source selection
- [ ] file selection and content load
- [ ] worker quick-link routing from Workers view
- [ ] delete button state and confirmation flow
- [ ] empty and error states

Suggested new or updated files:

- [ ] `src/Test.Shared/Suites/LogManagementSuite.cs`
- [ ] [src/Test.Shared/TempoSuites.cs](/abs/path/C:/Code/Tempo/Tempo/src/Test.Shared/TempoSuites.cs)
- [ ] [src/Test.Xunit/Test.Xunit.csproj](/abs/path/C:/Code/Tempo/Tempo/src/Test.Xunit/Test.Xunit.csproj) if coverage tooling is added there
- [ ] dashboard test config and package updates

Coverage guidance:

- aim for near-100% coverage on the new logging slice and directly touched code paths
- do not claim repo-wide literal 100% unless that broader campaign is actually completed
- document any intentionally uncovered edge cases

Developer notes:

-

## Phase 7: Final Validation

Goal: Validate the feature end to end in development and Docker.

Checklist:

- [ ] local build passes
- [ ] shared/xUnit test suite passes
- [ ] dashboard build passes
- [ ] dashboard tests pass
- [ ] Docker deployment starts cleanly
- [ ] server logs are visible in the dashboard
- [ ] worker logs are visible in the dashboard
- [ ] MCP tools work against the running Docker deployment
- [ ] Postman requests work against the running Docker deployment
- [ ] docs match shipped behavior

Suggested verification commands:

```powershell
dotnet build .\src\Tempo.sln
dotnet test .\src\Test.Xunit\Test.Xunit.csproj
npm.cmd --prefix .\dashboard run build
```

If dashboard tests are added:

```powershell
npm.cmd --prefix .\dashboard test
```

Docker verification:

```powershell
docker compose -f .\docker\compose.yaml up -d
docker ps -a
```

Manual checks:

- open `/dashboard/logs`
- open a worker from `/dashboard/workers` and jump to its logs
- read a bounded tail of the current server log
- read a bounded tail of one worker log
- attempt deletion of an allowed stale file
- verify deletion of a protected/current file is rejected if that policy is implemented

Developer notes:

-

## Acceptance Summary

The feature is complete when all of the following are true:

- [ ] worker logs are stored on stable, server-visible file-backed storage
- [ ] `Tempo.Server` exposes admin-only REST routes for source list, file list, read, and delete
- [ ] the routes appear in OpenAPI
- [ ] the dashboard has a dedicated Logs page
- [ ] the Workers page links directly to worker logs
- [ ] MCP has typed log tools
- [ ] Postman includes a Logs folder
- [ ] docs describe the feature accurately
- [ ] tests cover the new logging slice with strong backend and UI coverage

## Risks and Mitigations

### Risk: Worker logs remain container-local

Impact:

- the server cannot enumerate worker logs safely
- the dashboard feature becomes partial or misleading

Mitigation:

- solve storage normalization first in Phase 1

### Risk: Unsafe file access

Impact:

- arbitrary file disclosure or deletion

Mitigation:

- canonical path resolution
- configured-root-only access
- no absolute client paths
- no path traversal

### Risk: Over-scoping into streaming/log aggregation

Impact:

- delays delivery and increases operational complexity

Mitigation:

- keep v1 polling-based and file-backed
- defer live streaming and aggregation

### Risk: Weak UI coverage

Impact:

- regressions in source/file selection and worker deep-link flows

Mitigation:

- add a proper dashboard test harness in this effort

## Open Questions

Track unresolved product or implementation questions here.

- [x] Should deleting the currently active server log always be rejected, or allowed when the file can be reopened safely by the runtime?  It should be allowed, the server/worker will re-create the file if necessary
- [x] Should the API support raw download in v1, or is bounded text read sufficient?  Yes, support raw download, and expose that through the dashboard also.  The user should be able to see all of the available log files, and download or view the log file through the dashboard, API, MCP, Postman, etc.
- [x] Should dashboard logs remain admin-only even when tenant admins exist later?  Dashboard logs are not in scope.

## Completion Notes

Use this section when the work is done.

- Implementation PR:
- Validation summary:
  - `dotnet test .\src\Test.Xunit\Test.Xunit.csproj` passed with 205/205 tests green after the log-viewer backend and integration coverage landed.
  - `npm.cmd --prefix .\dashboard run test` passed with the new `LogsView` coverage.
  - `npm.cmd --prefix .\dashboard run build` passed for the dashboard bundle.
  - `dotnet build .\src\Tempo.sln` passed while the feature was in development.
  - A live Docker/Postman smoke was not re-run in this implementation pass; use the Phase 7 manual checks when validating against a running deployment.
- Follow-up work:
  - Optional future enhancement: live streaming or server-mediated `docker logs` support. This remains intentionally out of scope for the v1 file-backed viewer.
