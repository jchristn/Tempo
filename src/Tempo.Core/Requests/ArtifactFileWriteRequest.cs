namespace Tempo.Core.Requests
{
    /// <summary>Request to create or replace one editable artifact file.</summary>
    public class ArtifactFileWriteRequest
    {
        /// <summary>Artifact-relative path. Query-string path wins when both are supplied.</summary>
        public string? Path { get; set; } = null;

        /// <summary>UTF-8 text content, or base64 content when <see cref="IsBinary"/> is true.</summary>
        public string? Content { get; set; } = null;

        /// <summary>Best-effort content type.</summary>
        public string? ContentType { get; set; } = null;

        /// <summary>True when <see cref="Content"/> is base64-encoded binary data.</summary>
        public bool IsBinary { get; set; } = false;
    }
}
