namespace Tempo.Core.Requests
{
    /// <summary>Request body for creating an artifact metadata record.</summary>
    public class ArtifactCreateRequest
    {
        /// <summary>Tenant-scoped artifact name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional artifact description.</summary>
        public string? Description { get; set; } = null;
    }
}
