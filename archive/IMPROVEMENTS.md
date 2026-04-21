# Tempo Platform Improvements

This document is the implementation plan for evolving Tempo into an Airflow-like
data flow orchestration platform. It is based on the final Codex/Claude Code
consensus from the identity-crisis review.

The goal is a unified execution model where a data flow invokes steps without
caring whether the work is performed by compiled-in .NET code, reflected
methods, REST services, uploaded Python, process-backed artifacts, containers,
MQ integrations, or future providers.

## Status Legend

Use these checkboxes as the implementation tracker.

- [ ] Not started
- [~] In progress
- [x] Complete
- [!] Blocked

For each phase, add:

- Owner:
- Started:
- Completed:
- Notes:
- PRs:

## Binding Architecture Rules

These requirements come from the markdown guidance under `C:\code\claude` and
must be followed throughout the work.

- [x] Use Watson 7 as the only HTTP stack.
- [x] Use route registrar classes per feature; do not add a monolithic REST handler.
- [x] Use typed request and response DTOs for fixed contracts.
- [x] Do not expose fixed API contracts as `JsonElement` or ad hoc dictionaries.
- [x] Only use schemaless JSON for true user-defined content such as JSON schema,
      user payload data, metadata blobs, or runtime registry converter internals.
- [x] Use `PrettyId` primary identifiers with stable prefixes.
- [x] Preserve tenant-aware data boundaries in HTTP routes, services, and database
      methods.
- [x] Enforce authorization on every server route; dashboard checks are UI-only.
- [x] Use provider-neutral database interfaces and provider-specific SQL for
      SQLite, MySQL, PostgreSQL, and SQL Server.
- [x] Do not make any migration SQLite-only.
- [x] Use Touchstone shared test suites for backend behavior.
- [x] Keep the dashboard on React/Vite with a fetch-based API client; do not add
      axios.
- [x] Keep OpenAPI accurate because the dashboard API Explorer depends on it.

## Consensus Architecture

The target execution path is:

```text
Flow -> StepRecord -> IStepExecutionResolver -> StepRuntimeRegistry
     -> IStepRuntimeProvider -> StepRunner/invoker
```

### Core Principles

- [x] Runtime provider is the primary extension seam.
- [x] A flow references a stable step execution key, not a display name.
- [x] Runtime config is typed at API and service boundaries.
- [x] Runtime config may be serialized as text JSON in storage, but not treated
      as schemaless in public DTOs.
- [x] Core owns schema validation, authorization, execution history,
      cancellation, timeout, protocol negotiation, artifact version snapshots,
      and diagnostics.
- [x] Providers own config DTOs, runtime descriptors, availability checks,
      marshaling, and invocation.
- [x] Tenant-created executable steps must be artifact-rooted.
- [x] Tenant-created steps must never point at arbitrary host filesystem paths.
- [x] Artifact-backed external execution is implicitly available, capacity-limited,
      and rooted in uploaded artifacts.
- [x] Host executable runtimes require an explicit server-operator allowlist
      setting.
- [x] The SDK and protocol envelope are part of Tempo's public platform
      contract, not helper code.

## Current State Summary

Observed current shape:

- `src/Tempo.Core/Models/StepRecord.cs`
  - Has `Id`, `TenantId`, `Name`, `StepType`, `Rest`, `MaxRuntimeMs`, activity
    flags, and timestamps.
  - Does not yet have `ExecutionKey`, `RuntimeKey`, typed runtime config,
    contract fields, or artifact fields.
- `src/Tempo/StepTransition.cs`
  - Uses transition `Name` and inline `StepType`/`Rest` for dynamic REST steps.
  - Comments still describe code steps as looked up from `StepManager` by name.
- `src/Tempo/Runners/DataFlowRunner.cs`
  - Resolves inline REST directly.
  - Resolves code steps directly through `StepManager`.
  - Does not yet use a resolver or runtime registry.
- `src/Tempo.Core/Database/Common/Implementations/StepMethods.cs`
  - Persists `step_type` and `rest_config`.
  - Does not yet query by execution key.
- `src/Tempo.Server/Routes/DataFlowRoutes.cs`
  - `ensure-steps` currently compares existing steps by `Name`.
- Existing architecture already includes:
  - `Tempo.Core`
  - `Tempo.Server`
  - `Tempo`
  - provider-specific database folders
  - route registrar classes
  - Touchstone test projects
  - dashboard project

The plan below refactors this shape in place instead of replacing the server,
database layer, test framework, or dashboard architecture.

## Terminology

- `StepRecord.Id`: PrettyId primary key, prefix `step_`.
- `StepRecord.ExecutionKey`: stable tenant-scoped key used by flows to execute a
  step, for example `validate_order`.
- `StepRecord.Name`: display name only, for example `Validate Order`.
- `RuntimeKey`: typed string wrapper identifying the runtime provider, for
  example `Builtin.Class`, `Builtin.Method`, `External.Rest`,
  `Artifact.Process`, `Artifact.Python`, `Artifact.JavaScript`, or
  `Artifact.DotnetProcess`.
- `RuntimeConfig`: typed provider-specific configuration object.
- `ContractType`: how core validates inputs and outputs.
- `Artifact`: tenant-owned uploaded package used by external runtimes.
- `Host.Executable`: operator-provisioned allowlisted executable runtime, never
  tenant-settable.
- `Legacy.InlineRest`: read-path-only compatibility provider for old flow JSON.

## Target Runtime Keys

Initial runtime keys:

- [x] `Builtin.Class`
- [x] `Builtin.Method`
- [x] `Builtin.Unknown`
- [x] `External.Rest`
- [x] `Legacy.InlineRest`
- [x] `Artifact.Process`
- [x] `Artifact.Python`
- [x] `Artifact.JavaScript`
- [x] `Artifact.DotnetProcess`
- [x] `Host.Executable`

Future runtime keys:

- [ ] `External.Container`
- [ ] `Hosted.Csharp`
- [ ] `MQ.Publish`
- [ ] `MQ.RequestReply`

Runtime keys should use a typed wrapper, not raw strings scattered through code.

```csharp
public readonly record struct RuntimeKey(string Value)
{
    public override string ToString() => Value;
}
```

Implementation notes:

- [x] Add validation so runtime keys are non-empty, bounded length, and use a
      predictable dotted token format.
- [x] Add JSON conversion for `RuntimeKey`.
- [x] Add database conversion helpers for `RuntimeKey`.
- [x] Keep constants for built-in runtime keys in one class.

## Target Step Model

`StepRecord` should evolve toward:

```text
Id
TenantId
ExecutionKey
Name
Description
RuntimeKey
RuntimeConfigJson
ContractType
InputSchema
OutputSchema
ValidateInput
ValidateOutput
ArtifactId
ArtifactVersion
RuntimeBindingState
RuntimeBindingMessage
MaxRuntimeMs
Active
IsProtected
CreatedUtc
LastUpdateUtc
LegacyStepType
LegacyRestConfig
```

Notes:

- `RuntimeConfigJson` is storage only.
- API/service DTOs expose typed `StepRuntimeConfig`, not `RuntimeConfigJson`.
- `LegacyStepType` and `LegacyRestConfig` exist only for compatibility and
  migration.
- `ArtifactId` and `ArtifactVersion` may be nullable because not every runtime
  uses artifacts.
- `ContractType`, `InputSchema`, `OutputSchema`, `ValidateInput`, and
  `ValidateOutput` are core-owned and apply consistently across runtimes.

## Target Provider Interfaces

Add these core abstractions.

```csharp
public interface IStepRuntimeProvider
{
    RuntimeKey RuntimeKey { get; }
    Type ConfigType { get; }
    StepRuntimeDescriptor Describe();
    Task<StepConfigValidationResult> ValidateAsync(
        StepRuntimeValidationContext context,
        CancellationToken token = default);
    Task<StepRunner> CreateRunnerAsync(
        StepExecutionContext context,
        StepRecord step,
        StepRuntimeConfig config,
        CancellationToken token = default);
}
```

```csharp
public interface IStepExecutionResolver
{
    Task<ResolvedStepExecution> ResolveAsync(
        string tenantId,
        string executionKey,
        FlowRunExecutionSnapshot snapshot,
        CancellationToken token = default);
}
```

Required services:

- [x] `StepRuntimeRegistry`
- [x] `RuntimeRegistryJsonTypeInfoResolver`
- [x] `StepRuntimeOpenApiSchemaProvider`
- [x] `IStepExecutionResolver`
- [x] `DatabaseStepExecutionResolver`
- [x] `InMemoryStepExecutionResolver`
- [x] `SchemaValidationService`
- [x] `StepCompatibilityMigrator`
- [x] `BuiltinStepReconciler`
- [x] `IArtifactMethods`
- [x] `IArtifactVersionMethods`
- [x] `IArtifactBlobStore`
- [x] `ArtifactCache`
- [x] `ExternalRuntimeCapacityManager`
- [x] `ProtocolNegotiator`

