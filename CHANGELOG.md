# Changelog

All notable changes to Tempo are documented in this file.

## [Unreleased]

## [0.5.1]

### Changed

- NuGet dependency updates: Padlock 1.1.0 -> 1.2.0, RestWrapper 3.3.0 -> 3.3.1, SyslogLogging 2.2.2 -> 2.3.1, Watson 7.2.0 -> 7.2.2, Microsoft.Data.SqlClient 7.1.0 -> 7.1.1, Voltaic 2.0.0 -> 2.2.1, Touchstone (Core, Cli, XunitAdapter, NunitAdapter) 0.1.12 -> 0.2.0, NUnit 4.6.1 -> 5.0.0
- Voltaic 2.2.1 behavior now reflected in the MCP surface and `McpTransport` suite: TCP and WebSocket clients initialize automatically on connect, `tools/call` arguments that fail the input schema return an `isError` tool result naming the property (previously JSON-RPC `-32602`), and `ping` under the stateless `2026-07-28` revision returns `-32601`
- `RuntimeCommandProbe` waits up to 10 seconds (was 3) for runtime probes such as `dotnet --info`, drains the probe's output while waiting, and no longer caches a timed-out probe, so a briefly slow host no longer marks `Artifact.DotnetProcess` unavailable until restart
- All projects under `src/` and the dashboard are versioned 0.5.1

### Fixed

- `Artifact.Process` reported a step as an exception ("Broken pipe") when the process exited without reading its stdin request, even though it wrote a valid result; the result is now decided by the exit code and stdout
- Added `src/Tempo.Core/Artifacts/` sources to the repository (previously hidden by the `.gitignore` rule fixed in 0.5.0) and cleared their two nullable warnings

### Tests

- `ArtifactProcess.ProcessIgnoringStdinSucceeds` covers a process that ignores a 512 KiB request (larger than the pipe buffer)
- `ArtifactProcess.PythonDependencyInstallPolicyFailure` is skipped when Python is not on the PATH, like the other Python cases
- The hydration sample-step assertion includes the step's exception message

## [0.5.0]

### Added

- Built-in observability across the platform. See [TELEMETRY.md](TELEMETRY.md) for the full catalog.
  - The `Tempo` library and `Tempo.Core` emit metrics and traces on a `Tempo` meter and activity source (`System.Diagnostics` only, no exporter dependency). All names are in `Tempo.Telemetry.TelemetryConstants`, and typed best-effort recorders are in `Tempo.Telemetry.TempoTelemetry`.
  - Flow and step runs (by runner and outcome, plus transitions), every dispatch pipeline stage (queued, plan, select, assign, execute or hand-off, complete) and the flow resolve, validate, and prepare stages, scheduling decisions, completions, recoveries, the queue depth, the executor and worker pools, worker sessions and protocol frames, worker assignment outcomes, external-process capacity and kills, artifact-package and Python-environment caches, internal limiter waits (coordinator gate, SQLite write lock), background and start-up tasks with last-success timestamps, authentication and authorization outcomes, MCP tool calls, handled errors, build info, and safe configuration gauges.
  - Client spans and integration metrics for every outbound call: all four database drivers (operation name only, never SQL text), REST steps, artifact subprocesses, C# source-step `dotnet publish`, worker artifact downloads, and MCP calls to the Tempo API.
  - W3C context propagation across every boundary: inbound HTTP (Watson), the enqueue-to-scheduler hand-off, the server-to-worker websocket (`traceParent` and `traceState` on the `assign` frame and on `run-completed`), outbound HTTP, and artifact subprocesses (`TRACEPARENT` and `TRACESTATE` environment variables).
- `src/Tempo.Hosting` (`TelemetryHost`): one Radiant host per process in `Tempo.Server`, `Tempo.Worker`, and `Tempo.McpServer`. It subscribes to `Tempo`, `Watson`, and `System.Net.Http`, exports OTLP (default `http://127.0.0.1:4317`), optionally serves Prometheus in-process, includes .NET runtime metrics, applies seconds buckets to Watson's HTTP duration histogram, and forwards server and worker log lines as trace-correlated OpenTelemetry logs. The server attaches the log bridge only after start-up hydration so the default credential is never exported.
- `telemetry` settings block (`TelemetrySettings`) for the server, worker, and MCP server, with `TEMPO_TELEMETRY_*` environment overrides, and `engine.queueDepthSampleIntervalMs`
- Observability stack in `docker/compose.yaml`: OpenTelemetry Collector, Prometheus, Grafana Tempo, Loki, and Grafana (host port 3001), with healthchecks, ordered startup, datasources provisioned with stable UIDs and trace-to-log links, and seven dashboards in a `Tempo` folder (`assets/grafana`): Overview, HTTP, Dispatch Pipeline, Flows & Steps, Workers & Capacity, Integrations, and Background Tasks & Runtime
- Dashboard home page **External services** card that links to Grafana, Prometheus, Grafana Tempo, and Loki, with copyable URLs, local-development credentials, and a reachability indicator (translated for every supported locale)
- `Telemetry` test suite (18 cases) proving emission for each instrumented area, failure paths, cross-process propagation, host export (Prometheus and OTLP), and bounded metric labels

