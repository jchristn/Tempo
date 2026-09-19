namespace Tempo.Core.Runtime
{
    /// <summary>Well-known runtime provider keys.</summary>
    public static class StepRuntimeKeys
    {
        /// <summary>Runtime key for built-in class-based steps.</summary>
        public static readonly RuntimeKey BuiltinClass = new RuntimeKey("Builtin.Class");
        /// <summary>Runtime key for built-in attribute-based method steps.</summary>
        public static readonly RuntimeKey BuiltinMethod = new RuntimeKey("Builtin.Method");
        /// <summary>Runtime key for unresolved built-in steps awaiting reconciliation.</summary>
        public static readonly RuntimeKey BuiltinUnknown = new RuntimeKey("Builtin.Unknown");
        /// <summary>Runtime key for external REST steps.</summary>
        public static readonly RuntimeKey ExternalRest = new RuntimeKey("External.Rest");
        /// <summary>Runtime key for legacy inline REST steps.</summary>
        public static readonly RuntimeKey LegacyInlineRest = new RuntimeKey("Legacy.InlineRest");
        /// <summary>Runtime key for artifact-rooted process steps.</summary>
        public static readonly RuntimeKey ArtifactProcess = new RuntimeKey("Artifact.Process");
        /// <summary>Runtime key for artifact-rooted Python steps.</summary>
        public static readonly RuntimeKey ArtifactPython = new RuntimeKey("Artifact.Python");
        /// <summary>Runtime key for artifact-rooted JavaScript steps.</summary>
        public static readonly RuntimeKey ArtifactJavaScript = new RuntimeKey("Artifact.JavaScript");
        /// <summary>Runtime key for artifact-rooted .NET process steps.</summary>
        public static readonly RuntimeKey ArtifactDotnetProcess = new RuntimeKey("Artifact.DotnetProcess");
        /// <summary>Runtime key for host executable steps.</summary>
        public static readonly RuntimeKey HostExecutable = new RuntimeKey("Host.Executable");
    }
}
