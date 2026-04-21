namespace Tempo.Core.Security
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Encrypted authentication token payload.
    /// </summary>
    public class AuthenticationToken
    {
        /// <summary>Administrator identifier if the token authenticates an administrator.</summary>
        public string? AdministratorId { get; set; } = null;

        /// <summary>Account identifier.</summary>
        public string? AccountId { get; set; } = null;

        /// <summary>Tenant identifier.</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>User identifier if the token authenticates a user.</summary>
        public string? UserId { get; set; } = null;

        /// <summary>Token issued-at time, in UTC.</summary>
        public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Token expiration time, in UTC.</summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddMinutes(1440);

        /// <summary>Random identifier used to ensure uniqueness.</summary>
        public string Nonce { get; set; } = IdGenerator.GenerateNonceId();

        /// <summary>Issuer string.</summary>
        public string Issuer { get; set; } = "tempo";
    }
}
