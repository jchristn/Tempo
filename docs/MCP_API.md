# Tempo MCP API

Tempo.McpServer exposes Tempo.Server through MCP tools. Use it when an agent should create, inspect, edit, invoke, or monitor Tempo data flows, steps, triggers, runs, and artifacts without hand-crafting every REST call.

The MCP server is a C# executable built on Voltaic. It is a client of Tempo.Server, not a replacement for Tempo.Server. Start Tempo.Server first, then start Tempo.McpServer with credentials that can access the target tenant.

## Installing with AI Coding Tools

Tempo.McpServer includes a built-in installer for Claude Code only.

The install command:

- updates `~/.claude.json`
- sets `mcpServers.tempo` to an HTTP MCP entry that points at the configured Tempo MCP Streamable HTTP URL (`/mcp`)
- writes or updates `~/.claude/agents/tempo.md`

Default MCP client endpoint (Streamable HTTP):

```text
http://127.0.0.1:8910/mcp
```

If you installed with an earlier build, your `~/.claude.json` entry may still point at `http://127.0.0.1:8910/rpc`. Re-run the install command (or change the URL to `/mcp` by hand). Claude Code 2.1.x talks the stateless `2026-07-28` MCP revision, and the legacy `/rpc` endpoint does not return that revision's result shape, so Claude Code lists zero Tempo tools there.

You do not need to have Tempo.McpServer already running to execute the install command. The install command only writes local Claude Code configuration. Tempo.McpServer does need to be running before Claude Code can connect to the endpoint.

Claude Code install:

```powershell
dotnet run --project src/Tempo.McpServer/Tempo.McpServer.csproj -- install
```

Claude Code install preview:

```powershell
dotnet run --no-build --project src/Tempo.McpServer/Tempo.McpServer.csproj -- install --dry-run
```

If you use a non-default MCP settings file, pass it before `install`:

```powershell
dotnet run --project src/Tempo.McpServer/Tempo.McpServer.csproj -- --config ./tempo.mcp.json install
```

After install, restart Claude Code.

The built-in installer does not configure Codex CLI, Gemini CLI, Cursor, or other MCP clients. For those clients, register the Tempo Streamable HTTP endpoint (`http://127.0.0.1:8910/mcp`) manually using the client's own MCP configuration format.

## Runtime Model

Tempo.McpServer connects to one Tempo.Server endpoint and forwards MCP tool calls to REST endpoints.

Default Tempo endpoint:

```text
http://127.0.0.1:8901
```

The default uses `127.0.0.1` because Tempo.Server binds IPv4 loopback by default. If you configure `localhost`, the MCP server still connects to loopback over IPv4 first, so tool calls do not pay the roughly 2 second refused-IPv6 fallback that `localhost` otherwise costs on Windows.

Default MCP transports:

| Transport | Default endpoint |
| --- | --- |
| HTTP Streamable HTTP (recommended for MCP clients) | `http://127.0.0.1:8910/mcp` |
| HTTP JSON-RPC (legacy) | `http://127.0.0.1:8910/rpc` |
| HTTP SSE events (legacy, pairs with `/rpc`) | `http://127.0.0.1:8910/events` |
| TCP | `tcp://127.0.0.1:8911` |
| WebSocket | `ws://127.0.0.1:8912/mcp` |

At least one MCP transport must be enabled.

Client URLs printed at startup and written by `install` are derived from the bind settings. Wildcard bind hosts (`*`, `+`, `0.0.0.0`, `::`), such as the ones in `docker/tempo.mcp.json`, are replaced with `127.0.0.1`, and IPv6 literals are bracketed. `softwareVersion` is rewritten at startup from the built assembly version and reported to clients as `serverInfo.version`.

