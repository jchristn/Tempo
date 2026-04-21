namespace Tempo.Core.Models
{
    using System;
    using System.Linq;
    using Tempo.Core.Helpers;

    /// <summary>Metadata for one uploaded artifact version.</summary>
    public class ArtifactVersionRecord
    {
        /// <summary>Artifact version identifier (prefix "arv_").</summary>
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

        /// <summary>Parent artifact identifier.</summary>
        public string ArtifactId
        {
            get => _ArtifactId;
            set => _ArtifactId = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(nameof(ArtifactId));
        }

        /// <summary>Artifact version label.</summary>
        public string Version
        {
            get => _Version;
            set => _Version = !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentNullException(nameof(Version));
        }

        /// <summary>Content SHA-256 digest as 64 lowercase hex characters.</summary>
        public string Sha256
        {
            get => _Sha256;
            set => _Sha256 = ValidateSha256(value);
        }

        /// <summary>Byte length of the stored blob.</summary>
        public long ByteLength
        {
            get => _ByteLength;
            set => _ByteLength = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(ByteLength));
        }

        /// <summary>Content type.</summary>
        public string? ContentType { get; set; } = null;

        /// <summary>Original file name supplied by the uploader.</summary>
        public string? OriginalFileName { get; set; } = null;

        /// <summary>Manifest JSON supplied with the artifact package.</summary>
        public string? ManifestJson { get; set; } = null;

        /// <summary>Blob-store storage key.</summary>
        public string? StorageKey { get; set; } = null;

        /// <summary>Whether this version is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether this version is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Soft-delete timestamp in UTC.</summary>
        public DateTime? DeletedUtc { get; set; } = null;

        /// <summary>Timestamp when this version becomes eligible for garbage collection.</summary>
        public DateTime? GcEligibleUtc { get; set; } = null;

        private string _Id = IdGenerator.GenerateArtifactVersionId();
        private string _TenantId = string.Empty;
        private string _ArtifactId = string.Empty;
        private string _Version = "1";
        private string _Sha256 = new string('0', 64);
        private long _ByteLength = 0;

        private static string ValidateSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Sha256));
            string trimmed = value.Trim().ToLowerInvariant();
            if (trimmed.Length != 64 || trimmed.Any(c => !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))))
                throw new ArgumentException("Sha256 must be 64 hexadecimal characters.", nameof(Sha256));
            return trimmed;
        }
    }
}
