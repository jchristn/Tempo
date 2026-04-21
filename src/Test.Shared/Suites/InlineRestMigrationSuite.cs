namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;
    using Tempo.Enums;
#if NET10_0
    using SyslogLogging;
    using Tempo.Server;
#endif
    using Touchstone.Core;

    public static class InlineRestMigrationSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "InlineRestMigration",
                displayName: "Inline REST compatibility migration",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("InlineRestMigration", "CreatesPersistedRestStep", "Inline REST transitions migrate to persisted External.Rest steps", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(NewInlineRestFlow(tenant.Id, "call_rest", "https://example.com"), ct);

                            StepCompatibilityMigrationResult result = await new StepCompatibilityMigrator(driver).MigrateFlowAsync(flow, ct);
                            DataFlowRecord readFlow = (await driver.DataFlows.ReadAsync(tenant.Id, flow.Id, ct))!;
                            StepRecord step = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "call_rest", ct))!;

                            Assert2.Equal(1, result.InlineRestStepsFound, "inline count");
                            Assert2.Equal(1, result.StepsCreated, "created count");
                            Assert2.Equal(1, result.FlowsUpdated, "flow updated");
                            Assert2.Equal(StepRuntimeKeys.ExternalRest, step.RuntimeKey, "runtime key");
                            Assert2.Equal(typeof(ExternalRestRuntimeConfig), step.RuntimeConfig!.GetType(), "config type");
                            Assert2.True(readFlow.Transitions["call_rest"].StepType == null, "step type cleared");
                            Assert2.True(readFlow.Transitions["call_rest"].Rest == null, "rest cleared");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("InlineRestMigration", "Idempotent", "Inline REST migration can run multiple times without duplicate steps", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(NewInlineRestFlow(tenant.Id, "call_rest", "https://example.com"), ct);
                            StepCompatibilityMigrator migrator = new StepCompatibilityMigrator(driver);

                            StepCompatibilityMigrationResult first = await migrator.MigrateFlowAsync(flow, ct);
                            DataFlowRecord migrated = (await driver.DataFlows.ReadAsync(tenant.Id, flow.Id, ct))!;
                            StepCompatibilityMigrationResult second = await migrator.MigrateFlowAsync(migrated, ct);
                            List<StepRecord> steps = await driver.Steps.AllAsync(tenant.Id, ct);

                            Assert2.Equal(1, first.StepsCreated, "first creates");
                            Assert2.Equal(0, second.StepsCreated, "second creates none");
                            Assert2.Equal(0, second.InlineRestStepsFound, "second sees no inline rest");
                            Assert2.Equal(1, steps.Count(s => s.RuntimeKey == StepRuntimeKeys.ExternalRest), "one rest step");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("InlineRestMigration", "ConflictRewritesReferences", "Inline REST migration uses a deterministic non-conflicting key and rewrites flow references", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                ExecutionKey = "call_rest",
                                Name = "Existing code step",
                                RuntimeKey = StepRuntimeKeys.BuiltinUnknown,
                                RuntimeConfig = new BuiltinUnknownRuntimeConfig { Identifier = "call_rest" }
                            }, ct);

                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                            {
                                TenantId = tenant.Id,
                                Name = "Flow",
                                StartStepId = "start",
                                Transitions = new Dictionary<string, StepTransition>
                                {
                                    ["start"] = new StepTransition { OnSuccess = "call_rest" },
                                    ["call_rest"] = InlineRestTransition("https://example.com"),
                                    ["done"] = new StepTransition()
                                }
                            }, ct);

                            await new StepCompatibilityMigrator(driver).MigrateFlowAsync(flow, ct);
                            DataFlowRecord readFlow = (await driver.DataFlows.ReadAsync(tenant.Id, flow.Id, ct))!;
                            string migratedKey = readFlow.Transitions.Keys.Single(k => k.StartsWith("call_rest_rest_", System.StringComparison.Ordinal));

                            Assert2.Equal(migratedKey, readFlow.Transitions["start"].OnSuccess!, "reference rewritten");
                            Assert2.False(readFlow.Transitions.ContainsKey("call_rest"), "inline key removed from flow");
                            StepRecord migratedStep = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, migratedKey, ct))!;
                            Assert2.Equal(StepRuntimeKeys.ExternalRest, migratedStep.RuntimeKey, "migrated step runtime");
                            StepRecord existing = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "call_rest", ct))!;
                            Assert2.Equal(StepRuntimeKeys.BuiltinUnknown, existing.RuntimeKey, "existing step preserved");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("InlineRestMigration", "MigratedRestExecutes", "Migrated REST flow executes through persisted step resolution", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            string url = StartOneShotHttpServer("migrated-rest", ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(NewInlineRestFlow(tenant.Id, "call_rest", url), ct);
                            await new StepCompatibilityMigrator(driver).MigrateFlowAsync(flow, ct);
                            DataFlowRecord migrated = (await driver.DataFlows.ReadAsync(tenant.Id, flow.Id, ct))!;

                            RegistryDataFlowRunner runner = new RegistryDataFlowRunner(new DatabaseStepExecutionResolver(driver), StepRuntimeRegistry.CreateDefault(new StepManager()));
                            StepResult result = await runner.Run(Tempo.Core.Services.FlowDispatchService.Hydrate(migrated), NewRequest(migrated.Id, "run_migrated_rest"), token: ct);

                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "success");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
#if NET10_0
                    ,
                    new TestCaseDescriptor("InlineRestMigration", "AdminRouteMigratesFlow", "Admin migration route migrates a scoped inline REST flow", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.AdminApiKey = "migration-route-key";
                            settings.RequestHistory.Enabled = false;
                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(NewInlineRestFlow(tenant.Id, "call_rest", "https://example.com"), ct);

                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, "migration-route-key");

                            string body = "{\"tenantId\":\"" + tenant.Id + "\",\"flowId\":\"" + flow.Id + "\"}";
                            HttpResponseMessage response = await client.PostAsync("/v1.0/migrations/inline-rest", new StringContent(body, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "migration status");
                            StepCompatibilityMigrationResult result = Deserialize<StepCompatibilityMigrationResult>(await response.Content.ReadAsStringAsync(ct));
                            Assert2.Equal(1, result.FlowsScanned, "flows scanned");
                            Assert2.Equal(1, result.InlineRestStepsFound, "inline rest count");
                            Assert2.Equal(1, result.StepsCreated, "steps created");

                            DataFlowRecord readFlow = (await driver.DataFlows.ReadAsync(tenant.Id, flow.Id, ct))!;
                            StepRecord step = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "call_rest", ct))!;
                            Assert2.True(readFlow.Transitions["call_rest"].Rest == null, "inline rest cleared");
                            Assert2.Equal(StepRuntimeKeys.ExternalRest, step.RuntimeKey, "step runtime");
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

        private static DataFlowRecord NewInlineRestFlow(string tenantId, string executionKey, string url)
        {
            return new DataFlowRecord
            {
                TenantId = tenantId,
                Name = "Flow",
                StartStepId = executionKey,
                Transitions = new Dictionary<string, StepTransition>
                {
                    [executionKey] = InlineRestTransition(url)
                }
            };
        }

        private static StepTransition InlineRestTransition(string url)
        {
            return new StepTransition
            {
                Name = "Call REST",
                StepType = StepTypeEnum.Rest,
                Rest = new RestStepConfiguration { Method = "GET", Url = url, TimeoutMs = 5000 }
            };
        }

        private static StepRequest NewRequest(string flowId, string requestId)
        {
            return new StepRequest { DataFlowId = flowId, RequestId = requestId, Data = null! };
        }

        private static string StartOneShotHttpServer(string responseBody, CancellationToken token)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Task.Run(async () =>
            {
                try
                {
                    using TcpClient client = await listener.AcceptTcpClientAsync().WaitAsync(token).ConfigureAwait(false);
                    await using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];
                    try { await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false); } catch { /* request may already be complete */ }

                    byte[] body = Encoding.UTF8.GetBytes(responseBody);
                    byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header.AsMemory(0, header.Length), token).ConfigureAwait(false);
                    await stream.WriteAsync(body.AsMemory(0, body.Length), token).ConfigureAwait(false);
                }
                finally
                {
                    listener.Stop();
                }
            }, CancellationToken.None);
            return "http://127.0.0.1:" + port + "/";
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

        private static T Deserialize<T>(string json)
        {
            T? value = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (value == null) throw new InvalidOperationException("Could not deserialize " + typeof(T).Name + ": " + json);
            return value;
        }
#endif
    }
}