## Phase 0 - Baseline Inventory And Safety Net

Owner:
Started:
Completed:
Notes: First identity slice implemented. `ExecutionKey` accepts trimmed non-empty
strings up to 255 chars and rejects control characters; legacy create/upsert
requests default it from `Name`.
PRs:

Goal: Capture current behavior before changing identity or execution.

### Tasks

- [x] Inventory every current code path that resolves a step by display `Name`.
- [x] Inventory every current code path that resolves a step by flow transition
      dictionary key.
- [x] Inventory every current code path that reads or writes `StepTypeEnum`,
      `PersistedStepTypeEnum`, or `RestStepConfiguration`.
- [x] Inventory all `steps` table schemas for SQLite, MySQL, PostgreSQL, and
      SQL Server.
- [x] Inventory all test coverage around:
  - [x] class-based code steps
  - [x] `[StepMethod]` attribute steps
  - [x] inline REST flow steps
  - [x] persisted REST steps
  - [x] `ensure-steps`
  - [x] flow run history
  - [x] step run history
- [x] Add missing Touchstone tests for current behavior before refactoring.
- [x] Add tests proving an existing inline REST flow still executes.
- [x] Add tests proving a persisted REST `StepRecord` round-trips through
      `StepMethods`.
- [x] Add tests proving `ensure-steps` currently creates missing step records.
- [x] Add tests proving class and method steps resolve for tenant and global
      registrations.
- [x] Run the shared tests through automated, xUnit, and NUnit runners.

### Acceptance Criteria

- [x] Baseline tests pass before the refactor begins.
- [x] Known current gaps are documented in this file or a linked issue.
- [x] No production behavior has changed in this phase.

### Suggested Verification

- [x] `dotnet build src/Tempo.sln`
- [x] `dotnet test src/Tempo.sln`
- [x] `dotnet run --project src/Test.Automated/Test.Automated.csproj`

## Phase 1 - Step Identity Cleanup

Owner:
Started:
Completed:
Notes: Runtime model slice implemented. Added runtime keys, typed config DTOs,
registry-driven polymorphic serialization, runtime step columns, and runtime
catalog/validation routes. Step create/update now use dedicated request DTOs,
step responses now use public response DTOs, and OpenAPI oneOf schema work is
complete on Watson 7.0.12.
PRs:

Goal: Separate primary ID, execution identity, and display name.

### Data Model

- [x] Add `ExecutionKey` to `StepRecord`.
- [x] Validate `ExecutionKey` as non-empty, bounded length, and stable.
- [x] Decide allowed characters for execution keys.
- [x] Add `ExecutionKey` to all create/update/read/enumeration DTOs.
- [x] Keep `Name` as display-only.
- [x] Add `execution_key` column to `steps` for all providers.
- [x] Backfill `execution_key` from existing names or flow transition keys.
- [x] Add unique index on `(tenant_id, execution_key)` for all providers.
- [x] Add lookup method:
      `Task<StepRecord?> ReadByExecutionKeyAsync(string tenantId, string executionKey, CancellationToken token = default)`.
- [x] Update `StepMethods.UpsertAsync` to define whether upsert is by `Id` or
      by `(TenantId, ExecutionKey)`.
- [x] Add tests covering duplicate execution keys within a tenant.
- [x] Add tests proving the same execution key can exist in different tenants.

### Runtime Code

- [x] Update `StepManager` so public lookup language uses execution key.
- [x] Keep compatibility methods such as `GetByIdentifier` only as shims.
- [x] Update `DataFlow.ValidateStartingStep` and `ValidateStepReferences` naming
      and comments to use execution key terminology.
- [x] Update `StepTransition` comments to say the transition dictionary key is
      the step execution key.
- [x] Stop using display `Name` in `DataFlowRoutes.EnsureStepsAsync`.
- [x] Make `ensure-steps` compare existing steps by `ExecutionKey`.
- [x] Ensure autogenerated step display names can differ from execution keys.

### Compatibility

- [x] Provide a migration rule for existing rows where `Name` was used as the
      execution key.
- [ ] Detect conflicting historical names within a tenant before adding the
      unique index.
- [ ] If conflicts exist, fail migration with a clear operator message or apply
      a deterministic suffix strategy approved by maintainers.
- [x] Keep old serialized flows runnable through their existing transition keys.
- [x] Document that `ExecutionKey` is stable and should not be casually renamed.

### Acceptance Criteria

- [x] Flows no longer depend on display `Name` for execution.
- [x] Display `Name` can be changed without breaking a flow.
- [x] `ensure-steps` creates and compares by `ExecutionKey`.
- [x] All provider schemas include the unique `(tenant_id, execution_key)` index.
- [x] Touchstone tests cover identity behavior.

## Phase 2 - Canonical Runtime Model And Typed Config

Owner:
Started:
Completed:
Notes: Initial descriptor registry and runtime catalog routes landed. Execution
provider invocation is now wired for the server worker through the Phase 3
registry runner. Dedicated step create/update DTOs, public step response DTOs,
and OpenAPI oneOf schema work are complete.
PRs:

Goal: Add the canonical runtime model without adding new runtime behavior yet.

### Enums And Types

- [x] Add `RuntimeKey` wrapper.
- [x] Add runtime key constants.
- [x] Add `StepContractTypeEnum`:
  - [x] `Loose`
  - [x] `Schema`
  - [x] `Typed`
  - [x] `Binary`
- [x] Add `StepPackagingTypeEnum` if needed for descriptors:
  - [x] `Builtin`
  - [x] `External`
  - [x] `Artifact`
  - [x] `Container`
  - [x] `Host`
- [x] Add abstract `StepRuntimeConfig`.
- [x] Add typed config DTOs:
  - [x] `BuiltinClassRuntimeConfig`
  - [x] `BuiltinMethodRuntimeConfig`
  - [x] `ExternalRestRuntimeConfig`
  - [x] `LegacyInlineRestRuntimeConfig`
  - [x] placeholder `ArtifactProcessRuntimeConfig`
  - [x] placeholder `ArtifactPythonRuntimeConfig`
  - [x] `ArtifactJavaScriptRuntimeConfig`
  - [x] placeholder `HostExecutableRuntimeConfig`
- [x] Ensure fixed config DTOs use concrete properties, not `JsonElement`.
- [x] Add validation attributes or explicit validators for all config DTOs.

### Polymorphic Serialization

- [x] Implement registry-driven polymorphic serialization for
      `StepRuntimeConfig`.
- [x] Use a composed resolver, not a replacement resolver:

```csharp
JsonTypeInfoResolver.Combine(SourceGen.Default, RuntimeRegistryResolver.Instance)
```

- [x] Ensure unknown runtime keys fail with a typed validation error.
- [x] Ensure runtime key mismatch between wrapper DTO and concrete config fails.
- [x] Ensure source-generated DTO serialization elsewhere does not regress.
- [x] Add tests for round-trip serialization of every built-in config.
- [x] Add tests for unknown discriminator behavior.
- [x] Add tests proving fixed runtime config is not handled through route-level
      `JsonElement.GetProperty(...)`.

### Database Schema

- [x] Add `runtime_key` to `steps`.
- [x] Add `runtime_config` to `steps`.
- [x] Add `contract_type` to `steps`.
- [x] Add `input_schema` to `steps`.
- [x] Add `output_schema` to `steps`.
- [x] Add `validate_input` to `steps`.
- [x] Add `validate_output` to `steps`.
- [x] Add `artifact_id` to `steps`.
- [x] Add `artifact_version` to `steps`.
- [x] Keep legacy `step_type` and `rest_config` during migration.
- [x] Update SQLite schema and migrations.
- [x] Update MySQL schema and migrations.
- [x] Update PostgreSQL schema and migrations.
- [x] Update SQL Server schema and migrations.
- [x] Backfill old `Rest` rows to `External.Rest`.
- [x] Backfill old `Code` rows to `Builtin.Unknown`.
- [x] Default old contract values to `Loose`.
- [x] Default old validation flags to false.

### Database Methods

- [x] Update `StepMethods.CreateAsync`.
- [x] Update `StepMethods.UpdateAsync`.
- [x] Update `StepMethods.UpsertAsync`.
- [x] Update `StepMethods.ReadAsync`.
- [x] Update `StepMethods.ReadByExecutionKeyAsync`.
- [x] Update `StepMethods.EnumerateAsync`.
- [x] Update `StepMethods.AllAsync`.
- [x] Add typed runtime config conversion at the service layer, not ad hoc route
      parsing.
- [~] Add tests for all CRUD operations across all supported providers.

### API DTOs

- [x] Stop accepting raw `StepRecord` as create/update route bodies.
- [x] Add `CreateStepRequest`.
- [x] Add `UpdateStepRequest`.
- [x] Add `StepResponse`.
- [x] Add `StepListResponse` or reuse existing enumeration wrapper with typed
      item DTOs.