### Changed

- REST steps now pass the step's cancellation token to the outbound HTTP call, so a step timeout also cancels the request
- Watson telemetry (`Settings.Telemetry`: metrics, traces, context propagation) is set explicitly on in `Tempo.Server`
- All projects under `src/` and the dashboard are versioned 0.5.0

### Fixed

- Tempo.McpServer rejected every tool call on Linux and macOS (including the Docker image) with "API path must be relative", because `Uri.TryCreate` treats `/v1.0/...` as an absolute `file://` URI on Unix
- `.gitignore` excluded `src/Tempo.Core/Artifacts/` on case-insensitive filesystems through the unanchored `artifacts/` rule. The rule is now root-anchored (`/artifacts/`)
- Added the missing `navigation.discord` dashboard translation for every non-English locale

### Earlier changes included in this release

#### Added

- Dashboard internationalization across login, navigation, workspace titles/subtitles, tables, filters, buttons, modal labels, hover/help text, shared chrome, and status/enum surfaces for the supported ship locales (`en`, `es`, `zh-Hans`, `yue-Hant-HK`, `ja`, `de`, `fr`, `it`, `zh-Hant-TW`)
- Dashboard i18n audit enforcement so extracted UI strings must be present for supported non-English locales and new raw localizable JSX text fails test coverage
- Explicit flow-level HTTP trigger invocation modes for public versus API-authenticated data flows, centered on the `invocationAuthMode` contract (`Public` and `ApiAuthenticated`)
- `McpTransport` test suite that boots an in-process Tempo.Server plus every Tempo.McpServer transport and drives it the way MCP clients do. It covers the handshake version cap, session and stateless (`2026-07-28`) Streamable HTTP `tools/list`/`tools/call`, invalid-argument errors, TCP and WebSocket tool discovery, and the installer URL. It also covers the reported server version, wildcard bind-host URLs, the admin API key `/me` principal, `isError` tool failures, and `localhost` connection latency. Voltaic 2.0.0 cases assert that every transport lists exactly the Tempo tools, that `ping` returns an empty result, that demo tools cannot be called, and that HTTP rejects bare tool-method calls
- `McpEndpointUrls` helper that derives connectable client URLs for every Tempo.McpServer transport from the bind settings

#### Changed

