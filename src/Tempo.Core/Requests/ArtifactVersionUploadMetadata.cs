namespace Tempo.Core.Requests
{
    /// <summary>Metadata supplied alongside a raw artifact version upload body.</summary>
    public class ArtifactVersionUploadMetadata
    {
        /// <summary>Artifact version label.</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>Expected SHA-256 digest. When omitted, the server computes it from the body.</summary>
        public string? Sha256 { get; set; } = null;

        /// <summary>Optional content type.</summary>
        public string? ContentType { get; set; } = null;

        /// <summary>Optional original file name.</summary>
        public string? OriginalFileName { get; set; } = null;

        /// <summary>Optional artifact manifest JSON.</summary>
        public string? ManifestJson { get; set; } = null;
    }
}
