namespace Tempo.Core.Responses
{
    using System;

    /// <summary>
    /// A single time bucket within a <see cref="RequestHistorySummary"/>.
    /// </summary>
    public class RequestHistoryBucket
    {
        /// <summary>Inclusive UTC start of the bucket.</summary>
        public DateTime BucketStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Exclusive UTC end of the bucket.</summary>
        public DateTime BucketEndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Successful requests (2xx/3xx).</summary>
        public int SuccessCount { get; set; } = 0;

        /// <summary>Failing requests (4xx/5xx).</summary>
        public int FailureCount { get; set; } = 0;

        /// <summary>Average duration, in milliseconds.</summary>
        public double AverageDurationMs { get; set; } = 0.0;
    }
}