- [x] Include typed `StepRuntimeConfig` in request/response DTOs.
- [x] Ensure OpenAPI includes concrete config schemas.

### Acceptance Criteria

- [x] Existing behavior still runs.
- [x] All persisted steps have an execution key and runtime key.
- [x] Public API surfaces typed runtime config.
- [x] OpenAPI does not reduce runtime config to an opaque `object`.
- [x] Legacy fields remain available only for compatibility and migration.

## Phase 3 - Runtime Registry, Resolver, Reconciliation, And REST Migration

Owner:
Started:
Completed:
Notes: Resolver-backed registry execution is wired into `FlowQueueWorker` through
`RegistryDataFlowRunner`. Built-in reconciliation now resolves legacy
`Builtin.Unknown` rows to deterministic class/method runtime configs and marks
ambiguous or orphaned bindings. Inline REST flow transitions are migrated to
persisted `External.Rest` steps at startup and on flow create/update. The old
public `Tempo.Runners.DataFlowRunner` is still preserved for compatibility.
PRs:

Goal: Make all current step behavior run through the new provider registry and
resolver.

### Runtime Registry

- [x] Add `StepRuntimeRegistry`.
- [x] Register `Builtin.Class`.
- [x] Register `Builtin.Method`.
- [x] Register `External.Rest`.
- [x] Register `Legacy.InlineRest` as read-path-only.
- [x] Register placeholders for future artifact runtimes as disabled/unavailable.
- [x] Add duplicate runtime key detection at startup.
- [x] Add runtime availability state:
  - [x] `Available`
  - [x] `DisabledBySettings`
  - [x] `MissingDependency`
  - [x] `UnsupportedPlatform`
  - [x] `Preview`
- [x] Probe configured Python, Node.js, and .NET commands cross-platform and
      mark dependent runtimes `MissingDependency` when commands are unavailable.
- [x] Add `StepRuntimeDescriptor` with:
  - [x] runtime key
  - [x] display name
  - [x] description
  - [x] packaging type
  - [x] supported contract types
  - [x] config schema
  - [x] artifact support
  - [x] versioning support
  - [x] availability
  - [x] security notes

### Resolver

- [x] Add `IStepExecutionResolver`.
- [x] Add `DatabaseStepExecutionResolver`.
- [x] Add `InMemoryStepExecutionResolver` for embedded/library compatibility.
- [x] Add `CompositeStepExecutionResolver` for database plus in-memory fallback.
- [~] Change `DataFlowRunner` to depend on `IStepExecutionResolver`.
- [~] Preserve existing constructor `DataFlowRunner(StepManager, Logger?)` as a
      compatibility adapter.
- [x] Move inline REST resolution into `Legacy.InlineRest`.
- [x] Move code step resolution into `Builtin.Class` and `Builtin.Method`.
- [x] Ensure `StepRunDetails.StepId` records the execution key and/or persisted
      step ID consistently.
- [~] Add diagnostics when a step cannot resolve.
- [x] Add tests proving all legacy step types still execute through the resolver.

### Core Execution Pipeline

Implement this order:

```text
resolve step
deserialize typed runtime config
resolve run-start artifact snapshot if applicable
core validates input contract
provider marshals and invokes runtime
core validates output contract
record diagnostics and history
choose next transition
```

- [x] Add `StepExecutionContext`.
- [x] Add `ResolvedStepExecution`.
- [x] Add `FlowRunExecutionSnapshot`.
- [x] Add `SchemaValidationService`.
- [x] Add input validation before provider invocation.
- [x] Add output validation after provider invocation.
- [x] Preserve existing max runtime behavior.
- [x] Preserve cancellation token behavior.
- [x] Capture provider diagnostics into step run history.

### Built-In Reconciliation

- [x] Add `StepManager` registration metadata:
  - [x] execution key
  - [x] source kind: class registration or scanned method
  - [x] declaring type
  - [x] method name
  - [x] assembly name
  - [x] assembly version
  - [x] signature hash
  - [x] max runtime
- [x] Add `BuiltinStepReconciler`.
- [x] After `StepManager` loads, scan `Builtin.Unknown` rows.
- [x] Resolve rows to `Builtin.Class` or `Builtin.Method`.
- [x] Update runtime config with deterministic metadata.
- [x] Detect ambiguous matches and mark them ambiguous.
- [x] Detect missing registered steps and mark them as orphaned.
- [x] Add dashboard-visible orphan status.
- [x] Add tests for resolved, ambiguous, and orphaned built-in rows.

### Inline REST Migration

- [x] Add `StepCompatibilityMigrator`.
- [x] Make it idempotent.
- [x] Scan existing flow definitions for inline REST transitions.
- [x] Create persisted `StepRecord` rows through `StepMethods`, not raw SQL.
- [x] Generate deterministic execution keys for migrated inline REST steps.
- [x] Rewrite flow transitions using normal flow serializers, not provider SQL
      JSON mutation.
- [x] Record migration completion and per-flow status.
- [x] Dashboard stops writing inline REST as soon as this phase lands.
- [x] Keep `Legacy.InlineRest` as read-path-only for one compatibility window.
- [x] Add tests proving old inline flows migrate and still execute.
- [x] Add tests proving the migrator can safely run multiple times.

### Runtime Routes

Add route registrar `RuntimeRoutes`.

- [x] `GET /v1.0/runtimes`
  - [x] Requires authentication.
  - [x] Lists server capabilities.
  - [x] Does not require `StepRead`.
- [x] `GET /v1.0/runtimes/{runtimeKey}`
  - [x] Requires authentication.
  - [x] Returns descriptor and OpenAPI/config schema metadata.
- [x] `GET /v1.0/tenants/{tenantId}/runtimes`
  - [x] Requires tenant authorization.
  - [x] Includes tenant policy and runtime availability state.
- [x] `POST /v1.0/tenants/{tenantId}/runtimes/validate`
  - [x] Requires `StepWrite`.
  - [x] Requires artifact permission checks when the config references artifacts.
  - [x] Uses typed validate request DTO.
- [~] Add OpenAPI metadata for all routes.

### Acceptance Criteria

- [~] `DataFlowRunner` no longer directly switches on inline REST vs code.
- [x] Current class, method, persisted REST, and legacy inline REST behavior works.
- [x] Runtime catalog is visible in OpenAPI.
- [x] Inline REST has a migration path and no new inline REST is written.
- [x] Built-in code rows reconcile deterministically after startup.

## Phase 4 - Artifact Store, Quotas, Retention, And GC

Owner:
Started:
Completed:
Notes:
PRs:

Goal: Add tenant-owned artifacts before any tenant-facing external execution.

### Identifiers And Constants

- [x] Add `ArtifactIdPrefix = "art_"`.
- [x] Add `ArtifactVersionIdPrefix = "arv_"`.
- [x] Add `IdGenerator.GenerateArtifactId()`.
- [x] Add `IdGenerator.GenerateArtifactVersionId()`.

### Models

- [x] Add `ArtifactRecord`.
- [x] Add `ArtifactVersionRecord`.
- [x] Add `ArtifactReference`.
- [x] Add `ArtifactRetentionPolicy`.
- [x] Add `ArtifactManifest`.
- [x] Add `ArtifactFileRecord` for mutable, dashboard-editable artifact files.

`ArtifactRecord` fields:

- [x] `Id`
- [x] `TenantId`
- [x] `Name`
- [x] `Description`
- [x] `Active`
- [x] `IsProtected`
- [x] `CreatedUtc`
- [x] `LastUpdateUtc`

`ArtifactVersionRecord` fields:

- [x] `Id`
- [x] `TenantId`
- [x] `ArtifactId`
- [x] `Version`
- [x] `Sha256`
- [x] `ByteLength`
- [x] `ContentType`
- [x] `OriginalFileName`
- [x] `ManifestJson`
- [x] `StorageKey`
- [x] `Active`
- [x] `IsProtected`
- [x] `CreatedUtc`
- [x] `LastUpdateUtc`
- [x] `DeletedUtc`
- [x] `GcEligibleUtc`

### Database

- [x] Add `artifacts` table for SQLite.
- [x] Add `artifact_versions` table for SQLite.
- [x] Add indexes for SQLite:
  - [x] `(tenant_id, name)`
  - [x] `(tenant_id, artifact_id, version)`
  - [x] `(tenant_id, sha256)`
  - [x] `gc_eligible_utc`
- [x] Repeat schema and indexes for MySQL.
- [x] Repeat schema and indexes for PostgreSQL.
- [x] Repeat schema and indexes for SQL Server.
- [x] Add `IArtifactMethods`.
- [x] Add `IArtifactVersionMethods`.
- [x] Add `artifact_files` table for SQLite, MySQL, PostgreSQL, and SQL Server.
- [x] Add `IArtifactFileMethods`.
- [x] Add provider-neutral implementations where possible.
- [x] Keep provider-specific SQL in provider folders where needed.
- [x] Add Touchstone database tests for all artifact methods.

