namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;

    /// <summary>Base type for typed runtime configuration DTOs.</summary>
    public abstract class StepRuntimeConfig
    {
        /// <summary>Runtime key for the concrete configuration.</summary>
        [JsonIgnore]
        public abstract RuntimeKey RuntimeKey { get; }

        /// <summary>Validate the configuration.</summary>
        public virtual IReadOnlyList<string> Validate()
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Configuration for a registered class-based built-in step.</summary>
    public sealed class BuiltinClassRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.BuiltinClass;
        public string? Identifier { get; set; }
        public string? TypeName { get; set; }
        public string? AssemblyName { get; set; }
        public string? AssemblyVersion { get; set; }
        public string? SignatureHash { get; set; }
        public override IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    /// <summary>Compatibility marker for legacy code steps before reconciliation.</summary>
    public sealed class BuiltinUnknownRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.BuiltinUnknown;
        public string? Identifier { get; set; }
        public override IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    /// <summary>Configuration for a registered method-based built-in step.</summary>
    public sealed class BuiltinMethodRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.BuiltinMethod;
        public string? Identifier { get; set; }
        public string? DeclaringType { get; set; }
        public string? MethodName { get; set; }
        public string? AssemblyName { get; set; }
        public string? AssemblyVersion { get; set; }
        public string? SignatureHash { get; set; }

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(MethodName)) errors.Add("methodName is required.");
            return errors;
        }
    }

    /// <summary>Configuration for persisted REST steps.</summary>
    public sealed class ExternalRestRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ExternalRest;
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public int TimeoutMs { get; set; } = 30000;

        public static ExternalRestRuntimeConfig FromLegacy(Tempo.RestStepConfiguration rest)
        {
            if (rest == null) throw new ArgumentNullException(nameof(rest));
            return new ExternalRestRuntimeConfig
            {
                Method = rest.Method,
                Url = rest.Url,
                Headers = new Dictionary<string, string>(rest.Headers),
                TimeoutMs = rest.TimeoutMs
            };
        }

        public Tempo.RestStepConfiguration ToLegacy()
        {
            return new Tempo.RestStepConfiguration
            {
                Method = Method,
                Url = Url,
                Headers = new Dictionary<string, string>(Headers),
                TimeoutMs = TimeoutMs
            };
        }

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Method)) errors.Add("method is required.");
            if (string.IsNullOrWhiteSpace(Url)) errors.Add("url is required.");
            if (TimeoutMs <= 0) errors.Add("timeoutMs must be greater than 0.");
            return errors;
        }
    }

    /// <summary>Read-path compatibility configuration for inline REST flow transitions.</summary>
    public sealed class LegacyInlineRestRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.LegacyInlineRest;
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public int TimeoutMs { get; set; } = 30000;

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Method)) errors.Add("method is required.");
            if (string.IsNullOrWhiteSpace(Url)) errors.Add("url is required.");
            if (TimeoutMs <= 0) errors.Add("timeoutMs must be greater than 0.");
            return errors;
        }
    }

    /// <summary>Configuration for process artifacts.</summary>
    public sealed class ArtifactProcessRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactProcess;
        public string? ArtifactId { get; set; }
        public string? ArtifactVersion { get; set; }
        public string? Entrypoint { get; set; }
        public List<string> Arguments { get; set; } = new List<string>();
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(ArtifactId)) errors.Add("artifactId is required.");
            foreach (string name in EnvironmentReferences)
            {
                if (string.IsNullOrWhiteSpace(name)) errors.Add("environmentReferences cannot contain empty names.");
                if (name.Contains("=")) errors.Add("environmentReferences must contain names only, not values.");
            }
            return errors;
        }
    }

    /// <summary>Configuration for Python artifacts.</summary>
    public sealed class ArtifactPythonRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactPython;
        public string? ArtifactId { get; set; }
        public string? ArtifactVersion { get; set; }
        public string? Entrypoint { get; set; }
        public string? Module { get; set; }
        public string Function { get; set; } = "run";
        public string? PythonVersion { get; set; }
        public List<string> Arguments { get; set; } = new List<string>();
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(ArtifactId)) errors.Add("artifactId is required.");
            if (string.IsNullOrWhiteSpace(Function)) errors.Add("function is required.");
            foreach (string name in EnvironmentReferences)
            {
                if (string.IsNullOrWhiteSpace(name)) errors.Add("environmentReferences cannot contain empty names.");
                if (name.Contains("=")) errors.Add("environmentReferences must contain names only, not values.");
            }
            return errors;
        }
    }

    /// <summary>Configuration for JavaScript artifacts.</summary>
    public sealed class ArtifactJavaScriptRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactJavaScript;
        public string? ArtifactId { get; set; }
        public string? ArtifactVersion { get; set; }
        public string? Entrypoint { get; set; }
        public string? Module { get; set; }
        public string Function { get; set; } = "run";
        public List<string> Arguments { get; set; } = new List<string>();
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(ArtifactId)) errors.Add("artifactId is required.");
            if (string.IsNullOrWhiteSpace(Function)) errors.Add("function is required.");
            foreach (string name in EnvironmentReferences)
            {
                if (string.IsNullOrWhiteSpace(name)) errors.Add("environmentReferences cannot contain empty names.");
                if (name.Contains("=")) errors.Add("environmentReferences must contain names only, not values.");
            }
            return errors;
        }
    }

    /// <summary>Configuration for .NET process artifacts.</summary>
    public sealed class ArtifactDotnetProcessRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactDotnetProcess;
        public string? ArtifactId { get; set; }
        public string? ArtifactVersion { get; set; }
        public string? Entrypoint { get; set; }
        public List<string> Arguments { get; set; } = new List<string>();
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(ArtifactId)) errors.Add("artifactId is required.");
            foreach (string name in EnvironmentReferences)
            {
                if (string.IsNullOrWhiteSpace(name)) errors.Add("environmentReferences cannot contain empty names.");
                if (name.Contains("=")) errors.Add("environmentReferences must contain names only, not values.");
            }
            return errors;
        }
    }

    /// <summary>Configuration for operator allowlisted host executables.</summary>
    public sealed class HostExecutableRuntimeConfig : StepRuntimeConfig
    {
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.HostExecutable;
        public string? AllowListKey { get; set; }
        public List<string> Arguments { get; set; } = new List<string>();

        public override IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(AllowListKey)) errors.Add("allowListKey is required.");
            if (!string.IsNullOrWhiteSpace(AllowListKey) && ContainsUnsafeKeyCharacter(AllowListKey!)) errors.Add("allowListKey contains invalid characters.");
            foreach (string? arg in Arguments)
            {
                if (arg == null) { errors.Add("arguments cannot contain null values."); continue; }
                if (arg.Any(char.IsControl)) errors.Add("arguments cannot contain control characters.");
            }
            return errors;
        }

        private static bool ContainsUnsafeKeyCharacter(string value)
        {
            foreach (char c in value)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok) return true;
            }

            return false;
        }
    }
}
