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