### Blob Storage

- [x] Add `IArtifactBlobStore`.
- [x] Add `LocalFilesystemArtifactBlobStore`.
- [x] Add settings for artifact root path.
- [x] Store artifacts by tenant and SHA256.
- [x] Validate SHA256 before accepting a version.
- [x] Enforce maximum upload size.
- [x] Enforce tenant quota.
- [x] Prevent path traversal on all blob operations.
- [x] Do not store large production artifacts in relational DB blobs.

### Archive Safety

Required before executable artifacts are allowed:

- [x] Validate ZIP entries before extraction.
- [x] Reject or normalize zip-slip paths.
- [x] Reject absolute paths.
- [x] Reject parent-directory traversal.
- [x] Reject symlinks unless a later explicit design allows them safely.
- [x] Reject hardlinks unless a later explicit design allows them safely.
- [x] Canonicalize extracted entrypoints under artifact root.
- [x] Verify executable entrypoint path remains under extracted artifact root.
- [x] Use tenant-isolated extraction cache directories.
- [x] Use tenant-isolated scratch directories.

### Routes

Add route registrar `ArtifactRoutes`.

- [x] `POST /v1.0/tenants/{tenantId}/artifacts`
- [x] `GET /v1.0/tenants/{tenantId}/artifacts`
- [x] `GET /v1.0/tenants/{tenantId}/artifacts/{id}`
- [x] `PUT /v1.0/tenants/{tenantId}/artifacts/{id}`
- [x] `DELETE /v1.0/tenants/{tenantId}/artifacts/{id}`
- [x] `POST /v1.0/tenants/{tenantId}/artifacts/{id}/versions`
- [x] `GET /v1.0/tenants/{tenantId}/artifacts/{id}/versions`
- [x] `GET /v1.0/tenants/{tenantId}/artifacts/{id}/versions/{version}`
- [x] `GET /v1.0/tenants/{tenantId}/artifacts/{id}/versions/{version}/download`
- [x] `DELETE /v1.0/tenants/{tenantId}/artifacts/{id}/versions/{version}`
- [x] `GET /v1.0/tenants/{tenantId}/artifacts/{id}/files`
- [x] `GET /v1.0/tenants/{tenantId}/artifacts/{id}/files/content?path=...`
- [x] `PUT /v1.0/tenants/{tenantId}/artifacts/{id}/files/content?path=...`
- [x] `DELETE /v1.0/tenants/{tenantId}/artifacts/{id}/files/content?path=...`

Route requirements:

- [x] Use typed metadata DTOs.
- [x] Keep file upload handling isolated from fixed metadata contracts.
- [x] Add OpenAPI metadata.
- [x] Enforce tenant authorization.
- [x] Add `Artifact` to `ResourceTypeEnum`.
- [x] Add or map create/read/update/delete operations for artifact resources.
- [x] Require artifact write permission for uploads.
- [x] Require artifact delete permission for deletes.

### Retention And GC

- [x] Add retention settings:
  - [x] version grace period days
  - [x] flow run replay retention days
  - [x] max artifact bytes per tenant
  - [x] max artifact versions per artifact
- [x] Protect artifact versions referenced by active `StepRecord`s.
- [x] Protect artifact versions referenced by flow runs inside the retention
      window.
- [x] Mark orphaned versions GC-eligible after grace period.
- [x] Add scheduled background GC task.
- [x] Ensure GC is not synchronous with delete.
- [x] Add metrics for artifact bytes, versions, and GC deletes.
- [x] Add tests for reference retention and GC eligibility.

### Acceptance Criteria

- [x] Tenants can upload, list, download, and delete artifact versions subject to
      authorization and quota.
- [x] Tenants can edit artifact files directly; Tempo rebuilds a mutable
      `current` runtime snapshot from those files.
- [x] Artifact storage is tenant-scoped.
- [x] Unsafe archive paths are rejected.
- [x] GC never deletes versions needed by active steps or retained runs.
- [x] No external runtime can execute yet unless later phases enable it.

## Phase 5 - Protocol V1 And SDKs

Owner:
Started: 2026-04-19
Completed: 2026-04-20
Notes: Protocol v1 envelope, negotiation helpers, persistence, and conformance
      suite landed. C#, Python, and JavaScript SDKs now live under `sdk/` with
      README files and executable coverage test apps. CLI/deployment automation
      remains a separate scope.
PRs:

Goal: Freeze the public external step protocol before stabilizing Python or
process execution.

### Protocol Envelope

- [x] Add `protocolVersion` to `StepRequest`.
- [x] Add `protocolVersion` to `StepResult`.
- [x] Decide exact protocol v1 JSON casing and naming.
- [x] Ensure `RequestId`/correlation ID always propagates.
- [x] Ensure `TenantId`, `DataFlowId`, `FlowRunId`, and `StepRunId` exposure is
      deliberate and documented.
- [x] Define success, error, exception, timeout, and cancellation mapping.
- [x] Define stdout/stderr behavior for process-backed runners.
- [x] Define maximum input and output payload sizes.
- [x] Define binary payload strategy as out of scope or separate v1 extension.
- [ ] Freeze v1 at GA.
- [x] Define v1.x as additive-only.
- [x] Define v2 breaking-change process and dual-support window.

### Negotiation

- [x] Add `ProtocolNegotiator`.
- [x] Add supported protocol versions to artifact manifest metadata.
- [x] Add launch-time enforcement using environment variable or handshake.
- [x] Reject incompatible artifacts before execution when manifest metadata is
      available.
- [x] Record negotiated protocol version in step run history.
- [x] Add tests for supported, unsupported, and ambiguous negotiation.

### Conformance Suite

- [x] Add protocol golden files.
- [x] Add conformance test runner.
- [x] Test valid success result.
- [x] Test valid error result.
- [x] Test exception mapping.
- [x] Test invalid JSON output.
- [x] Test missing required fields.
- [x] Test correlation propagation.
- [x] Test timeout behavior.
- [x] Test cancellation behavior where possible.
- [x] Version the conformance suite.
- [x] Require every future SDK/runtime shim to declare which protocol versions
      it passes.

### Python SDK

Create `tempo-sdk-python` in a suitable repository or workspace location.

- [x] Define package name and distribution strategy.
- [x] Implement `StepRequest` model.
- [x] Implement `StepResult` model.
- [x] Implement success helper.
- [x] Implement error helper.
- [x] Implement exception helper.
- [x] Implement `@step` decorator.
- [x] Implement stdin/stdout runner shim.
- [x] Preserve correlation/request IDs.
- [x] Expose protocol version support.
- [x] Add conformance tests.
- [x] Add examples:
  - [x] simple transform
  - [x] validation failure
  - [x] exception
  - [x] JSON schema input
- [x] Add README with protocol and local development guidance.

### C# SDK

- [x] Add SDK project under `sdk/csharp/Tempo.Sdk`.
- [x] Implement protocol constants, request/result models, result enum, handler
      interface, result helpers, correlation helper, and stdin/stdout runner.
- [x] Add README with handler and local test guidance.
- [x] Add executable test app with reflected public API coverage inventory.

### JavaScript SDK

- [x] Add SDK package under `sdk/js`.
- [x] Implement protocol constants, request/result models, result enum, handler
      marker, result helpers, correlation helper, and stdin/stdout runner.
- [x] Add README with handler and local test guidance.
- [x] Add executable test app with exported public API coverage inventory.

### CLI Scope

The SDK should establish the protocol first. CLI commands can follow.

- [ ] Design `tempo dev`.
- [ ] Design `tempo build`.
- [ ] Design `tempo deploy`.
- [ ] Decide whether CLI lives in the Python SDK package or a separate tool.
- [ ] Do not block protocol v1 on full deployment automation.

### Acceptance Criteria

- [x] Protocol v1 is documented.
- [x] Python SDK passes the conformance suite.
- [x] C#, Python, and JavaScript SDK test apps pass with public API coverage
      checks.
- [ ] Runtime shims can use the SDK instead of hand-writing envelopes.
- [x] Tempo can reject artifacts that cannot satisfy supported protocol
      versions.

## Phase 6 - External Execution Settings And Capacity Management

Owner:
Started: 2026-04-19
Completed: 2026-04-19
Notes: Added external execution capacity/path settings, startup warning, and an
      in-process capacity manager with active/queued counters. Step run history
      now persists capacity wait lifecycle fields, runtime status routes expose
      server and tenant pressure, and the dashboard home view shows runtime
      capacity status. Artifact-backed external execution is implicitly
      available as of 2026-04-20; `Host.Executable` remains operator-gated.
PRs:

Goal: Add the safety gates required before process-backed execution.

### Settings

