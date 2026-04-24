# Tempo Best Practices

This guide describes practical patterns for building, managing, and operating Tempo data flows, steps, triggers, artifacts, and runs.

Tempo is most reliable when teams treat each flow as a small, observable pipeline:

```text
artifact files or built-in code -> step -> data flow -> trigger -> run history
```

## Core Principles

| Principle | Practice |
| --- | --- |
| Make execution explicit | Use stable step `executionKey` values and clear flow transition names |
| Keep units small | Prefer steps that perform one operation and return one well-defined output |
| Preserve observability | Inspect flow runs and step runs after every new trigger or artifact change |
| Treat placement as an operator concern | Use worker labels and `routingHintLabel` intentionally rather than relying on incidental worker choice |
| Pin where reproducibility matters | Use immutable artifact version labels for production-critical behavior |
| Use mutable current intentionally | Use editable artifact files for iteration and local development |
| Protect production records | Set `isProtected` on production steps, flows, triggers, artifacts, and pinned artifact versions |
| Avoid hidden dependencies | Check runtime availability for Python, Node.js, .NET, process, and host executable steps |

## Naming and Keys

Tempo has both server-generated IDs and user-chosen keys.

Use server-generated IDs for REST paths:

```text
step_...
flow_...
trg_...
art_...
run_...
```

Use step `executionKey` values in data flow transitions:

```json
{
  "startStepId": "orders.validate",
  "transitions": {
    "orders.validate": {
      "onSuccess": "orders.enrich"
    }
  }
}
```

Recommended execution key format:

```text
{domain}.{verb_or_noun}
{domain}.{workflow}.{step}
```

Examples:

```text
orders.validate
orders.enrich_customer
orders.notify_failure
setup.echo
setup.random_1_to_10
setup.double_number
```

Avoid:

| Pattern | Problem |
| --- | --- |
| `step1`, `step2` | Meaning is lost when flows grow |
| Display names as keys | Renames become risky |
| Generated IDs as execution keys | Less readable and harder to audit |
| Environment-specific keys | Makes promotion harder |

Keep display `name` friendly. Keep `executionKey` stable.

## Step Design

A step should have one clear responsibility.

Good step boundaries:

| Step | Responsibility |
| --- | --- |
| `orders.validate` | Check required input and return normalized order data |
| `orders.enrich_customer` | Add customer data from another system |
| `orders.calculate_tax` | Calculate tax fields |
| `orders.notify_failure` | Send a failure notification |

Poor step boundaries:

| Step | Problem |
| --- | --- |
| `orders.process_everything` | Hard to test and retry |
| `orders.helper` | Purpose is unclear |
| `orders.call_api_and_save_and_notify` | Too many failure modes in one unit |

### Input and Output Shape

Return JSON objects with explicit fields. Avoid changing the top-level type between runs.

Good:

```json
{
  "ok": true,
  "orderId": "ord_123",
  "normalized": {
    "total": 42.5
  }
}
```

Avoid:

```json
"sometimes a string"
```

Use consistent failure payloads when a step handles recoverable business failures:

```json
{
  "ok": false,
  "reason": "missing_customer_id",
  "message": "customerId is required"
}
```

Throw exceptions for unexpected failures that should route through `onException`.

### Contracts and Schemas

Use `contractType`, `inputSchema`, `outputSchema`, `validateInput`, and `validateOutput` when a step sits on a boundary:

| Boundary | Recommendation |
| --- | --- |
| Public trigger input | Validate the first step input |
| External REST response | Validate output before chaining |
| Shared reusable step | Validate input and output |
| Internal throwaway prototype | Loose contract is acceptable |

Keep schemas focused. Overly broad schemas do not catch errors; overly strict schemas slow iteration.

### Timeouts

Set `maxRuntimeMs` on process-backed and network-backed steps.

Recommended starting points:

| Step type | Suggested timeout |
| --- | --- |
| Simple source step | 5000 to 15000 ms |
| External REST call | External timeout plus a small buffer |
| Multi-call enrichment | 30000 to 60000 ms |
| Long batch-like operation | Prefer a background workflow design instead |

Set data flow `maxRuntimeMs` high enough to cover the whole chain plus overhead. HTTP trigger invocation waits for flow completion up to the flow budget plus a small buffer, within server clamps.

## Runtime Selection

Choose the least powerful runtime that solves the problem.

| Need | Prefer |
| --- | --- |
| Built-in server behavior | `Builtin.Method` or `Builtin.Class` |
| HTTP call to another service | `External.Rest` |
| User-pasted Python | `Artifact.Python` via `/steps/source` |
| User-pasted JavaScript | `Artifact.JavaScript` via `/steps/source` |
| User-pasted C# | `Artifact.DotnetProcess` via `/steps/source` |
| Existing CLI packaged in an artifact | `Artifact.Process` |
| Operator-owned host binary | `Host.Executable` |

