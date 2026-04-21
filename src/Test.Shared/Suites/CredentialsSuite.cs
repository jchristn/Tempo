namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Touchstone.Core;

    public static class CredentialsSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Credentials",
                displayName: "Credential data access",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Credentials", "CreateGeneratesKeys", "Created credential has auto-generated keys", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "a@b", PasswordSha256 = "x" }, ct);
                            Credential c = await driver.Credentials.CreateAsync(new Credential { TenantId = t.Id, UserId = u.Id, Name = "k" }, ct);
                            Assert2.StartsWith("pub_", c.AccessKey, "access key prefix");
                            Assert2.StartsWith("key_", c.SecretKey, "secret key prefix");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Credentials", "ReadByAccessKey", "Credential retrievable by access key", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "a@b", PasswordSha256 = "x" }, ct);
                            string access = IdGenerator.GenerateAccessKey();
                            string secret = IdGenerator.GenerateSecretKey();
                            await driver.Credentials.CreateAsync(new Credential { TenantId = t.Id, UserId = u.Id, Name = "k", AccessKey = access, SecretKey = secret }, ct);
                            Credential? read = await driver.Credentials.ReadByAccessKeyAsync(access, ct);
                            Assert2.NotNull(read, "read");
                            Assert2.Equal(secret, read!.SecretKey, "secret match");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Credentials", "UniqueAccessKey", "Access keys must be unique", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "a@b", PasswordSha256 = "x" }, ct);
                            string key = IdGenerator.GenerateAccessKey();
                            await driver.Credentials.CreateAsync(new Credential { TenantId = t.Id, UserId = u.Id, Name = "k1", AccessKey = key, SecretKey = IdGenerator.GenerateSecretKey() }, ct);
                            bool threw = false;
                            try
                            {
                                await driver.Credentials.CreateAsync(new Credential { TenantId = t.Id, UserId = u.Id, Name = "k2", AccessKey = key, SecretKey = IdGenerator.GenerateSecretKey() }, ct);
                            }
                            catch (System.Exception) { threw = true; }
                            Assert2.True(threw, "duplicate access key rejected");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