Add settings under `Settings.Runtimes.ExternalExecution`.

- [x] No global `Enabled` switch; artifact-backed execution is implicit.
- [x] `MaxConcurrentProcessesServerWide`
- [x] `MaxConcurrentProcessesPerTenant`
- [x] `DefaultMaxRuntimeMs`
- [x] `MaxStdoutBytes`
- [x] `MaxStderrBytes`
- [x] `MaxInputBytes`
- [x] `MaxOutputBytes`
- [x] `ScratchRoot`
- [x] `CacheRoot`
- [x] `EnvironmentAllowList`
- [x] `NetworkPolicyMode` placeholder
- [x] `KillProcessTreeOnCancel`

Startup behavior:

- [x] Artifact.Process reports `Available` by default; Python, JavaScript, and
      .NET process runtimes report `MissingDependency` when their configured
      host commands are unavailable.
- [x] Write a clear startup warning that artifact-backed process execution is
      available.
- [x] Dashboard shows external execution capacity status.
- [x] Tests verify implicit artifact availability and host executable gating.

### Capacity Manager

- [x] Add `ExternalRuntimeCapacityManager`.
- [x] Enforce server-wide process cap.
- [x] Enforce per-tenant process cap.
- [x] Queue step runs as awaiting runtime capacity when cap is reached.
- [x] Add or extend run state model for capacity waits.
- [x] Emit metrics for:
  - [x] active external processes by tenant
  - [x] queued external steps by tenant
  - [x] capacity wait duration
  - [x] process runtime duration
  - [x] process kill count
- [x] Add tests for per-tenant noisy-neighbor isolation.
- [x] Add tests for server-wide cap.
- [x] Add tests for cancellation while queued.

### Acceptance Criteria

- [x] Artifact-backed external execution is artifact-rooted and implicitly
      available.
- [x] External execution cannot spawn unbounded processes.
- [x] Operators can see tenant-level runtime pressure.

## Phase 7 - Artifact.Process, Artifact.Python, And Artifact.JavaScript

Owner: Codex
Started: 2026-04-19
Completed: 2026-04-20
Notes:
- Implemented manifest-backed `Artifact.Process` and `Artifact.Python`
  runtimes under external execution capacity and size limits.
- Artifact packages are extracted into tenant/SHA-scoped cache
  directories with zip-slip, rooted path, drive path, and link-entry guards.
- Process execution uses JSON-over-stdin/stdout, per-step/default runtime
  limits, stdout/stderr/input limits, stderr redaction, scratch cleanup,
  process-tree kill, and Linux `setsid` process-group kill when available.
- Artifact.Python uses a generated SDK-style envelope and an operator-gated
  venv/dependency cache keyed by artifact SHA and Python executable/version.
- Run-start artifact snapshots are persisted on flow runs and copied onto step
  run history with artifact version, SHA, entrypoint, protocol, and capacity
  wait metadata.
- Validation completed with `dotnet build .\src\Tempo.sln`, Touchstone
  automated runner on `net8.0`/`net10.0`, xUnit/NUnit adapters on both target
  frameworks, and `npm.cmd run build` for the dashboard.
- 2026-04-20: Added manifest-backed `Artifact.JavaScript` with a Node.js
  runner shim, CommonJS/ESM module loading, OpenAPI `oneOf` schema coverage,
  startup sample seeding, and Touchstone coverage that executes JavaScript
  artifacts when Node.js is available.
PRs:

Goal: Add the first tenant-facing external runtimes, both artifact-rooted and
capacity-limited.

### Artifact Manifest

Add manifest file, for example `tempo.step.json`.

Required fields:

- [x] manifest version
- [x] runtime key
- [x] supported protocol versions
- [x] entrypoints
- [x] default entrypoint
- [x] command or module reference
- [x] args
- [x] allowed environment variable names
- [x] input schema
- [x] output schema
- [x] runtime-specific settings

Manifest rules:

- [x] Manifest is parsed into a typed DTO.
- [x] Manifest is validated at upload or version activation.
- [x] Entrypoints are names, not arbitrary tenant-provided host paths.
- [x] Entrypoints resolve under the verified artifact extraction root.
- [x] Manifest protocol versions must overlap server-supported versions.

### Artifact.Process

- [x] Add `ArtifactProcessRuntimeConfig`.
- [x] Config references:
  - [x] artifact ID
  - [x] artifact version or `latest`
  - [x] manifest entrypoint name
  - [x] optional args declared by schema
  - [x] environment references by name
- [x] Implement JSON-over-stdin/stdout invocation.
- [x] Serialize `StepRequest` to stdin.
- [x] Deserialize `StepResult` from stdout.
- [x] Map non-zero exit code to exception result.
- [x] Map invalid stdout to exception result.
- [x] Capture stderr diagnostics, truncated by settings.
- [x] Enforce max stdout/stderr bytes.
- [x] Enforce max runtime.
- [x] Enforce cancellation.
- [x] Kill process tree on cancellation or timeout.
- [x] Use Windows Job Objects eventually; initial implementation may use best
      effort process-tree kill if documented.
- [x] Use Linux process groups now; cgroups can be a later hardening item.
- [x] Add contract fixture executable for tests.

### Artifact.Python

- [x] Add `ArtifactPythonRuntimeConfig`.
- [x] Implement on top of `Artifact.Process` where possible.
- [x] Use Python SDK runner shim.
- [x] Support `def run(req: dict) -> dict` only through SDK-controlled envelope.
- [x] Add Python version selection.
- [x] Add virtual environment cache keyed by artifact SHA256 and Python version.
- [x] Add dependency install policy.
- [x] Add first-run venv build behavior.
- [x] Add cache reuse behavior.
- [x] Add cache cleanup tied to artifact GC.
- [x] Skip Python runtime tests when Python is unavailable, with clear test
      output.

### Artifact.JavaScript

- [x] Add `ArtifactJavaScriptRuntimeConfig`.
- [x] Register `Artifact.JavaScript` in the runtime registry.
- [x] Validate JavaScript manifest entrypoints with module/function metadata.
- [x] Resolve JavaScript modules under the artifact root only.
- [x] Execute JavaScript artifacts through Node.js with JSON-over-stdin/stdout.
- [x] Support CommonJS and ESM module exports.
- [x] Reuse protocol negotiation.
- [x] Reuse capacity manager.
- [x] Reuse artifact cache.
- [x] Add OpenAPI `oneOf` schema coverage.
- [x] Add startup template seeding for runnable JavaScript artifacts.
- [x] Skip JavaScript runtime tests when Node.js is unavailable, with clear test
      output.

### Run-Start Version Pinning

- [x] If a step references artifact version `latest`, resolve it once at flow-run
      start.
- [x] Store the resolved artifact version in `FlowRunExecutionSnapshot`.
- [x] Persist artifact snapshot information on `flow_runs` or related table.
- [x] Ensure all steps in the same flow run use the snapshot.
- [x] Record actual artifact version on each step run.
- [x] Add tests proving an upload during a running flow does not change that run.

### Security

- [x] Require `StepWrite` to create/update a step using artifact runtimes.
- [x] Require `ArtifactRead` or equivalent to reference an artifact.
- [x] Never embed secret values in runtime config.
- [x] Reference secrets by credential/secret name only.
- [x] Redact secrets from logs, diagnostics, request history, and step history.
- [x] Use tenant-isolated scratch directories.
- [x] Clean scratch directories after execution.
- [x] Document that untrusted code execution still requires stronger sandboxing
      before hostile tenant boundaries are supported.

### Acceptance Criteria

- [x] Tenant users can execute uploaded process artifacts only when external
      execution capacity permits it and authorization permits it.
- [x] Tenant users cannot execute arbitrary host paths.
- [x] Python artifacts execute through the SDK envelope.
- [x] JavaScript artifacts execute through the SDK-compatible envelope.
- [x] Protocol version, capacity, timeout, cancellation, and artifact snapshot
      behavior are tested.

## Phase 8 - Routes And Dashboard For Steps, Runtimes, And Artifacts