Every transport exposes the same Tempo tools through standard MCP discovery and invocation (`tools/list` and `tools/call`). `tools/list` returns only Tempo tools; Voltaic (2.0.0 and later) publishes no demo tools of its own, and the MCP `ping` request returns an empty result (`{}`). On the HTTP transport tools are reachable only through `tools/call`, which validates arguments against the input schema; a bare JSON-RPC call to a tool name returns `-32601`. The TCP and WebSocket transports also accept each tool name as a direct JSON-RPC method for callers that invoke tools without `tools/call`.

Protocol versions: the `initialize` handshake negotiates at most `2025-11-25` on every transport. The Streamable HTTP endpoint also serves the stateless `2026-07-28` revision (`server/discover` plus per-request `_meta` and `Mcp-Method` headers, no session), which is what current Claude Code releases use.

## Starting the MCP Server

Default settings file:

```text
./tempo.mcp.json
```

Start with defaults:

```powershell
dotnet run --project src/Tempo.McpServer/Tempo.McpServer.csproj
```

Use a specific settings file:

```powershell
dotnet run --project src/Tempo.McpServer/Tempo.McpServer.csproj -- --config ./tempo.mcp.json
```

Show resolved configuration and exit:

```powershell
dotnet run --project src/Tempo.McpServer/Tempo.McpServer.csproj -- --showconfig
```

Install Claude Code MCP and agent files:

```powershell
dotnet run --project src/Tempo.McpServer/Tempo.McpServer.csproj -- install
```

Preview install changes:

```powershell
dotnet run --no-build --project src/Tempo.McpServer/Tempo.McpServer.csproj -- install --dry-run
```

The install command updates:

- `~/.claude.json`
- `~/.claude/agents/tempo.md`

It does not configure non-Claude MCP clients.

Help:

```powershell
dotnet run --project src/Tempo.McpServer/Tempo.McpServer.csproj -- --help
```

## Configuration

Root settings shape:

```json
{
  "softwareVersion": "0.4.0",
  "tempo": {
    "endpoint": "http://127.0.0.1:8901",
    "timeoutMs": 30000,
    "defaultTenantId": "ten_example",
    "token": null,
    "apiKey": null,
    "accessKey": null
  },
  "http": {
    "enabled": true,
    "hostname": "127.0.0.1",
    "port": 8910,
    "rpcPath": "/rpc",
    "eventsPath": "/events"
  },
  "tcp": {
    "enabled": true,
    "address": "127.0.0.1",
    "port": 8911
  },
  "webSocket": {
    "enabled": true,
    "hostname": "127.0.0.1",
    "port": 8912,
    "path": "/mcp"
  }
}
```

Environment variables override the settings file:

| Environment variable | Setting |
| --- | --- |
| `TEMPO_ENDPOINT` | `tempo.endpoint` |
| `TEMPO_TOKEN` | `tempo.token` |
| `TEMPO_API_KEY` | `tempo.apiKey` |
| `TEMPO_ACCESS_KEY` | `tempo.accessKey` |
| `TEMPO_TENANT_ID` | `tempo.defaultTenantId` |
| `TEMPO_MCP_HTTP_HOSTNAME` | `http.hostname` |
| `TEMPO_MCP_HTTP_PORT` | `http.port` |
| `TEMPO_MCP_TCP_ADDRESS` | `tcp.address` |
| `TEMPO_MCP_TCP_PORT` | `tcp.port` |
| `TEMPO_MCP_WS_HOSTNAME` | `webSocket.hostname` |
| `TEMPO_MCP_WS_PORT` | `webSocket.port` |

Tenant-scoped tools use `tempo.defaultTenantId` when the tool argument omits `tenantId`. If neither is supplied, the tool fails before making a REST call.

## Authentication

Tempo.McpServer forwards these authentication settings as REST headers:

| Setting | Header |
| --- | --- |
| `tempo.token` | `x-token` |
| `tempo.apiKey` | `x-api-key` |
| `tempo.accessKey` | `Authorization: Bearer {accessKey}` when no token or API key is configured, otherwise `x-access-key` |
| `tempo.defaultTenantId` | `x-tenant-id` |

