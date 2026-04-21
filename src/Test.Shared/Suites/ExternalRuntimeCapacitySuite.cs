namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;
    using Touchstone.Core;

    public static class ExternalRuntimeCapacitySuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "ExternalRuntimeCapacity",
                displayName: "External runtime capacity management",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ExternalRuntimeCapacity", "ServerWideCapQueues", "Server-wide external process cap queues excess requests", async ct =>
                    {
                        ExternalRuntimeCapacityManager manager = NewManager(server: 1, tenant: 2);
                        using ExternalRuntimeCapacityLease first = await manager.AcquireAsync("ten_a", "sru_1", ct);
                        Task<ExternalRuntimeCapacityLease> queued = manager.AcquireAsync("ten_b", "sru_2", ct);

                        await WaitUntilAsync(() => manager.Snapshot().QueuedServerWide == 1, ct);
                        Assert2.False(queued.IsCompleted, "second request is queued");
                        first.Dispose();

                        using ExternalRuntimeCapacityLease second = await queued.WaitAsync(TimeSpan.FromSeconds(5), ct);
                        Assert2.Equal("ten_b", second.TenantId, "queued tenant acquired");
                        Assert2.True(second.CapacityWaitMs >= 0, "wait duration captured");

                        ExternalRuntimeCapacitySnapshot snapshot = manager.Snapshot();
                        Assert2.Equal(1, snapshot.ActiveServerWide, "one active");
                        Assert2.True(snapshot.TotalCapacityWaitMs >= 0, "total wait metric");
                    }),
                    new TestCaseDescriptor("ExternalRuntimeCapacity", "PerTenantIsolation", "Per-tenant cap queues one tenant without blocking another tenant when server capacity remains", async ct =>
                    {
                        ExternalRuntimeCapacityManager manager = NewManager(server: 2, tenant: 1);
                        using ExternalRuntimeCapacityLease tenantAFirst = await manager.AcquireAsync("ten_a", "sru_a1", ct);
                        Task<ExternalRuntimeCapacityLease> tenantASecond = manager.AcquireAsync("ten_a", "sru_a2", ct);

                        await WaitUntilAsync(() =>
                        {
                            ExternalRuntimeCapacitySnapshot s = manager.Snapshot();
                            return s.QueuedByTenant.TryGetValue("ten_a", out int queued) && queued == 1;
                        }, ct);

                        using ExternalRuntimeCapacityLease tenantB = await manager.AcquireAsync("ten_b", "sru_b1", ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
                        Assert2.Equal("ten_b", tenantB.TenantId, "other tenant acquired while tenant A queued");
                        Assert2.False(tenantASecond.IsCompleted, "tenant A second request still queued");

                        tenantAFirst.Dispose();
                        using ExternalRuntimeCapacityLease tenantASecondLease = await tenantASecond.WaitAsync(TimeSpan.FromSeconds(5), ct);
                        Assert2.Equal("ten_a", tenantASecondLease.TenantId, "tenant A second acquired after release");
                    }),
                    new TestCaseDescriptor("ExternalRuntimeCapacity", "CancellationWhileQueued", "Queued external process capacity requests clean up counters on cancellation", async ct =>
                    {
                        ExternalRuntimeCapacityManager manager = NewManager(server: 1, tenant: 1);
                        using ExternalRuntimeCapacityLease first = await manager.AcquireAsync("ten_a", "sru_1", ct);
                        using CancellationTokenSource queuedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        Task<ExternalRuntimeCapacityLease> queued = manager.AcquireAsync("ten_b", "sru_2", queuedCts.Token);

                        await WaitUntilAsync(() => manager.Snapshot().QueuedServerWide == 1, ct);
                        queuedCts.Cancel();
                        bool cancelled = false;
                        try { await queued; }
                        catch (OperationCanceledException) { cancelled = true; }
                        Assert2.True(cancelled, "queued request cancelled");

                        await WaitUntilAsync(() => manager.Snapshot().QueuedServerWide == 0, ct);
                        ExternalRuntimeCapacitySnapshot snapshot = manager.Snapshot();
                        Assert2.Equal(1, snapshot.ActiveServerWide, "original lease remains active");
                        first.Dispose();
                        Assert2.Equal(0, manager.Snapshot().ActiveServerWide, "release clears active count");
                    }),
                    new TestCaseDescriptor("ExternalRuntimeCapacity", "RuntimeAndKillMetrics", "Capacity manager tracks runtime duration and process kill count", async ct =>
                    {
                        ExternalRuntimeCapacityManager manager = NewManager(server: 1, tenant: 1);
                        using (ExternalRuntimeCapacityLease lease = await manager.AcquireAsync("ten_a", "sru_1", ct))
                        {
                            await Task.Delay(10, ct);
                            manager.RecordProcessKilled();
                        }

                        ExternalRuntimeCapacitySnapshot snapshot = manager.Snapshot();
                        Assert2.True(snapshot.TotalProcessRuntimeMs >= 0, "runtime metric");
                        Assert2.Equal(1, snapshot.ProcessKillCount, "kill count");
                    }),
                    new TestCaseDescriptor("ExternalRuntimeCapacity", "StatusResponseHighlightsTenantPressure", "External execution status response includes settings and selected tenant pressure", async ct =>
                    {
                        Settings settings = new Settings();
                        settings.Runtimes.ExternalExecution.MaxConcurrentProcessesServerWide = 2;
                        settings.Runtimes.ExternalExecution.MaxConcurrentProcessesPerTenant = 1;
                        settings.Runtimes.ExternalExecution.DefaultMaxRuntimeMs = 4321;
                        settings.Runtimes.ExternalExecution.EnvironmentAllowList.Clear();
                        settings.Runtimes.ExternalExecution.EnvironmentAllowList.Add("PATH");

                        ExternalRuntimeCapacityManager manager = new ExternalRuntimeCapacityManager(settings.Runtimes.ExternalExecution);
                        ExternalRuntimeCapacityLease first = await manager.AcquireAsync("ten_a", "sru_1", ct);
                        Task<ExternalRuntimeCapacityLease> queued = manager.AcquireAsync("ten_a", "sru_2", ct);
                        ExternalRuntimeCapacityLease? second = null;
                        try
                        {
                            await WaitUntilAsync(() =>
                            {
                                ExternalRuntimeCapacitySnapshot s = manager.Snapshot();
                                return s.QueuedByTenant.TryGetValue("ten_a", out int queuedCount) && queuedCount == 1;
                            }, ct);

                            ExternalExecutionStatusResponse response = ExternalExecutionStatusResponse.From(settings, manager.Snapshot(), "ten_a");
                            Assert2.Equal(2, response.MaxConcurrentProcessesServerWide, "server cap");
                            Assert2.Equal(1, response.MaxConcurrentProcessesPerTenant, "tenant cap");
                            Assert2.Equal(4321, response.DefaultMaxRuntimeMs, "runtime limit");
                            Assert2.Equal("PATH", response.EnvironmentAllowList[0], "environment allowlist");
                            Assert2.Equal("ten_a", response.TenantId!, "tenant id");
                            Assert2.Equal(1, response.TenantActiveProcesses, "tenant active");
                            Assert2.Equal(1, response.TenantQueuedSteps, "tenant queued");
                            Assert2.Equal(1, response.Capacity.ActiveByTenant["ten_a"], "capacity active by tenant");
                            Assert2.Equal(1, response.Capacity.QueuedByTenant["ten_a"], "capacity queued by tenant");
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
                    })
                });
        }

        private static ExternalRuntimeCapacityManager NewManager(int server, int tenant)
        {
            return new ExternalRuntimeCapacityManager(new ExternalExecutionSettings
            {
                MaxConcurrentProcessesServerWide = server,
                MaxConcurrentProcessesPerTenant = tenant
            });
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
    }
}