Owner:
Started:
Completed:
Notes:
2026-04-19: Added typed step write DTOs, route runtime validation, artifact-reference
authorization on step writes, explicit OpenAPI request/response metadata for typed
step/runtime/artifact APIs, runtime descriptor read route, dashboard runtime-aware step
editor, runtime catalog/tenant availability view, and artifact list/version/upload view.
Validation passed: solution build, Touchstone net8/net10, xUnit net8/net10, NUnit
net8/net10, and dashboard production build.
2026-04-19: Added descriptor-driven generic runtime config fields, orphaned built-in
badges in the step grid, artifact quota/retention usage tiles, flow-run artifact
snapshot details, and an admin-only inline REST migration route. Validation passed:
solution build, Touchstone net8/net10, xUnit net8/net10, NUnit net8/net10, and
dashboard production build.
2026-04-19: Completed the remaining route authorization audit items for artifacts
and runtime validation. Artifact runtime validation now requires artifact read
permission and verifies artifact ids in the current tenant. Cross-tenant artifact
read and cross-tenant artifact-backed step reference regressions are covered.
2026-04-19: Added core JSON-schema subset enforcement for step input/output
contracts and retained flow-run snapshot protection in artifact GC. Documentation
deliverables are filled in under `docs/`. Validation passed: solution build,
Touchstone automated net8/net10, xUnit net8/net10, NUnit net8/net10, and
dashboard production build.
2026-04-20: Added source-code step creation for pasted Python, JavaScript, and
C# files. The backend packages source into tenant artifacts, creates runnable
steps, and documents the source-step route in OpenAPI. The dashboard now includes
a source step modal, first-run setup wizard that creates a step/data flow/run
end to end, page titles/subtitles for every workspace, setup/version controls in
the sidebar footer, first-user-oriented FLOWS ordering, and independent sidebar
and workspace scrolling.
2026-04-20: Reworked artifacts as mutable editable file trees. Added
`ArtifactFileRecord`, provider-neutral artifact file persistence, file CRUD
routes, path sanitization at the database method boundary, ZIP import into files,
and automatic `current` package snapshots for artifact-backed runtimes. Source
step packages and startup sample artifacts now create editable files and point
steps at the `current` snapshot. Dashboard artifact management now opens a
file-first editor with ZIP import/export as secondary actions. Validation passed:
Debug solution build, dashboard production build, and clean-start server health
check after resetting the local SQLite deployment.
PRs:

Goal: Make the new platform usable without violating frontend architecture.

### Backend Route Cleanup

- [x] Refactor `StepRoutes` to use typed create/update DTOs.
- [x] Add runtime validation route integration.
- [x] Add artifact routes.
- [x] Add runtime routes.
- [x] Add migration/admin route if inline REST migration is operator-triggered.
- [~] Ensure all routes have OpenAPI metadata.
- [x] Ensure all routes use tenant-scoped authorization.
- [x] Ensure error responses use typed `ErrorResponse`.

### Dashboard API Client

- [x] Extend existing fetch-based API client.
- [x] Add runtime catalog methods.
- [x] Add tenant runtime policy methods.
- [x] Add runtime validation method.
- [x] Add artifact CRUD methods.
- [x] Add artifact version upload/download methods.
- [x] Add artifact file list/read/save/delete methods.
- [x] Add step create/update methods using typed runtime config.
- [x] Add source-code step creation method.
- [x] Do not add axios.

### Dashboard Views

- [x] Runtime catalog view.
- [x] Tenant runtime availability view.
- [x] Step editor with runtime selection.
- [x] Generic runtime config form rendered from descriptors/schema.
- [x] Custom helper UI for REST runtime.
- [x] Custom helper UI for Python runtime.
- [x] Artifact list view.
- [x] Artifact version detail view.
- [x] Upload flow for artifacts.
- [x] Artifact file editor backed by mutable `current` snapshots.
- [x] Runtime validation feedback.
- [x] Orphaned built-in step badge.
- [x] External execution capacity/status tiles.
- [x] Tenant concurrency usage panel.
- [x] Artifact quota usage panel.
- [x] Flow run artifact snapshot details.
- [x] Source-code step creation for pasted Python, JavaScript, and C# files.
- [x] Setup wizard that creates the first step, data flow, and run in a modal.
- [x] Page title and subtitle on every workspace.
- [x] FLOWS navigation ordered by first-use creation sequence.
- [x] Application version shown in the sidebar footer.
- [x] Sidebar and workspace content scroll independently.

### API Explorer

- [x] Verify `/openapi.json` includes runtime polymorphic schemas.
- [x] Verify API Explorer can render runtime validation requests.
- [x] Verify API Explorer can render artifact metadata routes.
- [x] Verify API Explorer includes source-code step creation.
- [x] Decide how file upload routes appear in the explorer.

### Acceptance Criteria

- [x] Dashboard can create persisted REST steps without inline REST.
- [x] Dashboard can upload artifacts.
- [x] Dashboard can edit artifact files and rebuild the `current` snapshot.
- [x] Dashboard can create artifact-backed process/Python steps.
- [x] Dashboard can create artifact-backed Python, JavaScript, and C# steps from
      pasted source code.
- [x] Dashboard can guide a new user through creating and running a first data
      flow without leaving the setup wizard.
- [x] Every workspace explains its purpose with a title and subtitle.
- [x] Dashboard clearly shows disabled runtimes and why they are disabled.
- [x] Dashboard does not rely on client-only authorization for enforcement.

Remaining:
- Browser visual verification is still useful for final polish, but the
  production dashboard build succeeds.

## Phase 9 - Host.Executable Operator Runtime

Owner:
Started:
Completed:
Notes:
2026-04-19: Added `Settings.Runtimes.HostExecutables`, operator allowlist entries,
tenant-facing `allowListKey` validation, argument policy enforcement, and a
Host.Executable provider/runner that uses the external process protocol and capacity
manager. Host.Executable remains disabled unless host executables are enabled.
Validation passed: solution build, Touchstone net8/net10,
xUnit net8/net10, NUnit net8/net10, and dashboard production build.
PRs:

Goal: Support operator-provisioned host executables without exposing host paths
to tenants.

### Settings

Add settings under `Settings.Runtimes.HostExecutables`.

- [x] `Enabled = false`
- [x] `AllowList`
- [x] per-entry executable path
- [x] per-entry display name
- [x] per-entry allowed args schema
- [x] per-entry environment allowlist
- [x] per-entry max runtime override

### Runtime

- [x] Add `HostExecutableRuntimeConfig`.
- [x] Config references allowlist key only.
- [x] Tenant request never contains host path.
- [x] Validate allowlist key at create/update.
- [x] Execute through the same process/protocol machinery as artifact runtimes.
- [x] Reuse capacity manager.
- [x] Reuse stdout/stderr limits.
- [x] Add tests proving arbitrary path submission is rejected.

### Acceptance Criteria

- [x] Operators can expose known tools.
- [x] Tenants cannot submit or discover arbitrary host executable paths.
- [x] Runtime is disabled by default.

Remaining:
- Add dashboard settings affordances for editing allowlist entries without raw JSON.
- Consider richer JSON-schema style argument validation if simple exact/prefix policy is
  insufficient for production operators.

## Phase 10 - C# Process Runtime

Owner:
Started:
Completed: 2026-04-20
Notes: Added `Artifact.DotnetProcess`, a minimal Tempo SDK handler interface and
stdin/stdout host helper, manifest `handlerType` validation, registry/provider
registration, OpenAPI `oneOf` schema coverage, and packaged fixture conformance
tests. The runtime executes artifact `.dll` entrypoints through the existing
external process runner and `dotnet`, so capacity, protocol negotiation,
artifact cache, redaction, and run-start artifact snapshot behavior are reused.
PRs:

Goal: Support external C# without destabilizing the server process.

### Runtime

- [x] Add `Artifact.DotnetProcess`.
- [x] Require a Tempo SDK handler interface.
- [x] Run through `dotnet` child process.
- [x] Reference artifacts and entrypoints through manifest.
- [x] Reuse protocol negotiation.
- [x] Reuse capacity manager.
- [x] Reuse artifact cache.
- [x] Add conformance tests.

### Hosted C# Later

- [ ] Design `Hosted.Csharp` separately.
- [ ] Mark it unsafe or advanced because of dependency isolation risks.
- [ ] Require opt-in settings.
- [ ] Require unload and memory leak tests.
- [ ] Do not implement hosted C# before process-backed C# works.

### Acceptance Criteria

- [x] C# workloads can run out-of-process.
- [x] Server process remains isolated from user dependencies.

## Phase 11 - Container Runtime

Owner:
Started:
Completed:
Notes:
PRs:

Goal: Add container execution as an optional runtime provider.

### Runtime

- [ ] Add `External.Container` or `Container.Process` after final naming review.
- [ ] Runtime registers only if container engine is available.
- [ ] Do not make Docker a hard dependency of Tempo server.
- [ ] Support image reference.
- [ ] Support tag/digest.
- [ ] Support args.
- [ ] Support environment references by name.
- [ ] Support pull policy.
- [ ] Support registry auth references.
- [ ] Support resource limits.
- [ ] Prefer stdin/stdout protocol first.
- [ ] Add HTTP transport later only if needed.
- [ ] Reuse protocol negotiation.
- [ ] Reuse core schema validation.
- [ ] Reuse run-start snapshotting for image digests where possible.

### Security

- [ ] Add network policy settings.
- [ ] Add volume mount policy settings.
- [ ] Add per-tenant resource limits.
- [ ] Add image allowlist/denylist option.
- [ ] Add registry credential redaction.

### Acceptance Criteria

