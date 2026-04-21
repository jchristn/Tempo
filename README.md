<div align="center">
  <img src="https://raw.githubusercontent.com/jchristn/tempo/main/assets/logo.png" width="182" height="182" alt="Tempo logo">
</div>

# Tempo

[![NuGet](https://img.shields.io/nuget/v/Tempo.svg)](https://www.nuget.org/packages/Tempo/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

Tempo is a workflow automation platform for building, running, and monitoring tenant-scoped data flows. A flow is made of reusable steps, invoked through triggers, persisted with artifacts, and observed through run history, request history, OpenAPI, and MCP.

Tempo ships as:

- `Tempo`: the core workflow/orchestration library
- `Tempo.Core`: server-facing persistence, runtime, settings, and management contracts
- `Tempo.Server`: the REST API host
- `Tempo.McpServer`: an MCP facade over Tempo.Server built on Voltaic
- `dashboard/`: a React/Vite operator UI
- `sdk/csharp`, `sdk/js`, and `sdk/python`: artifact runtime SDKs with exhaustive test apps

## Highlights

- Tenant-scoped CRUD for data flows, steps, triggers, runs, artifacts, users, credentials, roles, and permissions
- Runtime model that supports `Builtin.Class`, `Builtin.Method`, `External.Rest`, `Artifact.Process`, `Artifact.Python`, `Artifact.JavaScript`, `Artifact.DotnetProcess`, and `Host.Executable`
- Source-step creation from the UI or API for Python, JavaScript, and C#
- Mutable artifact packages with dashboard file browsing and in-place editing
- Runtime-aware startup seeding that creates working sample steps for each available runtime type
- OpenAPI-backed API Explorer and MCP server for agent-driven automation
- First-run setup wizard that creates and invokes example flows end to end
- K-sortable PrettyId identifiers with fixed prefixes and a maximum length of 32
- Docker Compose deployment, image build scripts, and NuGet publish script

## Quick Start

### Docker Compose

From the repository root:

```powershell
docker compose -f .\docker\compose.yaml up --build
```

Default endpoints:

- Dashboard: `http://localhost:3000`
- Tempo.Server: `http://localhost:8901`
- Tempo.McpServer HTTP RPC: `http://127.0.0.1:8910/rpc`
- Tempo.McpServer TCP: `127.0.0.1:8911`
- Tempo.McpServer WebSocket: `ws://127.0.0.1:8912/mcp`

Default seeded credentials on an empty database:

- Email: `admin@tempo.local`
- Password: `password`

Compose uses named volumes for server config, database, logs, runtime cache, scratch storage, dashboard logs, and MCP configuration. The service images in the compose file are pinned to `v0.2.0`.

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
dotnet run --project .\src\Tempo.McpServer\Tempo.McpServer.csproj

cd .\dashboard
npm install
npm run dev
```

Helper scripts at the repository root:

- `build-server.bat v0.2.0`
- `build-mcp.bat v0.2.0`
- `build-dashboard.bat v0.2.0`
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

## Runtime Keys

| Runtime key | Purpose |
| --- | --- |
| `Builtin.Class` | Executes a registered `Tempo.Step` subclass |
| `Builtin.Method` | Executes a registered `[StepMethod]` method |
| `External.Rest` | Executes a persisted outbound HTTP request |
| `Artifact.Process` | Executes a package-local process that speaks Tempo protocol v1 |
| `Artifact.Python` | Executes a Python handler from an artifact package |
| `Artifact.JavaScript` | Executes a Node.js handler from an artifact package |
| `Artifact.DotnetProcess` | Executes a .NET handler from an artifact package using the Tempo SDK host |
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

## Public HTTP Trigger Response Contract

Public HTTP trigger routes are:

```text
/v1.0/triggers/http/{triggerId}
```

For successful trigger execution:

- The HTTP response body is the final step output body
- Execution metadata is returned in headers, not mixed into the JSON body

Current response metadata headers include:

- `x-tenant-id`
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

## Mutable Artifacts and Source Steps

Artifacts are file packages, not opaque zip-only deployment units. Tempo stores package contents in a way that supports:

- Uploading artifacts and versions
- Editing individual files in the dashboard
- Creating steps directly from pasted Python, JavaScript, or C# source
- Reusing one artifact across multiple steps and versions

For artifact-backed runtimes and manifests, see [docs/ARTIFACT_MANIFEST.md](docs/ARTIFACT_MANIFEST.md).

## APIs and Documentation

Primary reference material:

- [REST_API.md](REST_API.md)
- [MCP_API.md](MCP_API.md)
- [BEST_PRACTICES.md](BEST_PRACTICES.md)
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

- `Tempo.Core`
- `Tempo.Sdk`
- their matching `.snupkg` symbol packages

## Repository Layout

| Path | Purpose |
| --- | --- |
| `src/Tempo` | Core orchestration library |
| `src/Tempo.Core` | Persistence, runtimes, settings, server contracts |
| `src/Tempo.Server` | REST API host |
| `src/Tempo.McpServer` | MCP bridge over Tempo.Server |
| `dashboard` | React/Vite operator UI |
| `sdk/csharp` | C# SDK and test app |
| `sdk/js` | JavaScript SDK and test app |
| `sdk/python` | Python SDK and test app |
| `docker` | Compose file and container config |
| `docs` | Focused operator and developer guides |

## Contributing

Follow the coding and review rules in [CLAUDE.md](CLAUDE.md). Keep README, changelog, API docs, and the Postman collection in sync with code changes.

## License

MIT. See [LICENSE.md](LICENSE.md).

## Logo

Logo provided by [softicons.com](https://www.softicons.com/object-icons/vista-musical-instruments-icons-by-icons-land/metronome-icon).
