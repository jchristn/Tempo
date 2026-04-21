namespace Tempo.Core.Settings
{
    using System;

    /// <summary>Artifact blob storage limits and paths.</summary>
    public class ArtifactSettings
    {
        /// <summary>Filesystem root for artifact blobs. Default: ./artifacts.</summary>
        public string RootPath
        {
            get => _RootPath;
            set => _RootPath = !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentNullException(nameof(RootPath));
        }

        /// <summary>Maximum accepted upload size in bytes. Default: 100 MiB.</summary>
        public long MaxUploadBytes
        {
            get => _MaxUploadBytes;
            set => _MaxUploadBytes = Math.Clamp(value, 1, 2L * 1024L * 1024L * 1024L);
        }

        /// <summary>Maximum stored artifact bytes per tenant. Default: 1 GiB.</summary>
        public long MaxBytesPerTenant
        {
            get => _MaxBytesPerTenant;
            set => _MaxBytesPerTenant = Math.Clamp(value, 1, 1024L * 1024L * 1024L * 1024L);
        }

        /// <summary>Grace period before deleted artifact versions can be garbage collected. Default: 7 days.</summary>
        public int VersionGracePeriodDays
        {
            get => _VersionGracePeriodDays;
            set => _VersionGracePeriodDays = Math.Clamp(value, 0, 3650);
        }

        /// <summary>Flow-run replay retention window for future artifact snapshots. Default: 30 days.</summary>
        public int FlowRunReplayRetentionDays
        {
            get => _FlowRunReplayRetentionDays;
            set => _FlowRunReplayRetentionDays = Math.Clamp(value, 1, 3650);
        }

        /// <summary>Maximum active versions retained per artifact. 0 means unlimited.</summary>
        public int MaxVersionsPerArtifact
        {
            get => _MaxVersionsPerArtifact;
            set => _MaxVersionsPerArtifact = Math.Clamp(value, 0, 100000);
        }

        /// <summary>Maximum versions swept in one scheduled GC pass. Default: 100.</summary>
        public int GcBatchSize
        {
            get => _GcBatchSize;
            set => _GcBatchSize = Math.Clamp(value, 1, 10000);
        }

        /// <summary>Interval between scheduled artifact GC passes. Default: 60 minutes.</summary>
        public int GcIntervalMinutes
        {
            get => _GcIntervalMinutes;
            set => _GcIntervalMinutes = Math.Clamp(value, 1, 1440);
        }

        private string _RootPath = "./artifacts";
        private long _MaxUploadBytes = 100L * 1024L * 1024L;
        private long _MaxBytesPerTenant = 1024L * 1024L * 1024L;
        private int _VersionGracePeriodDays = 7;
        private int _FlowRunReplayRetentionDays = 30;
        private int _MaxVersionsPerArtifact = 0;
        private int _GcBatchSize = 100;
        private int _GcIntervalMinutes = 60;
    }
}