- Updated NuGet dependencies solution-wide:
  - `Voltaic` 0.6.0 to 2.0.0 (through 1.1.0; see the Voltaic `MIGRATE_V1_TO_V2.md` guide)
  - `Watson` 7.1.0 to 7.2.0
  - `RestWrapper` 3.2.0 to 3.3.0
  - `Padlock` 1.0.4 to 1.1.0
  - `SyslogLogging` 2.2.1 to 2.2.2
  - `Microsoft.Data.Sqlite` 10.0.11 to 10.0.12
  - `Microsoft.Data.SqlClient` 7.0.2 to 7.1.0
  - `PrettyId` 2.0.0 to 2.0.1 (C# SDK)
  - `Microsoft.NET.Test.Sdk` 18.9.0 to 18.10.1
  - `NUnit3TestAdapter` 6.2.0 to 6.3.0
- Tempo.McpServer inherits Voltaic 1.1.0 protocol behavior: `initialize` negotiates at most `2025-11-25` on every transport, and stateless `2026-07-28` results carry `resultType` (plus `ttlMs`/`cacheScope` on list results)
- Tempo.McpServer inherits Voltaic 2.0.0 behavior. `tools/list` now returns only Tempo tools: Voltaic's demo tools (`ping`, `echo`, `getTime`, `getSessions`, `getClients`) are no longer published, which also removes `getSessions`, a tool that exposed every client's `Mcp-Session-Id`. The protocol `ping` returns `{}` instead of `"pong"`. On the HTTP transport a Tempo tool is reachable only through `tools/call`, so a bare JSON-RPC call to a tool name returns `-32601`. The TCP and WebSocket transports still register each tool as a direct method on purpose. The servers are constructed without the removed `includeDefaultMethods` argument
- Tempo MCP tool handlers now run asynchronously and honor transport cancellation instead of blocking on the Tempo REST call
- The Tempo.McpServer startup banner now prints the Streamable HTTP client URL (`/mcp`) and labels `/rpc` as legacy
- Tempo.McpServer's default Tempo endpoint is now `http://127.0.0.1:8901`, matching Tempo.Server's default IPv4 loopback bind and Tempo.Worker's default
- `GET /v1.0/me` now returns `{ "type": "adminApiKey", "id": "admin-api-key", "isAdmin": true }` for the global admin API key; it used to return `{ "type": "anonymous" }`

#### Fixed

- Claude Code (2.1.x, stateless `2026-07-28` MCP revision) listed zero Tempo tools. The `install` command now configures `http://<host>:<port>/mcp`, the Streamable HTTP endpoint, instead of the legacy `/rpc` endpoint, which does not return the stateless result shape. Existing installs should re-run `install` or change the URL to `/mcp`
- The TCP and WebSocket MCP transports registered Tempo tools only as raw JSON-RPC methods, so standard MCP clients saw no Tempo tools in `tools/list` and `tools/call` failed with "tool not found". Tools are now registered as MCP tools on every transport, and direct method invocation still works
- Tempo.McpServer hard-coded its version in `serverInfo.version` and `softwareVersion`, so it drifted from the project `Version` whenever that changed. The version is now read from the built assembly
- Tempo.Server's `GET /` reported a hard-coded `version` of `0.1.0`. It now reports the Tempo.Server assembly version
- Every MCP tool call took about 2 seconds on Windows when the Tempo endpoint used `localhost`: the connection tried `::1` first, was refused, and fell back to IPv4, and Tempo.Server's `Connection: close` repeated that on every call. The API client now connects to loopback hosts over IPv4 first (about 2,000 ms to 60-150 ms per call)
- Tool execution failures (Tempo.Server unreachable, timeouts, invalid argument values) surfaced as opaque JSON-RPC `-32603 Internal error` responses. They now return MCP `isError` tool results with an actionable message; direct JSON-RPC method calls on TCP/WebSocket still return JSON-RPC errors
- Client URLs from `install` and the startup banner were unusable for wildcard bind hosts, for example `http://*:8910/mcp` with the Docker config. Wildcards now map to `127.0.0.1` and IPv6 literals are bracketed
- `ArtifactProcess` "kills the child process when execution is cancelled" test was flaky under load: its fixed 200 ms cancellation could fire before the child process was spawned. It now cancels only after the process holds its capacity lease

#### Documentation

- README coverage for dashboard internationalization, including supported ship locales, operator-facing scope, and audit enforcement
- README coverage for authenticated versus public data flows, including trigger invocation behavior and generated `curl` expectations
- Changelog coverage for the `invocationAuthMode` model so public and API-authenticated flow behavior is called out alongside other platform capabilities
- Archived the working `I18N.md` and `SCALE.md` planning documents under `archive/`
- `docs/MCP_API.md` and README now point MCP clients at the Streamable HTTP `/mcp` endpoint, list `/rpc` as legacy, and describe per-transport tool exposure and protocol-version negotiation
- `docs/REST_API.md` documents the `/v1.0/me` principal types; `docs/MCP_API.md` documents the `127.0.0.1` default endpoint, wildcard-host handling, the legacy `/events` path, and the `tempo_me` admin API key principal
- Version references across the projects, README, MCP settings, and docs are aligned on `0.3.0`; an interim bump to `0.4.0` was reverted and the compose file stays pinned to `v0.3.0` images

## [0.3.0] - 2026-04-21

### Added

- `Tempo.Worker`, the first-party distributed execution worker daemon for whole-flow-run remote execution
- Distributed execution schema and contracts, including workers, worker sessions, run assignments, worker activity, and server instance heartbeats
- `FlowRunExecutionPlan`, remote worker transport, authenticated artifact download for workers, and worker token rotation
- Worker management REST routes:
  - `GET /v1.0/workers`
  - `GET /v1.0/workers/{id}`
  - `POST /v1.0/workers/{id}/drain`
  - `POST /v1.0/workers/{id}/resume`
  - `POST /v1.0/workers/{id}/rotate-token`
- Dashboard worker management with worker list/detail views, drain/resume actions, run placement columns, and distributed execution settings
- Dashboard, REST, MCP, and Postman log-management surfaces for file-backed server and worker logs
- MCP worker tools:
  - `listWorkers`
  - `readWorker`
  - `drainWorker`
  - `resumeWorker`
  - `listLogSources`
  - `listLogFiles`
  - `readLogFile`
  - `downloadLogFile`
  - `deleteLogFile`
- C# SDK worker-protocol DTOs
- Operator documentation for distributed execution plus a formal worker-protocol reference
- Worker Docker assets, compose wiring, and `build-worker.bat`

### Changed

- Replaced the legacy queue-claim worker path with a single authoritative `RunDispatchCoordinator`
- Server-local execution now runs through the same execution-plan and assignment path as remote workers via a pseudo-worker
- `flow_runs` now persist dispatch and placement metadata such as `dispatchState`, `dispatchAttempt`, `assignedWorkerId`, `runAssignmentId`, and `executionNodeKind`
- Engine scheduling settings now include:
  - `serverCanExecuteWorkload`
  - `loadBalancingStrategy`
  - `workerHeartbeatTimeoutMs`
  - `leaseDurationMs`
  - `maxAssignmentAttempts`
  - `allowDuplicateScheduler`
- Docker compose now includes a worker and uses `v0.3.0` image tags
- Docker compose now mounts shared worker log storage read-only into the server for the admin log viewer
- Project, package, dashboard, and MCP version strings now align on `0.3.0`

### Fixed

- Queue ownership and queued-run cancellation now route through one authority instead of multiple direct mutation paths
- Worker disconnects, stale leases, and duplicate completion frames now recover without leaving stale assignments applied twice
- Split-brain protection now suppresses scheduling on a second live server by default
- Operator visibility gaps around worker state and run placement
- Compose first-run behavior now restores the intended server and worker defaults instead of falling back to generated localhost or empty-admin-key settings when config volumes are absent
- Factory resets now restore artifact and log storage layouts that survive container recreation and support the admin log viewer

## [0.2.0] - 2026-04-20

### Added

- `Tempo.McpServer`, a C# MCP bridge built on Voltaic with HTTP, TCP, and WebSocket transports plus an install workflow for local MCP client setup
- Artifact-backed runtimes for `Artifact.Process`, `Artifact.Python`, `Artifact.JavaScript`, and `Artifact.DotnetProcess`
- Source-step creation for Python, JavaScript, and C# so a user can paste code into the UI or API and run it as a step
- Mutable artifact package editing in the dashboard, including per-file browsing and editing
- First-run setup wizard that creates:
  - an echo source step
  - an echo flow and trigger
  - a chained flow that generates a random number and doubles it
  - sample invocations with response body and response header inspection
- Runtime-aware startup seeding for one working sample step per available runtime type, including sample artifacts where needed
- REST and MCP reference docs, best-practices guidance, runtime provider guidance, and refreshed SDK documentation
- Docker assets:
  - `docker/compose.yaml`
  - per-component Dockerfiles
  - root image build scripts for server, dashboard, and MCP
- `publish-nuget.bat` for `Tempo` and the C# SDK, including symbol package publishing

### Changed

- Runtime config schemas in `/openapi.json` now emit concrete `oneOf` contracts instead of a flattened approximation
- Public HTTP trigger responses now return the final step payload in the response body and move execution metadata into response headers
- Identifier generation now uses PrettyId K-sortable IDs with a `{prefix}_{ksort}_{random}` shape and a maximum length of 32 characters
- The dashboard now includes page titles and subtitles across workspaces, better initial flow organization, and independent sidebar/workspace scrolling
- The API Explorer now reads the generated OpenAPI document and renders a more usable operation picker and request editor
- The setup wizard now explains each step in terms of what the user is doing, why it matters, and how it maps to Tempo concepts
- Server runtime command resolution now uses configurable executable names or paths for Node.js, Python, and `dotnet`
- Server-side projects target `net10.0`; the C# SDK targets `net8.0` and `net10.0`
- Docker image references now use `v0.2.0` tags in Compose

### Fixed

- Built-in runtime reconciliation so seeded built-in method samples no longer appear orphaned when the backing method exists
- Deletion protection across linked resources, including preventing step deletion when the step is still referenced by a data flow
- Cascade-delete behavior across persisted entities so deletes respect references and protected rows correctly
- Setup wizard consistency issues around naming, trigger methods, request examples, response presentation, and refresh after creation
- Request history and setup-wizard response displays so response headers and response body are presented separately
- Dashboard artifact file editing for source files, plus layout issues in the artifact package editor
- Log message punctuation normalization so Tempo log messages no longer end with terminal periods
- The `CA2022` warning in `HydrationSuite`

### Packaging and Tooling

- Normalized project and package versions to `0.2.0`
- Updated the C# SDK package metadata and symbol packaging
- Refreshed the Postman collection to match the current runtime model, source-step flows, trigger surface, and request history behavior
- Added root build helpers:
  - `build-server.bat`
  - `build-mcp.bat`
  - `build-dashboard.bat`

### Documentation

- Rewrote the root README to match the current repository shape and release contents
- Added or refreshed:
  - `docs/REST_API.md`
  - `docs/MCP_API.md`
  - `docs/BEST_PRACTICES.md`
  - SDK READMEs
  - operator/runtime docs under `docs/`

## [0.1.0] - 2026-04-18

Initial Tempo release with the core workflow engine, class/method/REST step execution, multi-tenant flow management, triggers, metrics, and test coverage.
