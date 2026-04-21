namespace Tempo.Core.Models
{
    using System;

    /// <summary>Retention controls for artifact versions.</summary>
    public class ArtifactRetentionPolicy
    {
        public int VersionGracePeriodDays
        {
            get => _VersionGracePeriodDays;
            set => _VersionGracePeriodDays = Math.Clamp(value, 0, 3650);
        }

        public int FlowRunReplayRetentionDays
        {
            get => _FlowRunReplayRetentionDays;
            set => _FlowRunReplayRetentionDays = Math.Clamp(value, 1, 3650);
        }

        public long MaxArtifactBytesPerTenant
        {
            get => _MaxArtifactBytesPerTenant;
            set => _MaxArtifactBytesPerTenant = Math.Clamp(value, 0, 1024L * 1024L * 1024L * 1024L);
        }

        public int MaxVersionsPerArtifact
        {
            get => _MaxVersionsPerArtifact;
            set => _MaxVersionsPerArtifact = Math.Clamp(value, 0, 100000);
        }

        private int _VersionGracePeriodDays = 7;
        private int _FlowRunReplayRetentionDays = 30;
        private long _MaxArtifactBytesPerTenant = 0;
        private int _MaxVersionsPerArtifact = 0;
    }
}
