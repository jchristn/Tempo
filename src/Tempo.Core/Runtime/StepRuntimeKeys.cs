namespace Tempo.Core.Runtime
{
    /// <summary>Well-known runtime provider keys.</summary>
    public static class StepRuntimeKeys
    {
        public static readonly RuntimeKey BuiltinClass = new RuntimeKey("Builtin.Class");
        public static readonly RuntimeKey BuiltinMethod = new RuntimeKey("Builtin.Method");
        public static readonly RuntimeKey BuiltinUnknown = new RuntimeKey("Builtin.Unknown");
        public static readonly RuntimeKey ExternalRest = new RuntimeKey("External.Rest");
        public static readonly RuntimeKey LegacyInlineRest = new RuntimeKey("Legacy.InlineRest");
        public static readonly RuntimeKey ArtifactProcess = new RuntimeKey("Artifact.Process");
        public static readonly RuntimeKey ArtifactPython = new RuntimeKey("Artifact.Python");
        public static readonly RuntimeKey ArtifactJavaScript = new RuntimeKey("Artifact.JavaScript");
        public static readonly RuntimeKey ArtifactDotnetProcess = new RuntimeKey("Artifact.DotnetProcess");
        public static readonly RuntimeKey HostExecutable = new RuntimeKey("Host.Executable");
    }
}