Store credentials in environment variables for local agent sessions when possible. Treat settings files containing tokens or access keys as private. `x-secret-key` is not supported for Tempo API authentication.

## Tool Response Envelope

All REST-backed tools return the same response envelope:

```json
{
  "statusCode": 200,
  "success": true,
  "contentType": "application/json",
  "headers": {
    "x-run-id": "run_example"
  },
  "body": {},
  "text": null
}
```

| Field | Meaning |
| --- | --- |
| `statusCode` | HTTP status from Tempo.Server |
| `success` | True for 2xx responses |
| `contentType` | Response content type |
| `headers` | Response and content headers captured from Tempo.Server |
| `body` | Parsed JSON response when possible |
| `text` | Plain text response for non-JSON bodies |

For HTTP trigger invocation, run metadata such as `x-worker-id`, `x-run-id`, `x-dataflow-id`, `x-trigger-id`, and `x-runtime-ms` is in `headers`. The flow output is in `body`.

## Tool Catalog

### System Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `tempo_health` | none | Calls `GET /v1.0/api/health` |
| `tempo_me` | none | Calls `GET /v1.0/me`. With the global admin API key the principal is `{ "type": "adminApiKey", "id": "admin-api-key", "isAdmin": true }` |
| `tempo_settings_meta` | none | Calls `GET /v1.0/settings/meta` |
| `tempo_request` | `method`, `path`, optional `body` | Generic REST call for endpoints not covered by typed tools |

`tempo_request.path` must be `/` or start with `/v1.0/`. Supported methods are `GET`, `POST`, `PUT`, and `DELETE`.

### Worker Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `listWorkers` | optional `pageNumber`, `pageSize`, `state`, `search`, `enabled`, `drainMode` | List Tempo workers visible to an administrator |
| `readWorker` | `id` | Read one worker |
| `drainWorker` | `id` | Put a worker into drain mode |
| `resumeWorker` | `id` | Resume a drained worker |
| `blockWorker` | `id` | Block a worker, disconnect it, and deny future connects |
| `unblockWorker` | `id` | Unblock a worker so it can reconnect |

These tools are not tenant-scoped. They require the MCP server to authenticate to Tempo.Server as an administrator, typically through `tempo.apiKey`.

### Log Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `listLogSources` | none | List admin-visible server and worker log sources |
| `listLogFiles` | `sourceKind`, `sourceId` | List available log files for one source |
| `readLogFile` | `sourceKind`, `sourceId`, `path`, optional `tailLines`, optional `maxBytes` | Read a bounded tail from one log file |
| `downloadLogFile` | `sourceKind`, `sourceId`, `path` | Download the complete log file as plain text |
| `deleteLogFile` | `sourceKind`, `sourceId`, `path` | Delete an archived log file or clear the current one |

These tools are also not tenant-scoped and require administrator credentials.

List sources:

```json
{}
```

List worker log files:

```json
{
  "sourceKind": "worker",
  "sourceId": "wrk_docker_1"
}
```

Read a bounded tail:

```json
{
  "sourceKind": "server",
  "sourceId": "server",
  "path": "tempo.log",
  "tailLines": 200,
  "maxBytes": 131072
}
```

Download a complete file:

```json
{
  "sourceKind": "worker",
  "sourceId": "wrk_docker_1",
  "path": "tempo-worker.log"
}
```

`downloadLogFile` returns the normal MCP response envelope. Because the REST
route responds with `text/plain`, the complete log file is exposed in the
envelope's `text` field rather than `body`.

Delete or clear a file:

```json
{
  "sourceKind": "worker",
  "sourceId": "wrk_docker_1",
  "path": "tempo-worker.1.log"
}
```

Delete behavior matches REST:

| Target | Result |
| --- | --- |
| Current log file | Cleared by truncation |
| Archived log file | Deleted from disk |

