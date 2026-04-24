# Building Steps In Tempo

This guide is for developers who need to author steps that run well inside Tempo, emit useful diagnostics, and behave predictably in production.

It covers:

- which runtime to choose
- what request and result shape your code must honor
- how logging works for each runner
- how to build source steps, packaged artifacts, built-in steps, and operator-owned host executable steps

Related references:

- [REST_API.md](./REST_API.md)
- [ARTIFACT_MANIFEST.md](./ARTIFACT_MANIFEST.md)
- [PROTOCOL_V1.md](./PROTOCOL_V1.md)
- [EXTERNAL_EXECUTION_OPERATOR_GUIDE.md](./EXTERNAL_EXECUTION_OPERATOR_GUIDE.md)
- [PYTHON_ARTIFACT_QUICKSTART.md](./PYTHON_ARTIFACT_QUICKSTART.md)

## Runtime Selection

Use the simplest runtime that matches your deployment and ownership model.

| Runtime | Best for | Authoring model | Key tradeoff |
| --- | --- | --- | --- |
| `External.Rest` | Calling an existing HTTP endpoint | Configuration only | No local code package, but behavior lives outside Tempo |
| `Artifact.Python` | Tenant-authored Python logic | Python function in an artifact or source step | Depends on Python availability on the worker/server |
| `Artifact.JavaScript` | Tenant-authored JavaScript or Node logic | JavaScript function in an artifact or source step | Depends on Node availability on the worker/server |
| `Artifact.DotnetProcess` | Tenant-authored .NET step logic | .NET process artifact inheriting `TempoStepHandlerBase` or implementing `ITempoStepHandler` | `stdout` is protocol-only, so use the host logger |
| `Artifact.Process` | Any executable that can speak Tempo protocol over stdin/stdout | Manual package and protocol implementation | Most flexible, least ergonomic |
| `Host.Executable` | Operator-approved local tools | Tenant references an allowlist key only | Good for curated tools, not for arbitrary tenant code |
| `Builtin.Class` | Server-owned in-process code | `Step` subclass in the server process | Fastest path, but deployment-coupled |
| `Builtin.Method` | Server-owned in-process static methods | `[StepMethod]` on a static method | Same deployment coupling as other built-ins |

Recommended defaults:

- Choose `Artifact.Python`, `Artifact.JavaScript`, or `Artifact.DotnetProcess` for tenant-authored code.
- Choose `External.Rest` when the real implementation already exists behind HTTP.
- Choose `Artifact.Process` only when you already have a non-Python, non-JavaScript, non-.NET executable and do not want to wrap it in one of the higher-level runtimes.
- Choose `Builtin.Class` or `Builtin.Method` only when the step ships with the Tempo server application itself.

## Common Rules

Every step runtime should follow these rules.

### Keep `executionKey` stable

Flows reference steps by `executionKey`, not by the step record ID. Treat `executionKey` as the durable API name of the step.

Good:

- `customer.normalize_email`
- `orders.calculate_tax`
- `inventory.reserve_items`

Avoid:

- `test1`
- `step_new`
- names that encode a transient artifact version

### Understand the request contract

Tempo sends a `StepRequest` into every runner. The important fields are:

| Field | Meaning |
| --- | --- |
| `protocolVersion` | Current step protocol version, currently `1.0` |
| `tenantId` | Owning tenant when available |
| `dataFlowId` | Flow identifier |
| `flowRunId` | Run identifier |
| `stepRunId` | Step-run identifier |
| `requestId` | Request correlation ID |
| `data` | Primary payload for your step |
| `metadata` | Additional context carried with the request |
| `previousResult` | The prior step's result type when this is not the first step |

See [PROTOCOL_V1.md](./PROTOCOL_V1.md) for the wire shape used by process-backed runners.

### Return JSON-serializable output

Whatever you place in `StepResult.Data` should be serializable. If your runtime wrapper is Python or JavaScript, return plain JSON-compatible values.

Good outputs:

- objects/dictionaries
- arrays/lists
- strings, booleans, numbers, null

Avoid:

- open file handles
- raw framework-specific objects
- opaque exceptions embedded inside `data`

### Use result types intentionally

Tempo supports these step result types:

- `Success`
- `Timeout`
- `Error`
- `Exception`
- `MaxIterationsExceeded`

Practical guidance:

