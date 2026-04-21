namespace Tempo.Core.Requests
{
    using System;

    /// <summary>
    /// Filter for request history queries.
    /// </summary>
    public class RequestHistoryFilter : EnumerationFilter
    {
        /// <summary>Tenant identifier. Null for global (admin only).</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>User identifier. Null for all users.</summary>
        public string? UserId { get; set; } = null;

        /// <summary>HTTP method filter (exact, case-insensitive). Null for any.</summary>
        public string? Method { get; set; } = null;

        /// <summary>Exact status code filter. Null for any.</summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>Substring match on <c>Path</c> / <c>Url</c>. Null for any.</summary>
        public string? PathContains { get; set; } = null;

        /// <summary>Inclusive lower UTC bound. Null for unbounded.</summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>Exclusive upper UTC bound. Null for unbounded.</summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>Bucket size in minutes used by the summary endpoint. Default: 15. Range: 1 to 10080.</summary>
        public int BucketMinutes
        {
            get
            {
                return _BucketMinutes;
            }
            set
            {
                _BucketMinutes = Math.Clamp(value, 1, 10080);
            }
        }

        private int _BucketMinutes = 15;
    }
}
