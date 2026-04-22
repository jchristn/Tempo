# Tempo Worker Protocol

This document defines the v0.3.0 WebSocket protocol used between `Tempo.Server` and `Tempo.Worker`.

The protocol is intentionally small:

- whole-flow-run assignment only
- authenticated worker sessions
- at-least-once delivery
- server-owned persistence and completion application

Workers do not connect to the Tempo database.

Workers do write per-run log files to the shared `runLogs.rootPath` configured
in `tempo.worker.json` or `TEMPO_RUN_LOG_ROOT`. Those logs are later surfaced
through tenant-scoped run-log APIs and the dashboard `Runs` view.

## Transport

- Endpoint: `GET /v1.0/workers/connect`
- Protocol: WebSocket text frames carrying JSON
- Authentication headers:
  - `x-worker-id`
  - `x-worker-token`

The worker token is issued by:

```text
POST /v1.0/workers/{id}/rotate-token
```

Workers can be blocked by an administrator with:

```text
POST /v1.0/workers/{id}/block
```

Blocked workers are disconnected and subsequent connection attempts are denied until:

```text
POST /v1.0/workers/{id}/unblock
```

## Session Flow

1. Worker opens the WebSocket with `x-worker-id` and `x-worker-token`.
2. Server authenticates the headers.
3. Worker sends `hello`.
4. Server persists the worker session, records the `hello` payload to `worker_activity`, and replies with `hello-ack`.
5. Worker sends periodic `heartbeat` frames.
6. Server sends `assign`, `drain`, or `resume` frames.
7. Worker replies with `assign-ack` for each assignment and later `run-completed` when the run reaches a terminal state.

## Delivery Semantics

- Delivery is at-least-once.
- A `flow_run` is assigned as one unit.
- A run may be retried on a new assignment when a worker disconnects or a lease expires.
- The server accepts a completion only when all of these match the current assignment:
  - `runAssignmentId`
  - `leaseToken`
  - `workerSessionId`
- Late or duplicate completions that do not match are ignored and recorded as `worker_activity.event_type = "orphan_completion"`.
- v0.3.0 does not support in-flight cancel for already-running remote assignments.

## Frame Types

### `hello`

Worker-to-server registration frame sent immediately after connect.

```json
{
  "type": "hello",
  "protocolVersion": "1.0",
  "workerId": "wrk_docker_1",
  "name": "tempo-worker-1",
  "kind": "Worker",
  "version": "0.3.0",
  "hostName": "worker-host",
  "maxConcurrentRuns": 1,
  "maxTaskTimeoutMs": 300000,
  "labels": ["gpu"],
  "capabilities": [
    {
      "executionKey": "*",
      "tenantScope": "*",
      "sourceKind": "Registry",
      "runtimeKey": "External.Rest",
      "signatureHash": "*"
    }
  ]
}
```

Notes:

- `capabilities` are matched per required step capability.
- `maxTaskTimeoutMs` is a worker-local execution ceiling for one assigned task. `0` means no explicit worker timeout.
- Wildcards are allowed for `executionKey`, `tenantScope`, and `signatureHash`.

### `hello-ack`

Server-to-worker acknowledgement.

```json
{
  "type": "hello-ack",
  "protocolVersion": "1.0",
  "workerId": "wrk_docker_1",
  "workerSessionId": "wse_...",
  "heartbeatIntervalMs": 10000,
  "heartbeatTimeoutMs": 30000,
  "leaseDurationMs": 300000,
  "drainMode": false
}
```

## `heartbeat`

Worker-to-server liveness frame.

```json
{
  "type": "heartbeat",
  "workerId": "wrk_docker_1",
  "workerSessionId": "wse_...",
  "activeRuns": 0,
  "sentUtc": "2026-04-21T20:00:00.0000000Z"
}
```

If the server does not observe a heartbeat within `engine.workerHeartbeatTimeoutMs`, the session is considered stale and active assignments are recovered.

## `assign`

Server-to-worker run-delivery frame.

