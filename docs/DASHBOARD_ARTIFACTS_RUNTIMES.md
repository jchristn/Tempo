# Dashboard Artifacts and Runtimes

The dashboard uses the server OpenAPI document and fetch-based API client.

Runtime catalog:

- Server runtime list shows all registered providers and availability state.
- Tenant runtime list uses the tenant path and enforces tenant access server-side.
- Runtime validation sends typed config with a `runtimeKey` discriminator.
- Disabled runtimes show the server-provided reason.

Artifacts:

- Artifact metadata can be created, edited, listed, and deleted.
- Versions can be uploaded as raw bytes or zip packages.
- Quota usage is shown from artifact storage settings and blob accounting.
- Flow runs show the artifact versions snapshotted when the run started.

Steps:

- The step editor is descriptor-driven from runtime provider metadata.
- Persisted REST, artifact process, artifact Python, artifact .NET process, and
  host executable configs are submitted as typed runtime config payloads.
- Orphaned built-in rows remain visible with binding status so operators can
  reconcile or delete them.
