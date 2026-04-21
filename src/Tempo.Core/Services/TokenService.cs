namespace Tempo.Core.Services
{
    using System;
    using Tempo.Core.Security;
    using Tempo.Core.Settings;

    /// <summary>
    /// Issues and validates authentication tokens.
    /// </summary>
    public class TokenService
    {
        private readonly TokenCipher _Cipher;
        private readonly AuthSettings _Auth;

        /// <summary>Instantiate.</summary>
        /// <param name="auth">Authentication settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="auth"/> is null.</exception>
        public TokenService(AuthSettings auth)
        {
            if (auth == null) throw new ArgumentNullException(nameof(auth));
            _Auth = auth;
            _Cipher = new TokenCipher(auth.SigningKey);
        }

        /// <summary>Issue a user token.</summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="userId">User identifier.</param>
        /// <param name="accountId">Optional account identifier.</param>
        /// <returns>Encrypted token string.</returns>
        public string IssueUserToken(string tenantId, string userId, string? accountId = null)
        {
            AuthenticationToken token = new AuthenticationToken
            {
                AccountId = accountId,
                TenantId = tenantId,
                UserId = userId,
                IssuedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(_Auth.TokenExpirationMinutes),
                Issuer = _Auth.Issuer
            };
            return _Cipher.Encrypt(token);
        }

        /// <summary>Issue an administrator token.</summary>
        /// <param name="administratorId">Administrator identifier.</param>
        /// <param name="accountId">Optional account identifier.</param>
        /// <returns>Encrypted token string.</returns>
        public string IssueAdminToken(string administratorId, string? accountId = null)
        {
            AuthenticationToken token = new AuthenticationToken
            {
                AdministratorId = administratorId,
                AccountId = accountId,
                IssuedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(_Auth.TokenExpirationMinutes),
                Issuer = _Auth.Issuer
            };
            return _Cipher.Encrypt(token);
        }

        /// <summary>
        /// Decrypt and validate a token. Returns null when the token is invalid or expired.
        /// </summary>
        public AuthenticationToken? Validate(string tokenString)
        {
            AuthenticationToken? t = _Cipher.Decrypt(tokenString);
            if (t == null) return null;
            if (t.ExpiresUtc < DateTime.UtcNow) return null;
            return t;
        }
    }
}