- [ ] Container runtime appears unavailable when dependencies are absent.
- [ ] Container runtime follows the same step interface as every other runtime.
- [ ] Container-specific security controls are explicit.

## Phase 12 - MQ And Event Systems

Owner:
Started:
Completed:
Notes:
PRs:

Goal: Add MQ support without confusing triggers and steps.

### Trigger Providers

- [ ] Design trigger provider registry separately from step runtime registry.
- [ ] Support inbound flow activation from MQ.
- [ ] Add trigger provider descriptors.
- [ ] Add tenant authorization for trigger creation.
- [ ] Add durable consumer lifecycle management.
- [ ] Do not run long-lived consumers inside step runtime providers.

### Step Providers

- [ ] Add outbound publish step runtime.
- [ ] Add request-reply step runtime if needed.
- [ ] Add timeout behavior.
- [ ] Add correlation ID propagation.
- [ ] Add dead-letter/error mapping.
- [ ] Add schema validation before publish and after reply.

### Acceptance Criteria

- [ ] MQ triggers start flows.
- [ ] MQ steps publish or request/reply within flows.
- [ ] Trigger lifecycle is not coupled to per-step execution lifecycle.

## Phase 13 - GA Hardening

Owner:
Started:
Completed:
Notes:
2026-04-19: Added startup seeding for one protected example step per enabled
runtime type, including small sample artifacts for artifact-backed runtimes.
Added dependency guards for destructive routes so linked steps, data flows,
triggers, artifacts, and artifact versions are rejected with conflict responses
instead of being silently removed. Data flow deletes now cascade retained run
history, tenant/account deletes cascade tenant-owned children, and focused
Touchstone coverage validates the protected-delete behavior.
2026-04-20: Tightened startup runtime templates so they are executable examples,
not metadata placeholders. Preview/compatibility runtimes are no longer seeded as
templates, protected existing templates are repaired in place, the External.Rest
template targets a local Tempo sample endpoint, and Artifact.DotnetProcess ships
a real packaged .NET sample handler with the server build.
PRs:

Goal: Make the platform production-ready.

### Security

- [x] Threat model artifact upload.
- [x] Threat model external execution.
- [x] Threat model tenant isolation.
- [x] Threat model secret redaction.
- [ ] Add audit records for denied artifact/runtime operations.
- [ ] Add audit records for host executable allowlist changes.
- [x] Add operator documentation for trust boundaries.
- [x] Document that hostile multi-tenant code execution requires stronger OS
      isolation, resource limits, network egress controls, and per-tenant
      sandboxing.

### Operations

- [ ] Add runtime metrics.
- [ ] Add artifact metrics.
- [ ] Add queue/capacity metrics.
- [ ] Add protocol negotiation metrics.
- [ ] Add dashboard operational panels.
- [ ] Add backup/restore coverage for artifact metadata.
- [x] Document filesystem artifact blob backup requirements.
- [x] Add cleanup/recovery procedure for partial artifact uploads.
- [x] Add recovery procedure for interrupted artifact extraction.

### Data Integrity And Deletes

- [x] Seed protected sample steps on first startup for every enabled runtime
      descriptor.
- [x] Seed only runnable `Available` runtime descriptors as templates; do not
      seed preview compatibility markers as template steps.
- [x] Repair protected existing template steps and sample artifact versions
      idempotently when the template definition changes.
- [x] Create sample artifacts and versions when startup sample steps require
      artifact-backed configuration.
- [x] Ship a real `Artifact.DotnetProcess` sample handler assembly with the
      server build and package it into the startup sample artifact.
- [x] Point the `External.Rest` template at Tempo's local pre-auth sample route
      instead of an external internet service.
- [x] Re-run startup sample seeding idempotently for existing tenants without
      duplicating rows.
- [x] Block step deletion when any data flow references the step as the start
      step or in transition targets.
- [x] Block data flow deletion when any trigger targets the flow.
- [x] Block trigger deletion when any data flow references the trigger.
- [x] Block artifact deletion when any step references the artifact.
- [x] Block artifact version deletion when any step or retained flow-run
      snapshot references the version.
- [x] Preserve protected rows from direct and bulk destructive route requests.
- [x] Cascade data flow deletion to flow runs and step runs.
- [x] Cascade tenant/account deletion through tenant-owned child records.

### Compatibility

- [x] Announce inline REST deprecation.
- [x] Provide migration command/status.
- [x] Keep read-path compatibility for one planned window.
- [x] Remove write-path support immediately after migration phase.
- [ ] Plan legacy field removal separately.
- [x] Document `StepTypeEnum`/`PersistedStepTypeEnum` compatibility status.

### Performance

- [ ] Load test large runtime catalogs.
- [ ] Load test artifact upload/download.
- [ ] Load test process startup under capacity limits.
- [ ] Measure Python venv cold start.
- [ ] Measure Python venv warm start.
- [ ] Add optional prewarming after measurement.
- [ ] Verify SQLite behavior under local concurrent load.
- [ ] Verify server databases under concurrent flow runs.

### Acceptance Criteria

- [ ] Security review completed.
- [x] Upgrade path documented.
- [x] Operator docs completed.
- [x] Dashboard supports core runtime operations.
- [x] Touchstone suites pass.
- [x] Provider database tests pass.
- [x] Protocol conformance tests pass.

## Database Migration Checklist

Use this checklist for every database change.

- [ ] Add model changes.
- [ ] Add interface changes.
- [ ] Add common implementation changes only where SQL is provider-neutral.
- [ ] Add SQLite schema and migration.
- [ ] Add MySQL schema and migration.
- [ ] Add PostgreSQL schema and migration.
- [ ] Add SQL Server schema and migration.
- [ ] Add idempotency checks.
- [ ] Add index creation.
- [ ] Add rollback or recovery notes if rollback is not supported.
- [ ] Add Touchstone tests.
- [ ] Test fresh database initialization.
- [ ] Test upgrade from existing database.
- [ ] Test migration re-run safety.

## Authorization Matrix

Server must enforce these rules. Dashboard checks are only presentation.

| Action | Auth | Tenant Scope | Permission |
| --- | --- | --- | --- |
| List server runtimes | Required | None | Authenticated user |
| List tenant runtimes | Required | Tenant | Tenant access |
| Validate runtime config | Required | Tenant | `StepWrite` plus artifact permissions when needed |
| Create REST step | Required | Tenant | `StepWrite` |
| Create artifact-backed step | Required | Tenant | `StepWrite` and artifact read/reference permission |
| Upload artifact | Required | Tenant | Artifact write permission |
| Download artifact | Required | Tenant | Artifact read permission |
| Delete artifact version | Required | Tenant | Artifact delete permission |
| Configure external execution limits | Operator config | Server | Not tenant-settable |
| Configure host executable allowlist | Operator config | Server | Not tenant-settable |
| Run flow | Required | Tenant | Existing flow run permission |

Implementation tasks:

- [x] Add `Artifact` to `ResourceTypeEnum`.
- [x] Add any missing operation mappings for artifact create/read/update/delete.
- [x] Decide whether runtime validation uses `StepWrite` only or a new runtime
      operation.
- [x] Ensure admin-class routes never use compatibility fallback authorization.
- [x] Add tests for explicit deny overriding permit.
- [x] Add tests for cross-tenant artifact access rejection.

## OpenAPI Requirements

- [x] Runtime config polymorphism emits discriminator metadata.
- [x] Runtime config polymorphism emits `oneOf` schemas.
- [x] Runtime routes are tagged consistently.
- [x] Artifact routes are tagged consistently.
- [x] Step create/update DTOs show concrete runtime config options.
- [x] API Explorer can flatten the new operations.
- [x] File upload behavior is documented for the API Explorer.
- [x] No fixed request body appears as untyped `object` unless it is truly
      user-defined JSON.

## Testing Strategy

### Touchstone Suites

Add or extend suites under `src/Test.Shared/Suites`.

- [x] `StepIdentitySuite`
- [x] `RuntimeConfigSuite`
- [x] `RuntimeRegistrySuite`
- [x] `StepResolverSuite`
- [x] `BuiltinReconciliationSuite`
- [x] `InlineRestMigrationSuite`
- [x] `ArtifactSuite`
- [x] `ProtocolSuite`
- [x] `ExternalRuntimeCapacitySuite`
- [x] `ArtifactProcessSuite`
- [x] `ArtifactPythonSuite` coverage in `ArtifactProcessSuite`
- [x] `ArtifactJavaScriptSuite` coverage in `ArtifactProcessSuite`
- [x] `RuntimeAuthorizationSuite` coverage for artifact-backed step writes in
      `ArtifactProcessSuite`
- [x] `DeletionProtectionSuite`
- [x] `HydrationSuite` coverage for startup runtime sample seeding and
      execution of every runnable seeded template.

### Provider Database Tests

