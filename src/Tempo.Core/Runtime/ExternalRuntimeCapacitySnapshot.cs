namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;

    /// <summary>Point-in-time external runtime capacity counters.</summary>
    public class ExternalRuntimeCapacitySnapshot
    {
        public int MaxServerWide { get; set; }
        public int MaxPerTenant { get; set; }
        public int ActiveServerWide { get; set; }
        public int QueuedServerWide { get; set; }
        public Dictionary<string, int> ActiveByTenant { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> QueuedByTenant { get; set; } = new Dictionary<string, int>();
        public long TotalCapacityWaitMs { get; set; }
        public long TotalProcessRuntimeMs { get; set; }
        public int ProcessKillCount { get; set; }
    }
}
