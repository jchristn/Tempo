# Changelog

All notable changes to Tempo are documented in this file.

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
- `publish-nuget.bat` for `Tempo.Core` and the C# SDK, including symbol package publishing

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