### Tenant Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `tenant_list` | optional `pageNumber`, `pageSize`, `includeInactive` | List tenants |
| `tenant_get` | `id` | Read one tenant |

### Step Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `step_list` | optional `tenantId`, `pageNumber`, `pageSize`, `includeInactive` | List steps |
| `step_get` | `id`, optional `tenantId` | Read one step |
| `step_create` | `body`, optional `tenantId` | Create a step from a full REST step body |
| `step_update` | `id`, `body`, optional `tenantId` | Update a step |
| `step_registered` | none | List registered built-in steps |
| `step_create_from_source` | `name`, `language`, `code`, optional source options | Package pasted source into an artifact-backed step |

`step_create_from_source` is the preferred tool for ad hoc Python, JavaScript, and C# steps.

Arguments:

| Argument | Required | Notes |
| --- | --- | --- |
| `tenantId` | no | Uses `tempo.defaultTenantId` when omitted |
| `name` | yes | Step display name |
| `language` | yes | `Python`, `JavaScript`, or `CSharp` |
| `code` | yes | Complete source file text |
| `executionKey` | no | Stable key used by data flows |
| `description` | no | Step description |
| `function` | no | Python or JavaScript function, default is server-defined |
| `handlerType` | no | C# handler type, usually a class inheriting `TempoStepHandlerBase` |
| `entrypoint` | no | Entrypoint file or assembly |
| `fileName` | no | Simple source file name, no path separators |
| `artifactName` | no | Artifact display name |
| `module` | no | Runtime module name |

Example:

```json
{
  "tenantId": "ten_example",
  "executionKey": "mcp.echo_js",
  "name": "MCP echo JavaScript",
  "language": "JavaScript",
  "fileName": "handler.js",
  "function": "run",
  "code": "exports.run = async function(input) { return { ok: true, input }; };"
}
```

### Flow Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `flow_list` | optional `tenantId`, `pageNumber`, `pageSize`, `includeInactive` | List data flows |
| `flow_get` | `id`, optional `tenantId` | Read one data flow |
| `flow_create` | `body`, optional `tenantId` | Create a data flow |
| `flow_update` | `id`, `body`, optional `tenantId` | Update a data flow |
| `flow_enqueue_run` | `flowId`, optional `tenantId`, optional `body` | Enqueue a direct flow run |

Example `flow_create` body:

```json
{
  "tenantId": "ten_example",
  "body": {
    "name": "MCP echo flow",
    "startStepId": "mcp.echo_js",
    "invocationAuthMode": "Public",
    "transitions": {
      "mcp.echo_js": {
        "name": "Echo",
        "onSuccess": null,
        "onFailure": null,
        "onException": null,
        "maxTransitions": 1
      }
    },
    "active": true
  }
}
```

Example direct run:

```json
{
  "tenantId": "ten_example",
  "flowId": "flow_example",
  "body": {
    "data": {
      "value": "hello from MCP"
    },
    "metadata": {
      "source": "mcp"
    }
  }
}
```

`flow_enqueue_run` returns the run record and does not wait for completion. Use `run_get` and `run_steps` to monitor.

Set flow `invocationAuthMode` to `ApiAuthenticated` when HTTP trigger invocation should require the same Tempo credentials configured for the MCP server. `trigger_fire` and `tempo_request` forward those credentials automatically when `tempo.token`, `tempo.apiKey`, or `tempo.accessKey` is configured.

### Trigger Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `trigger_list` | optional `tenantId`, `pageNumber`, `pageSize`, `includeInactive` | List triggers |
| `trigger_get` | `id`, optional `tenantId` | Read one trigger |
| `trigger_create` | `body`, optional `tenantId` | Create a trigger |
| `trigger_update` | `id`, `body`, optional `tenantId` | Update a trigger |
| `trigger_fire` | `triggerId`, optional `body` | POST to an HTTP trigger |

Example trigger creation:

