namespace Tempo.Core.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tempo.Core.Database.Common.Implementations;
    using Tempo.Core.Database.Sqlite.Queries;
    using Tempo.Core.Enums;
    using Tempo.Core.Settings;

    /// <summary>
    /// SQLite implementation of <see cref="DatabaseDriverBase"/>. Serializes writes via <see cref="SemaphoreSlim"/>.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        /// <inheritdoc/>
        public override DatabaseTypeEnum DatabaseType => DatabaseTypeEnum.Sqlite;

        /// <summary>Connection string computed from settings.</summary>
        public string ConnectionString { get; }

        private readonly DatabaseSettings _Settings;
        private readonly SemaphoreSlim _WriteLock = new SemaphoreSlim(1, 1);
        private readonly SqlDialect _Dialect = SqlDialect.Ansi;
        private bool _Disposed = false;

        /// <summary>Instantiate.</summary>
        /// <param name="settings">Database settings.</param>
        public SqliteDatabaseDriver(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Settings = settings;

            string file = string.IsNullOrWhiteSpace(settings.Filename) ? "./tempo.db" : settings.Filename!;
            string? directory = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            ConnectionString = "Data Source=" + file + ";Cache=Shared;";

            Accounts = new AccountMethods(this, _Dialect);
            Administrators = new AdministratorMethods(this, _Dialect);
            Tenants = new TenantMethods(this, _Dialect);
            Users = new UserMethods(this, _Dialect);
            Credentials = new CredentialMethods(this, _Dialect);
            Roles = new RoleMethods(this, _Dialect);
            UserRoleMaps = new UserRoleMapMethods(this, _Dialect);
            Permissions = new PermissionMethods(this, _Dialect);
            RolePermissionMaps = new RolePermissionMapMethods(this, _Dialect);
            DataFlows = new DataFlowMethods(this, _Dialect);
            Steps = new StepMethods(this, _Dialect);
            Artifacts = new ArtifactMethods(this, _Dialect);
            ArtifactVersions = new ArtifactVersionMethods(this, _Dialect);
            ArtifactFiles = new ArtifactFileMethods(this, _Dialect);
            Triggers = new TriggerMethods(this, _Dialect);
            FlowRuns = new FlowRunMethods(this, _Dialect);
            RequestHistory = new RequestHistoryMethods(this, _Dialect);
        }

        /// <inheritdoc/>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            IReadOnlyList<SchemaMigration> migrations = SchemaQueries.All();
            await ExecuteQueryAsync(migrations[0].Statements[0], false, token).ConfigureAwait(false);

            HashSet<int> applied = new HashSet<int>();
            DataTable existing = await ExecuteQueryAsync("SELECT version FROM schema_migrations", false, token).ConfigureAwait(false);
            foreach (DataRow row in existing.Rows) applied.Add(Convert.ToInt32(row[0]));

            foreach (SchemaMigration m in migrations.OrderBy(x => x.Version))
            {
                if (applied.Contains(m.Version)) continue;
                List<string> batch = new List<string>(m.Statements);
                batch.Add(
                    "INSERT INTO schema_migrations(version, description, applied_utc) VALUES (" +
                    m.Version + ", " + _Dialect.Quote(m.Description) + ", " + _Dialect.Quote(DateTime.UtcNow) + ");");
                await ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));
            token.ThrowIfCancellationRequested();
            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try { return await ExecuteInternalAsync(new[] { query }, isTransaction, token).ConfigureAwait(false); }
            finally { _WriteLock.Release(); }
        }

        /// <inheritdoc/>
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));
            token.ThrowIfCancellationRequested();
            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try { return await ExecuteInternalAsync(queries, isTransaction, token).ConfigureAwait(false); }
            finally { _WriteLock.Release(); }
        }

        /// <inheritdoc/>
        public override Task CloseAsync(CancellationToken token = default)
        {
            SqliteConnection.ClearAllPools();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing) _WriteLock.Dispose();
            _Disposed = true;
            base.Dispose(disposing);
        }

        private async Task<DataTable> ExecuteInternalAsync(IEnumerable<string> queries, bool isTransaction, CancellationToken token)
        {
            DataTable result = new DataTable();
            using (SqliteConnection connection = new SqliteConnection(ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                SqliteTransaction? tx = isTransaction ? (SqliteTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false) : null;
                try
                {
                    foreach (string q in queries)
                    {
                        if (string.IsNullOrWhiteSpace(q)) continue;
                        using (SqliteCommand cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = q;
                            cmd.CommandTimeout = _Settings.CommandTimeoutSeconds;
                            if (tx != null) cmd.Transaction = tx;
                            using (SqliteDataReader reader = (SqliteDataReader)await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                            {
                                DataTable table = new DataTable();
                                table.Load(reader);
                                if (table.Columns.Count > 0) result = table;
                            }
                        }
                    }
                    if (tx != null) await tx.CommitAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    if (tx != null) { try { await tx.RollbackAsync(token).ConfigureAwait(false); } catch { } }
                    throw;
                }
                finally { if (tx != null) await tx.DisposeAsync().ConfigureAwait(false); }
            }
            return result;
        }
    }
}
