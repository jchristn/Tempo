namespace Tempo.Sample.DotnetProcess
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Protocol;

    public static class Program
    {
        public static int Main(string[] args)
        {
            return TempoStepHost.RunAsync(new SampleHandler()).GetAwaiter().GetResult();
        }

        public sealed class SampleHandler : TempoStepHandlerBase
        {
            public override Task<StepResult> RunAsync(StepRequest request, CancellationToken token)
            {
                LogInfo("Sample .NET process step received request " + request.RequestId);
                Dictionary<string, object?> data = new Dictionary<string, object?>
                {
                    ["sample"] = "artifact-dotnet-process",
                    ["message"] = "Hello from the Tempo .NET process template.",
                    ["requestId"] = request.RequestId,
                    ["input"] = request.Data,
                    ["protocolEnvironment"] = Environment.GetEnvironmentVariable(ProtocolVersions.ProtocolVersionEnvironmentVariable),
                    ["supportedProtocolEnvironment"] = Environment.GetEnvironmentVariable(ProtocolVersions.SupportedProtocolVersionsEnvironmentVariable)
                };

                Dictionary<string, object> metadata = new Dictionary<string, object>
                {
                    ["template"] = "startup",
                    ["sdk"] = "dotnet"
                };

                return Task.FromResult(Success(request, data, metadata));
            }
        }
    }
}
