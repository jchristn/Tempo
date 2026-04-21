namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Tempo.Enums;
    using TempoStep = Tempo.Step;
    using TempoStepManager = Tempo.StepManager;
    using TempoStepRequest = Tempo.StepRequest;
    using TempoStepResult = Tempo.StepResult;
    using Touchstone.Core;

    public static class HydrationSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Hydration",
                displayName: "First-boot seeding",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Hydration", "SeedsDefaults", "Seeds tenant/admin/user/credential on first boot", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            HydrationSettings cfg = new HydrationSettings();
                            HydrationService svc = new HydrationService(driver, cfg);
                            await svc.HydrateAsync(ct);

                            Assert2.NotNull(svc.DefaultTenant, "default tenant");
                            Assert2.NotNull(svc.DefaultAdministrator, "default admin");
                            Assert2.NotNull(svc.DefaultUser, "default user");
                            Assert2.NotNull(svc.DefaultCredential, "default credential");
                            Assert2.StartsWith("pub_", svc.DefaultCredential!.AccessKey, "access key");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Hydration", "Idempotent", "Second hydrate does not duplicate seed", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            HydrationSettings cfg = new HydrationSettings();
                            HydrationService svc = new HydrationService(driver, cfg);
                            await svc.HydrateAsync(ct);
                            await svc.HydrateAsync(ct);
                            var tenants = await driver.Tenants.AllAsync(ct);
                            var admins = await driver.Administrators.AllAsync(ct);
                            Assert2.Equal(1, tenants.Count, "one tenant");
                            Assert2.Equal(1, admins.Count, "one admin");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Hydration", "SeedDisabled", "SeedDefaults=false skips seeding", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            HydrationSettings cfg = new HydrationSettings { SeedDefaults = false };
                            HydrationService svc = new HydrationService(driver, cfg);
                            await svc.HydrateAsync(ct);
                            var tenants = await driver.Tenants.AllAsync(ct);
                            Assert2.Equal(0, tenants.Count, "no tenants when disabled");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Hydration", "SeedsRuntimeSamples", "First boot creates a sample step for every startup runtime type and sample artifacts where needed", async ct =>
                    {
                        string root = Path.Combine(Path.GetTempPath(), "tempo-hydration-samples-" + System.Guid.NewGuid().ToString("N"));
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        int restPort = FreePort();
                        using OneShotHttpServer restServer = new OneShotHttpServer(restPort);
                        try
                        {
                            TempoStepManager manager = new TempoStepManager();
                            manager.Add(new HydrationSampleClassStep());
                            MethodInfo method = typeof(HydrationSuite).GetMethod(nameof(HydrationSampleMethod), BindingFlags.Static | BindingFlags.NonPublic)!;
                            manager.RegisterMethod(DefaultRuntimeStepSeeder.BuiltinMethodExecutionKey, method);

                            ArtifactSettings artifacts = new ArtifactSettings { RootPath = Path.Combine(root, "artifacts") };
                            RuntimeSettings runtimes = new RuntimeSettings();
                            runtimes.ExternalExecution.CacheRoot = Path.Combine(root, "cache");
                            runtimes.ExternalExecution.ScratchRoot = Path.Combine(root, "scratch");
                            LocalFilesystemArtifactBlobStore blobStore = new LocalFilesystemArtifactBlobStore(artifacts);
                            RestSettings rest = new RestSettings { Hostname = "127.0.0.1", Port = restPort };
                            HydrationService svc = new HydrationService(driver, new HydrationSettings(), null, artifacts, runtimes, manager, blobStore, rest);
                            await svc.HydrateAsync(ct);

                            Tenant tenant = svc.DefaultTenant!;
                            StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(
                                manager,
                                runtimes: runtimes,
                                database: driver,
                                artifactBlobStore: blobStore,
                                externalCapacity: new ExternalRuntimeCapacityManager(runtimes.ExternalExecution));
                            string[] expected = ExpectedSeedExecutionKeys(registry);
                            foreach (string executionKey in expected)
                            {
                                StepRecord? step = await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, executionKey, ct);
                                Assert2.NotNull(step, "seed step " + executionKey);
                                Assert2.True(step!.IsProtected, "seed step protected " + executionKey);
                            }

                            int artifactExpected = expected.Count(k =>
                                k == DefaultRuntimeStepSeeder.ArtifactProcessExecutionKey ||
                                k == DefaultRuntimeStepSeeder.ArtifactPythonExecutionKey ||
                                k == DefaultRuntimeStepSeeder.ArtifactJavaScriptExecutionKey ||
                                k == DefaultRuntimeStepSeeder.ArtifactDotnetProcessExecutionKey);
                            Assert2.Equal(artifactExpected, (await driver.Artifacts.AllAsync(tenant.Id, ct)).Count, "sample artifact count");
                            Assert2.NotNull(svc.RuntimeStepSeedResult, "runtime seed result");
                            Assert2.True(svc.RuntimeStepSeedResult!.StepsCreated.Count >= expected.Length, "runtime sample steps created");

                            foreach (string executionKey in expected)
                            {
                                Task? restTask = executionKey == DefaultRuntimeStepSeeder.ExternalRestExecutionKey
                                    ? restServer.ServeOnceAsync(ct)
                                    : null;

                                TempoStepResult result = await ExecuteSeededStepAsync(driver, registry, tenant.Id, executionKey, ct);
                                Assert2.Equal(StepResultTypeEnum.Success, result.Result, "template step executes " + executionKey);
                                if (restTask != null) await restTask;
                            }
                        }
                        finally
                        {
                            await TempTestStore.DisposeAsync(driver);
                            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
                        }
                    })
                });
        }

        private static string[] ExpectedSeedExecutionKeys(StepRuntimeRegistry registry)
        {
            HashSet<RuntimeKey> available = registry.DescribeAll()
                .Where(d => d.Availability == StepRuntimeAvailabilityStateEnum.Available)
                .Select(d => d.RuntimeKey)
                .ToHashSet();
            List<string> keys = new List<string>();
            if (available.Contains(StepRuntimeKeys.BuiltinClass)) keys.Add(DefaultRuntimeStepSeeder.BuiltinClassExecutionKey);
            if (available.Contains(StepRuntimeKeys.BuiltinMethod)) keys.Add(DefaultRuntimeStepSeeder.BuiltinMethodExecutionKey);
            if (available.Contains(StepRuntimeKeys.ExternalRest)) keys.Add(DefaultRuntimeStepSeeder.ExternalRestExecutionKey);
            if (available.Contains(StepRuntimeKeys.ArtifactProcess)) keys.Add(DefaultRuntimeStepSeeder.ArtifactProcessExecutionKey);
            if (available.Contains(StepRuntimeKeys.ArtifactPython)) keys.Add(DefaultRuntimeStepSeeder.ArtifactPythonExecutionKey);
            if (available.Contains(StepRuntimeKeys.ArtifactJavaScript)) keys.Add(DefaultRuntimeStepSeeder.ArtifactJavaScriptExecutionKey);
            if (available.Contains(StepRuntimeKeys.ArtifactDotnetProcess)) keys.Add(DefaultRuntimeStepSeeder.ArtifactDotnetProcessExecutionKey);
            return keys.ToArray();
        }

        private static async Task<TempoStepResult> ExecuteSeededStepAsync(SqliteDatabaseDriver driver, StepRuntimeRegistry registry, string tenantId, string executionKey, CancellationToken token)
        {
            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
            {
                TenantId = tenantId,
                Name = "template-" + executionKey,
                StartStepId = executionKey,
                Transitions = new Dictionary<string, Tempo.StepTransition> { [executionKey] = new Tempo.StepTransition() }
            }, token);

            FlowRun run = await driver.FlowRuns.CreateAsync(new FlowRun { TenantId = tenantId, DataFlowId = flow.Id }, token);
            FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(driver, run, flow, token);
            RegistryDataFlowRunner runner = new RegistryDataFlowRunner(new DatabaseStepExecutionResolver(driver), registry);
            return await runner.Run(FlowDispatchService.Hydrate(flow), new TempoStepRequest
            {
                TenantId = tenantId,
                DataFlowId = flow.Id,
                FlowRunId = run.Id,
                RequestId = run.Id,
                Data = new Dictionary<string, object> { ["value"] = 123 }
            }, snapshot, token);
        }

        private sealed class HydrationSampleClassStep : TempoStep
        {
            public HydrationSampleClassStep()
            {
                Identifier = DefaultRuntimeStepSeeder.BuiltinClassExecutionKey;
                TenantId = "global";
                Name = "Hydration sample class";
            }

            public override Task<TempoStepResult> Run(TempoStepRequest req)
            {
                return Task.FromResult(HydrationSampleResult(req, "class"));
            }
        }

        private static Task<TempoStepResult> HydrationSampleMethod(TempoStepRequest req)
        {
            return Task.FromResult(HydrationSampleResult(req, "method"));
        }

        private static TempoStepResult HydrationSampleResult(TempoStepRequest req, string kind)
        {
            return new TempoStepResult
            {
                ProtocolVersion = req.ProtocolVersion,
                TenantId = req.TenantId,
                DataFlowId = req.DataFlowId,
                FlowRunId = req.FlowRunId,
                StepRunId = req.StepRunId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = new Dictionary<string, object> { ["kind"] = kind },
                Metadata = req.Metadata
            };
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<bool> PythonAvailableAsync(CancellationToken token)
        {
            try
            {
                using Process process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.StartInfo.ArgumentList.Add("--version");
                if (!process.Start()) return false;
                await process.WaitForExitAsync(token);
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private static async Task<bool> NodeAvailableAsync(CancellationToken token)
        {
            try
            {
                using Process process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.StartInfo.ArgumentList.Add("--version");
                if (!process.Start()) return false;
                await process.WaitForExitAsync(token);
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private sealed class OneShotHttpServer : IDisposable
        {
            private readonly TcpListener _Listener;

            public OneShotHttpServer(int port)
            {
                _Listener = new TcpListener(IPAddress.Loopback, port);
                _Listener.Start();
            }

            public async Task ServeOnceAsync(CancellationToken token)
            {
                using TcpClient client = await _Listener.AcceptTcpClientAsync(token);
                await using NetworkStream stream = client.GetStream();
                await DrainRequestHeadersAsync(stream, token);
                byte[] body = Encoding.UTF8.GetBytes("{\"sample\":\"external-rest\",\"ok\":true}");
                byte[] header = Encoding.UTF8.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json\r\n" +
                    "Content-Length: " + body.Length + "\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(header.AsMemory(0, header.Length), token);
                await stream.WriteAsync(body.AsMemory(0, body.Length), token);
            }

            private static async Task DrainRequestHeadersAsync(NetworkStream stream, CancellationToken token)
            {
                using StreamReader reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                try
                {
                    while (true)
                    {
                        string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                        if (string.IsNullOrEmpty(line)) break;
                    }
                }
                catch { }
            }

            public void Dispose()
            {
                try { _Listener.Stop(); } catch { }
            }
        }
    }
}
