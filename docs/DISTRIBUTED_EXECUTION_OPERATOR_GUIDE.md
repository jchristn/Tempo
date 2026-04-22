# Distributed Execution Operator Guide

This guide covers the operational model introduced in Tempo v0.3.0.

## Overview

Tempo now runs in two planes:

- `Tempo.Server` is the control plane.
- `Tempo.Worker` is the execution plane.

The server owns:

- REST and OpenAPI
- MCP
- scheduling
- persistence
- worker management
- artifact download authorization

Workers own:

- WebSocket session management
- capability advertisement
- assigned run execution
- completion reporting

The server can optionally execute workload itself through a pseudo-worker when:

```json
{
  "engine": {
    "serverCanExecuteWorkload": true
  }
}
```

Set that to `false` when you want a control-plane-only server.

## Core Settings

Distributed scheduling settings live under `engine` and are reboot-required.

```json
{
  "engine": {
    "queueEnabled": true,
    "serverCanExecuteWorkload": false,
    "maxConcurrentRuns": 4,
    "pollIntervalMs": 1000,
    "loadBalancingStrategy": "LeastLoaded",
    "workerHeartbeatTimeoutMs": 30000,
    "leaseDurationMs": 300000,
    "maxAssignmentAttempts": 3,
    "allowDuplicateScheduler": false
  }
}
```

Recommended starting values:

| Setting | Recommendation |
| --- | --- |
| `serverCanExecuteWorkload` | `false` for a dedicated control plane, `true` for single-node or fallback execution |
| `loadBalancingStrategy` | `LeastLoaded` unless you need label-based placement |
| `workerHeartbeatTimeoutMs` | 15 to 30 seconds |
| `leaseDurationMs` | Long enough to cover the longest expected assignment |
| `maxAssignmentAttempts` | 2 to 5 |
| `allowDuplicateScheduler` | Leave `false`; it is an unsupported override |

## Local Compose

The repository ships a local compose example with:

- one `tempo-server`
- three workers: `tempo-worker-1`, `tempo-worker-2`, and `tempo-worker-3`
- one dashboard
- one MCP server

Bring it up from the repo root:

```powershell
docker compose -f .\docker\compose.yaml up -d
```

Local development defaults:

| Item | Value |
| --- | --- |
| Dashboard | `http://localhost:3000` |
| Tempo.Server | `http://localhost:8901` |
| Admin email | `admin@tempo.local` |
| Admin password | `password` |
| Local admin API key | `tempo-local-admin-api-key` |

## Bootstrapping a Worker

Issue a worker token:

```http
POST /v1.0/workers/wrk_example/rotate-token
x-api-key: tempo-local-admin-api-key
```

Response:

```json
{
  "workerId": "wrk_example",
  "token": "key_...",
  "issuedUtc": "2026-04-21T20:00:00.0000000Z"
}
```

Minimal worker settings:

```json
{
  "serverEndpoint": "http://127.0.0.1:8901",
  "workerId": "wrk_example",
  "workerToken": "key_...",
  "name": "wrk_example",
  "kind": "Worker",
  "maxConcurrentRuns": 1,
  "maxTaskTimeoutMs": 300000,
  "labels": ["gpu"]
}
```

Start the worker:

```powershell
dotnet run --project .\src\Tempo.Worker\Tempo.Worker.csproj -- --config .\tempo.worker.json
```

## Placement Strategies

Tempo v0.3.0 ships two strategies:

### `LeastLoaded`

Default strategy. The server chooses the eligible executor with the lowest active assignment count.

### `LabelPinned`

Use when a flow must land on a worker with a specific label.

Set the flow field:

```json
{
  "routingHintLabel": "gpu"
}
```

Set the worker labels:

```json
{
  "labels": ["gpu", "python"]
}
```

If no live executor can satisfy the plan, Tempo fails fast with a `no_eligible_worker` decision instead of silently stalling.

## Worker Operations

List workers:

```http
GET /v1.0/workers
Authorization: Bearer {admin-token}
```

Read one worker:

```http
GET /v1.0/workers/wrk_example
Authorization: Bearer {admin-token}
```

Drain a worker:

```http
POST /v1.0/workers/wrk_example/drain
x-api-key: tempo-local-admin-api-key
```

Resume a worker:

```http
POST /v1.0/workers/wrk_example/resume
x-api-key: tempo-local-admin-api-key
```

Block a worker:

```http
POST /v1.0/workers/wrk_example/block
x-api-key: tempo-local-admin-api-key
```

Unblock a worker:

