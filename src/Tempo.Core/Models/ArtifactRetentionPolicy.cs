namespace Tempo.Core.Models
{
    using System;

    /// <summary>Retention controls for artifact versions.</summary>
    public class ArtifactRetentionPolicy
    {
        /// <summary>
        /// Number of days a superseded artifact version is retained before eligible for cleanup.
        /// Default: 7. Range: 0 to 3650 (values are clamped).
        /// </summary>
        public int VersionGracePeriodDays
        {
            get => _VersionGracePeriodDays;
            set => _VersionGracePeriodDays = Math.Clamp(value, 0, 3650);
        }

        /// <summary>
        /// Number of days flow-run replay data is retained.
        /// Default: 30. Range: 1 to 3650 (values are clamped).
        /// </summary>
        public int FlowRunReplayRetentionDays
        {
            get => _FlowRunReplayRetentionDays;
            set => _FlowRunReplayRetentionDays = Math.Clamp(value, 1, 3650);
        }

        /// <summary>
        /// Maximum total artifact storage in bytes allowed per tenant (0 for unlimited).
        /// Default: 0 (unlimited). Range: 0 to 1 TiB (values are clamped).
        /// </summary>
        public long MaxArtifactBytesPerTenant
        {
            get => _MaxArtifactBytesPerTenant;
            set => _MaxArtifactBytesPerTenant = Math.Clamp(value, 0, 1024L * 1024L * 1024L * 1024L);
        }

        /// <summary>
        /// Maximum number of versions retained per artifact (0 for unlimited).
        /// Default: 0 (unlimited). Range: 0 to 100000 (values are clamped).
        /// </summary>
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
