# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Tempo is a .NET 8 library for orchestrating data flows through coordinated steps. It implements a workflow engine with multi-tenant support, triggers, and configurable step transitions.

## Build and Test Commands

```bash
# Build the solution
dotnet build src/Tempo.sln

# Build in Release mode
dotnet build src/Tempo.sln -c Release

# Run the automated test project
dotnet run --project src/Test.Automated/Test.Automated.csproj

# Run the sample application (demonstrates data flow execution)
dotnet run --project src/Test.SampleApp/Test.SampleApp.csproj

# Create NuGet package (enabled via GeneratePackageOnBuild)
dotnet build src/Tempo.sln -c Release
```

## Core Architecture

### Data Flow Execution Model

The library is built around a **state machine pattern** with three ways to define steps:

1. **Class-based Steps**: Inherit from abstract `Step` class and implement `Run(StepRequest)`
2. **Attribute-based Steps**: Static methods decorated with `[StepMethod]` attribute
3. **Inline REST Steps**: Defined directly in the DataFlow using `RestStepConfiguration`

**Core Components**:
- **DataFlow**: Orchestrates a workflow by defining step transitions
- **StepTransition**: Defines routing logic with three outcomes: `OnSuccess`, `OnFailure`, `OnException`
- **StepManager**: Manages both class-based and attribute-based steps with thread-safe storage
- **DataFlowRunner**: Executes data flows by walking through step transitions
- **Trigger**: Initiates data flow execution (currently supports HTTP, RabbitMQ, Native)

### Key Architectural Relationships

1. **DataFlow Structure** (src/Tempo/DataFlow.cs):
   - Contains a dictionary of `StepTransition` objects keyed by step identifier
   - References a `StartStepId` to begin execution
   - Associates with a `Trigger` that initiates the flow
   - Supports `MaxRuntimeMs` for flow-level timeout control
   - Implements validation methods: `ValidateStartingStep()`, `ValidateStepReferences()`, `HasCycles()`

2. **Step Execution Patterns**:
   - **Class-based** (src/Tempo/Step.cs): Abstract class requiring implementation of `async Task<StepResult> Run(StepRequest req)`
   - **Attribute-based** (src/Tempo/StepMethodAttribute.cs): Static methods with signature `static Task<StepResult> MethodName(StepRequest req)` decorated with `[StepMethod("identifier")]`
   - **REST-based** (src/Tempo/RestStepConfiguration.cs): Inline HTTP requests configured with URL templates, methods, headers
   - All steps are reusable across multiple data flows

3. **StepManager Workflow** (src/Tempo/StepManager.cs):
   - Register class-based steps via `Add(Step step)`
   - Register attribute-based steps via `RegisterMethod()` or `ScanAssembly()`
   - Lookup steps using `GetStepRunner(identifier, tenantId)` which checks both regular steps and attribute-based methods
   - Supports fallback: tenant-specific lookup → global lookup if tenant lookup fails

4. **DataFlowRunner Execution** (src/Tempo/Runners/DataFlowRunner.cs):
   - Resolves step runners from either `StepManager` or inline `StepTransition` definitions
   - Executes steps in sequence based on transition outcomes
   - Tracks execution with `transitionCounts` to enforce `MaxTransitions` per step (prevents infinite loops)
   - Enforces flow-level and step-level timeouts
   - Writes metrics to optional `MetricsStore` for observability

5. **Transition Logic** (src/Tempo/StepTransition.cs):
   - Each step has three possible exit paths: `OnSuccess`, `OnFailure`, `OnException`
   - Null transition values terminate the data flow
   - Supports `MaxTransitions` to limit how many times a step can be visited
   - Can embed inline step configuration via `StepType` and `Rest` properties

6. **Multi-tenancy** (src/Tempo/Tenant.cs):
   - All core entities (DataFlow, Step, Tenant) have a `TenantId` property
   - `StepManager.GetByIdentifier()` supports tenant-scoped lookups
   - Attribute-based steps can specify `TenantId` in the `[StepMethod]` attribute
   - Note: There's a logic bug in `StepManager.All()` at line 73 where the null check condition is inverted

### Identifier Generation

All entities use the PrettyId library to generate human-readable identifiers:
- Format: `{type}_{random_string}` (e.g., `dataflow_abc123...`)
- Length: 64 characters
- Generated at object construction time

### Step Request/Result Flow

- **StepRequest**: Contains `DataFlowId`, `RequestId`, `Data`, and `Metadata`
- **StepResult**: Returns `Result` (enum), `Data`, `Exception`, and `Metadata`
- **StepResultTypeEnum**: Success, Error, or Exception

## Dependencies

- **PrettyId** (v2.0.0): Identifier generation
- **RestWrapper** (v3.1.8): HTTP operations (used by HttpTrigger and RestStepRunner)
- **Microsoft.Data.Sqlite** (v9.0.10): SQLite database support for metrics storage
- **SyslogLogging** (v2.0.11): Logging framework

## Important Implementation Notes

### When Implementing Custom Steps

