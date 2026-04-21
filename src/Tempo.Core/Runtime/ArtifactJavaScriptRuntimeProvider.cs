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
    using Tempo.Runners;

    /// <summary>Runtime provider for Artifact.JavaScript.</summary>
    public class ArtifactJavaScriptRuntimeProvider : IStepRuntimeProvider
    {
        private readonly StepRuntimeDescriptor _Descriptor;
        private readonly DatabaseDriverBase? _Database;
        private readonly IArtifactBlobStore? _BlobStore;
        private readonly ExternalExecutionSettings _Settings;
        private readonly ExternalRuntimeCapacityManager? _Capacity;

        public ArtifactJavaScriptRuntimeProvider(
            StepRuntimeAvailabilityStateEnum availability,
            string securityNotes,
            ExternalExecutionSettings settings,
            DatabaseDriverBase? database = null,
            IArtifactBlobStore? blobStore = null,
            ExternalRuntimeCapacityManager? capacity = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database;
            _BlobStore = blobStore;
            _Capacity = capacity;
            _Descriptor = new StepRuntimeDescriptor
            {
                RuntimeKey = StepRuntimeKeys.ArtifactJavaScript,
                DisplayName = "Artifact JavaScript",
                Description = "Uploaded JavaScript artifact executed through a Node.js Tempo protocol envelope.",
                PackagingType = StepPackagingTypeEnum.Artifact,
                Availability = availability,
                SupportsArtifacts = true,
                SupportsVersioning = true,
                SecurityNotes = securityNotes,
                ConfigTypeName = nameof(ArtifactJavaScriptRuntimeConfig),
                SupportedContractTypes = new List<StepContractTypeEnum> { StepContractTypeEnum.Loose, StepContractTypeEnum.Schema },
                ConfigProperties = new List<StepRuntimeConfigPropertyDescriptor>
                {
                    Prop("artifactId", "string", true),
                    Prop("artifactVersion", "string", false),
                    Prop("entrypoint", "string", false),
                    Prop("module", "string", false),
                    Prop("function", "string", false),
                    Prop("arguments", "array", false),
                    Prop("environmentReferences", "array", false)
                }
            };
        }

        public RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactJavaScript;
        public Type ConfigType => typeof(ArtifactJavaScriptRuntimeConfig);
        public StepRuntimeDescriptor Describe() => _Descriptor;

        public async Task<StepConfigValidationResult> ValidateAsync(StepRuntimeValidationContext context, CancellationToken token = default)
        {
            if (_Descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available)
                return StepConfigValidationResult.Failure(new[] { "Runtime '" + RuntimeKey + "' is not available: " + _Descriptor.Availability + ". " + _Descriptor.SecurityNotes });
            if (context.Config is not ArtifactJavaScriptRuntimeConfig config)
                return StepConfigValidationResult.Failure(new[] { "config type must be ArtifactJavaScriptRuntimeConfig." });
            List<string> errors = new List<string>(config.Validate());
            await ArtifactRuntimePlan.AddArtifactReferenceValidationErrorsAsync(_Database, context.TenantId, config.ArtifactId, errors, token).ConfigureAwait(false);
            return errors.Count == 0 ? StepConfigValidationResult.Success() : StepConfigValidationResult.Failure(errors);
        }

        public async Task<StepRunner> CreateRunnerAsync(StepExecutionContext context, StepRecord step, StepRuntimeConfig config, CancellationToken token = default)
        {
            if (_Descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available)
                throw new NotSupportedException("Runtime '" + RuntimeKey + "' is not available: " + _Descriptor.Availability + ". " + _Descriptor.SecurityNotes);
            if (_Database == null || _BlobStore == null || _Capacity == null)
                throw new NotSupportedException("Artifact JavaScript runtime requires database, artifact blob store, and capacity manager services.");
            if (config is not ArtifactJavaScriptRuntimeConfig jsConfig)
                throw new ArgumentException("config type must be ArtifactJavaScriptRuntimeConfig.", nameof(config));

            ArtifactProcessRuntimeConfig processShape = new ArtifactProcessRuntimeConfig
            {
                ArtifactId = jsConfig.ArtifactId,
                ArtifactVersion = jsConfig.ArtifactVersion,
                Entrypoint = jsConfig.Entrypoint,
                Arguments = new List<string>(jsConfig.Arguments),
                EnvironmentReferences = new List<string>(jsConfig.EnvironmentReferences)
            };
            ArtifactRuntimePlan plan = await ArtifactRuntimePlan.ResolveAsync(_Database, _BlobStore, _Settings, context, step, processShape, RuntimeKey, token).ConfigureAwait(false);
            ArtifactManifestEntrypoint entry = plan.Entrypoint;
            string module = string.IsNullOrWhiteSpace(jsConfig.Module) ? entry.Module ?? string.Empty : jsConfig.Module!;
            string function = string.IsNullOrWhiteSpace(jsConfig.Function) ? entry.Function : jsConfig.Function;
            if (string.IsNullOrWhiteSpace(module)) throw new InvalidOperationException("Artifact.JavaScript entrypoint requires module.");
            if (string.IsNullOrWhiteSpace(function)) throw new InvalidOperationException("Artifact.JavaScript entrypoint requires function.");

            List<string> args = new List<string>(entry.Args);
            args.AddRange(jsConfig.Arguments);
            List<string> env = MergeEnvironment(jsConfig.EnvironmentReferences, plan.Manifest.EnvironmentAllowList, entry.EnvironmentAllowList);
            return new ArtifactJavaScriptStepRunner(context.TenantId, plan.Artifact, plan.ArtifactRoot, plan.EntrypointName, _Settings.NodeExecutable, module, function, args, env, _Settings, _Capacity, step.MaxRuntimeMs);
        }

        private static List<string> MergeEnvironment(IEnumerable<string> requested, IEnumerable<string> manifestAllowed, IEnumerable<string> entryAllowed)
        {
            HashSet<string> allowed = new HashSet<string>(manifestAllowed ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (string name in entryAllowed ?? Array.Empty<string>()) allowed.Add(name);
            if (allowed.Count == 0) return new List<string>();
            return (requested ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n) && allowed.Contains(n))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static StepRuntimeConfigPropertyDescriptor Prop(string name, string type, bool required)
        {
            return new StepRuntimeConfigPropertyDescriptor { Name = name, Type = type, Required = required };
        }
    }
}
