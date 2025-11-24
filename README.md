<div align="center">
  <img src="https://github.com/jchristn/tempo/blob/main/assets/logo.png" width="182" height="182">
</div>

# Tempo

[![NuGet](https://img.shields.io/nuget/v/Tempo.svg)](https://www.nuget.org/packages/Tempo/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

Tempo is a .NET 8 library for orchestrating data flows through coordinated steps. It implements a flexible workflow engine with multi-tenant support, configurable step transitions, and multiple execution patterns.

## Features

- **Three ways to define steps**: Class-based inheritance, attribute-based methods, or inline REST API calls
- **State machine workflow**: Define complex flows with success, failure, and exception paths
- **Multi-tenant support**: Isolate steps and flows by tenant with built-in tenant-aware lookups
- **Timeout controls**: Flow-level and step-level timeout enforcement
- **Loop prevention**: Configurable max transitions per step to prevent infinite loops
- **Observability**: Optional metrics store for tracking flow and step execution
- **Thread-safe**: StepManager provides concurrent access to registered steps
- **Multiple Trigger Types**: HTTP, RabbitMQ, and native trigger support

## Quick Start

### 1. Define Steps Using Attributes

```bash
dotnet add package Tempo
```

```csharp
using Tempo;

public class MySteps
{
    [StepMethod("generate_number")]
    public static async Task<StepResult> GenerateNumber(StepRequest req)
    {
        int number = Random.Shared.Next(1, 100);

        return new StepResult
        {
            DataFlowId = req.DataFlowId,
            RequestId = req.RequestId,
            Result = StepResultTypeEnum.Success,
            Data = number
        };
    }

    [StepMethod("multiply_by_two")]
    public static async Task<StepResult> MultiplyByTwo(StepRequest req)
    {
        int input = (int)req.Data;
        int result = input * 2;

        return new StepResult
        {
            DataFlowId = req.DataFlowId,
            RequestId = req.RequestId,
            Result = StepResultTypeEnum.Success,
            Data = result
        };
    }
}
```

### 2. Build and Execute a Data Flow

```csharp
using Tempo;
using Tempo.Runners;

// Create step manager and scan for attribute-based steps
StepManager stepManager = new StepManager();
stepManager.ScanEntryAssembly();

// Build data flow with step transitions
DataFlow flow = new DataFlow
{
    Identifier = "my_flow",
    StartStepId = "generate_number",
    Steps = new Dictionary<string, StepTransition>
    {
        ["generate_number"] = new StepTransition
        {
            OnSuccess = "multiply_by_two",
            OnFailure = null,
            OnException = null
        },
        ["multiply_by_two"] = new StepTransition
        {
            OnSuccess = null,  // End of flow
            OnFailure = null,
            OnException = null
        }
    }
};

// Execute the flow
DataFlowRunner runner = new DataFlowRunner(stepManager);
StepRequest request = new StepRequest
{
    DataFlowId = flow.Identifier,
    RequestId = "req_123"
};

StepResult result = await runner.Run(flow, request);
Console.WriteLine($"Final result: {result.Data}");
```

## Three Ways to Define Steps

### Option 1: Attribute-Based Steps (Recommended)

Decorate static methods with `[StepMethod]`:

```csharp
[StepMethod("process_order", TenantId = "tenant_123", MaxRuntimeMs = 5000)]
public static async Task<StepResult> ProcessOrder(StepRequest req)
{
    // Your logic here
    return new StepResult { Result = StepResultTypeEnum.Success };
}
```

Register with `StepManager.ScanAssembly()` or `ScanEntryAssembly()`.

### Option 2: Class-Based Steps

Inherit from the `Step` abstract class:

```csharp
public class ProcessOrderStep : Step
{
    public ProcessOrderStep() : base()
    {
        Identifier = "process_order";
        TenantId = "tenant_123";
        MaxRuntimeMs = 5000;
    }

    public override async Task<StepResult> Run(StepRequest req)
    {
        // Your logic here
        return new StepResult { Result = StepResultTypeEnum.Success };
    }
}
```

Register with `StepManager.Add(new ProcessOrderStep())`.

### Option 3: Inline REST Steps

Define HTTP requests directly in step transitions:

```csharp
new StepTransition
{
    StepType = StepTypeEnum.Rest,
    Rest = new RestStepConfiguration
    {
        Method = "POST",
        Url = "https://api.example.com/orders/{orderId}",
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Authorization"] = "Bearer {token}"
        },
        TimeoutMs = 30000
    },
    OnSuccess = "next_step",
    OnFailure = "handle_error"
}
```

## Step Transitions

Each step has three possible outcomes that determine the next step:

```csharp
new StepTransition
{
    OnSuccess = "next_step_id",      // Executed when Result = Success
    OnFailure = "error_handler_id",  // Executed when Result = Error
    OnException = "exception_log_id" // Executed when Result = Exception or timeout
}
```

Set any transition to `null` to terminate the flow.

## Advanced Features

### Timeout Controls

```csharp
// Flow-level timeout
DataFlow flow = new DataFlow
{
    MaxRuntimeMs = 60000,  // Entire flow must complete in 60 seconds
    // ...
};

// Step-level timeout (attribute-based)
[StepMethod("slow_step", MaxRuntimeMs = 5000)]
public static async Task<StepResult> SlowStep(StepRequest req) { /* ... */ }
```

### Loop Prevention

```csharp
new StepTransition
{
    MaxTransitions = 10,  // Step can only be visited 10 times max
    OnSuccess = "same_step",  // Could create a loop
    // ...
}
```

### Multi-Tenancy

```csharp
// Define tenant-specific step
[StepMethod("process_payment", TenantId = "tenant_acme")]
public static async Task<StepResult> ProcessPayment(StepRequest req) { /* ... */ }

// Lookup respects tenant isolation
StepRunner runner = stepManager.GetStepRunner("process_payment", "tenant_acme");
```

### Metrics and Observability

```csharp
using Tempo.Metrics;

DataFlowRunner runner = new DataFlowRunner(stepManager);
runner.MetricsStore = new SqliteMetricsStore("metrics.db");

// Metrics are automatically tracked for each flow and step execution
```

## Step Result Types

- **Success**: Operation completed successfully, triggers `OnSuccess` transition
- **Error**: Expected failure case (e.g., validation error), triggers `OnFailure` transition
- **Exception**: Unexpected error, triggers `OnException` transition
- **Timeout**: Step or flow exceeded its configured timeout, triggers `OnException` transition
- **MaxIterationsExceeded**: Step was visited more times than `MaxTransitions` allows, terminates flow

## Data Flow Between Steps

Data flows through the `StepRequest` and `StepResult` objects:

```csharp
public override async Task<StepResult> Run(StepRequest req)
{
    // Access data from previous step
    int inputValue = (int)req.Data;
    object previousMetadata = req.Metadata;

    // Process and return result
    return new StepResult
    {
        DataFlowId = req.DataFlowId,
        RequestId = req.RequestId,
        Result = StepResultTypeEnum.Success,
        Data = inputValue * 2,           // Passed to next step
        Metadata = "Processing complete" // Metadata passed to next step
    };
}
```

## Examples

See the `src/Test.SampleApp` project for a complete working example demonstrating:
- Random number generation with conditional branching
- Success, failure, and exception paths
- Data flow between steps
- Prime number validation

## Building

```bash
# Build the solution
dotnet build src/Tempo.sln

# Run tests
dotnet run --project src/Test.Automated/Test.Automated.csproj

# Run sample application
dotnet run --project src/Test.SampleApp/Test.SampleApp.csproj

# Create NuGet package
dotnet build src/Tempo.sln -c Release
```

## Contributing

Contributions are welcome! Please ensure all code follows the coding standards documented in `CLAUDE.md`.

## License

MIT License. See [LICENSE.md](LICENSE.md) for details.

## Logo

Logo provided by [softicons.com](https://www.softicons.com/object-icons/vista-musical-instruments-icons-by-icons-land/metronome-icon). Many thanks!