Use `/v1.0/runtimes` or the MCP `runtime_list` tool before creating steps that depend on `python`, `node`, `dotnet`, or configured host executables.

## Distributed Execution

Tempo v0.3.0 schedules whole flow runs onto either the server pseudo-worker or a remote `Tempo.Worker`.

Recommended operator settings:

| Setting | Guidance |
| --- | --- |
| `engine.serverCanExecuteWorkload` | Set `false` when Tempo.Server should be control-plane only |
| `engine.loadBalancingStrategy` | Use `LeastLoaded` by default; use `LabelPinned` only when a flow really needs a labeled worker pool |
| `engine.workerHeartbeatTimeoutMs` | Keep it short enough to recover dead workers quickly but long enough to tolerate expected network jitter |
| `engine.leaseDurationMs` | Set longer than the expected longest assignment dispatch-to-completion interval |
| `engine.maxAssignmentAttempts` | Keep retries bounded so dead workers do not cause infinite churn |

### Worker Labels and Routing Hints

Use worker labels for durable placement constraints, not for ad hoc queueing.

Good examples:

| Label | Meaning |
| --- | --- |
| `python` | Worker has the required Python environment or packages |
| `gpu` | Worker has GPU access |
| `isolated` | Worker pool is reserved for a sensitive workflow class |

Set a flow's `routingHintLabel` only when the flow genuinely needs that pool. Otherwise leave placement to `LeastLoaded`.

### Recovery Expectations

Distributed execution is at-least-once. Design flows so a retried assignment does not produce unsafe side effects.

Practical guidance:

1. Make outbound calls idempotent when possible.
2. Use stable business identifiers in payloads so downstream systems can deduplicate.
3. Inspect `dispatchAttempt`, `assignedWorkerId`, and `runAssignmentId` when diagnosing recovery.
4. Drain workers before maintenance instead of killing them mid-run.

### Built-In Steps

Built-in steps are best for stable platform behavior:

| Use built-in when | Reason |
| --- | --- |
| The behavior ships with Tempo.Server | Deployment is controlled with server versioning |
| The code needs direct access to server services | It avoids subprocess overhead |
| Performance matters | In-process execution avoids external runtime startup |

Use `/v1.0/steps/registered` to discover registrations. If a built-in step appears orphaned, compare the step runtime config with the registered `identifier`, `declaringType`, `methodName`, `assemblyName`, and `signatureHash`.

### External REST Steps

Use `External.Rest` for simple request-forwarding and integration calls.

Best practices:

1. Set `timeoutMs`.
2. Use explicit HTTP method.
3. Include required headers in the step config.
4. Keep secrets out of persisted headers when possible; use environment or gateway configuration.
5. Validate output before chaining to business-critical steps.

Avoid inline REST transitions in new flows. Persist REST behavior as steps so deletion guards, runtime validation, and run history remain consistent.

### Source Steps

Use `/v1.0/tenants/{tenantId}/steps/source` or MCP `step_create_from_source` for pasted single-file code.

Provide:

| Field | Why |
| --- | --- |
| `executionKey` | Keeps flows readable and stable |
| `fileName` | Makes artifact editing understandable |
| `function` | Makes Python and JavaScript handler resolution explicit |
| `handlerType` | Makes C# handler resolution explicit, typically a `TempoStepHandlerBase` subclass |
| `maxRuntimeMs` | Prevents runaway code |

Python handler shape:

```python
def run(input):
    return {"ok": True, "input": input}
```

JavaScript handler shape:

```javascript
exports.run = async function(input) {
  return { ok: true, input };
};
```

C# source-step packages compile to an executable entrypoint. Editing `.cs` files inside the artifact is useful for inspection and versioning, but it does not recompile the assembly by itself. To change C# runtime behavior, recreate the source step or upload a rebuilt artifact package.

C# handler shape:

```csharp
public sealed class Handler : TempoStepHandlerBase
{
    public override Task<StepResult> RunAsync(StepRequest request, CancellationToken token)
    {
        LogInfo("processing request " + request.RequestId);
        return Task.FromResult(Success(request, new { ok = true, input = request.Data }));
    }
}
```

## Artifact Management

Artifacts are mutable by design in Tempo. The file editor and file REST endpoints operate on individual files and rebuild the mutable current snapshot.

Use artifacts in two modes:

| Mode | Use |
| --- | --- |
| Mutable `current` | Fast iteration, dashboard editing, local testing |
| Pinned version | Production reproducibility and auditability |

### File Layout

Use clear package paths:

```text
handler.py
handler.js
src/Handler.cs
manifest.json
README.md
```

Avoid:

| Pattern | Problem |
| --- | --- |
| Deep generated paths | Hard to edit in the dashboard |
| Random file names | Hard to connect steps to source |
| Multiple unrelated handlers in one artifact | Makes versioning and rollback unclear |

### Editing Files

When saving artifact files:

1. Keep paths artifact-relative.
2. URL-encode nested paths in REST queries.
3. Use accurate `contentType`.
4. Check `snapshotUpdated`.
5. If `snapshotError` is present, fix the package before invoking dependent steps.

After edits, run a trigger or direct flow run and inspect:

| Field | Where |
| --- | --- |
| `artifactId` | Step run |
| `artifactVersion` | Step run |
| `artifactSha256` | Step run |
| `manifestEntrypoint` | Step run |

### Versioning Policy

Recommended policy:

| Environment | Artifact version |
| --- | --- |
| Local development | `current` |
| Shared test | Build label, for example `test-20260420.1` |
| Production | Immutable release label, for example `1.4.0` |

When promoting from `current` to production, upload or preserve a named version and update the step runtime config to that version.

### Artifact Deletion

Artifact deletion is blocked when:

| Condition | Meaning |
| --- | --- |
| Artifact is protected | It is intentionally retained |
| A step references the artifact | Deleting it would break execution |
| A step references a specific artifact version | Version deletion would break execution |

Do not force-delete artifact files from storage outside Tempo. Run history depends on artifact metadata for audit and diagnostics.

## Data Flow Design

Design flows around the path of data, not around UI screens or implementation classes.

Good flow:

```text
orders.validate -> orders.enrich_customer -> orders.calculate_tax -> orders.persist
```

Good failure branch:

```text
orders.validate --onFailure--> orders.reject
orders.enrich_customer --onException--> orders.notify_failure
```

### Transition Rules

Use transition fields consistently:

| Field | Use |
| --- | --- |
| `onSuccess` | Normal continuation |
| `onFailure` | Expected business failure or step error result |
| `onException` | Unexpected exception, timeout, or runtime crash |
| `maxTransitions` | Guard loops and retries |

A terminal step should set next-step fields to `null`.

### Chaining

The next step receives the previous step output as input. Design outputs as inputs to the next step.

Example:

Step 1 returns:

```json
{
  "number": 7
}
```

Step 2 expects:

```json
{
  "number": 7
}
```

Step 2 returns:

```json
{
  "number": 7,
  "doubled": 14
}
```

The flow's final response is the output of the last step.

### Loops and Retries

Use loops sparingly. Always set `maxTransitions` for any step that can route back to itself or to an earlier step.

Prefer explicit retry steps when retries have business meaning:

```text
send_email -> wait_before_retry -> send_email
```

Avoid hidden retry loops inside a step unless they are bounded and logged.

### Flow Timeouts

Set `maxRuntimeMs` on flows exposed by HTTP triggers. This controls how long the public trigger route can wait for a result. If it is too low, clients receive `202` while the run may still be processing. If it is too high, clients wait too long on synchronous trigger calls.

## Trigger Design

Triggers are how external callers invoke flows.

### HTTP Method Selection

Use:

| Method | Best for |
| --- | --- |
| `GET` | No-input or query-independent actions, demos, generated examples |
| `POST` | Workflows with a JSON request body |

For chained flows that generate their own data, a GET trigger is simpler and avoids meaningless request bodies.

Always set explicit `allowedMethods`:

```json
{
  "allowedMethods": ["POST"]
}
```

or:

```json
{
  "allowedMethods": ["GET"]
}
```

### Trigger Response Model

HTTP triggers return:

| Location | Data |
| --- | --- |
| Response body | Final flow output on success |
| Response headers | Run metadata |

Important headers:

```text
x-tenant-id
x-run-id
x-dataflow-id
x-trigger-id
x-run-state
x-run-created-utc
x-run-started-utc
x-run-completed-utc
x-run-last-update-utc
x-runtime-ms
x-run-error
```

Always log or capture `x-run-id` when a caller reports a problem.

### Public Trigger Safety

Public trigger endpoints are not tenant-scoped and are intended for invocation. Treat trigger IDs as sensitive.

For flows that should not be callable by anyone holding the trigger URL, set the flow's `invocationAuthMode` to `ApiAuthenticated`. The HTTP trigger route will then require the same Tempo API credentials used by management endpoints and will only enqueue the run if the principal can act on the flow's tenant.