- Return `Success` for normal completion.
- Return `Error` for business-rule failures that are expected and should route through failure transitions.
- Let Tempo produce `Exception` for unhandled code or protocol failures.
- Let Tempo handle `Timeout` when the runtime exceeds limits.

For .NET process steps, prefer `TempoStepHandlerBase.Success(...)` / `TempoStepHandlerBase.Error(...)` or `TempoStepHost.Success(...)` / `TempoStepHost.Error(...)` so correlation fields stay correct.

### Treat `stdout` carefully

For process-backed runtimes, `stdout` is reserved for the protocol response.

- `Artifact.Process`: `stdout` must contain only the serialized `StepResult`
- `Artifact.DotnetProcess`: `TempoStepHost` writes the `StepResult` to `stdout`
- `Artifact.Python` and `Artifact.JavaScript`: Tempo injects shims that keep user logging out of protocol `stdout`

If you write arbitrary log text to `stdout` in a generic process or .NET process, Tempo will fail the step because `stdout` is no longer valid `StepResult` JSON.

### Use schemas when the contract matters

Tempo steps support:

- `contractType`
- `inputSchema`
- `outputSchema`
- `validateInput`
- `validateOutput`

Use them when:

- the step is reused by multiple flows
- the payload structure is stable
- you want bad inputs rejected before your code runs

Leave the step `Loose` when the payload is intentionally dynamic or the step is a quick integration shim.

### Respect timeouts and secrets

- `maxRuntimeMs` on the step is the per-step override
- process-backed runtimes also obey external execution limits
- `environmentReferences` and manifest allowlists should contain names only, never literal secret values

If your step needs credentials, pass environment variable names through Tempo configuration and let the runtime read them from the process environment.

## Logging Model

Run logs are a first-class part of debugging Tempo steps. The most important rule is:

Do not assume every runtime logs the same way.

Tempo passes these environment variables into process-backed runtimes:

| Variable | Purpose |
| --- | --- |
| `TEMPO_RUN_LOG_DIR` | Attempt-scoped log directory |
| `TEMPO_RUN_LOG_FILE` | Primary per-step log file |
| `TEMPO_FLOW_RUN_ID` | Flow-run ID |
| `TEMPO_RUN_ASSIGNMENT_ID` | Assignment ID |
| `TEMPO_STEP_RUN_ID` | Step-run ID |
| `TEMPO_STEP_ID` | Step execution key |
| `TEMPO_WORKER_ID` | Worker or pseudo-worker ID |
| `TEMPO_PROTOCOL_VERSION` | Negotiated protocol version |
| `TEMPO_SUPPORTED_PROTOCOL_VERSIONS` | Comma-separated supported versions |

Runtime-specific behavior:

| Runtime | What to use for logs | Where it ends up |
| --- | --- | --- |
| `Artifact.Python` | `print(...)`, root `logging`, or `stderr` | Step log files |
| `Artifact.JavaScript` | `console.log/info/warn/error/debug` or `stderr` | Step log files |
| `Artifact.DotnetProcess` | `LogInfo`, `LogWarn`, `LogError`, or `TempoExecutionContext.Current.Logger` | Step log files |
| `Artifact.Process` | `stderr` or an invocation-scoped writer opened from `TEMPO_RUN_LOG_FILE` | `stderr` log or step log file |
| Built-in / in-process | Server logging and run-log instrumentation around step execution | Server logs plus run activity |

General logging best practices:

- Log inputs in a redacted or summarized form.
- Log the important branch decisions.
- Log start and completion for long-running operations.
- Do not log raw credentials, tokens, connection strings, or secrets.
- Prefer one logical event per line.

## Source Steps

Source steps are the fastest path when you want Tempo to package the code for you.

Create them through:

- `POST /v1.0/tenants/{tenantId}/steps/source`
- the dashboard step editor
- the setup wizard
- MCP `step_create_from_source`

Supported source languages:

- `Python`
- `JavaScript`
- `CSharp`

Important source-step request fields:

| Field | Notes |
| --- | --- |
| `executionKey` | Optional but recommended |
| `name` | Required |
| `language` | `Python`, `JavaScript`, or `CSharp` |
| `code` | Required source file contents |
| `fileName` | Simple file name only, no path separators |
| `artifactName` | Optional generated artifact display name |
| `entrypoint` | Defaults to `main` |
| `function` | Required for Python and JavaScript |
| `handlerType` | Required for C#, usually a `TempoStepHandlerBase` subclass |
| `contractType`, `inputSchema`, `outputSchema` | Optional contract controls |
| `maxRuntimeMs` | Per-step timeout override |

