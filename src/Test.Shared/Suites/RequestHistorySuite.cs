namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Specialized;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Touchstone.Core;
#if NET10_0
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using SyslogLogging;
    using Tempo.Server;
    using Tempo.Server.Helpers;
#endif

    public static class RequestHistorySuite
    {
        private const string EchoExecutionKey = "test.requesthistory.echo";

        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("RequestHistory", "CreateAndRead", "Create/read roundtrip", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        RequestHistoryEntry e = new RequestHistoryEntry
                        {
                            Method = "GET", Path = "/v1.0/api/health", Url = "/v1.0/api/health",
                            StatusCode = 200, DurationMs = 5.5, CreatedUtc = DateTime.UtcNow,
                            CompletedUtc = DateTime.UtcNow
                        };
                        await driver.RequestHistory.CreateAsync(e, ct);
                        RequestHistoryEntry? read = await driver.RequestHistory.ReadAsync(null, e.Id, ct);
                        Assert2.NotNull(read, "read");
                        Assert2.Equal("GET", read!.Method, "method");
                        Assert2.Equal(200, read.StatusCode, "status");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("RequestHistory", "FilterPaging", "Filter and pagination", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        DateTime t0 = DateTime.UtcNow.AddMinutes(-30);
                        for (int i = 0; i < 15; i++)
                        {
                            int status = i < 10 ? 200 : 500;
                            await driver.RequestHistory.CreateAsync(new RequestHistoryEntry
                            {
                                Method = "GET", Path = "/p", Url = "/p", StatusCode = status,
                                DurationMs = 10, CreatedUtc = t0.AddMinutes(i), CompletedUtc = t0.AddMinutes(i)
                            }, ct);
                        }
                        var page = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter { PageSize = 5 }, ct);
                        Assert2.Equal(5, page.Items.Count, "page size 5");
                        Assert2.Equal(15, page.TotalCount, "total 15");

                        var errors = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter { StatusCode = 500 }, ct);
                        Assert2.Equal(5, errors.TotalCount, "5 error rows");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("RequestHistory", "Summarize", "Summary counts successes and failures in buckets", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        DateTime t0 = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
                        await driver.RequestHistory.CreateAsync(new RequestHistoryEntry { Method = "GET", Path = "/", Url = "/", StatusCode = 200, DurationMs = 10, CreatedUtc = t0 }, ct);
                        await driver.RequestHistory.CreateAsync(new RequestHistoryEntry { Method = "GET", Path = "/", Url = "/", StatusCode = 200, DurationMs = 20, CreatedUtc = t0.AddMinutes(5) }, ct);
                        await driver.RequestHistory.CreateAsync(new RequestHistoryEntry { Method = "GET", Path = "/", Url = "/", StatusCode = 500, DurationMs = 30, CreatedUtc = t0.AddMinutes(20) }, ct);

                        var summary = await driver.RequestHistory.SummarizeAsync(new RequestHistoryFilter
                        {
                            FromUtc = t0,
                            ToUtc = t0.AddHours(1),
                            BucketMinutes = 15
                        }, ct);
                        Assert2.Equal(3, summary.TotalCount, "total 3");
                        Assert2.Equal(2, summary.TotalSuccess, "success 2");
                        Assert2.Equal(1, summary.TotalFailure, "failure 1");
                        Assert2.Equal(4, summary.Buckets.Count, "4 buckets in 1 hour at 15 min");
                        Assert2.Equal(2, summary.Buckets[0].SuccessCount, "bucket 0 success");
                        Assert2.Equal(1, summary.Buckets[1].FailureCount, "bucket 1 failure");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("RequestHistory", "Prune", "Prune deletes rows older than cutoff", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        DateTime old = DateTime.UtcNow.AddDays(-31);
                        DateTime recent = DateTime.UtcNow;
                        await driver.RequestHistory.CreateAsync(new RequestHistoryEntry { Method = "GET", Path = "/", Url = "/", StatusCode = 200, DurationMs = 1, CreatedUtc = old }, ct);
                        await driver.RequestHistory.CreateAsync(new RequestHistoryEntry { Method = "GET", Path = "/", Url = "/", StatusCode = 200, DurationMs = 1, CreatedUtc = recent }, ct);
                        int removed = await driver.RequestHistory.PruneAsync(DateTime.UtcNow.AddDays(-30), ct);
                        Assert2.Equal(1, removed, "one pruned");
                        var remaining = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter(), ct);
                        Assert2.Equal(1, remaining.TotalCount, "one remains");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("RequestHistory", "CaptureRedactsHeaders", "Capture service redacts sensitive headers", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        RequestHistorySettings cfg = new RequestHistorySettings { Enabled = true };
                        RequestHistoryCaptureService cap = new RequestHistoryCaptureService(driver, cfg);
                        RequestHistoryEntry e = new RequestHistoryEntry
                        {
                            Method = "POST", Path = "/v1.0/token", Url = "/v1.0/token",
                            StatusCode = 200, DurationMs = 1, CreatedUtc = DateTime.UtcNow
                        };
                        e.RequestHeaders["Authorization"] = "Bearer secret-jwt";
                        e.RequestHeaders["X-Api-Key"] = "shh";
                        e.RequestHeaders["Content-Type"] = "application/json";
                        cap.Capture(e);

                        // poll for async insert
                        for (int i = 0; i < 20; i++)
                        {
                            var page = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter(), ct);
                            if (page.TotalCount >= 1)
                            {
                                var full = await driver.RequestHistory.ReadAsync(null, page.Items[0].Id, ct);
                                Assert2.Equal("****", full!.RequestHeaders["Authorization"], "auth redacted");
                                Assert2.Equal("****", full.RequestHeaders["X-Api-Key"], "api key redacted");
                                Assert2.Equal("application/json", full.RequestHeaders["Content-Type"], "content-type kept");
                                return;
                            }
                            await Task.Delay(50, ct);
                        }
                        Assert2.True(false, "capture never persisted");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("RequestHistory", "CaptureTruncates", "Bodies beyond the threshold are truncated", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        RequestHistorySettings cfg = new RequestHistorySettings { Enabled = true, MaxRequestBodyBytes = 32 };
                        RequestHistoryCaptureService cap = new RequestHistoryCaptureService(driver, cfg);
                        RequestHistoryEntry e = new RequestHistoryEntry
                        {
                            Method = "POST", Path = "/", Url = "/",
                            StatusCode = 200, DurationMs = 1, CreatedUtc = DateTime.UtcNow,
                            RequestBody = new string('a', 200)
                        };
                        cap.Capture(e);
                        for (int i = 0; i < 20; i++)
                        {
                            var page = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter(), ct);
                            if (page.TotalCount >= 1)
                            {
                                var full = await driver.RequestHistory.ReadAsync(null, page.Items[0].Id, ct);
                                Assert2.True(full!.RequestBodyTruncated, "truncated flag");
                                Assert2.Equal(200L, full.RequestBodyBytes, "original size recorded");
                                Assert2.True((full.RequestBody?.Length ?? 0) <= 64, "body truncated in storage");
                                return;
                            }
                            await Task.Delay(50, ct);
                        }
                        Assert2.True(false, "capture never persisted");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("RequestHistory", "DeleteMany", "Bulk delete honors filters", async ct =>
                {
                    SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                    try
                    {
                        DateTime t0 = DateTime.UtcNow.AddMinutes(-10);
                        for (int i = 0; i < 10; i++)
                        {
                            int status = i < 6 ? 200 : 500;
                            await driver.RequestHistory.CreateAsync(new RequestHistoryEntry
                            {
                                Method = "GET", Path = "/" + i, Url = "/" + i,
                                StatusCode = status, DurationMs = 1, CreatedUtc = t0.AddSeconds(i)
                            }, ct);
                        }
                        int removed = await driver.RequestHistory.DeleteManyAsync(new RequestHistoryFilter { StatusCode = 500 }, ct);
                        Assert2.Equal(4, removed, "4 removed");
                        var remaining = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter(), ct);
                        Assert2.Equal(6, remaining.TotalCount, "6 remain");
                    }
                    finally { await TempTestStore.DisposeAsync(driver); }
                }),
                new TestCaseDescriptor("RequestHistory", "ClientIpResolver", "Client IP resolution prefers Forwarded, then X-Forwarded-For, then the direct socket IP", async ct =>
                {
                    await Task.CompletedTask;

                    NameValueCollection headers = new NameValueCollection();
                    headers["Forwarded"] = "for=\"[2001:db8:cafe::17]:4711\";proto=https";
                    headers["X-Forwarded-For"] = "198.51.100.11:4321, 203.0.113.44";

                    string? resolved = ClientIpResolver.Resolve(headers, "127.0.0.1");
                    Assert2.Equal("2001:db8:cafe::17", resolved, "forwarded preferred");

                    headers["Forwarded"] = "for=unknown";
                    resolved = ClientIpResolver.Resolve(headers, "127.0.0.1");
                    Assert2.Equal("198.51.100.11", resolved, "xff fallback");

                    headers.Remove("X-Forwarded-For");
                    resolved = ClientIpResolver.Resolve(headers, "127.0.0.1");
                    Assert2.Equal("127.0.0.1", resolved, "remote fallback");
                })
            };

