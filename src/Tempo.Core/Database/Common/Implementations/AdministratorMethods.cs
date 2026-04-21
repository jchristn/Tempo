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

    /// <summary>Driver-agnostic implementation of <see cref="IAdministratorMethods"/>.</summary>
    public class AdministratorMethods : IAdministratorMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public AdministratorMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<Administrator> CreateAsync(Administrator a, CancellationToken token = default)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (string.IsNullOrWhiteSpace(a.Id)) a.Id = IdGenerator.GenerateAdminId();
            a.CreatedUtc = DateTime.UtcNow;
            a.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO administrators(id, account_id, first_name, last_name, email, password_sha256, telephone, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(a.Id) + ", " + _D.Quote(a.AccountId) + ", " + _D.Quote(a.FirstName) + ", " + _D.Quote(a.LastName) + ", " +
                _D.Quote(a.Email.ToLowerInvariant()) + ", " + _D.Quote(a.PasswordSha256) + ", " + _D.Quote(a.Telephone) + ", " +
                _D.Bit(a.Active) + ", " + _D.Bit(a.IsProtected) + ", " + _D.Quote(a.CreatedUtc) + ", " + _D.Quote(a.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return a;
        }

        /// <inheritdoc/>
        public async Task<Administrator> UpdateAsync(Administrator a, CancellationToken token = default)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            a.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE administrators SET " +
                "account_id = " + _D.Quote(a.AccountId) + ", first_name = " + _D.Quote(a.FirstName) + ", last_name = " + _D.Quote(a.LastName) + ", " +
                "email = " + _D.Quote(a.Email.ToLowerInvariant()) + ", password_sha256 = " + _D.Quote(a.PasswordSha256) + ", " +
                "telephone = " + _D.Quote(a.Telephone) + ", active = " + _D.Bit(a.Active) + ", is_protected = " + _D.Bit(a.IsProtected) + ", " +
                "last_update_utc = " + _D.Quote(a.LastUpdateUtc) +
                " WHERE id = " + _D.Quote(a.Id) + ";",
                false, token).ConfigureAwait(false);
            return a;
        }

        /// <inheritdoc/>
        public async Task<Administrator?> ReadAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM administrators WHERE id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<Administrator?> ReadByEmailAsync(string email, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentNullException(nameof(email));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM administrators WHERE email = " + _D.Quote(email.ToLowerInvariant()) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<Administrator>> EnumerateAsync(EnumerationFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string where = filter.IncludeInactive ? "" : " WHERE active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM administrators" + where + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM administrators" + where + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<Administrator> r = new EnumerationResult<Administrator> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<Administrator>> AllAsync(CancellationToken token = default)
        {
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM administrators ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<Administrator> list = new List<Administrator>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            await _Driver.ExecuteQueryAsync("DELETE FROM administrators WHERE id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return true;
        }

        private static Administrator Map(DataRow row)
        {
            return new Administrator
            {
                Id = Converters.String(row, "id"),
                AccountId = Converters.StringOrNull(row, "account_id"),
                FirstName = Converters.String(row, "first_name"),
                LastName = Converters.String(row, "last_name"),
                Email = Converters.String(row, "email"),
                PasswordSha256 = Converters.String(row, "password_sha256"),
                Telephone = Converters.StringOrNull(row, "telephone"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