```json
{
  "tenantId": "ten_example",
  "body": {
    "name": "MCP echo trigger",
    "triggerType": "Http",
    "dataFlowId": "flow_example",
    "configuration": "{\"allowedMethods\":[\"POST\"]}",
    "active": true
  }
}
```

Example trigger fire:

```json
{
  "triggerId": "trg_example",
  "body": {
    "value": "hello from MCP"
  }
}
```

`trigger_fire` always uses `POST`. For a GET-only trigger, use `tempo_request`:

```json
{
  "method": "GET",
  "path": "/v1.0/triggers/http/trg_example"
}
```

### Artifact Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `artifact_list` | optional `tenantId`, `pageNumber`, `pageSize`, `includeInactive` | List artifacts |
| `artifact_get` | `id`, optional `tenantId` | Read artifact metadata |
| `artifact_create` | `body`, optional `tenantId` | Create artifact metadata |
| `artifact_update` | `id`, `body`, optional `tenantId` | Update artifact metadata |
| `artifact_files` | `artifactId`, optional `tenantId` | List mutable files |
| `artifact_file_read` | `artifactId`, `path`, optional `tenantId` | Read a mutable file |
| `artifact_file_save` | `artifactId`, `path`, `content`, optional `tenantId`, optional `contentType` | Save a mutable text file |

Create artifact metadata:

```json
{
  "tenantId": "ten_example",
  "body": {
    "name": "MCP editable artifact",
    "description": "Files managed through MCP"
  }
}
```

Save a file:

```json
{
  "tenantId": "ten_example",
  "artifactId": "art_example",
  "path": "handler.js",
  "content": "exports.run = async function(input) { return { ok: true, input }; };",
  "contentType": "application/javascript"
}
```

Read a file:

```json
{
  "tenantId": "ten_example",
  "artifactId": "art_example",
  "path": "handler.js"
}
```

The typed `artifact_file_save` tool is for text content. Use `tempo_request` against the REST artifact version upload endpoint for binary package upload or download workflows.

### Run Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `run_list` | optional `tenantId`, `pageNumber`, `pageSize`, `includeInactive` | List runs |
| `run_get` | `id`, optional `tenantId` | Read one run |
| `run_steps` | `id`, optional `tenantId` | List step runs for a run |
| `run_activity` | `id`, optional `tenantId` | Read the run plus assignment attempts and worker activity |
| `run_logs_list` | `id`, optional `tenantId` | List file-backed logs for one run |
| `run_logs_read` | `id`, `path`, optional `tenantId`, optional `tailLines`, optional `maxBytes` | Read a bounded tail from one run-log file |
| `run_logs_download` | `id`, `path`, optional `tenantId` | Download the complete run-log file |
| `run_logs_delete` | `id`, `path`, optional `tenantId` | Delete one archived run-log file |
| `run_logs_delete_all` | `id`, optional `tenantId` | Delete every run-log file for one completed run |

Generic collection helpers also register `run_create` and `run_update`, but Tempo's workflow API creates runs through `flow_enqueue_run` or HTTP trigger invocation. Prefer those workflow tools. Use `tempo_request` for cancel and delete operations:

```json
{
  "method": "POST",
  "path": "/v1.0/tenants/ten_example/runs/run_example/cancel"
}
```

```json
{
  "method": "DELETE",
  "path": "/v1.0/tenants/ten_example/runs/run_example"
}
```

Read run activity:

```json
{
  "tenantId": "ten_example",
  "id": "run_example"
}
```

List run logs:

```json
{
  "tenantId": "ten_example",
  "id": "run_example"
}
```

Read a bounded run-log tail:

```json
{
  "tenantId": "ten_example",
  "id": "run_example",
  "path": "attempt-001-ras_example/worker.log",
  "tailLines": 200,
  "maxBytes": 131072
}
```

Download a complete run log:

```json
{
  "tenantId": "ten_example",
  "id": "run_example",
  "path": "run.log"
}
```

