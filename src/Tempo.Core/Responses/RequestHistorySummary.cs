namespace Tempo.Core.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Time-bucketed summary of request history.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>Total number of requests in the window.</summary>
        public int TotalCount { get; set; } = 0;

        /// <summary>Number of requests with a 2xx/3xx status code.</summary>
        public int TotalSuccess { get; set; } = 0;

        /// <summary>Number of requests with a 4xx/5xx status code.</summary>
        public int TotalFailure { get; set; } = 0;

        /// <summary>Average duration across the window, in milliseconds.</summary>
        public double AverageDurationMs { get; set; } = 0.0;

        /// <summary>Buckets within the window. The server emits every bucket including empty ones.</summary>
        public List<RequestHistoryBucket> Buckets { get; set; } = new List<RequestHistoryBucket>();
    }
}
