# Tempo C# SDK

Protocol v1 SDK for .NET artifact step handlers.

## API

- `ProtocolVersions`: protocol constants, supported versions, and launch
  environment variable names.
- `StepRequest`: request envelope received from Tempo.
- `StepResult`: result envelope returned to Tempo.
- `StepResultType`: valid result states.
- `ITempoStepHandler`: async handler interface.
- `TempoStepHost`: JSON serialization, result helpers, correlation, and
  stdin/stdout runner.
- `Tempo.Sdk.Workers.*`: worker-protocol frame DTOs and JSON options for
  building custom C# worker clients.

## Handler

```csharp
using Tempo.Sdk;

public sealed class Handler : ITempoStepHandler
{
    public Task<StepResult> RunAsync(StepRequest request, CancellationToken token)
    {
        return Task.FromResult(TempoStepHost.Success(request, new { ok = true }));
    }
}

return await TempoStepHost.RunAsync(new Handler());
```

`TempoStepHost.RunAsync` reads one `StepRequest` JSON object from stdin and
writes one `StepResult` JSON object to stdout. Helper results preserve
`protocolVersion`, tenant, run, step-run, and request correlation fields.

## Worker Protocol DTOs

The SDK also includes the v0.3.0 worker websocket DTOs under
`Tempo.Sdk.Workers`:

- `WorkerProtocolJson`
- `WorkerFrameTypes`
- `WorkerHelloMessage`
- `WorkerHelloAckMessage`
- `WorkerHeartbeatMessage`
- `WorkerAssignMessage`
- `WorkerAssignAckMessage`
- `WorkerRunCompletedMessage`
- `WorkerDrainMessage`
- `WorkerResumeMessage`

`WorkerAssignMessage.Assignment`, `WorkerAssignMessage.Plan`, and
`WorkerRunCompletedMessage.Completion` are exposed as raw `JsonElement`
payloads so the SDK can stay aligned with the wire contract without taking a
dependency on Tempo.Server or Tempo.Core orchestration types.

## Test App

Run from the repository root:

```powershell
dotnet run --project .\sdk\csharp\Tempo.Sdk.TestApp\Tempo.Sdk.TestApp.csproj
```

The test application reflects the public SDK surface and asserts every public
symbol is covered by the test program.
