namespace Tempo.Core.Responses
{
    using System;

    /// <summary>
    /// Token issuance response.
    /// </summary>
    public class TokenResponse
    {
        /// <summary>Encrypted bearer token.</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>UTC expiration time.</summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Tenant identifier, if any.</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>User identifier, if any.</summary>
        public string? UserId { get; set; } = null;

        /// <summary>Administrator identifier, if any.</summary>
        public string? AdministratorId { get; set; } = null;
    }
}
