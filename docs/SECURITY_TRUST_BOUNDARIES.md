# Security and Trust Boundaries

Tempo enforces tenant boundaries in API routes, database methods, artifact blob
paths, and runtime artifact resolution. Dashboard checks are presentation only.

Important boundaries:

- Tenant routes call `CanActOnTenant` before data access.
- Artifact routes require `Artifact` create/read/update/delete permissions.
- Artifact-backed step writes require `Step` create/update and `Artifact` read.
- Runtime validation for artifact config requires `Step` update and
  `Artifact` read.
- Artifact ids are resolved through tenant-scoped database reads.
- Artifact blob storage is rooted by tenant id and validates paths.
- Host executable tenants submit allowlist keys, not paths.

External process execution is not a hostile multi-tenant sandbox. For untrusted
code, operators still need OS/container isolation, resource controls, network
egress policy, and operational monitoring beyond Tempo's first-line checks.

## Threat Model Summary

Artifact upload:

- Treat every uploaded package as untrusted bytes until validation completes.
- Store blobs under tenant-scoped paths only after SHA-256 and quota checks pass.
- Reject archive traversal, symlink, and hardlink entries before extraction.
- Resolve artifact ids and versions through tenant-scoped database methods.
- Keep uploaded package manifests as metadata; never trust a manifest path until
  it is resolved inside the extracted artifact root.

External execution:

- Artifact-backed external execution is available implicitly and constrained by
  artifact-root resolution plus process capacity/size limits.
- Artifact-backed runtimes execute only package-rooted commands.
- `Host.Executable` accepts only operator-defined allowlist keys, never tenant
  file paths.
- Server-wide and per-tenant process capacity limits reduce noisy-neighbor
  impact but do not replace OS isolation.
- Protocol input, stdout, stderr, and parsed output are size-limited.

Tenant isolation:

- Tenant route access must pass `CanActOnTenant` before data access.
- Database methods that handle tenant-owned resources include tenant id in reads
  and writes.
- Flow-run snapshots pin artifact versions by tenant and artifact id at run
  start.
- Cross-tenant artifact references are rejected during step write and runtime
  validation.

Secret redaction:

- Tenants reference environment variable names only; values come from the server
  process environment and are never persisted in runtime configs.
- Diagnostics redact allowed environment values before stderr is written into
  exception messages.
- Secret values shorter than four characters should not be relied on for
  redaction because they are too ambiguous to redact safely.
