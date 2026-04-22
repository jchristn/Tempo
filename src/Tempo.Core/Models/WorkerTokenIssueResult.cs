namespace Tempo.Core.Models
{
    using System;

    /// <summary>
    /// One-time plaintext worker token returned after rotation.
    /// </summary>
    public class WorkerTokenIssueResult
    {
        /// <summary>Worker identifier.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Plaintext worker token. Persisted only by the caller.</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>UTC time the token was issued.</summary>
        public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    }
}