Delete one archived run log:

```json
{
  "tenantId": "ten_example",
  "id": "run_example",
  "path": "attempt-001-ras_example/step-001-sru_example-step.echo.log"
}
```

`run_logs_download` returns the normal MCP response envelope. Because the underlying REST route responds with `text/plain`, the complete file is exposed in the envelope's `text` field rather than `body`.

### Runtime Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `runtime_list` | none | List runtime providers |
| `runtime_get` | `runtimeKey` | Read one runtime provider |
| `runtime_external_execution` | none | Read process-backed runtime availability and capacity |

Known runtime keys:

```text
Builtin.Class
Builtin.Method
Builtin.Unknown
External.Rest
Legacy.InlineRest
Artifact.Process
Artifact.Python
Artifact.JavaScript
Artifact.DotnetProcess
Host.Executable
```

Use runtime tools before creating Python, JavaScript, .NET, process, or host-executable steps. Missing local dependencies are reported as runtime availability states.

## Workflow Recipes

### Create and Invoke an Echo Flow

1. Call `step_create_from_source`.

```json
{
  "executionKey": "recipe.echo",
  "name": "Recipe echo",
  "language": "JavaScript",
  "fileName": "handler.js",
  "function": "run",
  "code": "exports.run = async function(input) { return { ok: true, input }; };"
}
```

2. Call `flow_create` using `recipe.echo` as `startStepId` and transition key.

```json
{
  "body": {
    "name": "Recipe echo flow",
    "startStepId": "recipe.echo",
    "transitions": {
      "recipe.echo": {
        "name": "Echo",
        "onSuccess": null,
        "onFailure": null,
        "onException": null,
        "maxTransitions": 1
      }
    },
    "active": true
  }
}
```

3. Call `trigger_create` with the returned `flow.id`.

```json
{
  "body": {
    "name": "Recipe echo trigger",
    "triggerType": "Http",
    "dataFlowId": "flow_example",
    "configuration": "{\"allowedMethods\":[\"POST\"]}",
    "active": true
  }
}
```

4. Call `trigger_fire`.

```json
{
  "triggerId": "trg_example",
  "body": {
    "value": "hello"
  }
}
```

5. Read run metadata from `headers`, especially `x-run-id`, then call `run_steps`.

```json
{
  "id": "run_example"
}
```

### Edit a JavaScript Artifact and Re-run

1. Use `step_get` to find `runtimeConfig.artifactId`.
2. Use `artifact_files` to inspect package contents.
3. Use `artifact_file_read` on the handler path.
4. Use `artifact_file_save` to update the handler source.
5. Fire the trigger again.
6. Confirm the new run's step run has the expected `artifactVersion` and `artifactSha256`.

For Python and JavaScript source-step artifacts, source edits can affect execution through the rebuilt current snapshot. For C# source-step artifacts, editing `.cs` source alone does not recompile the entrypoint assembly. Recreate the source step or upload a rebuilt package for compiled behavior changes.

### Invoke a GET Trigger

`trigger_fire` posts. For GET triggers:

```json
{
  "method": "GET",
  "path": "/v1.0/triggers/http/trg_example"
}
```

The response `body` is the flow output. Run metadata is in `headers`.

### Create a Two-Step Flow

Use two step execution keys and chain `onSuccess`:

```json
{
  "body": {
    "name": "Recipe chain",
    "startStepId": "recipe.random",
    "transitions": {
      "recipe.random": {
        "name": "Generate random",
        "onSuccess": "recipe.double",
        "onFailure": null,
        "onException": null,
        "maxTransitions": 1
      },
      "recipe.double": {
        "name": "Double number",
        "onSuccess": null,
        "onFailure": null,
        "onException": null,
        "maxTransitions": 1
      }
    },
    "active": true
  }
}
```

The output of `recipe.random` becomes the input to `recipe.double`. The output of `recipe.double` becomes the flow output returned by an HTTP trigger.

