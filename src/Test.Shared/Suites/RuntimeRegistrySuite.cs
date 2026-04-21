namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Enums;
    using Tempo.Runners;
#if NET10_0
    using SyslogLogging;
    using Tempo.Core;
    using Tempo.Core.Database.Sqlite;
#endif
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;
#if NET10_0
    using Tempo.Server;
#endif
    using Touchstone.Core;

    public static class RuntimeRegistrySuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RuntimeRegistry",
                displayName: "Runtime registry",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("RuntimeRegistry", "DefaultCatalog", "Default registry exposes the initial runtime catalog", async _ =>
                    {
                        await Task.CompletedTask;
                        StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault();
                        var descriptors = registry.DescribeAll();
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.BuiltinClass), "Builtin.Class exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.BuiltinMethod), "Builtin.Method exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.BuiltinUnknown), "Builtin.Unknown exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.ExternalRest), "External.Rest exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.LegacyInlineRest), "Legacy.InlineRest exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.ArtifactProcess), "Artifact.Process exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.ArtifactPython), "Artifact.Python exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.ArtifactJavaScript), "Artifact.JavaScript exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.ArtifactDotnetProcess), "Artifact.DotnetProcess exists");
                        Assert2.True(descriptors.Any(d => d.RuntimeKey == StepRuntimeKeys.HostExecutable), "Host.Executable exists");
                    }),
                    new TestCaseDescriptor("RuntimeRegistry", "Validation", "Registry validates unknown keys and runtime config mismatches", async ct =>
                    {
                        StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault();
                        StepConfigValidationResult ok = await registry.ValidateAsync("ten_1", StepRuntimeKeys.ExternalRest, new ExternalRestRuntimeConfig { Method = "GET", Url = "https://example.com" }, ct);
                        Assert2.True(ok.Valid, "valid rest config");

                        StepConfigValidationResult mismatch = await registry.ValidateAsync("ten_1", StepRuntimeKeys.ExternalRest, new BuiltinUnknownRuntimeConfig(), ct);
                        Assert2.False(mismatch.Valid, "mismatch rejected");

                        StepConfigValidationResult unknown = await registry.ValidateAsync("ten_1", new RuntimeKey("External.Nope"), new ExternalRestRuntimeConfig(), ct);
                        Assert2.False(unknown.Valid, "unknown rejected");
                    }),
                    new TestCaseDescriptor("RuntimeRegistry", "ArtifactRuntimeAvailabilityReflectsCommands", "Artifact runtime availability reflects configured host commands while host executables stay gated", async ct =>
                    {
                        RuntimeSettings settings = new RuntimeSettings();
                        StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(runtimes: settings);
                        var descriptors = registry.DescribeAll();
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.Available, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactProcess).Availability, "process available");
                        Assert2.Equal(RuntimeCommandProbe.ProbePython(settings.ExternalExecution).Available ? StepRuntimeAvailabilityStateEnum.Available : StepRuntimeAvailabilityStateEnum.MissingDependency, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactPython).Availability, "python availability");
                        Assert2.Equal(RuntimeCommandProbe.ProbeNode(settings.ExternalExecution).Available ? StepRuntimeAvailabilityStateEnum.Available : StepRuntimeAvailabilityStateEnum.MissingDependency, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactJavaScript).Availability, "javascript availability");
                        Assert2.Equal(RuntimeCommandProbe.ProbeDotnetRuntime(settings.ExternalExecution).Available ? StepRuntimeAvailabilityStateEnum.Available : StepRuntimeAvailabilityStateEnum.MissingDependency, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactDotnetProcess).Availability, "dotnet availability");
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.DisabledBySettings, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.HostExecutable).Availability, "host executable disabled");

                        StepConfigValidationResult result = await registry.ValidateAsync("ten_1", StepRuntimeKeys.ArtifactProcess, new ArtifactProcessRuntimeConfig { ArtifactId = "art_1" }, ct);
                        Assert2.True(result.Valid, "artifact runtime validation uses config validation");
                    }),
                    new TestCaseDescriptor("RuntimeRegistry", "ArtifactRuntimeMissingDependencies", "Missing configured host commands mark dependent artifact runtimes unavailable", async ct =>
                    {
                        await Task.CompletedTask;
                        RuntimeSettings settings = new RuntimeSettings();
                        string missing = "tempo-missing-command-" + Guid.NewGuid().ToString("N");
                        settings.ExternalExecution.PythonExecutable = missing;
                        settings.ExternalExecution.NodeExecutable = missing;
                        settings.ExternalExecution.DotnetExecutable = missing;
                        StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(runtimes: settings);
                        var descriptors = registry.DescribeAll();
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.Available, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactProcess).Availability, "generic process still available");
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.MissingDependency, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactPython).Availability, "python missing");
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.MissingDependency, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactJavaScript).Availability, "node missing");
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.MissingDependency, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.ArtifactDotnetProcess).Availability, "dotnet missing");
                    }),
                    new TestCaseDescriptor("RuntimeRegistry", "HostExecutableDisabledByDefault", "Host.Executable rejects validation until host executables are enabled", async ct =>
                    {
                        StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(runtimes: new RuntimeSettings());
                        var descriptors = registry.DescribeAll();
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.DisabledBySettings, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.HostExecutable).Availability, "host executable gated");

                        StepConfigValidationResult result = await registry.ValidateAsync("ten_1", StepRuntimeKeys.HostExecutable, new HostExecutableRuntimeConfig { AllowListKey = "fixture" }, ct);
                        Assert2.False(result.Valid, "disabled host executable validation fails");
                    }),
                    new TestCaseDescriptor("RuntimeRegistry", "HostExecutableValidation", "Host.Executable validates allowlist keys and tenant argument policy", async ct =>
                    {
                        RuntimeSettings runtimes = HostRuntimeSettings(FixtureExecutable(), allowTenantArgs: true);
                        StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(runtimes: runtimes, externalCapacity: new ExternalRuntimeCapacityManager(runtimes.ExternalExecution));
                        var descriptors = registry.DescribeAll();
                        Assert2.Equal(StepRuntimeAvailabilityStateEnum.Available, descriptors.First(d => d.RuntimeKey == StepRuntimeKeys.HostExecutable).Availability, "host executable available");

                        StepConfigValidationResult ok = await registry.ValidateAsync("ten_1", StepRuntimeKeys.HostExecutable, new HostExecutableRuntimeConfig { AllowListKey = "fixture", Arguments = new List<string> { "--safe=value" } }, ct);
                        Assert2.True(ok.Valid, "valid allowlist key");

                        StepConfigValidationResult unknown = await registry.ValidateAsync("ten_1", StepRuntimeKeys.HostExecutable, new HostExecutableRuntimeConfig { AllowListKey = "missing" }, ct);
                        Assert2.False(unknown.Valid, "unknown key rejected");

                        StepConfigValidationResult pathKey = await registry.ValidateAsync("ten_1", StepRuntimeKeys.HostExecutable, new HostExecutableRuntimeConfig { AllowListKey = "../tool" }, ct);
                        Assert2.False(pathKey.Valid, "path-like key rejected");

                        StepConfigValidationResult disallowedArg = await registry.ValidateAsync("ten_1", StepRuntimeKeys.HostExecutable, new HostExecutableRuntimeConfig { AllowListKey = "fixture", Arguments = new List<string> { "--unsafe=value" } }, ct);
                        Assert2.False(disallowedArg.Valid, "argument policy rejects unsafe argument");
                    }),
                    new TestCaseDescriptor("RuntimeRegistry", "HostExecutableExecutesFixture", "Host.Executable runs an operator allowlisted process through the external protocol", async ct =>
                    {
                        RuntimeSettings runtimes = HostRuntimeSettings(FixtureExecutable(), allowTenantArgs: false);
                        ExternalRuntimeCapacityManager capacity = new ExternalRuntimeCapacityManager(runtimes.ExternalExecution);
                        StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(runtimes: runtimes, externalCapacity: capacity);
                        IStepRuntimeProvider provider = registry.Get(StepRuntimeKeys.HostExecutable)!;
                        HostExecutableRuntimeConfig config = new HostExecutableRuntimeConfig { AllowListKey = "fixture" };
                        StepConfigValidationResult validation = await registry.ValidateAsync("ten_host", StepRuntimeKeys.HostExecutable, config, ct);
                        Assert2.True(validation.Valid, "host config valid");

                        StepRunner runner = await provider.CreateRunnerAsync(
                            new StepExecutionContext { TenantId = "ten_host", ExecutionKey = "fixture" },
                            new StepRecord { TenantId = "ten_host", ExecutionKey = "fixture", Name = "Fixture", RuntimeKey = StepRuntimeKeys.HostExecutable, RuntimeConfig = config },
                            config,
                            ct);
                        Tempo.StepResult result = await runner.Execute("fixture", new Tempo.StepRequest
                        {
                            ProtocolVersion = "1.0",
                            TenantId = "ten_host",
                            DataFlowId = "flow_host",
                            FlowRunId = "run_host",
                            StepRunId = "sru_host",
                            RequestId = "req_host"
                        }, token: ct);

                        Assert2.Equal(StepResultTypeEnum.Success, result.Result, "host executable result");
                    })