**Option 1: Class-based Steps**
1. Inherit from `Step` abstract class
2. Implement `async Task<StepResult> Run(StepRequest req)`
3. Register with `StepManager.Add(step)`
4. Set `TenantId` and optionally `MaxRuntimeMs`

**Option 2: Attribute-based Steps** (Recommended for simplicity)
1. Create a static method with signature: `static Task<StepResult> MethodName(StepRequest req)`
2. Decorate with `[StepMethod("step_identifier", TenantId = "...", MaxRuntimeMs = 5000)]`
3. Register by calling `StepManager.ScanAssembly()` or `ScanEntryAssembly()`

**Option 3: Inline REST Steps**
1. Set `StepTransition.StepType = StepTypeEnum.Rest`
2. Provide `StepTransition.Rest` configuration with URL, method, headers, timeout
3. No need to register in StepManager

### Step Result Guidelines

- Return `StepResultTypeEnum.Success` for successful operations
- Return `StepResultTypeEnum.Error` for expected failures (triggers `OnFailure` transition)
- Return `StepResultTypeEnum.Exception` or throw an exception for unexpected failures (triggers `OnException` transition)
- Set `StepResult.Data` to pass data to the next step
- `StepRequest.Data` from the previous step is available in your step

### DataFlowRunner Usage

```csharp
// Create step manager and register steps
StepManager stepManager = new StepManager();
stepManager.ScanEntryAssembly(); // Scans for [StepMethod] attributes

// Create data flow runner
DataFlowRunner runner = new DataFlowRunner(stepManager);

// Optional: Add metrics store for observability
runner.MetricsStore = new SqliteMetricsStore("metrics.db");

// Execute data flow
StepRequest request = new StepRequest { DataFlowId = flow.Identifier, RequestId = "req_123" };
StepResult result = await runner.Run(flow, request);
```

### Data Flow Validation

DataFlowRunner automatically validates before execution, but you can validate manually:
```csharp
if (!dataFlow.ValidateStartingStep()) { /* handle */ }
if (!dataFlow.ValidateStepReferences(out List<string> errors)) { /* handle errors */ }
// Note: HasCycles() is not currently implemented but referenced in architecture
```

### Known Code Issues

- **StepManager.cs:73**: Logic bug where the null check condition is inverted in `All()` method
- **Nullable warnings**: Code uses `#pragma warning disable CS8625/CS8603` to suppress nullable reference warnings

## Project Structure

- `src/Tempo/` - Main library source code
  - `Runners/` - Step execution engines (DataFlowRunner, CodeStepRunner, CodeAttributeStepRunner, RestStepRunner)
  - `Triggers/` - Flow initiation mechanisms (HttpTrigger, etc.)
  - `Metrics/` - Observability components (MetricsStore, SqliteMetricsStore, DataFlowRunDetails, StepRunDetails)
  - `Enums/` - Shared enumerations (StepResultTypeEnum, StepTypeEnum)
  - `Logs/` - Logging infrastructure
- `src/Tempo.Server/` - Server/hosting project (executable)
- `src/Test.Automated/` - Automated test project
- `src/Test.SampleApp/` - Sample application demonstrating data flow execution
- Target framework: .NET 8
- Package settings configured for NuGet publishing

## Key Design Patterns

1. **Runner Pattern**: Different runner types (CodeStepRunner, CodeAttributeStepRunner, RestStepRunner) inherit from abstract `StepRunner` base class and implement `ExecuteInternal()`. The base class handles timeout enforcement.

2. **Registry Pattern**: `StepManager` acts as a registry for both class-based steps and attribute-based methods, providing unified lookup via `GetStepRunner()`.

3. **State Machine**: DataFlowRunner walks through step transitions based on execution results, with each step having three possible exit paths.

4. **Strategy Pattern**: Steps can be defined via multiple strategies (class inheritance, attribute decoration, inline REST config) but all execute through the same runner interface.

## Coding Standards

**These rules must be followed STRICTLY in all code files.**

### File and Namespace Structure

- Namespace declaration at the top, using statements INSIDE the namespace block
- Microsoft and standard system library usings first (alphabetical order)
- Other using statements after system usings (alphabetical order)
- One class or one enum per file (no nested classes/enums)
- Regions (Public-Members, Private-Members, etc.) NOT required for files under 500 lines

```csharp
namespace Tempo.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using RestWrapper;
    using Tempo.Enums;

    public class ExampleRunner
    {
        // implementation
    }
}
```

### Documentation Standards

- ALL public members, constructors, and public methods MUST have XML code documentation
- NO code documentation on private members or private methods
- Document thread safety guarantees in XML comments
- Document nullability expectations
- Document default/min/max values where appropriate
- Document exceptions using `/// <exception>` tags

```csharp
/// <summary>
/// Maximum runtime in milliseconds (0 for no timeout).
/// Default: 0 (no timeout). Range: 0 to int.MaxValue.
/// </summary>
/// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative.</exception>
public int MaxRuntimeMs
{
    get => _MaxRuntimeMs;
    set => _MaxRuntimeMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(MaxRuntimeMs));
}
```

