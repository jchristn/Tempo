namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Settings;

    /// <summary>
    /// Helper that produces an isolated SQLite driver per test case.
    /// Each call writes to a unique file under the OS temp directory so cases are independent.
    /// </summary>
    public static class TempTestStore
    {
        /// <summary>Create and initialize a SQLite driver backed by a unique temp file.</summary>
        public static async Task<SqliteDatabaseDriver> CreateAsync(CancellationToken token = default)
        {
            string path = Path.Combine(Path.GetTempPath(), "tempo-test-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseSettings settings = new DatabaseSettings { Type = Tempo.Core.Enums.DatabaseTypeEnum.Sqlite, Filename = path };
            SqliteDatabaseDriver driver = new SqliteDatabaseDriver(settings);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            return driver;
        }

        /// <summary>Dispose a driver and delete its backing file.</summary>
        public static async Task DisposeAsync(DatabaseDriverBase driver)
        {
            if (driver == null) return;
            try { await driver.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
            driver.Dispose();
            if (driver is SqliteDatabaseDriver sqlite)
            {
                string connectionString = sqlite.ConnectionString;
                int start = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
                if (start < 0) return;
                int end = connectionString.IndexOf(';', start);
                string path = end < 0
                    ? connectionString.Substring(start + "Data Source=".Length)
                    : connectionString.Substring(start + "Data Source=".Length, end - (start + "Data Source=".Length));
                try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
            }
        }
    }
}
