namespace Tempo.Core.Models
{
    /// <summary>Reference to a tenant-owned artifact version.</summary>
    public class ArtifactReference
    {
        /// <summary>
        /// Identifier of the referenced artifact.
        /// Default: empty string.
        /// </summary>
        public string ArtifactId { get; set; } = string.Empty;

        /// <summary>
        /// Version of the referenced artifact.
        /// Default: empty string.
        /// </summary>
        public string Version { get; set; } = string.Empty;
    }
}
