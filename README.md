<div align="center">
  <img src="https://raw.githubusercontent.com/jchristn/tempo/main/assets/logo.png" width="182" height="182" alt="Tempo logo">
</div>

# Tempo

> **Note**  
> v0.3.0 - Tempo is in ALPHA - API surface and data structures subject to change

[![NuGet](https://img.shields.io/nuget/v/Tempo.svg)](https://www.nuget.org/packages/Tempo/)
[![NuGet Tempo.Sdk](https://img.shields.io/nuget/v/Tempo.Sdk.svg)](https://www.nuget.org/packages/Tempo.Sdk/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

Tempo is a workflow automation platform for building, running, and monitoring tenant-scoped data flows. A flow is made of reusable steps, invoked through triggers, persisted with artifacts, and observed through run history, request history, OpenAPI, and MCP.

Tempo ships as:

- `Tempo`: the core workflow/orchestration library
- `Tempo.Core`: server-facing persistence, runtime, settings, and management contracts
- `Tempo.Server`: the REST API host
- `Tempo.Worker`: the first-party distributed execution worker daemon
- `Tempo.McpServer`: an MCP facade over Tempo.Server built on Voltaic
- `dashboard/`: a React/Vite operator UI
- `sdk/csharp`, `sdk/js`, and `sdk/python`: artifact runtime SDKs with exhaustive test apps

## Highlights

- Tenant-scoped CRUD for data flows, steps, triggers, runs, artifacts, users, credentials, roles, and permissions
- Flow-level invocation auth modes so HTTP-triggered data flows can be public bearer-capability endpoints or require normal Tempo API authentication (`Public` vs `ApiAuthenticated`)
- Runtime model that supports `Builtin.Class`, `Builtin.Method`, `External.Rest`, `Artifact.Process`, `Artifact.Python`, `Artifact.JavaScript`, `Artifact.DotnetProcess`, and `Host.Executable`
- Source-step creation from the UI or API for Python, JavaScript, and C#
- Mutable artifact packages with dashboard file browsing and in-place editing
- Runtime-aware startup seeding that creates working sample steps for each available runtime type
- Distributed execution with a control-plane/server split, authenticated workers, worker drain/resume/block control, and capability-aware run placement
- Run placement metadata, worker management REST routes, dashboard worker views, and MCP worker tools
- Durable per-run log capture with tenant-scoped run activity and run-log APIs plus dashboard run-log viewing
- Admin log viewer for file-backed server and worker logs in the dashboard, REST API, MCP, and Postman
- OpenAPI-backed API Explorer and MCP server for agent-driven automation
- First-run setup wizard that creates and invokes example flows end to end
- Dashboard internationalization across login, shell navigation, workspace headers, tables, filters, modals, and hover/help text, with locale selection, locale-aware formatting, generated locale resources, and audit enforcement for supported ship locales
- K-sortable PrettyId identifiers with fixed prefixes and a maximum length of 32
- Docker Compose deployment, image build scripts, and NuGet publish script

## Quick Start

### Docker Compose

From the repository root:

```powershell
docker compose -f .\docker\compose.yaml up -d
```

Default endpoints:

- Dashboard: `http://localhost:3000`
- Tempo.Server: `http://localhost:8901`
- Tempo.Worker: included in the compose stack as `tempo-worker-1`, `tempo-worker-2`, and `tempo-worker-3`
- Tempo.McpServer HTTP RPC: `http://127.0.0.1:8910/rpc`
- Tempo.McpServer TCP: `127.0.0.1:8911`
- Tempo.McpServer WebSocket: `ws://127.0.0.1:8912/mcp`

Default seeded credentials on an empty database:

- Email: `admin@tempo.local`
- Password: `password`
- Local admin API key: `tempo-local-admin-api-key`

Compose bind-mounts `docker/tempo.server.json` and `docker/tempo.worker.json` so first-run deployments use the intended control-plane and worker settings without depending on pre-seeded config volumes. Persistent named volumes remain in place for the server database, server artifact blob storage, server logs/runtime cache/scratch, shared worker logs, shared run logs, dashboard logs, and MCP configuration. Worker runtime-cache and scratch paths remain container-local anonymous volumes so scaled workers do not share mutable runtime state, while worker log files are written to a shared named volume that `Tempo.Server` mounts read-only for the admin log viewer. Per-run logs are written to a separate shared volume mounted read-write by the server and workers so run logs survive container restarts and remain visible through the `Runs` view and tenant-scoped run-log APIs. The service images in the compose file are pinned to `v0.3.0`.

### Distributed Execution Model

Tempo v0.3.0 splits the platform into:

- `Tempo.Server` as the control plane for REST, MCP, scheduling, persistence, worker management, and authenticated artifact download
- `Tempo.Worker` as the execution plane for assigned flow runs

The server can still participate in execution through the local pseudo-worker controlled by `engine.serverCanExecuteWorkload`. Placement is whole-flow-run based, with `LeastLoaded` and `LabelPinned` strategies.

### Local Development

Recommended prerequisites:

| Tool | Required for |
| --- | --- |
| .NET 10 SDK | `Tempo`, `Tempo.Core`, `Tempo.Server`, `Tempo.McpServer`, tests |
| Node.js | Dashboard development and `Artifact.JavaScript` runtime |
| Python 3 | `Artifact.Python` runtime |
| `dotnet` command | `Artifact.DotnetProcess` runtime and C# source-step packaging |

Tempo.Server does not fail startup when optional runtime commands are unavailable. Instead, those runtimes are surfaced as unavailable in the runtime catalog and their startup template steps are skipped. Configure command names or absolute paths in `tempo.json` under `runtimes.externalExecution`.

Build and run:

```powershell
dotnet build .\src\Tempo.sln
dotnet run --project .\src\Tempo.Server\Tempo.Server.csproj
dotnet run --project .\src\Tempo.Worker\Tempo.Worker.csproj
dotnet run --project .\src\Tempo.McpServer\Tempo.McpServer.csproj

cd .\dashboard
npm install
npm run dev
```

Helper scripts at the repository root:

- `build-server.bat v0.3.0`
- `build-worker.bat v0.3.0`
- `build-mcp.bat v0.3.0`
- `build-dashboard.bat v0.3.0`
- `publish-nuget.bat <nuget-api-key>`

## Core Concepts

| Concept | Purpose |
| --- | --- |
| Step | A reusable execution unit bound to a runtime and an execution key |
| Data flow | A directed workflow that chains steps through success, failure, and exception edges |
| Trigger | A reusable entry point that invokes a data flow |
| Artifact | A mutable package of files used by artifact-backed runtimes |
| Run | One execution of a data flow |
| Request history | Captured inbound HTTP traffic, response bodies, headers, and summary buckets |

Flows reference steps by `executionKey`, not by step record ID. This keeps flow definitions stable even when the step row is edited or replaced.

Each flow also controls how its HTTP trigger is invoked through `invocationAuthMode`:

- `Public` - anyone with the trigger URL can invoke the flow
- `ApiAuthenticated` - the caller must present normal Tempo API credentials and be allowed to act on the flow's tenant

The dashboard surfaces this in the Data Flows workspace as explicit public versus API-authenticated trigger choices.

## Dashboard Internationalization

The dashboard now ships with operator-facing internationalization wired through the core UI surface:

- language selection before authentication on the login page and after authentication in the topbar
- persisted locale preference via `tempo.locale`
- locale-aware formatting for dates, times, numbers, durations, byte sizes, booleans, and lists
- localized workspace titles and subtitles, table headers, buttons, modal labels, filters, tooltips, status/enum chips, and shared navigation chrome
- generated locale resources plus an audit test that fails CI if new raw localizable UI text or unsupported English fallback is introduced

Supported ship locales in the dashboard selector are:

- `en`
- `es`
- `zh-Hans`
- `yue-Hant-HK`
- `ja`
- `de`
- `fr`
- `it`
- `zh-Hant-TW`

If product or operators refer to "Kanji" as a selector label, the locale registry treats it as an alias for `ja` rather than a separate language.

## Authenticated And Public Data Flows

Tempo supports two HTTP-trigger invocation modes at the data-flow level:

| `invocationAuthMode` | Behavior | Typical use |
| --- | --- | --- |
| `Public` | Anyone with the trigger URL can invoke the flow | Webhooks, low-friction inbound automation, capability-URL patterns |
| `ApiAuthenticated` | Caller must supply standard Tempo API credentials and have access to the flow tenant | Internal automations, tenant-private flows, operator-driven integrations |

The generated `curl` guidance in the dashboard follows the selected mode. Public flows generate a bare trigger call, while API-authenticated flows add an `Authorization: Bearer ...` header placeholder.

This distinction is carried through the dashboard UX: the Data Flows workspace exposes the run policy explicitly, the API Explorer and trigger guidance reflect the expected auth shape, and route-level docs call out whether a trigger should be treated as a capability URL or a tenant-authenticated endpoint.

## Runtime Keys

| Runtime key | Purpose |
| --- | --- |
| `Builtin.Class` | Executes a registered `Tempo.Step` subclass |
| `Builtin.Method` | Executes a registered `[StepMethod]` method |
| `External.Rest` | Executes a persisted outbound HTTP request |
| `Artifact.Process` | Executes a package-local process that speaks Tempo protocol v1 |
| `Artifact.Python` | Executes a Python handler from an artifact package |
| `Artifact.JavaScript` | Executes a Node.js handler from an artifact package |
| `Artifact.DotnetProcess` | Executes a .NET handler from an artifact package using the Tempo SDK host and `TempoStepHandlerBase` helpers |
| `Host.Executable` | Executes an operator allowlisted host executable |

`Legacy.InlineRest` remains a compatibility read path. New REST steps should use `External.Rest`.

## First-Run Experience

On an empty database, Tempo seeds:

- A default tenant, administrator, tenant user, and credential
- Four protected tenant roles: `Administrator`, `Editor`, `Operator`, and `ReadOnly`
- Built-in runtime sample steps
- Artifact-backed sample steps and sample artifacts for every available artifact runtime
- A host executable sample only when a host allowlist entry is enabled

The dashboard opens a setup wizard on first access. The wizard explains what Tempo is about to create, then creates:

- An echo step packaged from source
- An echo flow and POST trigger
- A chained flow that generates a random number and doubles it
- A GET trigger for the chained flow
- Sample invocations that show both response bodies and response headers

Every workspace in the dashboard includes a page title and subtitle, and sidebar scrolling is independent from workspace scrolling.

## HTTP Trigger Invocation And Response Contract

HTTP trigger routes are:

```text
/v1.0/triggers/http/{triggerId}
```

Flows default to public trigger invocation, where the trigger ID acts as a bearer capability. Set the flow field `invocationAuthMode` to `ApiAuthenticated` when trigger calls should require normal Tempo API credentials and tenant access.

For successful trigger execution:

- The HTTP response body is the final step output body
- Execution metadata is returned in headers, not mixed into the JSON body

Current response metadata headers include:

- `x-tenant-id`
- `x-worker-id` when the run has been assigned
- `x-run-id`
- `x-dataflow-id`
- `x-trigger-id`
- `x-run-state`
- `x-run-created-utc`
- `x-run-started-utc`
- `x-run-completed-utc`
- `x-run-last-update-utc`
- `x-runtime-ms`
- `x-run-error` when applicable

## SDKs

Tempo includes SDKs for artifact-backed handlers:

- [sdk/csharp/README.md](sdk/csharp/README.md)
- [sdk/js/README.md](sdk/js/README.md)
- [sdk/python/README.md](sdk/python/README.md)

Notes:

- The C# SDK targets `net8.0` and `net10.0`
- The server-side projects target `net10.0`
- Each SDK ships with a test application intended to exercise the public API surface exhaustively
- The SDKs expose ambient execution context and file-backed step logging so handler code can write diagnostics without corrupting protocol stdout

## Mutable Artifacts and Source Steps

Artifacts are file packages, not opaque zip-only deployment units. Tempo stores package contents in a way that supports:

- Uploading artifacts and versions
- Editing individual files in the dashboard
- Creating steps directly from pasted Python, JavaScript, or C# source
- Reusing one artifact across multiple steps and versions

For artifact-backed runtimes and manifests, see [docs/ARTIFACT_MANIFEST.md](docs/ARTIFACT_MANIFEST.md).

## APIs and Documentation

Primary reference material:

- [docs/REST_API.md](docs/REST_API.md)
- [docs/MCP_API.md](docs/MCP_API.md)
- [docs/BEST_PRACTICES.md](docs/BEST_PRACTICES.md)
- [docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md](docs/DISTRIBUTED_EXECUTION_OPERATOR_GUIDE.md)
- [docs/WORKER_PROTOCOL.md](docs/WORKER_PROTOCOL.md)
- [Tempo.postman_collection.json](Tempo.postman_collection.json)

Additional operator and implementation guides:

- [docs/RUNTIME_PROVIDER_AUTHORING.md](docs/RUNTIME_PROVIDER_AUTHORING.md)
- [docs/ARTIFACT_MANIFEST.md](docs/ARTIFACT_MANIFEST.md)
- [docs/PROTOCOL_V1.md](docs/PROTOCOL_V1.md)
- [docs/PYTHON_ARTIFACT_QUICKSTART.md](docs/PYTHON_ARTIFACT_QUICKSTART.md)
- [docs/EXTERNAL_EXECUTION_OPERATOR_GUIDE.md](docs/EXTERNAL_EXECUTION_OPERATOR_GUIDE.md)
- [docs/SECURITY_TRUST_BOUNDARIES.md](docs/SECURITY_TRUST_BOUNDARIES.md)
- [docs/INLINE_REST_MIGRATION.md](docs/INLINE_REST_MIGRATION.md)
- [docs/DASHBOARD_ARTIFACTS_RUNTIMES.md](docs/DASHBOARD_ARTIFACTS_RUNTIMES.md)

Archived planning docs:

- [archive/I18N.md](archive/I18N.md)
- [archive/SCALE.md](archive/SCALE.md)

OpenAPI is exposed at:

```text
http://localhost:8901/openapi.json
```

Runtime configuration schemas in OpenAPI use `oneOf`, which keeps the API Explorer and generated clients aligned with the concrete runtime config being used.

## Build, Test, and Pack

Core solution:

```powershell
dotnet build .\src\Tempo.sln
dotnet run --project .\src\Test.Automated\Test.Automated.csproj
dotnet test .\src\Test.Xunit\Test.Xunit.csproj
dotnet test .\src\Test.Nunit\Test.Nunit.csproj
npm.cmd --prefix .\dashboard run build
```

SDK test applications:

```powershell
dotnet run --project .\sdk\csharp\Tempo.Sdk.TestApp\Tempo.Sdk.TestApp.csproj
npm.cmd --prefix .\sdk\js test
python .\sdk\python\test_app\test_sdk.py
```

NuGet packaging:

```powershell
publish-nuget.bat YOUR_NUGET_API_KEY
```

That script packs and pushes:

- `Tempo`
- `Tempo.Sdk`
- their matching `.snupkg` symbol packages

## Repository Layout

| Path | Purpose |
| --- | --- |
| `src/Tempo` | Core orchestration library |
| `src/Tempo.Core` | Persistence, runtimes, settings, server contracts |
| `src/Tempo.Server` | REST API host |
| `src/Tempo.Worker` | Worker daemon for distributed execution |
| `src/Tempo.McpServer` | MCP bridge over Tempo.Server |
| `dashboard` | React/Vite operator UI |
| `sdk/csharp` | C# SDK and test app |
| `sdk/js` | JavaScript SDK and test app |
| `sdk/python` | Python SDK and test app |
| `docker` | Compose file and container config |
| `docs` | Focused operator and developer guides |
| `archive` | Superseded planning notes and archived implementation docs |

## Technology Stack

| Technology | Role in Tempo |
| --- | --- |
| [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) | Primary runtime for `Tempo`, `Tempo.Core`, `Tempo.Server`, `Tempo.McpServer`, and the server-side test projects |
| [Watson](https://www.nuget.org/packages/Watson/) | Embedded web server used by `Tempo.Server` for HTTP routing, OpenAPI exposure, and trigger/API handling |
| [Voltaic](https://www.nuget.org/packages/Voltaic/) | MCP scaffolding and transport layer used by `Tempo.McpServer` |
| [React 19](https://react.dev/) | Component model for the dashboard UI |
| [React Router 7](https://reactrouter.com/) | Client-side routing for dashboard workspaces and navigation |
| [Vite 6](https://vite.dev/) | Dashboard development server and production build toolchain |
| [i18next](https://www.i18next.com/) and [react-i18next](https://react.i18next.com/) | Dashboard internationalization, locale detection, translation lookup, and locale-aware UI wiring |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/) | SQLite persistence provider for local and lightweight Tempo deployments |
| [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/) | SQL Server persistence provider |
| [Npgsql](https://www.nuget.org/packages/Npgsql/) | PostgreSQL persistence provider |
| [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/) | MySQL persistence provider |
| [PrettyId](https://www.nuget.org/packages/PrettyId/) | K-sortable ID generation for Tempo resource identifiers |
| [RestWrapper](https://www.nuget.org/packages/RestWrapper/) | Outbound HTTP execution support for REST-backed steps |
| [SyslogLogging](https://www.nuget.org/packages/SyslogLogging/) | Structured logging used across the server-side projects |

## Contributing

Follow the coding and review rules in [CLAUDE.md](CLAUDE.md). Keep README, changelog, API docs, and the Postman collection in sync with code changes.

## License

MIT. See [LICENSE.md](LICENSE.md).

## Logo

Logo provided by [softicons.com](https://www.softicons.com/object-icons/vista-musical-instruments-icons-by-icons-land/metronome-icon).
