namespace Tempo.Core.Requests
{
    /// <summary>
    /// Password-based login request body.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>Tenant identifier. Optional when logging in as an administrator.</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>Email address.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Password. Either plaintext or a SHA-256 hex string is accepted.</summary>
        public string Password { get; set; } = string.Empty;
    }
}