```http
POST /v1.0/workers/wrk_example/unblock
x-api-key: tempo-local-admin-api-key
```

Operational guidance:

1. Drain a worker before planned maintenance when you want in-flight work to finish.
2. Block a worker when you need it disconnected immediately or want to deny reconnect attempts.
3. Wait for `activeAssignmentCount` to reach zero when draining gracefully.
4. Stop or restart the worker.
5. Resume or unblock it when ready.

## Dashboard Surface

The dashboard adds:

- `Workers` view with filters plus block/unblock and drain/resume actions
- `Logs` view for browsing file-backed server and worker logs
- worker detail drawer
- worker heartbeat recency chart
- run placement columns in the `Runs` view
- distributed execution settings in `Settings`

Use the `Runs` view to inspect:

- `dispatchState`
- `assignedWorkerId`
- `executionNodeKind`
- `runAssignmentId`
- `dispatchAttempt`

The `Workers` view also deep-links directly into the `Logs` page for the
selected worker.

## File-Backed Logs

The Docker deployment stores worker logs on a named shared volume so the
control plane can expose them safely without shelling into containers.

Conceptual layout:

```text
/var/lib/tempo-server/logs/tempo.log
/var/lib/tempo-server/worker-logs/wrk_docker_1/tempo-worker.log
/var/lib/tempo-server/worker-logs/wrk_docker_2/tempo-worker.log
/var/lib/tempo-server/worker-logs/wrk_docker_3/tempo-worker.log
```

Operational notes:

1. Each worker writes to its own subdirectory keyed by worker ID.
2. `Tempo.Server` mounts the same worker-log root read-only for the admin log viewer.
3. Current log files are cleared by truncation; archived log files are deleted outright.
4. Bounded reads clamp `tailLines` and `maxBytes` to configured limits.

Available operator surfaces:

- Dashboard: `/dashboard/logs`
- REST: `/v1.0/logs/sources`, `/v1.0/logs/files`, `/v1.0/logs/files/content`, `/v1.0/logs/files/download`
- MCP: `listLogSources`, `listLogFiles`, `readLogFile`, `downloadLogFile`, `deleteLogFile`

If worker logs are not visible, check:

1. the shared `tempo_worker_logs` named volume exists
2. each worker has a distinct `TEMPO_WORKER_LOG_DIRECTORY`
3. the server has `TEMPO_LOG_VIEWER_WORKER_ROOT` pointing at the mounted worker-log root

## Recovery Model

Tempo uses at-least-once delivery.

Recovery events:

- worker disconnect
- stale heartbeat
- expired lease
- duplicate or stale completion

What to expect:

| Event | Server behavior |
| --- | --- |
| Worker disconnect before completion | Requeue the run if retry budget remains |
| Lease expiry | Recover the assignment and retry or fail once attempts are exhausted |
| Duplicate completion | Ignore it and record `orphan_completion` |
| Second live scheduler detected | New server starts in API-only mode unless unsupported override is enabled |

Design flows and downstream integrations to tolerate retries.

## Troubleshooting

### Runs stay queued

Check:

1. `GET /v1.0/workers`
2. worker `state`
3. worker `enabled`
4. worker `drainMode`
5. `serverCanExecuteWorkload`

If `serverCanExecuteWorkload = false` and there are no connected workers, new runs remain queued and HTTP trigger wait can still return `202`.

### Run failed with no eligible worker

Compare:

- flow `routingHintLabel`
- worker `labels`
- worker `capabilities`
- runtime requirements in the flow's steps

Typical causes:

- built-in-only flow with remote workers only
- flow pinned to a missing label
- worker missing the required runtime

### Worker connects then drops

Check:

1. `workerHeartbeatTimeoutMs`
2. network path between worker and server
3. worker token freshness
4. server logs for worker authentication failures

Rotate the token if needed:

```http
POST /v1.0/workers/wrk_example/rotate-token
```

### Duplicate server detected

Tempo v0.3.0 is single-scheduler by design. If another live server heartbeat is present, the new server suppresses scheduling and stays API-only.

## Recommended Runbook

1. Start `Tempo.Server`.
2. Verify `/v1.0/api/health`.
3. Rotate a worker token.
4. Start one or more workers.
5. Verify `GET /v1.0/workers`.
6. Create or update flows.
7. Use `routingHintLabel` only where needed.
8. Test one run and inspect placement in `Runs`.
9. Drain workers before maintenance.
10. Review failed runs for `dispatchState`, `dispatchAttempt`, and worker placement metadata.
