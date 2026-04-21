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

    /// <summary>Driver-agnostic implementation of <see cref="IUserMethods"/>.</summary>
    public class UserMethods : IUserMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public UserMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<User> CreateAsync(User u, CancellationToken token = default)
        {
            if (u == null) throw new ArgumentNullException(nameof(u));
            if (string.IsNullOrWhiteSpace(u.Id)) u.Id = IdGenerator.GenerateUserId();
            u.CreatedUtc = DateTime.UtcNow;
            u.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO users(id, tenant_id, first_name, last_name, email, password_sha256, is_admin, is_tenant_admin, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(u.Id) + ", " + _D.Quote(u.TenantId) + ", " + _D.Quote(u.FirstName) + ", " + _D.Quote(u.LastName) + ", " +
                _D.Quote(u.Email.ToLowerInvariant()) + ", " + _D.Quote(u.PasswordSha256) + ", " +
                _D.Bit(u.IsAdmin) + ", " + _D.Bit(u.IsTenantAdmin) + ", " +
                _D.Bit(u.Active) + ", " + _D.Bit(u.IsProtected) + ", " +
                _D.Quote(u.CreatedUtc) + ", " + _D.Quote(u.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return u;
        }

        /// <inheritdoc/>
        public async Task<User> UpdateAsync(User u, CancellationToken token = default)
        {
            if (u == null) throw new ArgumentNullException(nameof(u));
            u.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE users SET first_name = " + _D.Quote(u.FirstName) + ", last_name = " + _D.Quote(u.LastName) + ", " +
                "email = " + _D.Quote(u.Email.ToLowerInvariant()) + ", password_sha256 = " + _D.Quote(u.PasswordSha256) + ", " +
                "is_admin = " + _D.Bit(u.IsAdmin) + ", is_tenant_admin = " + _D.Bit(u.IsTenantAdmin) + ", " +
                "active = " + _D.Bit(u.Active) + ", is_protected = " + _D.Bit(u.IsProtected) + ", " +
                "last_update_utc = " + _D.Quote(u.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(u.TenantId) + " AND id = " + _D.Quote(u.Id) + ";",
                false, token).ConfigureAwait(false);
            return u;
        }

        /// <inheritdoc/>
        public async Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM users WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentNullException(nameof(email));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM users WHERE tenant_id = " + _D.Quote(tenantId) + " AND email = " + _D.Quote(email.ToLowerInvariant()) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<User>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM users WHERE tenant_id = " + _D.Quote(tenantId) + active + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM users WHERE tenant_id = " + _D.Quote(tenantId) + active +
                " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";", false, token).ConfigureAwait(false);
            EnumerationResult<User> r = new EnumerationResult<User> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<User>> AllAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM users WHERE tenant_id = " + _D.Quote(tenantId) + " ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<User> list = new List<User>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string tq = _D.Quote(tenantId);
            string uq = _D.Quote(id);
            List<string> batch = new List<string>
            {
                "DELETE FROM credentials WHERE tenant_id = " + tq + " AND user_id = " + uq + ";",
                "DELETE FROM user_role_maps WHERE tenant_id = " + tq + " AND user_id = " + uq + ";",
                "DELETE FROM users WHERE tenant_id = " + tq + " AND id = " + uq + ";"
            };
            await _Driver.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            return true;
        }

        private static User Map(DataRow row)
        {
            return new User
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                FirstName = Converters.String(row, "first_name"),
                LastName = Converters.String(row, "last_name"),
                Email = Converters.String(row, "email"),
                PasswordSha256 = Converters.String(row, "password_sha256"),
                IsAdmin = Converters.Bool(row, "is_admin"),
                IsTenantAdmin = Converters.Bool(row, "is_tenant_admin"),
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
