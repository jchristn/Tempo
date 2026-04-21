namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Touchstone.Core;

    public static class UsersSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Users",
                displayName: "User data access",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Users", "CreateAndRead", "User created and read by tenant", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "alice@example.com", PasswordSha256 = "abc" }, ct);
                            User? read = await driver.Users.ReadAsync(t.Id, u.Id, ct);
                            Assert2.NotNull(read, "read");
                            Assert2.Equal("alice@example.com", read!.Email, "email");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Users", "EmailLowercased", "Emails are stored lowercase", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "BOB@EXAMPLE.COM", PasswordSha256 = "x" }, ct);
                            User? lookup = await driver.Users.ReadByEmailAsync(t.Id, "bob@example.com", ct);
                            Assert2.NotNull(lookup, "lookup mixed case works");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Users", "TenantScope", "Users are isolated per tenant", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant ta = await driver.Tenants.CreateAsync(new Tenant { Name = "A" }, ct);
                            Tenant tb = await driver.Tenants.CreateAsync(new Tenant { Name = "B" }, ct);
                            await driver.Users.CreateAsync(new User { TenantId = ta.Id, Email = "a@a", PasswordSha256 = "x" }, ct);
                            await driver.Users.CreateAsync(new User { TenantId = tb.Id, Email = "a@b", PasswordSha256 = "x" }, ct);
                            Assert2.Equal(1, (await driver.Users.AllAsync(ta.Id, ct)).Count, "tenant a");
                            Assert2.Equal(1, (await driver.Users.AllAsync(tb.Id, ct)).Count, "tenant b");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Users", "DeleteCascadesCredentials", "Deleting a user removes its credentials", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t", PasswordSha256 = "x" }, ct);
                            await driver.Credentials.CreateAsync(new Credential { TenantId = t.Id, UserId = u.Id, Name = "k" }, ct);
                            await driver.Users.DeleteAsync(t.Id, u.Id, ct);
                            Assert2.Equal(0, (await driver.Credentials.AllAsync(t.Id, ct)).Count, "credentials cascaded");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Users", "UpdateToggles", "Admin and active flags can be toggled", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t", PasswordSha256 = "x" }, ct);
                            u.IsAdmin = true;
                            u.IsTenantAdmin = true;
                            u.Active = false;
                            await driver.Users.UpdateAsync(u, ct);
                            User? read = await driver.Users.ReadAsync(t.Id, u.Id, ct);
                            Assert2.True(read!.IsAdmin, "isAdmin");
                            Assert2.True(read.IsTenantAdmin, "isTenantAdmin");
                            Assert2.False(read.Active, "deactivated");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