Packaging behavior:

- Python source steps become `Artifact.Python`
- JavaScript source steps become `Artifact.JavaScript`
- C# source steps become `Artifact.DotnetProcess`

Source steps are a good default when:

- the code is small enough to live in Tempo-managed artifacts
- you want the dashboard and API to own packaging
- you do not need a custom build pipeline

Use manually packaged artifacts instead when:

- you already have a separate build process
- you need multiple files, native binaries, or nontrivial dependencies
- you want stronger control over package layout

## C# Steps

There are two main C# authoring styles:

- source-created `Artifact.DotnetProcess`
- built-in in-process code using `Step` or `[StepMethod]`

### C# source or packaged `Artifact.DotnetProcess`

This is the recommended C# path for tenant-authored code.

Inherit `Tempo.Protocol.TempoStepHandlerBase`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Tempo;
using Tempo.Protocol;

namespace Tempo.UserSteps;

public sealed class Handler : TempoStepHandlerBase
{
    public override Task<StepResult> RunAsync(StepRequest request, CancellationToken token = default)
    {
        LogInfo("Echo step received input: " + request.Data);

        return Task.FromResult(Success(request, new
        {
            ok = true,
            input = request.Data
        }));
    }
}
```

Best practices:

- Use `Success(request, data)` or `Error(request, data)` from `TempoStepHandlerBase` instead of constructing `StepResult` manually.
- Use `LogDebug`, `LogInfo`, `LogWarn`, `LogWarning`, and `LogError` from `TempoStepHandlerBase` for step diagnostics.
- Honor the cancellation token when calling other services.
- Never write user logs to `Console.Out`.
- Do not read `TEMPO_RUN_LOG_FILE` from step code. `TempoStepHost` resolves it once per invocation and exposes the active logger through `TempoExecutionContext.Current`.
- Do not use `File.AppendAllText` as a logging strategy. The Tempo host owns the file-backed logger and serializes writes for the invocation.
- Existing handlers that directly implement `ITempoStepHandler` still work, but new C# code should use `TempoStepHandlerBase`.

Build and execution notes:

- C# source-step creation requires a .NET SDK because Tempo compiles the code with `dotnet publish`.
- Executing a packaged `.dll` requires the configured `dotnet` executable on the worker or server.
- For `Artifact.DotnetProcess`, the manifest entrypoint command must point at a package-local `.dll`, and the manifest entrypoint must declare `handlerType`.

### Built-in class-based steps

Use built-ins only when the code ships with the Tempo server application.

```csharp
using System.Threading.Tasks;

namespace Tempo.ServerSteps;

