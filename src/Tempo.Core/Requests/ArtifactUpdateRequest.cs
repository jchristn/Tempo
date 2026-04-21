namespace Tempo.Core.Requests
{
    /// <summary>Request body for updating an artifact metadata record.</summary>
    public class ArtifactUpdateRequest
    {
        /// <summary>Tenant-scoped artifact name.</summary>
        public string? Name { get; set; } = null;

        /// <summary>Optional artifact description.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Optional active flag.</summary>
        public bool? Active { get; set; } = null;

        /// <summary>Optional protected flag.</summary>
        public bool? IsProtected { get; set; } = null;
    }
}