### Naming Conventions

- Private class member variables: underscore + PascalCase (e.g., `_FooBar`, NOT `_fooBar`)
- No `var` - always use actual types
- Public members: explicit getters and setters using backing variables when value requires range or null validation

```csharp
// Correct
private string _Identifier;
private int _MaxRuntimeMs;
Dictionary<string, Step> steps = new Dictionary<string, Step>();

// Incorrect
private string _identifier;
private int _maxRuntimeMs;
var steps = new Dictionary<string, Step>();
```

### Async Patterns

- Every async method MUST accept a `CancellationToken` parameter (unless the class has a CancellationToken or CancellationTokenSource member)
- Use `.ConfigureAwait(false)` on all async calls where appropriate
- Check `token.ThrowIfCancellationRequested()` at appropriate places
- When implementing a method that returns `IEnumerable`, create an async variant with `CancellationToken`

```csharp
public async Task<StepResult> ExecuteAsync(StepRequest request, CancellationToken token = default)
{
    token.ThrowIfCancellationRequested();

    StepResult result = await _Step.Run(request).ConfigureAwait(false);

    token.ThrowIfCancellationRequested();

    return result;
}
```

### Exception Handling

- Use specific exception types rather than generic `Exception`
- Always include meaningful error messages with context
- Consider custom exception types for domain-specific errors
- Document exceptions with `/// <exception>` tags
- Use exception filters when appropriate: `catch (SqlException ex) when (ex.Number == 2601)`

```csharp
/// <exception cref="ArgumentNullException">Thrown when step is null.</exception>
/// <exception cref="InvalidOperationException">Thrown when step is already registered.</exception>
public void Add(Step step)
{
    if (step == null)
        throw new ArgumentNullException(nameof(step), "Step cannot be null.");

    if (_Steps.ContainsKey(step.Identifier))
        throw new InvalidOperationException($"Step with identifier '{step.Identifier}' is already registered.");

    _Steps.Add(step.Identifier, step);
}
```

### Null Safety

- Use nullable reference types (project already has `<Nullable>enable</Nullable>`)
- Validate input parameters with guard clauses at method start
- Use `ArgumentNullException.ThrowIfNull()` for .NET 6+ or manual null checks
- Use `.FirstOrDefault()` with null checks rather than `.First()` when element might not exist
- Proactively identify and eliminate situations where null might cause exceptions

```csharp
public Step GetByIdentifier(string id, string? tenantId = null)
{
    ArgumentNullException.ThrowIfNull(id);

    lock (_Lock)
    {
        Step? step = _Steps.Values.FirstOrDefault(s => s.Identifier == id);
        return step ?? throw new InvalidOperationException($"Step '{id}' not found.");
    }
}
```

### Resource Management

- Implement `IDisposable`/`IAsyncDisposable` when holding unmanaged resources or disposable objects
- Use `using` statements or `using` declarations for `IDisposable` objects
- Follow the full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- Always call `base.Dispose()` in derived classes

```csharp
public class ExampleManager : IDisposable
{
    private bool _Disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_Disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
            }
            _Disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
```

### Thread Safety

- Document thread safety guarantees in XML comments
- Use `Interlocked` operations for simple atomic operations
- Prefer `ReaderWriterLockSlim` over `lock` for read-heavy scenarios
- Use `lock` for write-heavy or mixed scenarios

```csharp
/// <summary>
/// Adds a step to the manager.
/// This method is thread-safe.
/// </summary>
public void Add(Step step)
{
    lock (_Lock)
    {
        _Steps.Add(step.Identifier, step);
    }
}
```

### LINQ Best Practices

- Prefer LINQ methods over manual loops when readability is not compromised
- Use `.Any()` instead of `.Count() > 0` for existence checks
- Be aware of multiple enumeration issues - consider `.ToList()` when needed
- Use `.FirstOrDefault()` with null checks rather than `.First()` when element might not exist

```csharp
// Correct
if (_Steps.Any(s => s.TenantId == tenantId))

// Incorrect
if (_Steps.Count(s => s.TenantId == tenantId) > 0)
```

### Configuration and Defaults

- Avoid hard-coded constant values for things developers may want to configure
- Use public members with backing private members set to reasonable defaults
- Document what different values mean or what effect they have

```csharp
/// <summary>
/// Default timeout in milliseconds for HTTP requests.
/// Default: 30000 (30 seconds). Set to 0 for no timeout.
/// </summary>
public int DefaultTimeoutMs
{
    get => _DefaultTimeoutMs;
    set => _DefaultTimeoutMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(DefaultTimeoutMs));
}

private int _DefaultTimeoutMs = 30000;
```

### Other Important Rules

- Do NOT use tuples unless absolutely necessary
- Do NOT make assumptions about opaque class members/methods - ask for the implementation
- If manual SQL strings are used, assume there's a good reason (don't suggest ORMs)
- NO `Console.WriteLine` statements in library code
- Compile code and ensure it's free of errors and warnings
- If a README exists, ensure it is accurate