## Generic REST Escape Hatch

Use `tempo_request` for management routes that do not have a dedicated tool:

| REST operation | Example `tempo_request` |
| --- | --- |
| Delete a step | `{ "method": "DELETE", "path": "/v1.0/tenants/ten_example/steps/step_example" }` |
| Ensure flow steps | `{ "method": "POST", "path": "/v1.0/tenants/ten_example/flows/flow_example/ensure-steps" }` |
| Cancel a run | `{ "method": "POST", "path": "/v1.0/tenants/ten_example/runs/run_example/cancel" }` |
| List artifact versions | `{ "method": "GET", "path": "/v1.0/tenants/ten_example/artifacts/art_example/versions" }` |
| Read runtime status for a tenant | `{ "method": "GET", "path": "/v1.0/tenants/ten_example/runtimes/external-execution" }` |

`tempo_request` cannot send arbitrary absolute URLs. It is intentionally scoped to the connected Tempo.Server.

## Monitoring Guidance

For each trigger invocation:

1. Check `success` and `statusCode`.
2. Read `body` for the flow output.
3. Read `headers.x-run-id`.
4. Call `run_get` for the final run record.
5. Call `run_steps` for per-step output, errors, artifacts, and timing.
6. Call `run_activity` to inspect assignment history and worker timeline data.
7. Call `run_logs_list` and `run_logs_read` to inspect the durable file-backed logs for that run.

Important run and step-run fields:

| Field | Where | Use |
| --- | --- | --- |
| `state` | Run | Queued, Running, Succeeded, Failed, Exception, Cancelled |
| `outputData` | Run | Final flow output for direct run polling |
| `errorMessage` | Run and step run | Failure diagnosis |
| `sequence` | Step run | Execution order |
| `result` | Step run | Success, Timeout, Error, Exception, MaxIterationsExceeded |
| `artifactId` | Step run | Which artifact ran |
| `artifactVersion` | Step run | Which version label ran |
| `artifactSha256` | Step run | Exact content hash |
| `capacityWaitMs` | Step run | External runtime queue pressure |

## Safety and Limits

MCP tools can mutate Tempo resources. Agents should follow these rules:

| Rule | Reason |
| --- | --- |
| Prefer `step_create_from_source` for pasted code | It creates the artifact, manifest, and step together |
| Always set or preserve `executionKey` intentionally | Flows depend on it |
| Use `runtime_list` before creating process-backed steps | Missing `python`, `node`, or `dotnet` makes runtimes unavailable |
| Inspect run headers after `trigger_fire` | Trigger responses put metadata in headers |
| Use `includeInactive` when reconciling records | Inactive resources may still explain historical runs |
| Do not delete referenced resources through `tempo_request` casually | REST deletion guards will block unsafe deletes, but attempted deletes still create operator noise |
| Keep public trigger IDs private | `Public` trigger invocation is not tenant-scoped; use `ApiAuthenticated` for private flows |

## Troubleshooting

| Symptom | Likely cause | Next step |
| --- | --- | --- |
| Tenant-scoped tool says tenant is required | Neither `tenantId` argument nor `tempo.defaultTenantId` is set | Set `TEMPO_TENANT_ID` or pass `tenantId` |
| Tool returns `401` | Missing or invalid credentials | Check token, API key, or access key |
| Tool returns `403` | Principal lacks tenant or operation permission | Use a principal with the required permission |
| `trigger_fire` returns `405` | Trigger does not allow POST | Use `tempo_request` GET or update `allowedMethods` |
| Source step creation fails for Python/JS/.NET | Runtime dependency is unavailable | Call `runtime_list` and inspect server runtime settings |
| Artifact edit succeeds but C# behavior does not change | Source was edited but assembly was not rebuilt | Recreate the source step or upload a rebuilt package |
| `body` is null but `text` has content | Response was not JSON | Inspect `contentType` and `text` |

