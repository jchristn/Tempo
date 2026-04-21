namespace Tempo.Core.Models
{
    using System;

    /// <summary>Editable file stored inside a tenant artifact working tree.</summary>
    public class ArtifactFileRecord
    {
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Parent artifact identifier.</summary>
        public string ArtifactId { get; set; } = string.Empty;

        /// <summary>Normalized artifact-relative path using forward slashes.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>UTF-8 text content, or base64 content when <see cref="IsBinary"/> is true.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Best-effort content type.</summary>
        public string? ContentType { get; set; } = null;

        /// <summary>True when <see cref="Content"/> is base64-encoded binary data.</summary>
        public bool IsBinary { get; set; } = false;

        /// <summary>SHA-256 digest of the decoded file bytes.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Decoded file byte length.</summary>
        public long ByteLength { get; set; } = 0;

        /// <summary>UTC creation timestamp.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC last update timestamp.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}
