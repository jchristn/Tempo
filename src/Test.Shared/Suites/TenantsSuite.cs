namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Touchstone.Core;

    public static class TenantsSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Tenants",
                displayName: "Tenant data access and cascade",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Tenants", "CRUD", "Create, read, update, delete a tenant", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "Acme", Region = "us-west-1" }, ct);
                            Assert2.NotNull(await driver.Tenants.ReadAsync(t.Id, ct), "read");
                            t.Name = "Acme Corp";
                            await driver.Tenants.UpdateAsync(t, ct);
                            Tenant? updated = await driver.Tenants.ReadAsync(t.Id, ct);
                            Assert2.Equal("Acme Corp", updated!.Name, "updated");
                            await driver.Tenants.DeleteAsync(t.Id, ct);
                            Assert2.IsNull(await driver.Tenants.ReadAsync(t.Id, ct), "deleted");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Tenants", "CascadeDelete", "Deleting a tenant deletes all child entities", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t" }, ct);
                            await driver.Credentials.CreateAsync(new Credential { TenantId = t.Id, UserId = u.Id, Name = "k" }, ct);
                            await driver.Roles.CreateAsync(new Role { TenantId = t.Id, Name = "r" }, ct);
                            await driver.Permissions.CreateAsync(new Permission { TenantId = t.Id, Name = "p" }, ct);
                            await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = t.Id, Name = "f", StartStepId = "s" }, ct);

                            await driver.Tenants.DeleteAsync(t.Id, ct);

                            Assert2.Equal(0, (await driver.Users.AllAsync(t.Id, ct)).Count, "users gone");
                            Assert2.Equal(0, (await driver.Credentials.AllAsync(t.Id, ct)).Count, "credentials gone");
                            Assert2.Equal(0, (await driver.Roles.AllAsync(t.Id, ct)).Count, "roles gone");
                            Assert2.Equal(0, (await driver.Permissions.AllAsync(t.Id, ct)).Count, "permissions gone");
                            Assert2.Equal(0, (await driver.DataFlows.AllAsync(t.Id, ct)).Count, "flows gone");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Tenants", "Paging", "Tenant paging reports correct total count", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            for (int i = 0; i < 12; i++) await driver.Tenants.CreateAsync(new Tenant { Name = "T" + i }, ct);
                            var r = await driver.Tenants.EnumerateAsync(new EnumerationFilter { PageNumber = 2, PageSize = 5 }, ct);
                            Assert2.Equal(5, r.Items.Count, "page 2 size");
                            Assert2.Equal(12, r.TotalCount, "total");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
