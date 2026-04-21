namespace Test.ArtifactFixture
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Protocol;

    public static class Program
    {
        public static int Main(string[] args)
        {
            string mode = args.Length == 0 ? "success" : args[0];
            if (mode == "invalid")
            {
                Console.Write("not-json");
                return 0;
            }
            if (mode == "exit")
            {
                Console.Error.Write("fixture failure");
                return 7;
            }
            if (mode == "secret")
            {
                Console.Error.Write(Environment.GetEnvironmentVariable("TEMPO_TEST_SECRET") ?? "missing-secret");
                return 5;
            }
            if (mode == "sleep")
            {
                Thread.Sleep(TimeSpan.FromMinutes(5));
                return 0;
            }

            return TempoStepHost.RunAsync(new FixtureHandler(mode)).GetAwaiter().GetResult();
        }

        private sealed class FixtureHandler : ITempoStepHandler
        {
            private readonly string _Mode;

            public FixtureHandler(string mode)
            {
                _Mode = mode;
            }

            public Task<StepResult> RunAsync(StepRequest request, CancellationToken token)
            {
                object result = new
                {
                    fixture = true,
                    mode = _Mode,
                    input = request.Data,
                    protocolEnvironment = Environment.GetEnvironmentVariable(ProtocolVersions.ProtocolVersionEnvironmentVariable),
                    supportedProtocolEnvironment = Environment.GetEnvironmentVariable(ProtocolVersions.SupportedProtocolVersionsEnvironmentVariable)
                };
                return Task.FromResult(TempoStepHost.Success(request, result, new { fixture = "artifact-process", sdk = "dotnet" }));
            }
        }
    }
}