#if NET10_0
                    ,
                    new TestCaseDescriptor("RuntimeRegistry", "ExternalExecutionStatusRoutes", "Runtime status routes expose external execution settings and tenant pressure", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.AdminApiKey = "runtime-route-key";
                            settings.RequestHistory.Enabled = false;
                            settings.Runtimes.ExternalExecution.MaxConcurrentProcessesServerWide = 2;
                            settings.Runtimes.ExternalExecution.MaxConcurrentProcessesPerTenant = 1;

                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Runtime Tenant" }, ct);
                            ExternalRuntimeCapacityLease first = await server.ExternalCapacity.AcquireAsync(tenant.Id, "sru_route_1", ct);
                            Task<ExternalRuntimeCapacityLease> queued = server.ExternalCapacity.AcquireAsync(tenant.Id, "sru_route_2", ct);
                            ExternalRuntimeCapacityLease? second = null;
                            try
                            {
                                await WaitUntilAsync(() =>
                                {
                                    ExternalRuntimeCapacitySnapshot snapshot = server.ExternalCapacity.Snapshot();
                                    return snapshot.QueuedByTenant.TryGetValue(tenant.Id, out int queuedCount) && queuedCount == 1;
                                }, ct);

                                using HttpClient client = new HttpClient();
                                client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                                client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, "runtime-route-key");

                                HttpResponseMessage serverResp = await client.GetAsync("/v1.0/runtimes/external-execution", ct);
                                Assert2.Equal(HttpStatusCode.OK, serverResp.StatusCode, "server status code");
                                ExternalExecutionStatusResponse serverStatus = Deserialize<ExternalExecutionStatusResponse>(await serverResp.Content.ReadAsStringAsync(ct));
                                Assert2.Equal(1, serverStatus.Capacity.ActiveByTenant[tenant.Id], "server status active tenant");
                                Assert2.Equal(1, serverStatus.Capacity.QueuedByTenant[tenant.Id], "server status queued tenant");

                                HttpResponseMessage tenantResp = await client.GetAsync("/v1.0/tenants/" + tenant.Id + "/runtimes/external-execution", ct);
                                Assert2.Equal(HttpStatusCode.OK, tenantResp.StatusCode, "tenant status code");
                                ExternalExecutionStatusResponse tenantStatus = Deserialize<ExternalExecutionStatusResponse>(await tenantResp.Content.ReadAsStringAsync(ct));
                                Assert2.Equal(tenant.Id, tenantStatus.TenantId!, "tenant id");
                                Assert2.Equal(1, tenantStatus.TenantActiveProcesses, "tenant active count");
                                Assert2.Equal(1, tenantStatus.TenantQueuedSteps, "tenant queued count");
                            }
                            finally
                            {
                                first.Dispose();
                                try
                                {
                                    second = await queued.WaitAsync(TimeSpan.FromSeconds(5), ct);
                                }
                                catch { }
                                second?.Dispose();
                            }
                        }
                        finally
                        {
                            try { server?.Dispose(); } catch { }
                            await TempTestStore.DisposeAsync(driver);
                        }
                    })
