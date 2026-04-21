namespace Tempo.Core.Responses
{
    using Tempo.Core.Models;

    /// <summary>Response after saving or deleting an editable artifact file.</summary>
    public class ArtifactFileWriteResponse
    {
        /// <summary>The saved file, or null after delete.</summary>
        public ArtifactFileRecord? File { get; set; } = null;

        /// <summary>The regenerated mutable artifact version when snapshotting succeeded.</summary>
        public ArtifactVersionRecord? ArtifactVersion { get; set; } = null;

        /// <summary>True when the runtime snapshot was regenerated.</summary>
        public bool SnapshotUpdated { get; set; } = false;

        /// <summary>Snapshot validation or packaging error. The file operation still succeeded.</summary>
        public string? SnapshotError { get; set; } = null;
    }
}
