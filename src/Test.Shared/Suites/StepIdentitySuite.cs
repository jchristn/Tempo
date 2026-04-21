namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Touchstone.Core;

    public static class StepIdentitySuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "StepIdentity",
                displayName: "Step execution identity",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("StepIdentity", "ExecutionKeyValidation", "Execution keys are non-empty, bounded, and free of control characters", async _ =>
                    {
                        await Task.CompletedTask;
                        Assert2.Throws<ArgumentNullException>(() => new StepRecord { ExecutionKey = "" }, "empty key rejected");
                        Assert2.Throws<ArgumentException>(() => new StepRecord { ExecutionKey = "bad\nkey" }, "control character rejected");
                        Assert2.Throws<ArgumentOutOfRangeException>(() => new StepRecord { ExecutionKey = new string('a', StepRecord.ExecutionKeyMaxLength + 1) }, "overlong key rejected");

                        StepRecord record = new StepRecord { ExecutionKey = "  validate-order  " };
                        Assert2.Equal("validate-order", record.ExecutionKey, "execution key trimmed");
                    }),
                    new TestCaseDescriptor("StepIdentity", "ExecutionKeyDefaultsAndRoundTrips", "Legacy clients without execution keys default to Name and round-trip", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            StepRecord created = await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                Name = "Validate Order",
                                StepType = PersistedStepTypeEnum.Rest,
                                Rest = new Tempo.RestStepConfiguration { Method = "GET", Url = "https://example.com" }
                            }, ct);

                            Assert2.Equal("Validate Order", created.ExecutionKey, "legacy default execution key");
                            StepRecord? read = await driver.Steps.ReadAsync(tenant.Id, created.Id, ct);
                            Assert2.NotNull(read, "read by id");
                            Assert2.Equal("Validate Order", read!.ExecutionKey, "execution key persisted");

                            StepRecord? byKey = await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "Validate Order", ct);
                            Assert2.NotNull(byKey, "read by execution key");
                            Assert2.Equal(created.Id, byKey!.Id, "same record");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepIdentity", "DisplayNameCanChange", "Display name changes do not break execution-key lookup", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            StepRecord created = await driver.Steps.CreateAsync(new StepRecord
                            {
                                TenantId = tenant.Id,
                                ExecutionKey = "validate_order",
                                Name = "Validate Order",
                                StepType = PersistedStepTypeEnum.Code
                            }, ct);

                            await driver.Steps.UpdateAsync(new StepRecord
                            {
                                Id = created.Id,
                                TenantId = tenant.Id,
                                Name = "Validate Purchase Order",
                                StepType = created.StepType
                            }, ct);

                            StepRecord? byKey = await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "validate_order", ct);
                            Assert2.NotNull(byKey, "read by unchanged execution key");
                            Assert2.Equal(created.Id, byKey!.Id, "same record");
                            Assert2.Equal("Validate Purchase Order", byKey.Name, "display name changed");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepIdentity", "DuplicateExecutionKeyRejected", "Duplicate execution keys are rejected within a tenant", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            await driver.Steps.CreateAsync(new StepRecord { TenantId = tenant.Id, ExecutionKey = "shared_key", Name = "First" }, ct);

                            bool threw = false;
                            try
                            {
                                await driver.Steps.CreateAsync(new StepRecord { TenantId = tenant.Id, ExecutionKey = "shared_key", Name = "Second" }, ct);
                            }
                            catch (Exception) { threw = true; }

                            Assert2.True(threw, "duplicate key rejected");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepIdentity", "SameExecutionKeyAcrossTenants", "The same execution key can be reused across tenants", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenantA = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant A" }, ct);
                            Tenant tenantB = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant B" }, ct);

                            StepRecord stepA = await driver.Steps.CreateAsync(new StepRecord { TenantId = tenantA.Id, ExecutionKey = "shared_key", Name = "Shared" }, ct);
                            StepRecord stepB = await driver.Steps.CreateAsync(new StepRecord { TenantId = tenantB.Id, ExecutionKey = "shared_key", Name = "Shared" }, ct);

                            StepRecord? readA = await driver.Steps.ReadByExecutionKeyAsync(tenantA.Id, "shared_key", ct);
                            StepRecord? readB = await driver.Steps.ReadByExecutionKeyAsync(tenantB.Id, "shared_key", ct);
                            Assert2.Equal(stepA.Id, readA!.Id, "tenant A lookup");
                            Assert2.Equal(stepB.Id, readB!.Id, "tenant B lookup");
                            Assert2.NotEqual(stepA.Id, stepB.Id, "distinct records");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("StepIdentity", "UpsertUsesExecutionKey", "Upsert updates by tenant and execution key", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            StepRecord created = await driver.Steps.UpsertAsync(new StepRecord { TenantId = tenant.Id, ExecutionKey = "upsert_key", Name = "First" }, ct);
                            StepRecord updated = await driver.Steps.UpsertAsync(new StepRecord { TenantId = tenant.Id, ExecutionKey = "upsert_key", Name = "Second" }, ct);

                            Assert2.Equal(created.Id, updated.Id, "same id");
                            StepRecord? read = await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "upsert_key", ct);
                            Assert2.Equal("Second", read!.Name, "updated by execution key");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
