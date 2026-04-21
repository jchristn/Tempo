namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>Driver-agnostic implementation of <see cref="IAccountMethods"/>.</summary>
    public class AccountMethods : IAccountMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _Dialect;

        /// <summary>Instantiate.</summary>
        public AccountMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<Account> CreateAsync(Account account, CancellationToken token = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (string.IsNullOrWhiteSpace(account.Id)) account.Id = IdGenerator.GenerateAccountId();
            account.CreatedUtc = DateTime.UtcNow;
            account.LastUpdateUtc = DateTime.UtcNow;

            string sql =
                "INSERT INTO accounts(id, name, additional_data, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _Dialect.Quote(account.Id) + ", " +
                _Dialect.Quote(account.Name) + ", " +
                _Dialect.Quote(account.AdditionalData) + ", " +
                _Dialect.Bit(account.Active) + ", " +
                _Dialect.Bit(account.IsProtected) + ", " +
                _Dialect.Quote(account.CreatedUtc) + ", " +
                _Dialect.Quote(account.LastUpdateUtc) + ");";
            await _Driver.ExecuteQueryAsync(sql, false, token).ConfigureAwait(false);
            return account;
        }

        /// <inheritdoc/>
        public async Task<Account> UpdateAsync(Account account, CancellationToken token = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            account.LastUpdateUtc = DateTime.UtcNow;
            string sql =
                "UPDATE accounts SET " +
                "name = " + _Dialect.Quote(account.Name) + ", " +
                "additional_data = " + _Dialect.Quote(account.AdditionalData) + ", " +
                "active = " + _Dialect.Bit(account.Active) + ", " +
                "is_protected = " + _Dialect.Bit(account.IsProtected) + ", " +
                "last_update_utc = " + _Dialect.Quote(account.LastUpdateUtc) +
                " WHERE id = " + _Dialect.Quote(account.Id) + ";";
            await _Driver.ExecuteQueryAsync(sql, false, token).ConfigureAwait(false);
            return account;
        }

        /// <inheritdoc/>
        public async Task<Account?> ReadAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM accounts WHERE id = " + _Dialect.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<Account>> EnumerateAsync(EnumerationFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string where = filter.IncludeInactive ? "" : " WHERE active = " + _Dialect.BoolLiteral(true);
            DataTable countTable = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM accounts" + where + ";", false, token).ConfigureAwait(false);
            int total = countTable.Rows.Count == 0 ? 0 : Convert.ToInt32(countTable.Rows[0][0]);

            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM accounts" + where + " ORDER BY created_utc DESC " + _Dialect.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);

            EnumerationResult<Account> r = new EnumerationResult<Account> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<Account>> AllAsync(CancellationToken token = default)
        {
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM accounts ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<Account> list = new List<Account>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string q = _Dialect.Quote(id);
            string tenantScope = "(SELECT id FROM tenants WHERE account_id = " + q + ")";
            List<string> batch = new List<string>
            {
                "DELETE FROM credentials WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM user_role_maps WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM role_permission_maps WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM permissions WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM roles WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM users WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM step_runs WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM flow_runs WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM artifact_files WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM artifact_versions WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM artifacts WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM triggers WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM data_flows WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM steps WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM request_history WHERE tenant_id IN " + tenantScope + ";",
                "DELETE FROM administrators WHERE account_id = " + q + ";",
                "DELETE FROM tenants WHERE account_id = " + q + ";",
                "DELETE FROM accounts WHERE id = " + q + ";"
            };
            await _Driver.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT 1 FROM accounts WHERE id = " + _Dialect.Quote(id) + " " + _Dialect.Paging(1, 0) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count > 0;
        }

        private static Account Map(DataRow row)
        {
            return new Account
            {
                Id = Converters.String(row, "id"),
                Name = Converters.String(row, "name"),
                AdditionalData = Converters.StringOrNull(row, "additional_data"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