For production:

1. Put Tempo behind TLS.
2. Use `ApiAuthenticated` for tenant-private flows.
3. Use a gateway if external caller authentication, rate limiting, or request signing needs to differ from Tempo API authentication.
4. Prefer POST for user-provided input.
5. Validate the first step input.
6. Set flow and step timeouts.
7. Monitor failed and exceptioned runs.

## Monitoring and Operations

Every run should be diagnosable from its run record and step runs.

Start with:

```http
GET /v1.0/tenants/{tenantId}/runs/{runId}
GET /v1.0/tenants/{tenantId}/runs/{runId}/steps
```

Review:

| Question | Field |
| --- | --- |
| Did the flow finish? | Run `state` |
| Where did it run? | Run `assignedWorkerId`, `executionNodeKind`, `dispatchAttempt` |
| What did the caller send? | Run `inputData` |
| What did the flow return? | Run `outputData` |
| Which step failed? | Step run `result` and `errorMessage` |
| What ran first? | Step run `sequence` |
| Which artifact executed? | Step run `artifactId`, `artifactVersion`, `artifactSha256` |
| Was there runtime queue pressure? | Step run `capacityWaitMs` |
| How long did the HTTP trigger wait? | Trigger response `x-runtime-ms` |

### Run States

| State | Operational meaning |
| --- | --- |
| `Queued` | Worker has not started the run |
| `Running` | The flow is executing |
| `Succeeded` | The last selected path completed successfully |
| `Failed` | A step returned a failure result |
| `Exception` | A step or runtime threw, timed out, or crashed |
| `Cancelled` | Run was cancelled before completion |

### Step Results

| Result | Operational meaning |
| --- | --- |
| `Success` | Step completed normally |
| `Timeout` | Step exceeded its runtime budget |
| `Error` | Step returned an error/failure result |
| `Exception` | Step threw or runtime failed |
| `MaxIterationsExceeded` | Transition guard stopped a loop |

### Capacity Monitoring

Process-backed runtimes use external execution capacity. Watch:

| Metric | Source |
| --- | --- |
| Runtime availability | `/v1.0/runtimes` |
| External capacity state | `/v1.0/runtimes/external-execution` |
| Tenant capacity state | `/v1.0/tenants/{tenantId}/runtimes/external-execution` |
| Per-step wait | Step run `capacityWaitMs` |

High `capacityWaitMs` means the runtime is saturated or capacity limits are too low for the workload.

## Deletion and Retention

Tempo protects relationships between objects.

| Delete target | Blocked when |
| --- | --- |
| Step | Protected, or referenced by any data flow transition/start step |
| Data flow | Protected, or referenced by any trigger |
| Trigger | Protected |
| Artifact | Protected, or referenced by any step |
| Artifact version | Protected, or referenced by any step |
| Run | Retention and permissions allow deletion; historical diagnostics are lost |

Prefer this order when cleaning up:

1. Inactivate the trigger.
2. Delete or update triggers that reference the flow.
3. Inactivate the flow.
4. Update flows to remove step references.
5. Delete unused steps.
6. Delete unused artifacts or versions after no steps reference them.

Set `isProtected` on known-good production records.

## Security

Tempo can run external code through artifact-backed runtimes and host executables. Treat this as privileged infrastructure.

Minimum production controls:

| Control | Why |
| --- | --- |
| Tenant isolation | Prevent cross-tenant data access |
| Runtime allowlists | Prevent arbitrary host command execution |
| Timeouts | Bound runaway workloads |
| Capacity limits | Prevent one tenant or flow from exhausting the host |
| Secret management | Avoid storing secrets in step JSON |
| Audit run history | Investigate what code and artifact version ran |
| TLS and gateway auth | Protect public trigger calls |

Do not accept untrusted code into artifact-backed runtimes without sandboxing, quotas, and review appropriate for the deployment.

## Environment Dependencies

Tempo.Server probes runtime dependencies from configured executable names or paths.

Common dependencies:

| Runtime | Dependency |
| --- | --- |
| `Artifact.Python` | Python executable |
| `Artifact.JavaScript` | Node.js executable |
| `Artifact.DotnetProcess` | .NET executable |
| `Artifact.Process` | Entrypoint executable available inside the artifact package |
| `Host.Executable` | Server-configured allowlisted host executable |

The server can start when optional runtime dependencies are missing, but affected runtime providers report unavailable or missing dependency states. This allows built-in, REST, and other available runtime types to keep working.

Check runtime state during startup validation and before enabling flows that depend on external runtimes.

## Testing Strategy

Test at four levels.

### Step Test

