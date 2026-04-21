namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>Tenant-owned artifact metadata.</summary>
    public class ArtifactRecord
    {
        /// <summary>Artifact identifier (prefix "art_").</summary>
        public string Id
        {
            get => _Id;
            set => _Id = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(nameof(Id));
        }

        /// <summary>Tenant identifier.</summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(nameof(TenantId));
        }

        /// <summary>Tenant-scoped artifact name.</summary>
        public string Name
        {
            get => _Name;
            set => _Name = !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentNullException(nameof(Name));
        }

        /// <summary>Optional description.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Whether the artifact is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the artifact is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateArtifactId();
        private string _TenantId = string.Empty;
        private string _Name = "My artifact";
    }
}