#endif
                });
        }

        private static RuntimeSettings HostRuntimeSettings(string executablePath, bool allowTenantArgs)
        {
            RuntimeSettings runtimes = new RuntimeSettings();
            runtimes.ExternalExecution.MaxConcurrentProcessesServerWide = 2;
            runtimes.ExternalExecution.MaxConcurrentProcessesPerTenant = 1;
            runtimes.HostExecutables.Enabled = true;
            runtimes.HostExecutables.AllowList.Add(new HostExecutableAllowListEntry
            {
                Key = "fixture",
                DisplayName = "Fixture",
                ExecutablePath = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                ArgumentPolicy = new HostExecutableArgumentPolicy
                {
                    AllowAdditionalArguments = allowTenantArgs,
                    MaxArguments = 2,
                    AllowedPrefixes = new List<string> { "--safe=" }
                }
            });
            return runtimes;
        }

        private static string FixtureExecutable()
        {
            return Path.Combine(FixtureDirectory(), "Test.ArtifactFixture.dll");
        }

        private static string FixtureDirectory()
        {
            const string tfm = "net10.0";
            string? dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++, dir = Directory.GetParent(dir)?.FullName)
            {
                string candidate = Path.Combine(dir, "src", "Test.ArtifactFixture", "bin", "Debug", tfm);
                if (File.Exists(Path.Combine(candidate, "Test.ArtifactFixture.dll"))) return candidate;
            }
            dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++, dir = Directory.GetParent(dir)?.FullName)
            {
                string candidate = Path.Combine(dir, "..", "..", "..", "..", "Test.ArtifactFixture", "bin", "Debug", tfm);
                candidate = Path.GetFullPath(candidate);
                if (File.Exists(Path.Combine(candidate, "Test.ArtifactFixture.dll"))) return candidate;
            }
            throw new FileNotFoundException("Test.ArtifactFixture.dll was not built for " + tfm + ".");
        }

#if NET10_0
        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                if (condition()) return;
                await Task.Delay(10, token);
            }

            throw new TimeoutException("Condition was not reached before timeout.");
        }

        private static T Deserialize<T>(string json)
        {
            T? value = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (value == null) throw new InvalidOperationException("Could not deserialize response as " + typeof(T).Name + ": " + json);
            return value;
        }
#endif
    }
}