```json
{
  "type": "assign",
  "assignment": {
    "id": "ras_...",
    "flowRunId": "run_...",
    "workerId": "wrk_docker_1",
    "workerSessionId": "wse_...",
    "attemptNumber": 1,
    "state": "Assigned",
    "leaseToken": "non_...",
    "leaseExpiresUtc": "2026-04-21T20:05:00.0000000Z",
    "assignedUtc": "2026-04-21T20:00:00.0000000Z",
    "completedUtc": null
  },
  "plan": {
    "flowRunId": "run_...",
    "tenantId": "ten_...",
    "dataFlowId": "flow_...",
    "placementLabel": "gpu",
    "requiredCapabilities": [],
    "flow": {},
    "steps": {},
    "executionSnapshot": {},
    "budget": {}
  }
}
```

The plan is the server-resolved execution contract. Workers execute the plan as-is and do not query the server database for step resolution.

## `assign-ack`

Worker-to-server acknowledgement that the assignment frame was received and either accepted or rejected.

```json
{
  "type": "assign-ack",
  "workerId": "wrk_docker_1",
  "workerSessionId": "wse_...",
  "runAssignmentId": "ras_...",
  "leaseToken": "non_...",
  "accepted": true,
  "message": null
}
```

If `accepted` is `false`, `message` should explain the reason, such as drain mode or max concurrency.

## `run-completed`

Worker-to-server terminal completion frame.

```json
{
  "type": "run-completed",
  "completion": {
    "flowRunId": "run_...",
    "runAssignmentId": "ras_...",
    "workerId": "wrk_docker_1",
    "workerSessionId": "wse_...",
    "leaseToken": "non_...",
    "finalState": "Succeeded",
    "outputData": "{\"ok\":true}",
    "errorMessage": null,
    "executionSnapshotJson": "{...}",
    "stepRuns": [],
    "completedUtc": "2026-04-21T20:00:03.0000000Z"
  }
}
```

The server is the only component that mutates authoritative run state.

## `drain`

Server-to-worker control frame instructing the worker to stop accepting new assignments.

```json
{
  "type": "drain",
  "workerId": "wrk_docker_1",
  "message": "operator_request"
}
```

Workers should continue current assignments but reject new ones while draining.

## `resume`

Server-to-worker control frame that clears drain mode.

```json
{
  "type": "resume",
  "workerId": "wrk_docker_1",
  "message": "operator_request"
}
```

## Recovery Rules

- Disconnect before `assign-ack`: the server recovers the assignment and requeues the run.
- Disconnect after `assign-ack` but before `run-completed`: the server recovers the assignment and requeues the run.
- Lease expiry with no completion: the server marks the assignment recovered and either retries or fails the run once `engine.maxAssignmentAttempts` is exhausted.
- Duplicate or stale `run-completed`: ignored, persisted as `orphan_completion`.

## Capability Matching

Workers advertise capabilities as:

```text
{ executionKey, tenantScope, sourceKind, runtimeKey, signatureHash }
```

The server generates required capabilities from the resolved execution plan. A worker is eligible only when every required capability has a matching advertised capability.

## Artifact Download

Large artifact payloads do not travel over the worker WebSocket.

Workers fetch artifact bytes over HTTP using worker-scoped authorization headers tied to:

- `workerId`
- `runAssignmentId`
- `leaseToken`

The server validates that the requested artifact hash exists in the run's execution snapshot for the active assignment.

## Execution Logging

During assignment execution, the worker and runtime stack write attempt-scoped
files beneath the shared run-log root. The worker protocol itself does not ship
log bytes over the WebSocket.

Expected file classes:

- `run.log` for high-level flow lifecycle events
- `worker.log` for assignment acceptance, completion, timeout, and cancellation
- `host.log` for runtime-host and protocol diagnostics
- per-step `.log` files for handler-written output
- per-step `.stderr.log` files for captured stderr

Important rule:

- `stdout` remains reserved for the final `StepResult` JSON when the worker launches external runtimes

This is why Tempo redirects user logging to files and stderr instead of allowing arbitrary stdout writes from step code.
