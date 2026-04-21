namespace Tempo.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Tempo.Core.Database.Common.Implementations;
    using Tempo.Core.Enums;
    using Tempo.Core.Settings;

    /// <summary>PostgreSQL implementation of <see cref="DatabaseDriverBase"/>.</summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        /// <inheritdoc/>
        public override DatabaseTypeEnum DatabaseType => DatabaseTypeEnum.Postgresql;

        /// <summary>Computed connection string.</summary>
        public string ConnectionString { get; }

        private readonly DatabaseSettings _Settings;
        private readonly SqlDialect _Dialect = PostgresqlDialect.Instance;
        private bool _Disposed = false;

        /// <summary>Instantiate.</summary>
        public PostgresqlDatabaseDriver(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Settings = settings;
            int port = settings.Port == 0 ? 5432 : settings.Port;
            ConnectionString =
                "Host=" + (settings.Server ?? "localhost") + ";" +
                "Port=" + port + ";" +
                "Database=" + (settings.DatabaseName ?? "tempo") + ";" +
                "Username=" + (settings.Username ?? "postgres") + ";" +
                "Password=" + (settings.Password ?? string.Empty) + ";" +
                "CommandTimeout=" + settings.CommandTimeoutSeconds + ";";

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
            IReadOnlyList<SchemaMigration> migrations = PostgresqlSchema.All();
            await ExecuteQueryAsync(migrations[0].Statements[0], false, token).ConfigureAwait(false);

            HashSet<int> applied = new HashSet<int>();
            DataTable existing = await ExecuteQueryAsync("SELECT version FROM schema_migrations", false, token).ConfigureAwait(false);
            foreach (DataRow row in existing.Rows) applied.Add(Convert.ToInt32(row[0]));

            foreach (SchemaMigration m in migrations.OrderBy(x => x.Version))
            {
                if (applied.Contains(m.Version)) continue;
                foreach (string s in m.Statements)
                {
                    if (!string.IsNullOrWhiteSpace(s)) await ExecuteQueryAsync(s, false, token).ConfigureAwait(false);
                }
                await ExecuteQueryAsync(
                    "INSERT INTO schema_migrations(version, description, applied_utc) VALUES (" +
                    m.Version + ", " + _Dialect.Quote(m.Description) + ", " + _Dialect.Quote(DateTime.UtcNow) + ");",
                    false, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));
            return await ExecuteInternalAsync(new[] { query }, isTransaction, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));
            return await ExecuteInternalAsync(queries, isTransaction, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override Task CloseAsync(CancellationToken token = default)
        {
            NpgsqlConnection.ClearAllPools();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;
            base.Dispose(disposing);
        }

        private async Task<DataTable> ExecuteInternalAsync(IEnumerable<string> queries, bool isTransaction, CancellationToken token)
        {
            DataTable result = new DataTable();
            using (NpgsqlConnection connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                NpgsqlTransaction? tx = isTransaction ? await connection.BeginTransactionAsync(token).ConfigureAwait(false) : null;
                try
                {
                    foreach (string q in queries)
                    {
                        if (string.IsNullOrWhiteSpace(q)) continue;
                        using (NpgsqlCommand cmd = new NpgsqlCommand(q, connection))
                        {
                            cmd.CommandTimeout = _Settings.CommandTimeoutSeconds;
                            if (tx != null) cmd.Transaction = tx;
                            using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
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
