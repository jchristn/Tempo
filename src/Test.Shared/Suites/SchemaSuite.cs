namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Database.Sqlite.Queries;
    using Touchstone.Core;

    /// <summary>Schema initialization and idempotence.</summary>
    public static class SchemaSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Schema",
                displayName: "SQLite schema migrations",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Schema", "InitializeCreatesTables", "Initialize creates every expected table", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            string[] expected = {
                                "schema_migrations", "accounts", "administrators", "tenants", "users",
                                "credentials", "roles", "user_role_maps", "permissions", "role_permission_maps",
                                "data_flows", "steps", "triggers", "flow_runs", "step_runs", "request_history",
                                "artifacts", "artifact_versions"
                            };
                            foreach (string name in expected)
                            {
                                DataTable dt = await driver.ExecuteQueryAsync(
                                    "SELECT name FROM sqlite_master WHERE type='table' AND name='" + name + "';", false, ct);
                                Assert2.Equal(1, dt.Rows.Count, "table " + name + " exists");
                            }
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Schema", "Idempotent", "Re-initializing an existing database is a no-op", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            await driver.InitializeAsync(ct);
                            await driver.InitializeAsync(ct);
                            DataTable dt = await driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM schema_migrations;", false, ct);
                            Assert2.Equal(SchemaQueries.All().Count, System.Convert.ToInt32(dt.Rows[0][0]), "all migrations applied once");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Schema", "EmptyDatabase", "Enumerations return no rows on a fresh database", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            var tenants = await driver.Tenants.AllAsync(ct);
                            Assert2.Equal(0, tenants.Count, "no tenants");
                            var accounts = await driver.Accounts.AllAsync(ct);
                            Assert2.Equal(0, accounts.Count, "no accounts");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