public sealed class NormalizeEmailStep : Step
{
    public override Task<StepResult> Run(StepRequest req)
    {
        string value = (req.Data?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
        return Task.FromResult(new StepResult
        {
            ProtocolVersion = req.ProtocolVersion,
            TenantId = req.TenantId,
            DataFlowId = req.DataFlowId,
            FlowRunId = req.FlowRunId,
            StepRunId = req.StepRunId,
            RequestId = req.RequestId,
            Result = Tempo.Enums.StepResultTypeEnum.Success,
            Data = new { email = value },
            Metadata = req.Metadata
        });
    }
}
```

Best practices:

- Register built-ins through `StepManager`.
- Keep built-ins for server-owned logic, not tenant-supplied logic.
- Treat them as application code, with the same deployment and versioning discipline as the server itself.

### Built-in method-based steps

You can also register static methods with `[StepMethod]`.

```csharp
using System.Threading.Tasks;

public static class BuiltinSteps
{
    [StepMethod("customer.normalize_email")]
    public static Task<StepResult> NormalizeEmail(StepRequest req)
    {
        string value = (req.Data?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
        return Task.FromResult(new StepResult
        {
            ProtocolVersion = req.ProtocolVersion,
            TenantId = req.TenantId,
            DataFlowId = req.DataFlowId,
            FlowRunId = req.FlowRunId,
            StepRunId = req.StepRunId,
            RequestId = req.RequestId,
            Result = Tempo.Enums.StepResultTypeEnum.Success,
            Data = new { email = value },
            Metadata = req.Metadata
        });
    }
}
```

## Python Steps

Python source steps and Python artifacts are the most ergonomic choice when your logic is simple and JSON-centric.

Tempo calls your function with the parsed request dictionary:

```python
def run(req):
    data = req.get("data") or {}
    value = data.get("value", 0)
    print(f"Double number step received value: {value}")
    return {
        "input": value,
        "value": value * 2
    }
```

What your function receives:

- `req["data"]` is the main payload
- `req["metadata"]` is request metadata
- `req["flowRunId"]`, `req["stepRunId"]`, and `req["requestId"]` are available for correlation

Logging behavior:

- `print(...)` is redirected into the step log
- the root Python `logging` logger is redirected into the step log
- `stderr` is captured

Best practices:

- Return plain dictionaries, lists, strings, booleans, numbers, or `None`.
- Treat `req.get("data")` as untrusted input and validate shape before using it.
- Keep imports package-local or deliberately managed.
- If you need third-party dependencies, coordinate with the operator because dependency installation is disabled unless `allowPythonDependencyInstall` is enabled.

Use `Artifact.Python` when:

- the step is naturally expressed as a Python function
- you want the shim to handle protocol correlation and logging
- your dependency story fits the host's Python policy

## JavaScript Steps

JavaScript source steps and JavaScript artifacts are a good fit for JSON-heavy transformations and API glue.

Tempo calls your exported function with the parsed request object:

```javascript
exports.run = async function(req) {
  const data = req.data || {}
  const value = typeof data === "number" ? data : Number(data.value || 0)
  console.log("Double number step received value:", value)
  return {
    input: value,
    value: value * 2
  }
}
```

Logging behavior:

- `console.log`, `console.info`, `console.warn`, `console.error`, and `console.debug` are redirected into the step log
- `stderr` is captured

Module loading notes:

- CommonJS works well for most Tempo steps
- Tempo can also fall back to ESM import when `require(...)` hits `ERR_REQUIRE_ESM`
- for source steps, the safest path is still a simple exported function in one file

Best practices:

- return plain JSON-compatible objects
- do not rely on ambient global state across runs
- be explicit about number coercion and null handling
- keep logs concise and structured

## Generic `Artifact.Process` Steps

Use `Artifact.Process` when you have an existing executable that can speak the Tempo protocol directly.

Your process must:

1. read one `StepRequest` JSON document from `stdin`
2. produce one `StepResult` JSON document on `stdout`
3. keep all diagnostic text off `stdout`

Pseudo-flow:

```text
stdin  -> parse StepRequest JSON
work   -> run your logic
stdout -> emit exactly one StepResult JSON object
stderr -> write diagnostics
```

Best practices:

- Reserve `stdout` for the protocol result only.
- Write diagnostics to `stderr` or to an invocation-scoped logger.
- Preserve request correlation fields when building the result.
- Exit nonzero only for host-level failures. If you can still emit a valid `StepResult`, do that instead.

Use `Artifact.Process` when:

- you already have a stable CLI tool
- the tool is not naturally wrapped as Python, JavaScript, or .NET
- you need full control over the protocol interaction

## Manual Artifact Packaging

If you are not using source steps, package your code as an artifact zip and include `tempo.step.json` at the zip root.

The manifest defines:

- runtime key
- supported protocol versions
- default entrypoint
- named entrypoints
- environment allowlists

Minimal `Artifact.Python` manifest:

```json
{
  "manifestVersion": "1",
  "runtimeKey": "Artifact.Python",
  "supportedProtocolVersions": ["1.0"],
  "defaultEntrypoint": "main",
  "entrypoints": {
    "main": {
      "module": "handler",
      "function": "run"
    }
  }
}
```

Minimal `Artifact.JavaScript` manifest:

```json
{
  "manifestVersion": "1",
  "runtimeKey": "Artifact.JavaScript",
  "supportedProtocolVersions": ["1.0"],
  "defaultEntrypoint": "main",
  "entrypoints": {
    "main": {
      "module": "handler.js",
      "function": "run"
    }
  }
}
```

Minimal `Artifact.DotnetProcess` manifest:

```json
{
  "manifestVersion": "1",
  "runtimeKey": "Artifact.DotnetProcess",
  "supportedProtocolVersions": ["1.0"],
  "defaultEntrypoint": "main",
  "entrypoints": {
    "main": {
      "command": "dotnet/MyStep.dll",
      "handlerType": "Tempo.UserSteps.Handler",
      "args": ["Tempo.UserSteps.Handler"]
    }
  }
}
```

Minimal `Artifact.Process` manifest:

```json
{
  "manifestVersion": "1",
  "runtimeKey": "Artifact.Process",
  "supportedProtocolVersions": ["1.0"],
  "defaultEntrypoint": "main",
  "entrypoints": {
    "main": {
      "command": "bin/my-tool"
    }
  }
}
```

Packaging rules that matter:

- manifest file name must be `tempo.step.json`
- entrypoint paths must be package-relative
- absolute paths and `..` traversal are rejected
- `Artifact.DotnetProcess` entrypoint command must reference a `.dll`
- environment allowlists contain variable names only

See [ARTIFACT_MANIFEST.md](./ARTIFACT_MANIFEST.md) for the manifest contract.

## `External.Rest` Steps

Use `External.Rest` when you do not need to package code and the real implementation already exists behind HTTP.

Best for:

- calling internal microservices
- invoking third-party APIs
- replacing older inline REST transitions with persisted steps

Best practices:

- keep the step focused on one outbound call
- make the timeout explicit
- validate the response shape with `outputSchema` if downstream steps depend on specific fields
- prefer `External.Rest` over custom code when the step would only proxy one HTTP call

## `Host.Executable` Steps

`Host.Executable` is for operator-owned tools, not tenant-supplied binaries.

Tenant configuration references only:

- `allowListKey`
- tenant-approved arguments

The operator owns:

- the executable path
- working directory
- fixed arguments
- environment allowlist
- maximum runtime
- argument policy

Use it when:

- a trusted local tool already exists on the host
- the operator wants to expose a narrow, controlled interface to tenants

Do not use it when:

- tenants need arbitrary code execution
- the executable path would need to come from tenant input

## Step Design Best Practices

Regardless of runtime:

- Keep one step responsible for one clear unit of work.
- Prefer explicit input and output shapes over ad hoc payload mutation.
- Make outputs stable for downstream steps.
- Log enough to debug, but not so much that logs become unreviewable.
- Put integration-specific code behind helper functions so the step body stays small.
- Treat `req.data` as untrusted input even inside trusted flows.
- Be careful with retries and idempotency when a step calls external systems.
- Do not embed secrets in step definitions, source code, or manifests.

## Troubleshooting

### The step fails with "stdout was not valid StepResult JSON"

Cause:

- your process wrote logs or extra text to `stdout`

Fix:

- keep `stdout` protocol-only
- move diagnostics to `stderr`
- for .NET, avoid `Console.WriteLine(...)` for logs

### Python or JavaScript source step will not run

Cause:

- runtime executable is missing on the worker or server

Fix:

- verify runtime availability through `/v1.0/runtimes` or the dashboard runtime view
- configure `pythonExecutable`, `nodeExecutable`, or `dotnetExecutable`

### C# source step creation fails

Cause:

- Tempo uses `dotnet publish` to package pasted C# source and could not compile it

Fix:

- install a compatible .NET SDK
- verify the `handlerType` exists and inherits `TempoStepHandlerBase` or implements `ITempoStepHandler`
- inspect the compilation error returned by Tempo

### Dependencies do not install for Python

Cause:

- Python dependency installation is disabled by operator settings

Fix:

- enable `allowPythonDependencyInstall` only if that trust model is acceptable
- otherwise vendor dependencies into the package or avoid them

### Logs are missing for a .NET process step

Cause:

- the handler only wrote to `stdout`, or wrote nowhere except the protocol result

Fix:

- inherit `TempoStepHandlerBase` and use `LogInfo`, `LogWarn`, or `LogError`
- if implementing `ITempoStepHandler` directly, use `TempoExecutionContext.Current?.Logger`

## Recommended Starting Point

If you are choosing a runtime from scratch:

1. Use `External.Rest` if there is already an HTTP endpoint that does the work.
2. Use a source step in `Python`, `JavaScript`, or `CSharp` if you want Tempo to own packaging.
3. Use a manually packaged artifact only when you already have a build pipeline or need multiple files.
4. Use `Artifact.Process` only when none of the language-specific runtimes fit.
5. Use built-ins only for server-owned code that ships with Tempo itself.

That sequence keeps the step model readable, supportable, and aligned with how Tempo actually executes work.
