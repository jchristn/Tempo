namespace Tempo.Core.Models
{
    /// <summary>Reference to a tenant-owned artifact version.</summary>
    public class ArtifactReference
    {
        public string ArtifactId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}
