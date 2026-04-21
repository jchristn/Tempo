namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Touchstone.Core;
#if NET10_0
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using SyslogLogging;
    using Tempo.Core.Settings;
    using Tempo.Server;
#endif

    public static class DeletionProtectionSuite
    {
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("DeletionProtection", "StepReferencesBlockDelete", "Data flow step links are detected before deleting a step", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                        StepRecord step = await driver.Steps.CreateAsync(new StepRecord { TenantId = tenant.Id, ExecutionKey = "linked", Name = "Linked step" }, ct);
                        await driver.DataFlows.CreateAsync(new DataFlowRecord
                        {
                            TenantId = tenant.Id,
                            Name = "flow",
                            StartStepId = "linked",
                            Transitions = new Dictionary<string, Tempo.StepTransition> { ["linked"] = new Tempo.StepTransition() }
                        }, ct);

                        DeletionDependencyResult result = await new DeletionDependencyService(driver).FindStepReferencesAsync(tenant.Id, step.ExecutionKey, ct);
                        Assert2.True(result.IsBlocked, "linked step delete blocked");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("DeletionProtection", "ArtifactReferencesBlockDelete", "Artifact and artifact version links are detected before deletion", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                        ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "tool" }, ct);
                        ArtifactVersionRecord version = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                        {
                            TenantId = tenant.Id,
                            ArtifactId = artifact.Id,
                            Version = "1.0.0",
                            Sha256 = new string('a', 64),
                            ByteLength = 1
                        }, ct);
                        await driver.Steps.CreateAsync(new StepRecord
                        {
                            TenantId = tenant.Id,
                            ExecutionKey = "artifact-step",
                            Name = "Artifact step",
                            RuntimeConfig = new ArtifactProcessRuntimeConfig { ArtifactId = artifact.Id, ArtifactVersion = version.Version }
                        }, ct);

                        DeletionDependencyService guard = new DeletionDependencyService(driver);
                        Assert2.True((await guard.FindArtifactReferencesAsync(tenant.Id, artifact.Id, ct)).IsBlocked, "artifact delete blocked");
                        Assert2.True((await guard.FindArtifactVersionReferencesAsync(tenant.Id, version, ct)).IsBlocked, "artifact version delete blocked");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("DeletionProtection", "FlowDeleteCascadesRuns", "Deleting a data flow cascades flow runs and step runs", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                        DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = tenant.Id, Name = "f", StartStepId = "s" }, ct);
                        FlowRun run = await driver.FlowRuns.CreateAsync(new FlowRun { TenantId = tenant.Id, DataFlowId = flow.Id }, ct);
                        await driver.FlowRuns.CreateStepRunAsync(new StepRun { TenantId = tenant.Id, FlowRunId = run.Id, DataFlowId = flow.Id, StepId = "s" }, ct);

                        await driver.DataFlows.DeleteAsync(tenant.Id, flow.Id, ct);

                        Assert2.IsNull(await driver.FlowRuns.ReadAsync(tenant.Id, run.Id, ct), "flow run cascaded");
                        Assert2.Equal(0, (await driver.FlowRuns.EnumerateStepRunsAsync(tenant.Id, run.Id, ct)).Count, "step runs cascaded");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("DeletionProtection", "AccountDeleteCascadesTenantChildren", "Deleting an account cascades tenant-owned child rows", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        Account account = await driver.Accounts.CreateAsync(new Account { Name = "A" }, ct);
                        Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "T", AccountId = account.Id }, ct);
                        User user = await driver.Users.CreateAsync(new User { TenantId = tenant.Id, Email = "u@example.com" }, ct);
                        await driver.Credentials.CreateAsync(new Credential { TenantId = tenant.Id, UserId = user.Id, Name = "key" }, ct);
                        await driver.Steps.CreateAsync(new StepRecord { TenantId = tenant.Id, ExecutionKey = "s", Name = "s" }, ct);
                        await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = tenant.Id, Name = "f", StartStepId = "s" }, ct);
                        ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "tool" }, ct);
                        await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord { TenantId = tenant.Id, ArtifactId = artifact.Id, Version = "1", Sha256 = new string('b', 64), ByteLength = 1 }, ct);

                        await driver.Accounts.DeleteAsync(account.Id, ct);

                        Assert2.Equal(0, (await driver.Tenants.AllAsync(ct)).Count, "tenant deleted");
                        Assert2.Equal(0, (await driver.Users.AllAsync(tenant.Id, ct)).Count, "users deleted");
                        Assert2.Equal(0, (await driver.Credentials.AllAsync(tenant.Id, ct)).Count, "credentials deleted");
                        Assert2.Equal(0, (await driver.Steps.AllAsync(tenant.Id, ct)).Count, "steps deleted");
                        Assert2.Equal(0, (await driver.DataFlows.AllAsync(tenant.Id, ct)).Count, "flows deleted");
                        Assert2.Equal(0, (await driver.Artifacts.AllAsync(tenant.Id, ct)).Count, "artifacts deleted");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                })
            };

#if NET10_0
            cases.Add(new TestCaseDescriptor("DeletionProtection", "StepDeleteRouteRejectsLinkedStep", "The step delete route returns 409 when a flow references the step", async ct =>
            {
                SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                TempoServer? server = null;
                try
                {
                    int port = FreePort();
                    Settings settings = new Settings();
                    settings.Rest.Port = port;
                    settings.Rest.Hostname = "127.0.0.1";
                    settings.Auth.AdminApiKey = "delete-guard-key";
                    settings.RequestHistory.Enabled = false;

                    LoggingModule logging = new LoggingModule();
                    logging.Settings.EnableConsole = false;
                    server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                    await server.StartAsync();

                    Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                    StepRecord step = await driver.Steps.CreateAsync(new StepRecord { TenantId = tenant.Id, ExecutionKey = "linked", Name = "Linked step" }, ct);
                    await driver.DataFlows.CreateAsync(new DataFlowRecord
                    {
                        TenantId = tenant.Id,
                        Name = "flow",
                        StartStepId = "linked",
                        Transitions = new Dictionary<string, Tempo.StepTransition> { ["linked"] = new Tempo.StepTransition() }
                    }, ct);

                    using HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                    client.DefaultRequestHeaders.Add(Tempo.Core.Constants.HeaderApiKey, "delete-guard-key");

                    HttpResponseMessage response = await client.DeleteAsync("/v1.0/tenants/" + tenant.Id + "/steps/" + step.Id, ct);
                    Assert2.Equal(HttpStatusCode.Conflict, response.StatusCode, "delete rejected as in use");
                    Assert2.NotNull(await driver.Steps.ReadAsync(tenant.Id, step.Id, ct), "step retained");
                }
                finally
                {
                    try { server?.Dispose(); } catch { }
                    await TempTestStore.DisposeAsync(driver);
                }
            }));
#endif

            return new TestSuiteDescriptor(
                suiteId: "DeletionProtection",
                displayName: "Deletion protection and cascades",
                cases: cases);
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
#endif
    }
}
