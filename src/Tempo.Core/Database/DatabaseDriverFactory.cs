namespace Tempo.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Mysql;
    using Tempo.Core.Database.Postgresql;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Database.SqlServer;
    using Tempo.Core.Enums;
    using Tempo.Core.Settings;

    /// <summary>
    /// Composition root for the database layer.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        /// <summary>
        /// Create a driver for the configured provider without initializing it.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Database driver.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the provider is not recognized.</exception>
        public static DatabaseDriverBase Create(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            switch (settings.Type)
            {
                case DatabaseTypeEnum.Sqlite: return new SqliteDatabaseDriver(settings);
                case DatabaseTypeEnum.Mysql: return new MysqlDatabaseDriver(settings);
                case DatabaseTypeEnum.Postgresql: return new PostgresqlDatabaseDriver(settings);
                case DatabaseTypeEnum.SqlServer: return new SqlServerDatabaseDriver(settings);
                default: throw new ArgumentException("Unknown database type: " + settings.Type.ToString());
            }
        }

        /// <summary>
        /// Create and initialize a driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Initialized database driver.</returns>
        public static async Task<DatabaseDriverBase> CreateAndInitializeAsync(DatabaseSettings settings, CancellationToken token = default)
        {
            DatabaseDriverBase driver = Create(settings);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            return driver;
        }
    }
}
