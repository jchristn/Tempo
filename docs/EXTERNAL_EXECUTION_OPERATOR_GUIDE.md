# External Execution Operator Guide

Artifact-backed process execution is available implicitly. Configure capacity,
scratch, and cache settings before accepting tenant-supplied packages on hosts
that need tighter resource controls. Enable `Host.Executable` only when the
host has appropriate OS-level controls for that trust model.

Tempo.Server starts even when optional runtime commands are missing. Runtime
providers that depend on missing commands are reported as `MissingDependency`
and are not seeded as startup templates. Configure command names or absolute
paths per host; bare command names are resolved through the service process
`PATH` on Linux, macOS, and Windows.

Detailed execution logs for artifact-backed runs are file-backed under the
shared `runLogs.rootPath`. Those logs survive container restarts when the root
is backed by persistent storage and are exposed through the tenant-scoped run
activity and run-log APIs.

```json
{
  "runtimes": {
    "externalExecution": {
      "maxConcurrentProcessesServerWide": 4,
      "maxConcurrentProcessesPerTenant": 1,
      "scratchRoot": "./scratch",
      "cacheRoot": "./artifact-cache",
      "pythonExecutable": "python",
      "nodeExecutable": "node",
      "dotnetExecutable": "dotnet"
    },
    "hostExecutables": {
      "enabled": true,
      "allowList": [
        {
          "key": "fixture",
          "displayName": "Fixture tool",
          "executablePath": "C:/tools/fixture.exe",
          "argumentPolicy": {
            "allowAdditionalArguments": false,
            "allowedValues": ["--mode=safe"],
            "allowedPrefixes": ["--input="]
          },
          "environmentAllowList": [],
          "maxRuntimeMs": 5000
        }
      ]
    }
  }
}
```

Tenants submit `allowListKey`, never `executablePath`. Changing external
execution limits, cache/scratch roots, or host executable allowlist settings
requires a server restart.

`Artifact.Python` uses `pythonExecutable`, `Artifact.JavaScript` uses
`nodeExecutable`, and `Artifact.DotnetProcess` uses `dotnetExecutable` when
launching `.dll` artifacts. Pasted C# source steps also use `dotnetExecutable`
and require a .NET SDK because Tempo compiles the submitted source with
`dotnet publish`.

Use low per-tenant concurrency until workload behavior is known. Keep scratch
and cache roots on storage that can be cleaned independently from application
configuration and database files.

## Run Log Contract

Tempo v0.3.0 reserves `stdout` for protocol JSON. User logs must not be written
to stdout for `Artifact.Process`, `Artifact.Python`, `Artifact.JavaScript`, or
`Artifact.DotnetProcess` steps.

Tempo passes these environment variables into process-backed step runtimes:

| Variable | Purpose |
| --- | --- |
| `TEMPO_RUN_LOG_DIR` | Attempt-scoped run-log directory |
| `TEMPO_RUN_LOG_FILE` | Primary per-step log file |
| `TEMPO_FLOW_RUN_ID` | Flow-run identifier |
| `TEMPO_RUN_ASSIGNMENT_ID` | Assignment identifier |
| `TEMPO_STEP_ID` | Step execution key |
| `TEMPO_STEP_RUN_ID` | Step-run identifier |
| `TEMPO_WORKER_ID` | Assigned worker or pseudo-worker identifier |

Behavior by runtime:

| Runtime | Logging behavior |
| --- | --- |
| `Artifact.Process` | Child stderr is captured separately and the host writes runtime/protocol diagnostics to `host.log` |
| `Artifact.Python` | `print(...)`, root `logging`, and stderr are redirected into the run-log files |
| `Artifact.JavaScript` | `console.*` and stderr are redirected into the run-log files |
| `Artifact.DotnetProcess` | `TempoStepHost` installs a file-backed logger; handlers should inherit `TempoStepHandlerBase` and use `LogInfo`/`LogWarn`/`LogError` while the host redirects `Console.Out` and `Console.Error` away from protocol stdout |

Operator guidance:

1. Keep `runLogs.rootPath` on persistent storage that is shared between the server and workers.
2. Keep retention bounded through `runLogs.retentionDays`.
3. Treat run logs as tenant-visible diagnostic output and avoid automatically logging secrets or raw credentials.

## Backup And Recovery

Tempo stores artifact metadata in the configured database and artifact package
bytes in the filesystem blob root. Back up both together:

- Database tables for artifacts, artifact versions, steps, flow runs, and step
  runs.
- The artifact blob root configured under `artifacts.rootPath`.
- The external execution cache root only if warm-cache recovery matters; it can
  otherwise be rebuilt from the blob root.

Partial artifact uploads are not usable unless both metadata and blob bytes are
present and the stored SHA-256 matches the uploaded bytes. To recover from an
interrupted upload, delete the inactive or incomplete artifact version metadata
and remove unreferenced blobs from the tenant blob directory after verifying no
active artifact version row points at the storage key.

Interrupted extraction can leave scratch or cache directories behind. Stop the
server, remove directories under `runtimes.externalExecution.scratchRoot`, and
remove only cache subdirectories whose artifact SHA no longer appears in active
artifact version metadata. The next run will extract the package again.

## Legacy Compatibility

Inline REST flow transitions are deprecated. Use the inline REST migration route
or startup migrator to convert them into persisted `External.Rest` steps.

`StepTypeEnum` and `PersistedStepTypeEnum` remain compatibility fields for old
rows and older clients. New API requests should use `runtimeKey` and
`runtimeConfig`; step responses omit legacy storage fields.