- [x] Fresh create schema.
- [ ] Upgrade schema.
- [x] Re-run migrations.
- [x] CRUD for runtime step columns.
- [x] CRUD for artifacts.
- [x] CRUD for artifact versions.
- [x] Tenant uniqueness.
- [x] Tenant isolation.
- [x] GC eligibility queries.

### Runtime Tests

- [x] Builtin class step.
- [x] Builtin method step.
- [x] Builtin class reconciliation.
- [x] Builtin method reconciliation.
- [x] Builtin ambiguous binding.
- [x] Builtin orphaned binding.
- [x] Persisted REST step.
- [x] Legacy inline REST read path.
- [x] Migrated REST step.
- [x] Artifact process success.
- [x] Artifact process non-zero exit.
- [x] Artifact process invalid stdout.
- [x] Artifact process timeout.
- [x] Artifact process cancellation.
- [x] Artifact Python success.
- [x] Artifact Python dependency failure.
- [x] Artifact Python protocol mismatch.
- [x] Artifact JavaScript success.
- [x] Artifact Dotnet process success.
- [x] Source-code step packaging for Python, JavaScript, and C#.

### Security Tests

- [x] Tenant cannot read another tenant artifact.
- [x] Tenant cannot reference another tenant artifact in a step.
- [x] Tenant cannot submit host executable path.
- [x] External runtime disabled blocks execution.
- [x] Runtime validation rejects unsafe config.
- [x] Archive zip-slip is rejected.
- [x] Symlink/hardlink archive entries are rejected or safely normalized.
- [x] Secret values are redacted from diagnostics.

### Dashboard Tests

- [ ] Runtime catalog renders.
- [x] Step editor renders descriptors.
- [x] Source-code step creation UI builds.
- [x] Setup wizard UI builds.
- [x] Artifact file editor UI builds.
- [ ] Artifact upload works.
- [ ] Disabled runtime state renders.
- [ ] Orphaned built-in status renders.
- [x] API Explorer loads new OpenAPI operations.
- [x] No axios dependency added.

## Documentation Deliverables

- [x] Update `README.md` with the new platform model.
- [x] Update `REST_API.md` for runtime and artifact endpoints.
- [x] Add runtime provider authoring guide.
- [x] Add artifact manifest reference.
- [x] Add protocol v1 reference.
- [x] Add Python SDK quickstart.
- [x] Add external execution operator guide.
- [x] Add security/trust-boundary guide.
- [x] Add migration guide for inline REST.
- [x] Add dashboard user guide for artifacts and runtimes.

## Provider Authoring Contract

A new runtime provider must supply:

- [x] Runtime key.
- [x] Typed config DTO.
- [x] Config validator.
- [x] Runtime descriptor.
- [x] Supported contract types.
- [x] Availability check.
- [x] OpenAPI/config schema contribution.
- [x] Runner creation logic.
- [x] Diagnostics mapping.
- [x] Touchstone tests.
- [x] Security notes.

Current providers do not:

- [x] Parse fixed config through route-level `JsonElement`.
- [x] Implement its own copy of core JSON schema validation.
- [x] Bypass tenant authorization.
- [x] Access another tenant's artifacts.
- [x] Accept tenant-supplied host filesystem paths.
- [x] Write secrets into logs or run history.

## Definition Of Done For The Platform Shift

The Airflow-like platform foundation is complete when:

- [x] A flow can reference steps uniformly by execution key.
- [x] Built-in class steps, built-in method steps, REST steps, artifact process
      steps, artifact Python steps, artifact JavaScript steps, and artifact
      Dotnet process steps all execute through the same resolver and runtime
      provider interface.
- [x] Step create/update APIs use typed runtime config.
- [x] Runtime providers appear in OpenAPI and the dashboard.
- [x] Inline REST has been migrated out of flow definitions.
- [x] Artifact upload/versioning/retention exists.
- [x] Artifact-backed process execution is implicitly available and capacity-limited;
      host executables remain operator-gated.
- [x] Protocol v1 and C#/Python/JavaScript SDK coverage tests exist.
- [x] `latest` artifact versions are snapshotted at flow-run start.
- [x] Tenant isolation is enforced in database, API, artifact store, and runtime
      resolution.
- [x] Fresh startup creates protected sample steps for every enabled runtime
      descriptor.
- [x] Startup sample steps execute successfully for every runnable seeded
      runtime descriptor.
- [x] Destructive routes protect linked objects and owner deletes cascade
      tenant-owned child records.
- [x] Touchstone suites pass through automated, xUnit, and NUnit runners.
- [x] Dashboard can manage runtimes, steps, artifacts, and observe execution.
- [x] Dashboard can create runnable steps from pasted Python, JavaScript, and C#
      code and guide first-time setup through a modal wizard.

## Deferred Items

These are intentionally not part of the first platform foundation.

- [ ] Full hostile multi-tenant sandboxing. Next action: choose isolation
      boundary per deployment target before enabling untrusted tenant code.
- [ ] Hosted in-process C# execution. Next action: design unload, dependency
      isolation, opt-in settings, and memory-leak tests separately from
      `Artifact.DotnetProcess`.
- [ ] Container runtime. Next action: finalize provider key naming and
      implement optional engine detection before adding image execution.
- [ ] MQ trigger/step split. Next action: design a trigger provider registry
      separate from step runtime providers.
- [x] JavaScript/Node SDK implemented under `sdk/js` with executable API
      coverage tests.
- [ ] Go SDK. Next action: implement after protocol v1 conformance packaging is
      stable.
- [ ] Advanced binary payload protocol. Next action: design as an additive v1.x
      extension or a v2 protocol if it changes the envelope contract.
- [ ] Distributed multi-node artifact cache. Next action: decide cache
      coherency and invalidation semantics before multi-node scheduling.
- [ ] Network egress policy enforcement. Next action: pair with container or OS
      sandboxing because Tempo process-level checks cannot enforce this alone.
- [ ] cgroup-based Linux resource isolation. Next action: design alongside the
      container/runtime isolation model.
- [ ] Windows Job Object hardening beyond first best-effort implementation. Next
      action: add platform-specific kill, memory, and process-tree tests.

## Risk Register

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Existing flows rely on display names | Flow breakage | Phase 1 migration and compatibility tests |
| Runtime config becomes untyped JSON | API and OpenAPI regression | Registry-driven typed polymorphism and tests |
| Tenant can execute host paths | Critical security issue | Artifact-rooted execution only; `Host.Executable` operator allowlist only |
| Inline REST migration corrupts flow JSON | Flow breakage | Application-level migrator using serializers, not SQL JSON surgery |
| Python venv cold start is slow | Poor UX | Cache by artifact SHA/Python version, measure before prewarming |
| External processes exhaust host resources | Noisy-neighbor outage | Server-wide and per-tenant capacity manager |
| Artifact GC deletes needed versions | Replay/debug loss | Reference retention and flow-run snapshot checks |
| OpenAPI misses polymorphic schemas | Dashboard/API Explorer breakage | Runtime descriptor driven `oneOf` generation tests |
| Provider SQL diverges | Migration failures | Provider-specific migration tests for all databases |
| SDK/protocol drift | Runtime incompatibility | Versioned conformance suite |

## Immediate Next PR Sequence

Use this order to keep review size manageable.

- [x] PR 1: Baseline tests and identity inventory.
- [x] PR 2: Add `ExecutionKey`, migrations, database methods, and tests.
- [x] PR 3: Update `ensure-steps` and display-name-safe execution tests.
- [x] PR 4: Add runtime key wrapper, contract enums, and typed config base.
- [x] PR 5: Add registry-driven runtime config serialization.
- [x] PR 6: Add runtime registry descriptors and runtime catalog routes.
- [x] PR 7: Add resolver and refactor `DataFlowRunner` through compatibility
      adapter.
- [x] PR 8: Add built-in reconciliation.
- [x] PR 9: Add inline REST migrator and stop dashboard inline REST writes.
- [x] PR 10: Add artifact metadata tables and database methods.
- [x] PR 11: Add local filesystem artifact blob store and routes.
- [x] PR 12: Add artifact retention and GC skeleton.
- [x] PR 13: Add protocol v1 and conformance tests.
- [x] PR 14: Add Python SDK runner shim.
- [x] PR 15: Add external execution settings and capacity manager.
- [x] PR 16: Add `Artifact.Process`.
- [x] PR 17: Add `Artifact.Python`.
- [x] PR 18: Add dashboard runtime/artifact/step editor updates.
- [x] PR 19: Add `Host.Executable` operator allowlist runtime.
- [x] PR 20: Finish artifact/runtime authorization audit and documentation.
- [x] PR 21: Add startup runtime sample seeding, deletion dependency guards,
      cascade coverage, and Touchstone verification.
- [x] PR 22: Convert startup runtime templates from placeholders into executable
      samples and verify them through Touchstone.
- [x] PR 23: Add `Artifact.JavaScript`, source-code step packaging, and dashboard
      first-run setup/onboarding improvements.
