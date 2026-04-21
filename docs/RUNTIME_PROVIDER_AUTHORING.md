# Runtime Provider Authoring

Runtime providers plug into `StepRuntimeRegistry` and turn a typed step config
into a `StepRunner`.

## Contract

A provider must supply:

- A stable `RuntimeKey`.
- A concrete `StepRuntimeConfig` DTO.
- Validation that rejects unsafe or incomplete config.
- A `StepRuntimeDescriptor` for OpenAPI and the dashboard.
- Supported contract types.
- Availability state and security notes.
- Runner creation logic.
- Diagnostics mapping when the runner has runtime metadata.
- Touchstone coverage for validation, execution, and security boundaries.
- OpenAPI branch schema coverage for the provider config DTO.

Provider config must not be parsed from route-level `JsonElement`. Fixed fields
belong in typed DTOs. User-defined JSON, such as input/output schemas and
provider metadata blobs, may remain schemaless.

## OpenAPI Checklist

Runtime config OpenAPI is hand-written in
`src/Tempo.Server/Routes/OpenApiSchemaCatalog.cs` so generated clients see a
real `oneOf` contract instead of a flattened object. When adding a provider:

- Add a typed `StepRuntimeConfig` subclass with a stable `RuntimeKey`.
- Register the provider in `StepRuntimeRegistry`.
- Add a component schema builder for the config DTO.
- Register that component in `OpenApiSchemaCatalog.RegisterSchemas()`.
- Add the component ref to `RuntimeConfigSchema().oneOf`.
- Add a discriminator mapping from the runtime key string to the component ref.
- Add Touchstone assertions for required fields, the single-value `runtimeKey`
  enum, and the discriminator mapping.

Do not add an OpenAPI branch before the provider and DTO exist. The schema must
match the real DTO/provider validation contract.

## Protocol Compatibility

Every external SDK or runtime shim must declare the protocol versions it supports
in artifact manifest metadata and pass the protocol conformance suite for those
versions. Runtime providers should reject artifacts whose declared versions do
not overlap the server-supported set before launching a process.

## Security Rules

Tenant-owned providers must use the tenant id from `StepExecutionContext` or the
route path. They must not accept tenant-supplied host filesystem paths. Artifact
providers must resolve artifact ids through `ArtifactVersionResolver`, which
uses tenant-scoped database methods and the run snapshot.

Operators can expose host executables through `Host.Executable`, but tenant
requests contain only `allowListKey` and policy-checked arguments.

## Validation

Use `StepRuntimeConfig.Validate()` for shape checks. Use provider validation for
settings and service-backed checks, such as:

- Runtime disabled by settings.
- Unknown host executable allowlist key.
- Artifact id not found under the current tenant.
- Argument policy rejection.

Runtime validation routes require tenant access and `Step` update permission.
Artifact-backed runtime validation also requires `Artifact` read permission.
