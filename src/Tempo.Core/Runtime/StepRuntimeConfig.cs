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
        /// <summary>Runtime key identifying this configuration as a class-based built-in step.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.BuiltinClass;

        /// <summary>Identifier of the registered step. May be null.</summary>
        public string? Identifier { get; set; }

        /// <summary>Fully qualified type name of the step class. May be null.</summary>
        public string? TypeName { get; set; }

        /// <summary>Name of the assembly containing the step class. May be null.</summary>
        public string? AssemblyName { get; set; }

        /// <summary>Version of the assembly containing the step class. May be null.</summary>
        public string? AssemblyVersion { get; set; }

        /// <summary>Signature hash used to detect changes to the step class. May be null.</summary>
        public string? SignatureHash { get; set; }

        /// <summary>Validate the configuration.</summary>
        /// <returns>An empty list; this configuration has no validation rules.</returns>
        public override IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    /// <summary>Compatibility marker for legacy code steps before reconciliation.</summary>
    public sealed class BuiltinUnknownRuntimeConfig : StepRuntimeConfig
    {
        /// <summary>Runtime key identifying this configuration as an unknown built-in step.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.BuiltinUnknown;

        /// <summary>Identifier of the legacy step. May be null.</summary>
        public string? Identifier { get; set; }

        /// <summary>Validate the configuration.</summary>
        /// <returns>An empty list; this configuration has no validation rules.</returns>
        public override IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    /// <summary>Configuration for a registered method-based built-in step.</summary>
    public sealed class BuiltinMethodRuntimeConfig : StepRuntimeConfig
    {
        /// <summary>Runtime key identifying this configuration as a method-based built-in step.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.BuiltinMethod;

        /// <summary>Identifier of the registered step. May be null.</summary>
        public string? Identifier { get; set; }

        /// <summary>Fully qualified name of the type declaring the step method. May be null.</summary>
        public string? DeclaringType { get; set; }

        /// <summary>Name of the step method. May be null.</summary>
        public string? MethodName { get; set; }

        /// <summary>Name of the assembly containing the step method. May be null.</summary>
        public string? AssemblyName { get; set; }

        /// <summary>Version of the assembly containing the step method. May be null.</summary>
        public string? AssemblyVersion { get; set; }

        /// <summary>Signature hash used to detect changes to the step method. May be null.</summary>
        public string? SignatureHash { get; set; }

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
        /// <summary>Runtime key identifying this configuration as an external REST step.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ExternalRest;

        /// <summary>HTTP method to use for the request. Default: "GET".</summary>
        public string Method { get; set; } = "GET";

        /// <summary>Target URL for the request. Default: empty string.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>HTTP headers to send with the request. Default: empty dictionary.</summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>Request timeout in milliseconds. Default: 30000 (30 seconds).</summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>Create an <see cref="ExternalRestRuntimeConfig"/> from a legacy REST step configuration.</summary>
        /// <param name="rest">The legacy REST step configuration to convert.</param>
        /// <returns>A new <see cref="ExternalRestRuntimeConfig"/> populated from the legacy configuration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rest"/> is null.</exception>
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

        /// <summary>Convert this configuration to a legacy REST step configuration.</summary>
        /// <returns>A new <see cref="Tempo.RestStepConfiguration"/> populated from this configuration.</returns>
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

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
        /// <summary>Runtime key identifying this configuration as a legacy inline REST step.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.LegacyInlineRest;

        /// <summary>HTTP method to use for the request. Default: "GET".</summary>
        public string Method { get; set; } = "GET";

        /// <summary>Target URL for the request. Default: empty string.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>HTTP headers to send with the request. Default: empty dictionary.</summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>Request timeout in milliseconds. Default: 30000 (30 seconds).</summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
        /// <summary>Runtime key identifying this configuration as a process artifact.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactProcess;

        /// <summary>Identifier of the artifact to execute. May be null.</summary>
        public string? ArtifactId { get; set; }

        /// <summary>Version of the artifact to execute. May be null.</summary>
        public string? ArtifactVersion { get; set; }

        /// <summary>Entrypoint within the artifact to execute. May be null.</summary>
        public string? Entrypoint { get; set; }

        /// <summary>Command-line arguments passed to the artifact. Default: empty list.</summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>Names of environment variables referenced by the artifact. Default: empty list.</summary>
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
        /// <summary>Runtime key identifying this configuration as a Python artifact.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactPython;

        /// <summary>Identifier of the artifact to execute. May be null.</summary>
        public string? ArtifactId { get; set; }

        /// <summary>Version of the artifact to execute. May be null.</summary>
        public string? ArtifactVersion { get; set; }

        /// <summary>Entrypoint within the artifact to execute. May be null.</summary>
        public string? Entrypoint { get; set; }

        /// <summary>Python module containing the function to invoke. May be null.</summary>
        public string? Module { get; set; }

        /// <summary>Name of the Python function to invoke. Default: "run".</summary>
        public string Function { get; set; } = "run";

        /// <summary>Python version required to execute the artifact. May be null.</summary>
        public string? PythonVersion { get; set; }

        /// <summary>Command-line arguments passed to the artifact. Default: empty list.</summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>Names of environment variables referenced by the artifact. Default: empty list.</summary>
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
        /// <summary>Runtime key identifying this configuration as a JavaScript artifact.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactJavaScript;

        /// <summary>Identifier of the artifact to execute. May be null.</summary>
        public string? ArtifactId { get; set; }

        /// <summary>Version of the artifact to execute. May be null.</summary>
        public string? ArtifactVersion { get; set; }

        /// <summary>Entrypoint within the artifact to execute. May be null.</summary>
        public string? Entrypoint { get; set; }

        /// <summary>JavaScript module containing the function to invoke. May be null.</summary>
        public string? Module { get; set; }

        /// <summary>Name of the JavaScript function to invoke. Default: "run".</summary>
        public string Function { get; set; } = "run";

        /// <summary>Command-line arguments passed to the artifact. Default: empty list.</summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>Names of environment variables referenced by the artifact. Default: empty list.</summary>
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
        /// <summary>Runtime key identifying this configuration as a .NET process artifact.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactDotnetProcess;

        /// <summary>Identifier of the artifact to execute. May be null.</summary>
        public string? ArtifactId { get; set; }

        /// <summary>Version of the artifact to execute. May be null.</summary>
        public string? ArtifactVersion { get; set; }

        /// <summary>Entrypoint within the artifact to execute. May be null.</summary>
        public string? Entrypoint { get; set; }

        /// <summary>Command-line arguments passed to the artifact. Default: empty list.</summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>Names of environment variables referenced by the artifact. Default: empty list.</summary>
        public List<string> EnvironmentReferences { get; set; } = new List<string>();

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
        /// <summary>Runtime key identifying this configuration as a host executable step.</summary>
        [JsonIgnore]
        public override RuntimeKey RuntimeKey => StepRuntimeKeys.HostExecutable;

        /// <summary>Key referencing the allowlisted host executable to run. May be null.</summary>
        public string? AllowListKey { get; set; }

        /// <summary>Command-line arguments passed to the executable. Default: empty list.</summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>Validate the configuration.</summary>
        /// <returns>A list of validation errors; empty when the configuration is valid.</returns>
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
