namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Settings;
    using Tempo.Logs;
    using Tempo.Runners;

    /// <summary>Registry of known step runtime providers.</summary>
    public class StepRuntimeRegistry
    {
        private readonly Dictionary<RuntimeKey, IStepRuntimeProvider> _Providers = new Dictionary<RuntimeKey, IStepRuntimeProvider>();

        /// <summary>Create a registry with the built-in provider set.</summary>
        public static StepRuntimeRegistry CreateDefault(
            StepManager? stepManager = null,
            Logger? logger = null,
            RuntimeSettings? runtimes = null,
            DatabaseDriverBase? database = null,
            IArtifactBlobStore? artifactBlobStore = null,
            ExternalRuntimeCapacityManager? externalCapacity = null)
        {
            ExternalExecutionSettings external = runtimes?.ExternalExecution ?? new ExternalExecutionSettings();
            RuntimeCommandProbeResult pythonProbe = RuntimeCommandProbe.ProbePython(external);
            RuntimeCommandProbeResult nodeProbe = RuntimeCommandProbe.ProbeNode(external);
            RuntimeCommandProbeResult dotnetProbe = RuntimeCommandProbe.ProbeDotnetRuntime(external);
            StepRuntimeAvailabilityStateEnum pythonAvailability = pythonProbe.Available ? StepRuntimeAvailabilityStateEnum.Available : StepRuntimeAvailabilityStateEnum.MissingDependency;
            StepRuntimeAvailabilityStateEnum nodeAvailability = nodeProbe.Available ? StepRuntimeAvailabilityStateEnum.Available : StepRuntimeAvailabilityStateEnum.MissingDependency;
            StepRuntimeAvailabilityStateEnum dotnetAvailability = dotnetProbe.Available ? StepRuntimeAvailabilityStateEnum.Available : StepRuntimeAvailabilityStateEnum.MissingDependency;
            bool hostExecutableEnabled = runtimes?.HostExecutables.Enabled == true;
            StepRuntimeAvailabilityStateEnum hostExecutableAvailability = hostExecutableEnabled
                ? StepRuntimeAvailabilityStateEnum.Available
                : StepRuntimeAvailabilityStateEnum.DisabledBySettings;
            string externalNotes = "Artifact-backed external runtimes are available. Tenant config must remain artifact-rooted.";
            string pythonNotes = pythonProbe.Available
                ? externalNotes + " Python executable: " + external.PythonExecutable + "."
                : "Artifact.Python is unavailable because " + pythonProbe.Message;
            string nodeNotes = nodeProbe.Available
                ? externalNotes + " Node.js executable: " + external.NodeExecutable + "."
                : "Artifact.JavaScript is unavailable because " + nodeProbe.Message;
            string dotnetNotes = dotnetProbe.Available
                ? externalNotes + " .NET executable: " + external.DotnetExecutable + "."
                : "Artifact.DotnetProcess is unavailable because " + dotnetProbe.Message;
            string hostExecutableNotes = hostExecutableEnabled
                ? "Tenant config references an operator allowlist key only, never a host path."
                : "Host executable runtime is disabled by settings.";

            StepRuntimeRegistry registry = new StepRuntimeRegistry();
            registry.Register(new DescriptorStepRuntimeProvider<BuiltinClassRuntimeConfig>(Build(
                StepRuntimeKeys.BuiltinClass,
                "Built-in class",
                "In-process class step registered by the host application.",
                StepPackagingTypeEnum.Builtin,
                StepRuntimeAvailabilityStateEnum.Available,
                false,
                false,
                "Runs inside the Tempo server process.",
                new[] { Prop("identifier", "string", false), Prop("typeName", "string", false), Prop("assemblyName", "string", false), Prop("assemblyVersion", "string", false), Prop("signatureHash", "string", false) }),
                (context, step, config, token) => CreateCodeRunnerAsync(stepManager, context, step, config.Identifier, token)));
            registry.Register(new DescriptorStepRuntimeProvider<BuiltinMethodRuntimeConfig>(Build(
                StepRuntimeKeys.BuiltinMethod,
                "Built-in method",
                "In-process method step discovered from StepMethod attributes.",
                StepPackagingTypeEnum.Builtin,
                StepRuntimeAvailabilityStateEnum.Available,
                false,
                false,
                "Runs inside the Tempo server process.",
                new[] { Prop("identifier", "string", false), Prop("declaringType", "string", false), Prop("methodName", "string", true), Prop("assemblyName", "string", false), Prop("assemblyVersion", "string", false), Prop("signatureHash", "string", false) }),
                (context, step, config, token) => CreateCodeRunnerAsync(stepManager, context, step, config.Identifier, token)));
            registry.Register(new DescriptorStepRuntimeProvider<BuiltinUnknownRuntimeConfig>(Build(
                StepRuntimeKeys.BuiltinUnknown,
                "Unresolved built-in",
                "Compatibility marker for legacy code steps before reconciliation.",
                StepPackagingTypeEnum.Builtin,
                StepRuntimeAvailabilityStateEnum.Preview,
                false,
                false,
                "Cannot execute directly until reconciled to a class or method runtime.",
                new[] { Prop("identifier", "string", false) }),
                (context, step, config, token) => CreateCodeRunnerAsync(stepManager, context, step, config.Identifier, token)));
            registry.Register(new DescriptorStepRuntimeProvider<ExternalRestRuntimeConfig>(Build(
                StepRuntimeKeys.ExternalRest,
                "External REST",
                "HTTP request step executed through Tempo's REST runner.",
                StepPackagingTypeEnum.External,
                StepRuntimeAvailabilityStateEnum.Available,
                false,
                false,
                "Requests leave the Tempo server process and may cross trust boundaries.",
                new[] { Prop("method", "string", true), Prop("url", "string", true), Prop("headers", "object", false), Prop("timeoutMs", "integer", true) }),
                (context, step, config, token) => Task.FromResult<StepRunner>(RestStepRunner.FromConfig(config.ToLegacy(), logger!))));
            registry.Register(new DescriptorStepRuntimeProvider<LegacyInlineRestRuntimeConfig>(Build(
                StepRuntimeKeys.LegacyInlineRest,
                "Legacy inline REST",
                "Read-path compatibility for REST configuration embedded in flow transitions.",
                StepPackagingTypeEnum.External,
                StepRuntimeAvailabilityStateEnum.Preview,
                false,
                false,
                "Compatibility runtime only; new flows should use persisted REST steps.",
                new[] { Prop("method", "string", true), Prop("url", "string", true), Prop("headers", "object", false), Prop("timeoutMs", "integer", true) }),
                (context, step, config, token) => Task.FromResult<StepRunner>(RestStepRunner.FromConfig(ToLegacyRest(config), logger!))));
            registry.Register(new ArtifactProcessRuntimeProvider(StepRuntimeAvailabilityStateEnum.Available, externalNotes, external, database, artifactBlobStore, externalCapacity));
            registry.Register(new ArtifactPythonRuntimeProvider(pythonAvailability, pythonNotes, external, database, artifactBlobStore, externalCapacity));
            registry.Register(new ArtifactJavaScriptRuntimeProvider(nodeAvailability, nodeNotes, external, database, artifactBlobStore, externalCapacity));
            registry.Register(new ArtifactDotnetProcessRuntimeProvider(dotnetAvailability, dotnetNotes, external, database, artifactBlobStore, externalCapacity));
            registry.Register(new HostExecutableRuntimeProvider(
                hostExecutableAvailability,
                hostExecutableNotes,
                external,
                runtimes?.HostExecutables ?? new HostExecutableSettings(),
                externalCapacity));
            return registry;
        }

        /// <summary>Register a provider.</summary>
        public void Register(IStepRuntimeProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (_Providers.ContainsKey(provider.RuntimeKey)) throw new InvalidOperationException("Runtime provider already registered: " + provider.RuntimeKey);
            _Providers.Add(provider.RuntimeKey, provider);
        }

        /// <summary>All registered providers.</summary>
        public IReadOnlyList<IStepRuntimeProvider> Providers => _Providers.Values.ToList();

        /// <summary>All runtime descriptors.</summary>
        public IReadOnlyList<StepRuntimeDescriptor> DescribeAll()
        {
            return _Providers.Values.Select(p => p.Describe()).OrderBy(d => d.RuntimeKey.ToString(), StringComparer.Ordinal).ToList();
        }

        /// <summary>Read a provider by key.</summary>
        public IStepRuntimeProvider? Get(RuntimeKey runtimeKey)
        {
            return _Providers.TryGetValue(runtimeKey, out IStepRuntimeProvider? provider) ? provider : null;
        }

        /// <summary>Read the concrete config type for a key.</summary>
        public Type? GetConfigType(RuntimeKey runtimeKey)
        {
            return Get(runtimeKey)?.ConfigType;
        }

        /// <summary>Validate a typed runtime config against a requested runtime key.</summary>
        public async Task<StepConfigValidationResult> ValidateAsync(string tenantId, RuntimeKey runtimeKey, StepRuntimeConfig? config, CancellationToken token = default)
        {
            IStepRuntimeProvider? provider = Get(runtimeKey);
            if (provider == null) return StepConfigValidationResult.Failure(new[] { "Unknown runtime key: " + runtimeKey });
            if (config == null) return StepConfigValidationResult.Failure(new[] { "config is required." });
            if (config.RuntimeKey != runtimeKey) return StepConfigValidationResult.Failure(new[] { "config runtime key '" + config.RuntimeKey + "' does not match requested runtime key '" + runtimeKey + "'." });
            return await provider.ValidateAsync(new StepRuntimeValidationContext { TenantId = tenantId, RuntimeKey = runtimeKey, Config = config }, token).ConfigureAwait(false);
        }

        private static StepRuntimeDescriptor Build(RuntimeKey key, string displayName, string description, StepPackagingTypeEnum packagingType, StepRuntimeAvailabilityStateEnum availability, bool artifactSupport, bool versioningSupport, string securityNotes, IEnumerable<StepRuntimeConfigPropertyDescriptor> properties)
        {
            return new StepRuntimeDescriptor
            {
                RuntimeKey = key,
                DisplayName = displayName,
                Description = description,
                PackagingType = packagingType,
                Availability = availability,
                SupportsArtifacts = artifactSupport,
                SupportsVersioning = versioningSupport,
                SecurityNotes = securityNotes,
                SupportedContractTypes = new List<StepContractTypeEnum> { StepContractTypeEnum.Loose, StepContractTypeEnum.Schema },
                ConfigProperties = properties.ToList()
            };
        }

        private static StepRuntimeConfigPropertyDescriptor Prop(string name, string type, bool required)
        {
            return new StepRuntimeConfigPropertyDescriptor { Name = name, Type = type, Required = required };
        }

        private static Task<StepRunner> CreateCodeRunnerAsync(StepManager? stepManager, StepExecutionContext context, StepRecord step, string? identifier, CancellationToken token)
        {
            if (stepManager == null) throw new InvalidOperationException("StepManager is required to execute built-in runtimes.");
            string executionKey = string.IsNullOrWhiteSpace(identifier) ? step.ExecutionKey : identifier!;
            StepRunner? runner = stepManager.GetStepRunner(executionKey, context.TenantId);
            if (runner == null) throw new InvalidOperationException("Step '" + executionKey + "' not found in step manager for tenant '" + context.TenantId + "'.");
            return Task.FromResult(runner);
        }

        private static Tempo.RestStepConfiguration ToLegacyRest(LegacyInlineRestRuntimeConfig config)
        {
            return new Tempo.RestStepConfiguration
            {
                Method = config.Method,
                Url = config.Url,
                Headers = new Dictionary<string, string>(config.Headers),
                TimeoutMs = config.TimeoutMs
            };
        }

        private sealed class DescriptorStepRuntimeProvider<TConfig> : IStepRuntimeProvider where TConfig : StepRuntimeConfig
        {
            private readonly StepRuntimeDescriptor _Descriptor;
            private readonly Func<StepExecutionContext, StepRecord, TConfig, CancellationToken, Task<StepRunner>>? _CreateRunner;

            public DescriptorStepRuntimeProvider(
                StepRuntimeDescriptor descriptor,
                Func<StepExecutionContext, StepRecord, TConfig, CancellationToken, Task<StepRunner>>? createRunner = null)
            {
                _Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
                _Descriptor.ConfigTypeName = typeof(TConfig).Name;
                _CreateRunner = createRunner;
            }

            public RuntimeKey RuntimeKey => _Descriptor.RuntimeKey;
            public Type ConfigType => typeof(TConfig);

            public StepRuntimeDescriptor Describe()
            {
                return _Descriptor;
            }

            public Task<StepConfigValidationResult> ValidateAsync(StepRuntimeValidationContext context, CancellationToken token = default)
            {
                if (context == null) throw new ArgumentNullException(nameof(context));
                if (_Descriptor.Availability == StepRuntimeAvailabilityStateEnum.DisabledBySettings)
                    return Task.FromResult(StepConfigValidationResult.Failure(new[] { "Runtime '" + RuntimeKey + "' is disabled by settings." }));
                if (context.Config == null) return Task.FromResult(StepConfigValidationResult.Failure(new[] { "config is required." }));
                if (context.Config is not TConfig) return Task.FromResult(StepConfigValidationResult.Failure(new[] { "config type must be " + typeof(TConfig).Name + "." }));
                IReadOnlyList<string> errors = context.Config.Validate();
                return Task.FromResult(errors.Count == 0 ? StepConfigValidationResult.Success() : StepConfigValidationResult.Failure(errors));
            }

            public Task<StepRunner> CreateRunnerAsync(StepExecutionContext context, StepRecord step, StepRuntimeConfig config, CancellationToken token = default)
            {
                if (_Descriptor.Availability == StepRuntimeAvailabilityStateEnum.DisabledBySettings)
                    throw new NotSupportedException("Runtime '" + RuntimeKey + "' is disabled by settings.");
                if (_CreateRunner == null) throw new NotSupportedException("Runtime runner creation is not implemented for " + RuntimeKey + ".");
                if (config is not TConfig typedConfig) throw new ArgumentException("config type must be " + typeof(TConfig).Name + ".", nameof(config));
                return _CreateRunner(context, step, typedConfig, token);
            }
        }
    }
}
