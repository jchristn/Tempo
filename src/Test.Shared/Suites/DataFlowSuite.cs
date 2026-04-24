namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Services;
    using Touchstone.Core;

    public static class DataFlowSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "DataFlows",
                displayName: "Flow, step, trigger persistence",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DataFlows", "TransitionsRoundtrip", "Flow transitions JSON round-trips intact", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            Dictionary<string, Tempo.StepTransition> trans = new Dictionary<string, Tempo.StepTransition>
                            {
                                ["start"] = new Tempo.StepTransition { OnSuccess = "middle" },
                                ["middle"] = new Tempo.StepTransition { OnSuccess = "end", OnFailure = "start", MaxTransitions = 3 },
                                ["end"] = new Tempo.StepTransition()
                            };
                            DataFlowRecord rec = await driver.DataFlows.CreateAsync(new DataFlowRecord
                            {
                                TenantId = t.Id,
                                Name = "flow",
                                StartStepId = "start",
                                InvocationAuthMode = DataFlowInvocationAuthModeEnum.ApiAuthenticated,
                                Transitions = trans
                            }, ct);
                            DataFlowRecord? read = await driver.DataFlows.ReadAsync(t.Id, rec.Id, ct);
                            Assert2.NotNull(read, "read");
                            Assert2.Equal(DataFlowInvocationAuthModeEnum.ApiAuthenticated, read!.InvocationAuthMode, "invocation auth mode");
                            Assert2.Equal(3, read!.Transitions.Count, "three transitions");
                            Assert2.Equal("middle", read.Transitions["start"].OnSuccess!, "onSuccess");
                            Assert2.Equal(3, read.Transitions["middle"].MaxTransitions, "max transitions");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("DataFlows", "ReadGlobal", "ReadGlobalAsync resolves across tenants (for worker)", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            DataFlowRecord rec = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = t.Id, Name = "f", StartStepId = "s" }, ct);
                            DataFlowRecord? g = await driver.DataFlows.ReadGlobalAsync(rec.Id, ct);
                            Assert2.NotNull(g, "global read");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("DataFlows", "Hydrate", "Hydrate converts record to in-memory DataFlow", async _ =>
                    {
                        await Task.CompletedTask;
                        DataFlowRecord rec = new DataFlowRecord
                        {
                            TenantId = "ten_a",
                            Name = "flow",
                            StartStepId = "start",
                            Transitions = new Dictionary<string, Tempo.StepTransition>
                            {
                                ["start"] = new Tempo.StepTransition()
                            }
                        };
                        Tempo.DataFlow flow = FlowDispatchService.Hydrate(rec);
                        Assert2.Equal(rec.Id, flow.Identifier, "id");
                        Assert2.Equal("start", flow.StartStepId, "start step");
                        Assert2.Equal(1, flow.Steps.Count, "one step");
                    }),
                    new TestCaseDescriptor("DataFlows", "StepsUpsert", "Step upsert creates then updates", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            StepRecord s = new StepRecord { TenantId = t.Id, Name = "first", StepType = PersistedStepTypeEnum.Rest, Rest = new Tempo.RestStepConfiguration { Method = "GET", Url = "https://example.com" } };
                            StepRecord created = await driver.Steps.UpsertAsync(s, ct);
                            created.Name = "renamed";
                            await driver.Steps.UpsertAsync(created, ct);
                            StepRecord? read = await driver.Steps.ReadAsync(t.Id, created.Id, ct);
                            Assert2.Equal("renamed", read!.Name, "renamed");
                            Assert2.NotNull(read.Rest, "rest retained");
                            Assert2.Equal("https://example.com", read.Rest!.Url, "url");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("DataFlows", "TriggersReadGlobal", "TriggerMethods.ReadGlobalAsync finds triggers across tenants", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            TriggerRecord rec = await driver.Triggers.CreateAsync(new TriggerRecord { TenantId = t.Id, Name = "http" }, ct);
                            TriggerRecord? read = await driver.Triggers.ReadGlobalAsync(rec.Id, ct);
                            Assert2.NotNull(read, "global read");
                            Assert2.Equal(t.Id, read!.TenantId, "tenant preserved");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("DataFlows", "DispatchEnqueuesRun", "FlowDispatchService enqueues a flow run", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = t.Id, Name = "f", StartStepId = "s" }, ct);
                            FlowDispatchService svc = new FlowDispatchService(driver);
                            FlowRun run = await svc.EnqueueAsync(t.Id, flow.Id, "{\"v\":1}", "usr_x", null, "203.0.113.10", ct);
                            Assert2.Equal(FlowRunStateEnum.Queued, run.State, "queued");
                            Assert2.Equal(FlowRunDispatchStateEnum.Pending, run.DispatchState, "dispatch pending");
                            Assert2.Equal("{\"v\":1}", run.InputData!, "input persisted");
                            Assert2.Equal("203.0.113.10", run.SourceIp!, "source ip persisted");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("DataFlows", "DispatchRejectsInactiveFlow", "Dispatch rejects inactive flow", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = t.Id, Name = "f", StartStepId = "s", Active = false }, ct);
                            FlowDispatchService svc = new FlowDispatchService(driver);
                            bool threw = false;
                            try { await svc.EnqueueAsync(t.Id, flow.Id, null, null, null, null, ct); }
                            catch (System.InvalidOperationException) { threw = true; }
                            Assert2.True(threw, "inactive flow rejected");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("DataFlows", "StepRunsInOrder", "Step runs enumerate by sequence", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = t.Id, Name = "f", StartStepId = "s" }, ct);
                            FlowRun run = await driver.FlowRuns.CreateAsync(new FlowRun { TenantId = t.Id, DataFlowId = flow.Id }, ct);
                            for (int i = 0; i < 5; i++)
                            {
                                await driver.FlowRuns.CreateStepRunAsync(new StepRun
                                {
                                    TenantId = t.Id, FlowRunId = run.Id, DataFlowId = flow.Id, StepId = "s" + i,
                                    Sequence = i
                                }, ct);
                            }
                            var steps = await driver.FlowRuns.EnumerateStepRunsAsync(t.Id, run.Id, ct);
                            Assert2.Equal(5, steps.Count, "5 step runs");
                            for (int i = 0; i < 5; i++) Assert2.Equal(i, steps[i].Sequence, "sequence " + i);
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("DataFlows", "StepRunCapacityWaitStateRoundTrip", "Step run capacity wait state persists across create and update", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = t.Id, Name = "f", StartStepId = "external" }, ct);
                            FlowRun run = await driver.FlowRuns.CreateAsync(new FlowRun { TenantId = t.Id, DataFlowId = flow.Id }, ct);
                            DateTime queuedUtc = DateTime.UtcNow.AddMilliseconds(-250);
                            StepRun created = await driver.FlowRuns.CreateStepRunAsync(new StepRun
                            {
                                TenantId = t.Id,
                                FlowRunId = run.Id,
                                DataFlowId = flow.Id,
                                StepId = "external",
                                Sequence = 0,
                                ExecutionState = StepRunExecutionStateEnum.AwaitingCapacity,
                                CapacityQueuedUtc = queuedUtc,
                                InputData = "{\"in\":true}",
                                StartedUtc = queuedUtc
                            }, ct);

                            StepRun waiting = (await driver.FlowRuns.EnumerateStepRunsAsync(t.Id, run.Id, ct))[0];
                            Assert2.Equal(StepRunExecutionStateEnum.AwaitingCapacity, waiting.ExecutionState, "awaiting capacity state");
                            Assert2.NotNull(waiting.CapacityQueuedUtc, "queued utc persisted");
                            Assert2.IsNull(waiting.CapacityAcquiredUtc, "capacity not acquired yet");
                            Assert2.IsNull(waiting.CapacityWaitMs, "wait ms not known yet");

                            created.ExecutionState = StepRunExecutionStateEnum.Running;
                            created.CapacityAcquiredUtc = queuedUtc.AddMilliseconds(123);
                            created.CapacityWaitMs = 123;
                            created.OutputData = "{\"status\":\"running\"}";
                            await driver.FlowRuns.UpdateStepRunAsync(created, ct);

                            StepRun running = (await driver.FlowRuns.EnumerateStepRunsAsync(t.Id, run.Id, ct))[0];
                            Assert2.Equal(StepRunExecutionStateEnum.Running, running.ExecutionState, "running state");
                            Assert2.NotNull(running.CapacityAcquiredUtc, "acquired utc persisted");
                            Assert2.Equal(123L, running.CapacityWaitMs, "wait ms persisted");
                            Assert2.Equal("{\"status\":\"running\"}", running.OutputData!, "output updated");

                            running.ExecutionState = StepRunExecutionStateEnum.Complete;
                            running.CompletedUtc = DateTime.UtcNow;
                            running.OutputData = "{\"ok\":true}";
                            await driver.FlowRuns.UpdateStepRunAsync(running, ct);

                            StepRun complete = (await driver.FlowRuns.EnumerateStepRunsAsync(t.Id, run.Id, ct))[0];
                            Assert2.Equal(StepRunExecutionStateEnum.Complete, complete.ExecutionState, "complete state");
                            Assert2.NotNull(complete.CompletedUtc, "completed utc persisted");
                            Assert2.Equal(123L, complete.CapacityWaitMs, "wait ms retained");
                            Assert2.Equal("{\"ok\":true}", complete.OutputData!, "final output updated");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
