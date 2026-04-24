namespace Tempo.Core.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Security;
    using Tempo.Core.Settings;

    /// <summary>
    /// Resolves an inbound request to an authenticated <see cref="RequestContext"/>.
    /// Supports bearer tokens, admin API key, credential access keys, and email/password headers.
    /// </summary>
    public class AuthenticationService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly TokenService _Tokens;
        private readonly AuthSettings _Auth;

        /// <summary>Instantiate.</summary>
        public AuthenticationService(DatabaseDriverBase database, TokenService tokens, AuthSettings auth)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _Auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        /// <summary>
        /// Attempt to authenticate using the supplied header values.
        /// </summary>
        /// <param name="tokenHeader">Value of the <see cref="Constants.HeaderToken"/> header.</param>
        /// <param name="bearerToken">Decoded bearer token.</param>
        /// <param name="apiKey">Admin API key from <see cref="Constants.HeaderApiKey"/>.</param>
        /// <param name="accessKey">Credential access key.</param>
        /// <param name="tenantIdHeader">Tenant identifier from <see cref="Constants.HeaderTenantId"/>.</param>
        /// <param name="emailHeader">Email from <see cref="Constants.HeaderEmail"/>.</param>
        /// <param name="passwordHeader">Password from <see cref="Constants.HeaderPassword"/>.</param>
        /// <param name="containsUnsupportedSecretKeyHeader">Whether the caller supplied the unsupported <c>x-secret-key</c> header.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Populated request context.</returns>
        public async Task<RequestContext> AuthenticateAsync(
            string? tokenHeader,
            string? bearerToken,
            string? apiKey,
            string? accessKey,
            string? tenantIdHeader,
            string? emailHeader,
            string? passwordHeader,
            bool containsUnsupportedSecretKeyHeader = false,
            CancellationToken token = default)
        {
            RequestContext ctx = new RequestContext
            {
                ContainsUnsupportedSecretKeyHeader = containsUnsupportedSecretKeyHeader
            };

            if (containsUnsupportedSecretKeyHeader)
            {
                ctx.AuthenticationResult = AuthenticationResultEnum.Invalid;
                return ctx;
            }

            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(_Auth.AdminApiKey)
                && string.Equals(apiKey, _Auth.AdminApiKey, StringComparison.Ordinal))
            {
                ctx.IsAuthenticated = true;
                ctx.IsAdmin = true;
                ctx.AuthenticationResult = AuthenticationResultEnum.Success;
                ctx.PrincipalName = "admin-api-key";
                return ctx;
            }

            string? bearerOrToken = !string.IsNullOrEmpty(tokenHeader) ? tokenHeader : bearerToken;
            if (!string.IsNullOrEmpty(bearerOrToken))
            {
                AuthenticationToken? parsed = _Tokens.Validate(bearerOrToken);
                if (parsed != null)
                {
                    if (!string.IsNullOrEmpty(parsed.AdministratorId))
                    {
                        Administrator? admin = await _Database.Administrators.ReadAsync(parsed.AdministratorId, token).ConfigureAwait(false);
                        if (admin == null) { ctx.AuthenticationResult = AuthenticationResultEnum.NotFound; return ctx; }
                        if (!admin.Active) { ctx.AuthenticationResult = AuthenticationResultEnum.Inactive; return ctx; }
                        PopulateAdmin(ctx, admin);
                        return ctx;
                    }

                    if (!string.IsNullOrEmpty(parsed.TenantId) && !string.IsNullOrEmpty(parsed.UserId))
                    {
                        User? user = await _Database.Users.ReadAsync(parsed.TenantId, parsed.UserId, token).ConfigureAwait(false);
                        if (user == null) { ctx.AuthenticationResult = AuthenticationResultEnum.NotFound; return ctx; }
                        if (!user.Active) { ctx.AuthenticationResult = AuthenticationResultEnum.Inactive; return ctx; }
                        Tenant? tenant = await _Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false);
                        if (tenant == null) { ctx.AuthenticationResult = AuthenticationResultEnum.NotFound; return ctx; }
                        if (!tenant.Active) { ctx.AuthenticationResult = AuthenticationResultEnum.Inactive; return ctx; }
                        PopulateUser(ctx, user, tenant);
                        return ctx;
                    }

                    ctx.AuthenticationResult = AuthenticationResultEnum.Invalid;
                    return ctx;
                }

                AuthenticationResultEnum credentialResult = await TryAuthenticateCredentialAsync(ctx, bearerOrToken, token).ConfigureAwait(false);
                if (credentialResult == AuthenticationResultEnum.Success)
                    return ctx;
                if (credentialResult != AuthenticationResultEnum.None)
                {
                    ctx.AuthenticationResult = credentialResult;
                    return ctx;
                }

                ctx.AuthenticationResult = AuthenticationResultEnum.Expired;
                return ctx;
            }

            if (!string.IsNullOrEmpty(accessKey))
            {
                AuthenticationResultEnum credentialResult = await TryAuthenticateCredentialAsync(ctx, accessKey, token).ConfigureAwait(false);
                if (credentialResult == AuthenticationResultEnum.Success)
                    return ctx;
                if (credentialResult != AuthenticationResultEnum.None)
                {
                    ctx.AuthenticationResult = credentialResult;
                    return ctx;
                }

                ctx.AuthenticationResult = AuthenticationResultEnum.NotFound;
                return ctx;
            }

            if (!string.IsNullOrEmpty(emailHeader) && !string.IsNullOrEmpty(passwordHeader))
            {
                if (string.IsNullOrEmpty(tenantIdHeader))
                {
                    Administrator? admin = await _Database.Administrators.ReadByEmailAsync(emailHeader, token).ConfigureAwait(false);
                    if (admin != null)
                    {
                        if (!admin.Active) { ctx.AuthenticationResult = AuthenticationResultEnum.Inactive; return ctx; }
                        if (!PasswordHasher.Verify(passwordHeader, admin.PasswordSha256))
                        { ctx.AuthenticationResult = AuthenticationResultEnum.Invalid; return ctx; }
                        PopulateAdmin(ctx, admin);
                        return ctx;
                    }
                    ctx.AuthenticationResult = AuthenticationResultEnum.NotFound;
                    return ctx;
                }
                else
                {
                    User? user = await _Database.Users.ReadByEmailAsync(tenantIdHeader, emailHeader, token).ConfigureAwait(false);
                    if (user == null) { ctx.AuthenticationResult = AuthenticationResultEnum.NotFound; return ctx; }
                    if (!user.Active) { ctx.AuthenticationResult = AuthenticationResultEnum.Inactive; return ctx; }
                    if (!PasswordHasher.Verify(passwordHeader, user.PasswordSha256))
                    { ctx.AuthenticationResult = AuthenticationResultEnum.Invalid; return ctx; }
                    Tenant? tenant = await _Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false);
                    if (tenant == null) { ctx.AuthenticationResult = AuthenticationResultEnum.NotFound; return ctx; }
                    if (!tenant.Active) { ctx.AuthenticationResult = AuthenticationResultEnum.Inactive; return ctx; }
                    PopulateUser(ctx, user, tenant);
                    return ctx;
                }
            }

            ctx.AuthenticationResult = AuthenticationResultEnum.None;
            return ctx;
        }

        private async Task<AuthenticationResultEnum> TryAuthenticateCredentialAsync(RequestContext ctx, string accessKey, CancellationToken token)
        {
            Credential? cred = await _Database.Credentials.ReadByAccessKeyAsync(accessKey, token).ConfigureAwait(false);
            if (cred == null) return AuthenticationResultEnum.None;
            if (!cred.Active) return AuthenticationResultEnum.Inactive;

            User? user = await _Database.Users.ReadAsync(cred.TenantId, cred.UserId, token).ConfigureAwait(false);
            if (user == null) return AuthenticationResultEnum.NotFound;
            if (!user.Active) return AuthenticationResultEnum.Inactive;
            Tenant? tenant = await _Database.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false);
            if (tenant == null) return AuthenticationResultEnum.NotFound;
            if (!tenant.Active) return AuthenticationResultEnum.Inactive;

            PopulateUser(ctx, user, tenant);
            ctx.CredentialId = cred.Id;
            ctx.Credential = cred;
            return AuthenticationResultEnum.Success;
        }

        private static void PopulateAdmin(RequestContext ctx, Administrator admin)
        {
            ctx.IsAuthenticated = true;
            ctx.AuthenticationResult = AuthenticationResultEnum.Success;
            ctx.AdministratorId = admin.Id;
            ctx.AccountId = admin.AccountId;
            ctx.IsAdmin = true;
            ctx.Email = admin.Email;
            ctx.PrincipalName = (admin.FirstName + " " + admin.LastName).Trim();
            ctx.Administrator = admin;
        }

        private static void PopulateUser(RequestContext ctx, User user, Tenant tenant)
        {
            ctx.IsAuthenticated = true;
            ctx.AuthenticationResult = AuthenticationResultEnum.Success;
            ctx.UserId = user.Id;
            ctx.TenantId = user.TenantId;
            ctx.AccountId = tenant.AccountId;
            ctx.IsAdmin = user.IsAdmin;
            ctx.IsTenantAdmin = user.IsTenantAdmin;
            ctx.Email = user.Email;
            ctx.PrincipalName = (user.FirstName + " " + user.LastName).Trim();
            ctx.User = user;
            ctx.Tenant = tenant;
        }
    }
}