#if NET10_0
            cases.Add(new TestCaseDescriptor("RequestHistory", "TriggerResponsesIncludeBody", "Trigger request-history rows persist the response body", async ct =>
            {
                SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                TempoServer? server = null;
                try
                {
                    int port = FreePort();
                    Settings settings = new Settings();
                    settings.Rest.Port = port;
                    settings.Rest.Hostname = "127.0.0.1";
                    settings.Auth.AdminApiKey = "request-history-key";
                    settings.RequestHistory.Enabled = true;
                    settings.Engine.PollIntervalMs = 25;

                    LoggingModule logging = new LoggingModule();
                    logging.Settings.EnableConsole = false;

                    Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Request History Tenant" }, ct);
                    Tempo.StepManager stepManager = new Tempo.StepManager();
                    stepManager.Add(new RequestHistoryEchoStep(tenant.Id));

                    server = new TempoServer(settings, logging, driver, stepManager);
                    await server.StartAsync();

                    await driver.Steps.CreateAsync(new StepRecord
                    {
                        TenantId = tenant.Id,
                        ExecutionKey = EchoExecutionKey,
                        Name = "Request history echo step",
                        RuntimeKey = StepRuntimeKeys.BuiltinClass,
                        RuntimeConfig = new BuiltinClassRuntimeConfig { Identifier = EchoExecutionKey }
                    }, ct);

                    DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                    {
                        TenantId = tenant.Id,
                        Name = "Request history echo flow",
                        StartStepId = EchoExecutionKey,
                        Transitions = new Dictionary<string, Tempo.StepTransition>
                        {
                            [EchoExecutionKey] = new Tempo.StepTransition()
                        }
                    }, ct);

                    TriggerRecord trigger = await driver.Triggers.CreateAsync(new TriggerRecord
                    {
                        TenantId = tenant.Id,
                        Name = "Request history echo trigger",
                        DataFlowId = flow.Id,
                        Configuration = "{\"allowedMethods\":[\"POST\"]}"
                    }, ct);

                    using HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri("http://127.0.0.1:" + port);

                    using StringContent request = new StringContent("{\"value\":\"hello world\"}", Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync("/v1.0/triggers/http/" + trigger.Id, request, ct);
                    string responseBody = await response.Content.ReadAsStringAsync(ct);

                    Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "trigger invocation status");
                    Assert2.Equal("{\"value\":\"hello world\"}", responseBody, "trigger response body");

                    for (int i = 0; i < 40; i++)
                    {
                        var page = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter { PageSize = 10 }, ct);
                        RequestHistoryEntry? summary = page.Items.FirstOrDefault(item => string.Equals(item.Url, "/v1.0/triggers/http/" + trigger.Id, StringComparison.Ordinal));
                        if (summary != null)
                        {
                            RequestHistoryEntry? full = await driver.RequestHistory.ReadAsync(null, summary.Id, ct);
                            Assert2.NotNull(full, "captured request history row");
                            Assert2.Equal(responseBody, full!.ResponseBody, "response body captured");
                            Assert2.True(full.ResponseHeaders.ContainsKey(Tempo.Core.Constants.HeaderRunId), "run id header captured");
                            return;
                        }

                        await Task.Delay(50, ct);
                    }

                    Assert2.True(false, "request history did not persist the trigger response");
                }
                finally
                {
                    try { server?.Dispose(); } catch { }
                    await TempTestStore.DisposeAsync(driver);
                }
            }));
            cases.Add(new TestCaseDescriptor("RequestHistory", "TriggerCapturesForwardedSourceIp", "Trigger runs and request-history rows persist proxy-aware source IPs", async ct =>
            {
                SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                TempoServer? server = null;
                try
                {
                    int port = FreePort();
                    Settings settings = new Settings();
                    settings.Rest.Port = port;
                    settings.Rest.Hostname = "127.0.0.1";
                    settings.Auth.AdminApiKey = "request-history-key";
                    settings.RequestHistory.Enabled = true;
                    settings.Engine.PollIntervalMs = 25;

                    LoggingModule logging = new LoggingModule();
                    logging.Settings.EnableConsole = false;

                    Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Forwarded Header Tenant" }, ct);
                    Tempo.StepManager stepManager = new Tempo.StepManager();
                    stepManager.Add(new RequestHistoryEchoStep(tenant.Id));

                    server = new TempoServer(settings, logging, driver, stepManager);
                    await server.StartAsync();

                    await driver.Steps.CreateAsync(new StepRecord
                    {
                        TenantId = tenant.Id,
                        ExecutionKey = EchoExecutionKey,
                        Name = "Forwarded header echo step",
                        RuntimeKey = StepRuntimeKeys.BuiltinClass,
                        RuntimeConfig = new BuiltinClassRuntimeConfig { Identifier = EchoExecutionKey }
                    }, ct);

                    DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord
                    {
                        TenantId = tenant.Id,
                        Name = "Forwarded header flow",
                        StartStepId = EchoExecutionKey,
                        Transitions = new Dictionary<string, Tempo.StepTransition>
                        {
                            [EchoExecutionKey] = new Tempo.StepTransition()
                        }
                    }, ct);

                    TriggerRecord trigger = await driver.Triggers.CreateAsync(new TriggerRecord
                    {
                        TenantId = tenant.Id,
                        Name = "Forwarded header trigger",
                        DataFlowId = flow.Id,
                        Configuration = "{\"allowedMethods\":[\"POST\"]}"
                    }, ct);

                    using HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri("http://127.0.0.1:" + port);

                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/v1.0/triggers/http/" + trigger.Id);
                    request.Headers.TryAddWithoutValidation("Forwarded", "for=\"198.51.100.77:5123\";proto=https");
                    request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.44, 127.0.0.1");
                    request.Content = new StringContent("{\"value\":\"forwarded\"}", Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.SendAsync(request, ct);
                    Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "trigger invocation status");

                    bool hasRunId = response.Headers.TryGetValues(Tempo.Core.Constants.HeaderRunId, out IEnumerable<string>? runIdValues);
                    IEnumerable<string> confirmedRunIdValues = runIdValues ?? Array.Empty<string>();
                    Assert2.True(hasRunId && confirmedRunIdValues.Any(), "run id header present");
                    string runId = confirmedRunIdValues.First();

                    for (int i = 0; i < 40; i++)
                    {
                        FlowRun? run = await driver.FlowRuns.ReadAsync(tenant.Id, runId, ct);
                        var page = await driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter { PageSize = 10 }, ct);
                        RequestHistoryEntry? summary = page.Items.FirstOrDefault(item => string.Equals(item.Url, "/v1.0/triggers/http/" + trigger.Id, StringComparison.Ordinal));
                        RequestHistoryEntry? full = summary != null ? await driver.RequestHistory.ReadAsync(null, summary.Id, ct) : null;

                        if (run != null && full != null)
                        {
                            Assert2.Equal("198.51.100.77", run.SourceIp, "flow run source ip");
                            Assert2.Equal("198.51.100.77", full.SourceIp, "request history source ip");
                            return;
                        }

                        await Task.Delay(50, ct);
                    }

                    Assert2.True(false, "proxy-aware source IP was not persisted");
                }
                finally
                {
                    try { server?.Dispose(); } catch { }
                    await TempTestStore.DisposeAsync(driver);
                }
            }));
#endif

            return new TestSuiteDescriptor(
                suiteId: "RequestHistory",
                displayName: "Request history capture, summary, prune",
                cases: cases);
        }

#if NET10_0
        private sealed class RequestHistoryEchoStep : Tempo.Step
        {
            public RequestHistoryEchoStep(string tenantId)
            {
                Identifier = EchoExecutionKey;
                TenantId = tenantId;
                Name = "Request history echo";
            }

            public override Task<Tempo.StepResult> Run(Tempo.StepRequest req)
            {
                return Task.FromResult(new Tempo.StepResult
                {
                    ProtocolVersion = req.ProtocolVersion,
                    TenantId = req.TenantId,
                    DataFlowId = req.DataFlowId,
                    FlowRunId = req.FlowRunId,
                    StepRunId = req.StepRunId,
                    RequestId = req.RequestId,
                    Result = Tempo.Enums.StepResultTypeEnum.Success,
                    Data = req.Data,
                    Metadata = req.Metadata
                });
            }
        }

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
