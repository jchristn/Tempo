namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Models;
    using Tempo.Core.Protocol;
    using Tempo.Enums;
    using Tempo.Protocol;
    using Tempo.Runners;
    using Touchstone.Core;

    public static class ProtocolSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Protocol",
                displayName: "Step protocol v1 envelope and conformance",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Protocol", "EnvelopeDefaultsAndJsonShape", "Step request/result envelopes default to v1 and serialize with v1 JSON names", async _ =>
                    {
                        await Task.CompletedTask;
                        StepRequest request = new StepRequest
                        {
                            TenantId = "ten_protocol",
                            DataFlowId = "flow_protocol",
                            FlowRunId = "fru_protocol",
                            StepRunId = "sru_protocol",
                            RequestId = "req_protocol",
                            PreviousResult = StepResultTypeEnum.Success,
                            Data = new { value = 1 },
                            Metadata = new { stdout = "", stderr = "" }
                        };

                        string requestJson = JsonSerializer.Serialize(request);
                        Assert2.Equal(ProtocolVersions.Current, request.ProtocolVersion, "request protocol default");
                        Assert2.True(requestJson.Contains("\"protocolVersion\":\"1.0\"", StringComparison.Ordinal), "request protocol json");
                        Assert2.True(requestJson.Contains("\"dataFlowId\":\"flow_protocol\"", StringComparison.Ordinal), "request dataFlowId json");
                        Assert2.True(requestJson.Contains("\"previousResult\":\"Success\"", StringComparison.Ordinal), "request previousResult string json");

                        StepResult result = new StepResult
                        {
                            TenantId = "ten_protocol",
                            DataFlowId = "flow_protocol",
                            FlowRunId = "fru_protocol",
                            StepRunId = "sru_protocol",
                            RequestId = "req_protocol",
                            Result = StepResultTypeEnum.Exception,
                            Exception = new InvalidOperationException("boom"),
                            Metadata = new { stderr = "boom" }
                        };

                        string resultJson = JsonSerializer.Serialize(result);
                        Assert2.Equal(ProtocolVersions.Current, result.ProtocolVersion, "result protocol default");
                        Assert2.True(resultJson.Contains("\"result\":\"Exception\"", StringComparison.Ordinal), "result enum string json");
                        Assert2.True(resultJson.Contains("\"exception\":\"boom\"", StringComparison.Ordinal), "exception message json");
                    }),
                    new TestCaseDescriptor("Protocol", "Negotiator", "Protocol negotiation accepts v1/default and rejects unsupported manifests", async _ =>
                    {
                        await Task.CompletedTask;
                        ProtocolNegotiator negotiator = new ProtocolNegotiator();
                        Assert2.Equal(ProtocolVersions.Current, negotiator.EnsureSupported((string?)null), "default negotiation");
                        Assert2.Equal(ProtocolVersions.Current, negotiator.EnsureSupported("1.0"), "v1 negotiation");
                        Assert2.True(negotiator.Negotiate(new ArtifactManifest { ProtocolVersion = "1.0" }).Supported, "manifest v1");
                        Assert2.False(negotiator.Negotiate(new ArtifactManifest { ProtocolVersion = "2.0" }).Supported, "manifest v2 rejected");
                        Assert2.False(negotiator.Negotiate(new[] { "1.0", "2.0" }).Supported, "ambiguous declarations rejected");
                        Assert2.Throws<NotSupportedException>(() => negotiator.EnsureSupported("2.0"), "unsupported version throws");
                    }),
                    new TestCaseDescriptor("Protocol", "RunnerNormalizesCorrelation", "StepRunner preserves host correlation fields even when a step returns different IDs", async ct =>
                    {
                        StepRequest request = new StepRequest
                        {
                            TenantId = "ten_protocol",
                            DataFlowId = "flow_protocol",
                            FlowRunId = "fru_protocol",
                            StepRunId = "sru_protocol",
                            RequestId = "req_protocol",
                            Data = "input"
                        };

                        StepResult result = await new UncorrelatedRunner().Execute("step_protocol", request, token: ct);
                        Assert2.Equal(ProtocolVersions.Current, result.ProtocolVersion, "protocol normalized");
                        Assert2.Equal("ten_protocol", result.TenantId!, "tenant normalized");
                        Assert2.Equal("flow_protocol", result.DataFlowId, "data flow normalized");
                        Assert2.Equal("fru_protocol", result.FlowRunId!, "flow run normalized");
                        Assert2.Equal("sru_protocol", result.StepRunId!, "step run normalized");
                        Assert2.Equal("req_protocol", result.RequestId, "request normalized");
                        Assert2.Equal("ok", result.Data!.ToString()!, "data preserved");
                    }),
                    new TestCaseDescriptor("Protocol", "StepRunsPersistProtocolVersion", "Persistent step run history records negotiated protocol version", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant t = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = t.Id, Name = "f", StartStepId = "s" }, ct);
                            FlowRun run = await driver.FlowRuns.CreateAsync(new FlowRun { TenantId = t.Id, DataFlowId = flow.Id }, ct);

                            await driver.FlowRuns.CreateStepRunAsync(new StepRun
                            {
                                TenantId = t.Id,
                                FlowRunId = run.Id,
                                DataFlowId = flow.Id,
                                StepId = "s",
                                Sequence = 0,
                                Result = StepResultTypeEnum.Success,
                                ProtocolVersion = ProtocolVersions.Current
                            }, ct);

                            List<StepRun> steps = await driver.FlowRuns.EnumerateStepRunsAsync(t.Id, run.Id, ct);
                            Assert2.Equal(1, steps.Count, "one step run");
                            Assert2.Equal(ProtocolVersions.Current, steps[0].ProtocolVersion, "protocol persisted");

                            DataTable dt = await driver.ExecuteQueryAsync("SELECT protocol_version FROM step_runs WHERE id = '" + steps[0].Id + "';", false, ct);
                            Assert2.Equal("1.0", dt.Rows[0]["protocol_version"].ToString()!, "protocol column");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Protocol", "ConformanceGoldenFiles", "Protocol conformance runner accepts v1 golden request/result envelopes", async _ =>
                    {
                        await Task.CompletedTask;
                        ProtocolConformanceRunner runner = new ProtocolConformanceRunner();
                        AssertValid(runner.ValidateStepRequestJson(Golden("v1-request.json")), "v1 request");
                        AssertValid(runner.ValidateStepResultJson(Golden("v1-success.json")), "v1 success");
                        AssertValid(runner.ValidateStepResultJson(Golden("v1-error.json")), "v1 error");
                        AssertValid(runner.ValidateStepResultJson(Golden("v1-exception.json")), "v1 exception");
                        AssertValid(runner.ValidateStepResultJson(Golden("v1-timeout.json")), "v1 timeout");
                    }),
                    new TestCaseDescriptor("Protocol", "ConformanceRejectsInvalidEnvelopes", "Protocol conformance runner rejects invalid JSON, missing fields, and unsupported versions", async _ =>
                    {
                        await Task.CompletedTask;
                        ProtocolConformanceRunner runner = new ProtocolConformanceRunner();
                        AssertInvalid(runner.ValidateStepResultJson("{"), "invalid json");
                        AssertInvalid(runner.ValidateStepResultJson("{\"protocolVersion\":\"1.0\",\"requestId\":\"req\"}"), "missing fields");
                        AssertInvalid(runner.ValidateStepResultJson("{\"protocolVersion\":\"2.0\",\"dataFlowId\":\"flow\",\"requestId\":\"req\",\"result\":\"Success\"}"), "unsupported version");
                        AssertInvalid(runner.ValidateStepResultJson("{\"protocolVersion\":\"1.0\",\"dataFlowId\":\"flow\",\"requestId\":\"req\",\"result\":\"Exception\"}"), "exception missing message");
                    })
                });
        }

        private static void AssertValid(ProtocolConformanceResult result, string message)
        {
            Assert2.True(result.Valid, message + ": " + string.Join("; ", result.Errors));
        }

        private static void AssertInvalid(ProtocolConformanceResult result, string message)
        {
            Assert2.False(result.Valid, message);
            Assert2.True(result.Errors.Count > 0, message + " errors");
        }

        private static string Golden(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "ProtocolGoldenFiles", fileName);
            return File.ReadAllText(path);
        }

        private sealed class UncorrelatedRunner : StepRunner
        {
            protected override Task<StepResult> ExecuteInternal(StepRequest req, CancellationToken token)
            {
                return Task.FromResult(new StepResult
                {
                    TenantId = "wrong_tenant",
                    DataFlowId = "wrong_flow",
                    FlowRunId = "wrong_run",
                    StepRunId = "wrong_step",
                    RequestId = "wrong_request",
                    Result = StepResultTypeEnum.Success,
                    Data = "ok"
                });
            }
        }
    }
}
