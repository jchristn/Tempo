namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Enums;
    using Touchstone.Core;

    public static class StepResolverSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "StepResolver",
                displayName: "Runtime registry step resolution",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("StepResolver", "InMemoryClassStep", "Class steps execute through the in-memory resolver and runtime registry", async ct =>
                    {
                        const string tenantId = "ten_runtime";
                        StepManager manager = new StepManager();
                        manager.Add(new EchoStep("class_step", tenantId, "class"));
                        RegistryDataFlowRunner runner = NewRunner(new InMemoryStepExecutionResolver(manager), manager);

                        StepResult result = await runner.Run(NewFlow(tenantId, "class_step"), NewRequest("flow_runtime", "run_class", "input"), token: ct);
                        Assert2.Equal(StepResultTypeEnum.Success, result.Result, "success");
                        Assert2.Equal("class:input", result.Data!.ToString()!, "data");
                    }),
                    new TestCaseDescriptor("StepResolver", "InMemoryMethodStep", "Method steps execute through the in-memory resolver and runtime registry", async ct =>
                    {
                        const string tenantId = "ten_runtime";
                        StepManager manager = new StepManager();
                        MethodInfo method = typeof(StepResolverSuite).GetMethod(nameof(MethodStep), BindingFlags.Static | BindingFlags.NonPublic)!;
                        manager.RegisterMethod("method_step", method, tenantId);
                        RegistryDataFlowRunner runner = NewRunner(new InMemoryStepExecutionResolver(manager), manager);

                        StepResult result = await runner.Run(NewFlow(tenantId, "method_step"), NewRequest("flow_runtime", "run_method", "input"), token: ct);
                        Assert2.Equal(StepResultTypeEnum.Success, result.Result, "success");
                        Assert2.Equal("method:input", result.Data!.ToString()!, "data");
                    }),
                    new TestCaseDescriptor("StepResolver", "DatabaseCodeStep", "Persisted code steps resolve from the database and execute through the registry", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            StepManager manager = new StepManager();
                            manager.Add(new EchoStep("db_step", tenant.Id, "db"));
                            BuiltinUnknownRuntimeConfig config = new BuiltinUnknownRuntimeConfig { Identifier = "db_step" };
                            await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                ExecutionKey = "db_step",
                                Name = "DB Step",
                                RuntimeKey = StepRuntimeKeys.BuiltinUnknown,
                                RuntimeConfig = config
                            }, ct);

                            RegistryDataFlowRunner runner = NewRunner(new DatabaseStepExecutionResolver(driver), manager);
                            StepResult result = await runner.Run(NewFlow(tenant.Id, "db_step"), NewRequest("flow_runtime", "run_db", "input"), token: ct);
                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "success");
                            Assert2.Equal("db:input", result.Data!.ToString()!, "data");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepResolver", "PersistedRestStep", "Persisted REST steps resolve from the database and execute through the registry", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            string url = StartOneShotHttpServer("persisted-rest", ct);
                            await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                ExecutionKey = "rest_step",
                                Name = "REST Step",
                                RuntimeKey = StepRuntimeKeys.ExternalRest,
                                RuntimeConfig = new ExternalRestRuntimeConfig { Method = "GET", Url = url, TimeoutMs = 5000 }
                            }, ct);

                            StepManager manager = new StepManager();
                            RegistryDataFlowRunner runner = NewRunner(new DatabaseStepExecutionResolver(driver), manager);
                            StepResult result = await runner.Run(NewFlow(tenant.Id, "rest_step"), NewRequest("flow_runtime", "run_rest", null), token: ct);
                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "success");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepResolver", "SchemaValidationRejectsInvalidInput", "Core input schema validation runs before provider invocation", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            StepManager manager = new StepManager();
                            manager.Add(new EchoStep("schema_input", tenant.Id, "schema"));
                            await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                ExecutionKey = "schema_input",
                                Name = "Schema Input",
                                RuntimeKey = StepRuntimeKeys.BuiltinUnknown,
                                RuntimeConfig = new BuiltinUnknownRuntimeConfig { Identifier = "schema_input" },
                                ValidateInput = true,
                                InputSchema = "{\"type\":\"object\",\"required\":[\"orderId\"],\"properties\":{\"orderId\":{\"type\":\"integer\"}}}"
                            }, ct);

                            RegistryDataFlowRunner runner = NewRunner(new DatabaseStepExecutionResolver(driver), manager);
                            bool rejected = false;
                            try
                            {
                                await runner.Run(NewFlow(tenant.Id, "schema_input"), NewRequest("flow_runtime", "run_schema_input", new Dictionary<string, object> { ["orderId"] = "bad" }), token: ct);
                            }
                            catch (InvalidOperationException ex) when (ex.Message.Contains("input contract failed", StringComparison.OrdinalIgnoreCase))
                            {
                                rejected = true;
                            }
                            Assert2.True(rejected, "invalid input rejected");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepResolver", "SchemaValidationRejectsInvalidOutput", "Core output schema validation runs after provider invocation", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            StepManager manager = new StepManager();
                            manager.Add(new EchoStep("schema_output", tenant.Id, "schema"));
                            await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                ExecutionKey = "schema_output",
                                Name = "Schema Output",
                                RuntimeKey = StepRuntimeKeys.BuiltinUnknown,
                                RuntimeConfig = new BuiltinUnknownRuntimeConfig { Identifier = "schema_output" },
                                ValidateOutput = true,
                                OutputSchema = "{\"type\":\"object\"}"
                            }, ct);

                            RegistryDataFlowRunner runner = NewRunner(new DatabaseStepExecutionResolver(driver), manager);
                            bool rejected = false;
                            try
                            {
                                await runner.Run(NewFlow(tenant.Id, "schema_output"), NewRequest("flow_runtime", "run_schema_output", "input"), token: ct);
                            }
                            catch (InvalidOperationException ex) when (ex.Message.Contains("output contract failed", StringComparison.OrdinalIgnoreCase))
                            {
                                rejected = true;
                            }
                            Assert2.True(rejected, "invalid output rejected");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepResolver", "LegacyInlineRestStep", "Legacy inline REST transitions execute through the registry read path", async ct =>
                    {
                        const string tenantId = "ten_runtime";
                        string url = StartOneShotHttpServer("legacy-rest", ct);
                        Tempo.DataFlow flow = NewFlow(tenantId, "inline_rest");
                        flow.Steps["inline_rest"] = new Tempo.StepTransition
                        {
                            StepType = Tempo.Enums.StepTypeEnum.Rest,
                            Rest = new Tempo.RestStepConfiguration { Method = "GET", Url = url, TimeoutMs = 5000 }
                        };

                        StepManager manager = new StepManager();
                        RegistryDataFlowRunner runner = NewRunner(new InMemoryStepExecutionResolver(manager), manager);
                        StepResult result = await runner.Run(flow, NewRequest("flow_runtime", "run_inline_rest", null), token: ct);
                        Assert2.Equal(StepResultTypeEnum.Success, result.Result, "success");
                    })
                });
        }

        private static RegistryDataFlowRunner NewRunner(IStepExecutionResolver resolver, StepManager manager)
        {
            return new RegistryDataFlowRunner(resolver, StepRuntimeRegistry.CreateDefault(manager));
        }

        private static Tempo.DataFlow NewFlow(string tenantId, string stepKey)
        {
            return new Tempo.DataFlow
            {
                Identifier = "flow_runtime",
                TenantId = tenantId,
                Name = "Runtime Flow",
                StartStepId = stepKey,
                Steps = new Dictionary<string, Tempo.StepTransition>
                {
                    [stepKey] = new Tempo.StepTransition()
                }
            };
        }

        private static StepRequest NewRequest(string flowId, string requestId, object? data)
        {
            return new StepRequest { DataFlowId = flowId, RequestId = requestId, Data = data! };
        }

        private static Task<StepResult> MethodStep(StepRequest request)
        {
            return Task.FromResult(new StepResult
            {
                DataFlowId = request.DataFlowId,
                RequestId = request.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = "method:" + request.Data
            });
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

        private sealed class EchoStep : Step
        {
            private readonly string _Prefix;

            public EchoStep(string identifier, string tenantId, string prefix)
            {
                Identifier = identifier;
                TenantId = tenantId;
                Name = identifier;
                _Prefix = prefix;
            }

            public override Task<StepResult> Run(StepRequest req)
            {
                return Task.FromResult(new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Success,
                    Data = _Prefix + ":" + req.Data
                });
            }
        }
    }
}
