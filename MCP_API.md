# Tempo MCP API

Tempo.McpServer exposes Tempo.Server through MCP tools. Use it when an agent should create, inspect, edit, invoke, or monitor Tempo data flows, steps, triggers, runs, and artifacts without hand-crafting every REST call.

The MCP server is a C# executable built on Voltaic. It is a client of Tempo.Server, not a replacement for Tempo.Server. Start Tempo.Server first, then start Tempo.McpServer with credentials that can access the target tenant.

## Installing with AI Coding Tools

Tempo.McpServer includes a built-in installer for Claude Code only.

The install command:

- updates `~/.claude.json`
- sets `mcpServers.tempo` to an HTTP MCP entry that points at the configured Tempo MCP RPC URL
- writes or updates `~/.claude/agents/tempo.md`

Default RPC endpoint:

```text
http://127.0.0.1:8910/rpc
```

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

The built-in installer does not configure Codex CLI, Gemini CLI, Cursor, or other MCP clients. For those clients, register the Tempo HTTP JSON-RPC endpoint manually using the client's own MCP configuration format.

## Runtime Model

Tempo.McpServer connects to one Tempo.Server endpoint and forwards MCP tool calls to REST endpoints.

Default Tempo endpoint:

```text
http://localhost:8901
```

Default MCP transports:

| Transport | Default endpoint |
| --- | --- |
| HTTP JSON-RPC | `http://127.0.0.1:8910/rpc` |
| HTTP SSE events | `http://127.0.0.1:8910/events` |
| TCP | `tcp://127.0.0.1:8911` |
| WebSocket | `ws://127.0.0.1:8912/mcp` |

At least one MCP transport must be enabled.

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
  "softwareVersion": "0.2.0",
  "tempo": {
    "endpoint": "http://localhost:8901",
    "timeoutMs": 30000,
    "defaultTenantId": "ten_example",
    "token": null,
    "apiKey": null,
    "accessKey": null,
    "secretKey": null
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
| `TEMPO_SECRET_KEY` | `tempo.secretKey` |
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
| `tempo.accessKey` | `x-access-key` |
| `tempo.secretKey` | `x-secret-key` |
| `tempo.defaultTenantId` | `x-tenant-id` |

Store credentials in environment variables for local agent sessions when possible. Treat settings files containing tokens or secrets as private.

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

For HTTP trigger invocation, run metadata such as `x-run-id`, `x-dataflow-id`, `x-trigger-id`, and `x-runtime-ms` is in `headers`. The flow output is in `body`.

## Tool Catalog

### System Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `tempo_health` | none | Calls `GET /v1.0/api/health` |
| `tempo_me` | none | Calls `GET /v1.0/me` |
| `tempo_settings_meta` | none | Calls `GET /v1.0/settings/meta` |
| `tempo_request` | `method`, `path`, optional `body` | Generic REST call for endpoints not covered by typed tools |

`tempo_request.path` must be `/` or start with `/v1.0/`. Supported methods are `GET`, `POST`, `PUT`, and `DELETE`.

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
| `handlerType` | no | C# handler type |
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

### Trigger Tools

| Tool | Arguments | Purpose |
| --- | --- | --- |
| `trigger_list` | optional `tenantId`, `pageNumber`, `pageSize`, `includeInactive` | List triggers |
| `trigger_get` | `id`, optional `tenantId` | Read one trigger |
| `trigger_create` | `body`, optional `tenantId` | Create a trigger |
| `trigger_update` | `id`, `body`, optional `tenantId` | Update a trigger |
| `trigger_fire` | `triggerId`, optional `body` | POST to a public HTTP trigger |

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

Generic collection helpers also register `run_create` and `run_update`, but Tempo's workflow API creates runs through `flow_enqueue_run` or public trigger invocation. Prefer those workflow tools. Use `tempo_request` for cancel and delete operations:

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
| Keep trigger IDs private | Public trigger routes are not tenant-scoped |

## Troubleshooting

| Symptom | Likely cause | Next step |
| --- | --- | --- |
| Tenant-scoped tool says tenant is required | Neither `tenantId` argument nor `tempo.defaultTenantId` is set | Set `TEMPO_TENANT_ID` or pass `tenantId` |
| Tool returns `401` | Missing or invalid credentials | Check token, API key, access key, or secret |
| Tool returns `403` | Principal lacks tenant or operation permission | Use a principal with the required permission |
| `trigger_fire` returns `405` | Trigger does not allow POST | Use `tempo_request` GET or update `allowedMethods` |
| Source step creation fails for Python/JS/.NET | Runtime dependency is unavailable | Call `runtime_list` and inspect server runtime settings |
| Artifact edit succeeds but C# behavior does not change | Source was edited but assembly was not rebuilt | Recreate the source step or upload a rebuilt package |
| `body` is null but `text` has content | Response was not JSON | Inspect `contentType` and `text` |

