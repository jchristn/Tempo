namespace Tempo.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>Artifact package manifest metadata.</summary>
    public class ArtifactManifest
    {
        public string ManifestVersion { get; set; } = "1";
        public string? RuntimeKey { get; set; } = null;
        public string? ProtocolVersion { get; set; } = null;
        public List<string> SupportedProtocolVersions { get; set; } = new List<string>();
        public string DefaultEntrypoint { get; set; } = "default";
        public Dictionary<string, ArtifactManifestEntrypoint> Entrypoints { get; set; } = new Dictionary<string, ArtifactManifestEntrypoint>();
        public List<string> EnvironmentAllowList { get; set; } = new List<string>();
        public string? InputSchema { get; set; } = null;
        public string? OutputSchema { get; set; } = null;
        public Dictionary<string, JsonElement> RuntimeSettings { get; set; } = new Dictionary<string, JsonElement>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>One named executable entrypoint inside an artifact manifest.</summary>
    public class ArtifactManifestEntrypoint
    {
        public string? Command { get; set; } = null;
        public string? Module { get; set; } = null;
        public string Function { get; set; } = "run";
        public string? HandlerType { get; set; } = null;
        public List<string> Args { get; set; } = new List<string>();
        public List<string> EnvironmentAllowList { get; set; } = new List<string>();
        public string? InputSchema { get; set; } = null;
        public string? OutputSchema { get; set; } = null;
        public string? ArgumentSchema { get; set; } = null;
        public Dictionary<string, JsonElement> RuntimeSettings { get; set; } = new Dictionary<string, JsonElement>();
    }
}
