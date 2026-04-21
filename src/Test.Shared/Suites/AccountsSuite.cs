namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Touchstone.Core;

    public static class AccountsSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Accounts",
                displayName: "Account data access",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Accounts", "CreateReadDelete", "Create, read back, and delete an account", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Account a = await driver.Accounts.CreateAsync(new Account { Name = "Alpha" }, ct);
                            Account? read = await driver.Accounts.ReadAsync(a.Id, ct);
                            Assert2.NotNull(read, "read after create");
                            Assert2.Equal("Alpha", read!.Name, "name preserved");
                            Assert2.True(await driver.Accounts.ExistsAsync(a.Id, ct), "exists");
                            await driver.Accounts.DeleteAsync(a.Id, ct);
                            Assert2.False(await driver.Accounts.ExistsAsync(a.Id, ct), "deleted");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Accounts", "Update", "Account update persists", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Account a = await driver.Accounts.CreateAsync(new Account { Name = "Alpha" }, ct);
                            a.Name = "Beta";
                            a.AdditionalData = "{\"note\":\"updated\"}";
                            await driver.Accounts.UpdateAsync(a, ct);
                            Account? read = await driver.Accounts.ReadAsync(a.Id, ct);
                            Assert2.Equal("Beta", read!.Name, "name updated");
                            Assert2.Equal("{\"note\":\"updated\"}", read.AdditionalData!, "additional data updated");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Accounts", "EnumeratePaging", "Paging limits and counts are correct", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            for (int i = 0; i < 7; i++) await driver.Accounts.CreateAsync(new Account { Name = "A" + i }, ct);
                            var p1 = await driver.Accounts.EnumerateAsync(new EnumerationFilter { PageNumber = 1, PageSize = 3 }, ct);
                            Assert2.Equal(3, p1.Items.Count, "page 1 size");
                            Assert2.Equal(7, p1.TotalCount, "total count");
                            var p3 = await driver.Accounts.EnumerateAsync(new EnumerationFilter { PageNumber = 3, PageSize = 3 }, ct);
                            Assert2.Equal(1, p3.Items.Count, "page 3 has remainder");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Accounts", "IncludeInactive", "Inactive rows hidden by default", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Account a = await driver.Accounts.CreateAsync(new Account { Name = "Active" }, ct);
                            Account b = await driver.Accounts.CreateAsync(new Account { Name = "Inactive" }, ct);
                            b.Active = false;
                            await driver.Accounts.UpdateAsync(b, ct);
                            var def = await driver.Accounts.EnumerateAsync(new EnumerationFilter(), ct);
                            Assert2.Equal(1, def.Items.Count, "only active by default");
                            var all = await driver.Accounts.EnumerateAsync(new EnumerationFilter { IncludeInactive = true }, ct);
                            Assert2.Equal(2, all.Items.Count, "both with includeInactive");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Accounts", "CascadeDeleteToTenants", "Deleting an account deletes its tenants and admins", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Account a = await driver.Accounts.CreateAsync(new Account { Name = "Acc" }, ct);
                            await driver.Tenants.CreateAsync(new Tenant { Name = "T", AccountId = a.Id }, ct);
                            await driver.Administrators.CreateAsync(new Administrator { Email = "x@y.z", AccountId = a.Id }, ct);
                            await driver.Accounts.DeleteAsync(a.Id, ct);
                            var tenants = await driver.Tenants.AllAsync(ct);
                            Assert2.Equal(0, tenants.Count, "tenant cascaded");
                            var admins = await driver.Administrators.AllAsync(ct);
                            Assert2.Equal(0, admins.Count, "admin cascaded");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Accounts", "SpecialChars", "Names with special chars round-trip", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Account a = await driver.Accounts.CreateAsync(new Account { Name = "O'Neill & Co <AG>" }, ct);
                            Account? read = await driver.Accounts.ReadAsync(a.Id, ct);
                            Assert2.Equal("O'Neill & Co <AG>", read!.Name, "escaped quote roundtrip");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
