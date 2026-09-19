namespace Tempo.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>Artifact package manifest metadata.</summary>
    public class ArtifactManifest
    {
        /// <summary>Version of the manifest format. Default: "1".</summary>
        public string ManifestVersion { get; set; } = "1";

        /// <summary>Optional key identifying the runtime for the artifact. Default: null.</summary>
        public string? RuntimeKey { get; set; } = null;

        /// <summary>Optional protocol version the artifact targets. Default: null.</summary>
        public string? ProtocolVersion { get; set; } = null;

        /// <summary>Protocol versions supported by the artifact. Default: empty list.</summary>
        public List<string> SupportedProtocolVersions { get; set; } = new List<string>();

        /// <summary>Name of the entrypoint used when none is specified. Default: "default".</summary>
        public string DefaultEntrypoint { get; set; } = "default";

        /// <summary>Named executable entrypoints defined by the artifact. Default: empty dictionary.</summary>
        public Dictionary<string, ArtifactManifestEntrypoint> Entrypoints { get; set; } = new Dictionary<string, ArtifactManifestEntrypoint>();

        /// <summary>Environment variable names permitted for the artifact. Default: empty list.</summary>
        public List<string> EnvironmentAllowList { get; set; } = new List<string>();

        /// <summary>Optional JSON schema describing the artifact's input. Default: null.</summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>Optional JSON schema describing the artifact's output. Default: null.</summary>
        public string? OutputSchema { get; set; } = null;

        /// <summary>Runtime-specific settings for the artifact. Default: empty dictionary.</summary>
        public Dictionary<string, JsonElement> RuntimeSettings { get; set; } = new Dictionary<string, JsonElement>();

        /// <summary>Arbitrary metadata key/value pairs. Default: empty dictionary.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>One named executable entrypoint inside an artifact manifest.</summary>
    public class ArtifactManifestEntrypoint
    {
        /// <summary>Optional command to execute for the entrypoint. Default: null.</summary>
        public string? Command { get; set; } = null;

        /// <summary>Optional module containing the entrypoint. Default: null.</summary>
        public string? Module { get; set; } = null;

        /// <summary>Function within the module to invoke. Default: "run".</summary>
        public string Function { get; set; } = "run";

        /// <summary>Optional handler type for the entrypoint. Default: null.</summary>
        public string? HandlerType { get; set; } = null;

        /// <summary>Command-line arguments passed to the entrypoint. Default: empty list.</summary>
        public List<string> Args { get; set; } = new List<string>();

        /// <summary>Environment variable names permitted for the entrypoint. Default: empty list.</summary>
        public List<string> EnvironmentAllowList { get; set; } = new List<string>();

        /// <summary>Optional JSON schema describing the entrypoint's input. Default: null.</summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>Optional JSON schema describing the entrypoint's output. Default: null.</summary>
        public string? OutputSchema { get; set; } = null;

        /// <summary>Optional JSON schema describing the entrypoint's arguments. Default: null.</summary>
        public string? ArgumentSchema { get; set; } = null;

        /// <summary>Runtime-specific settings for the entrypoint. Default: empty dictionary.</summary>
        public Dictionary<string, JsonElement> RuntimeSettings { get; set; } = new Dictionary<string, JsonElement>();
    }
}