Create the step and invoke it in the smallest possible flow. Confirm:

1. Input shape is accepted.
2. Output shape is stable.
3. Timeout is appropriate.
4. Errors route as expected.

### Flow Test

Run direct enqueue:

```http
POST /v1.0/tenants/{tenantId}/flows/{flowId}/runs
```

Then poll:

```http
GET /v1.0/tenants/{tenantId}/runs/{runId}
GET /v1.0/tenants/{tenantId}/runs/{runId}/steps
```

Confirm each branch:

| Branch | How to test |
| --- | --- |
| Success | Valid input |
| Failure | Expected business-invalid input |
| Exception | Controlled runtime error or dependency failure |
| Loop guard | Input that would revisit a transition |

### Trigger Test

Invoke the public trigger exactly as clients will invoke it.

For POST:

```cmd
curl.exe -i -X POST "http://localhost:8901/v1.0/triggers/http/trg_example" -H "Content-Type: application/json" -d "{\"value\":\"hello\"}"
```

For GET:

```cmd
curl.exe -i "http://localhost:8901/v1.0/triggers/http/trg_example"
```

Confirm:

1. Response body is the final flow output.
2. `x-run-state` is `Succeeded`.
3. `x-runtime-ms` is present.
4. `x-run-id` can be used with run monitoring endpoints.

### Artifact Edit Test

After editing an artifact file:

1. Confirm save response has `snapshotUpdated: true`.
2. Fire the dependent trigger.
3. Inspect step run `artifactSha256`.
4. Confirm output changed as expected.

For C# source-step artifacts, rebuild or recreate the source step rather than expecting `.cs` edits alone to change runtime behavior.

## Promotion Checklist

Before promoting a flow to production:

1. All transition keys resolve to active steps.
2. All runtime configs validate.
3. External runtimes are available on the target host.
4. Artifacts are pinned where reproducibility matters.
5. Flow `maxRuntimeMs` is set.
6. Process-backed steps have timeouts.
7. Flow `invocationAuthMode` matches the intended exposure: `Public` for URL-capability calls, `ApiAuthenticated` for tenant-private calls.
8. Public trigger `allowedMethods` is explicit.
9. First-step input validation is enabled for public input.
10. Trigger invocation has been tested with production-like curl or client code, including auth headers when `ApiAuthenticated` is used.
11. Run and step-run records are reviewed.
12. Production records are protected.
13. Rollback artifact versions or previous flow definitions are available.

## Troubleshooting Patterns

### Trigger returns metadata instead of expected output

Current behavior should return the final flow output as the body and run metadata in headers. If the body is a run object, the caller is likely using direct flow enqueue instead of public trigger invocation, or is hitting an older server build.

Use:

```text
/v1.0/triggers/http/{triggerId}
```

not:

```text
/v1.0/tenants/{tenantId}/flows/{flowId}/runs
```

when a synchronous client response is desired.

### Step delete is blocked

Find flows that reference the step `executionKey`. Update or delete those flows first. Deleting a step used by a flow is intentionally blocked.

### Flow delete is blocked

Find triggers that reference the flow ID. Delete or update those triggers first.

### Artifact delete is blocked

Find steps with runtime config referencing the artifact ID or artifact version. Update or delete those steps first.

### Source step cannot be created

Check:

1. `runtime_list` or `/v1.0/runtimes`.
2. Server settings for Python, Node.js, and .NET executable paths.
3. Source request fields: `name`, `language`, `code`, `function`, `handlerType`, `fileName`.
4. Server logs for packaging or compile errors.

### GET trigger receives 405

The trigger's `configuration.allowedMethods` does not include `GET`. Update the trigger configuration or invoke with an allowed method.

### Trigger returns 202

The flow did not finish before the trigger wait budget. Read the `x-run-id` header and poll the run. Increase flow `maxRuntimeMs` only if the workflow is expected to complete synchronously and clients can wait.

### Flow output is not what the setup wizard or dashboard showed

Compare invocation type:

| Invocation | Response body |
| --- | --- |
| Direct flow run enqueue | Run record |
| HTTP trigger fire | Final flow output |

Use trigger response headers for run metadata.

## Minimal Good Flow Template

Use this as a starting point:

1. Create an echo or validation step with a stable execution key.
2. Create a one-step flow that terminates on success.
3. Create an HTTP POST trigger with explicit `allowedMethods`.
4. Invoke the trigger and capture `x-run-id`.
5. Inspect `/runs/{id}/steps`.
6. Add the next step and update `onSuccess`.
7. Add failure or exception branches.
8. Set timeouts and validation.
9. Pin artifacts or protect records before production use.
