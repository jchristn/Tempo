namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Point-in-time external runtime capacity counters.</summary>
    public class ExternalRuntimeCapacitySnapshot
    {
        /// <summary>Maximum number of concurrent external runtimes allowed server-wide.</summary>
        public int MaxServerWide { get; set; }
        /// <summary>Maximum number of concurrent external runtimes allowed per tenant.</summary>
        public int MaxPerTenant { get; set; }
        /// <summary>Number of external runtimes currently active server-wide.</summary>
        public int ActiveServerWide { get; set; }
        /// <summary>Number of external runtimes currently queued and awaiting capacity server-wide.</summary>
        public int QueuedServerWide { get; set; }
        /// <summary>Number of active external runtimes keyed by tenant identifier.</summary>
        public Dictionary<string, int> ActiveByTenant { get; set; } = new Dictionary<string, int>();
        /// <summary>Number of queued external runtimes awaiting capacity keyed by tenant identifier.</summary>
        public Dictionary<string, int> QueuedByTenant { get; set; } = new Dictionary<string, int>();
        /// <summary>Total time in milliseconds spent waiting for capacity across all runtimes.</summary>
        public long TotalCapacityWaitMs { get; set; }
        /// <summary>Total process runtime in milliseconds accumulated across all runtimes.</summary>
        public long TotalProcessRuntimeMs { get; set; }
        /// <summary>Number of processes that have been forcibly killed.</summary>
        public int ProcessKillCount { get; set; }
    }
}
