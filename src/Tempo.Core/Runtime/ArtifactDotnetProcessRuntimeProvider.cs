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

    /// <summary>Runtime provider for Artifact.DotnetProcess.</summary>
    public class ArtifactDotnetProcessRuntimeProvider : IStepRuntimeProvider
    {
        private readonly StepRuntimeDescriptor _Descriptor;
        private readonly DatabaseDriverBase? _Database;
        private readonly IArtifactBlobStore? _BlobStore;
        private readonly ExternalExecutionSettings _Settings;
        private readonly ExternalRuntimeCapacityManager? _Capacity;

        public ArtifactDotnetProcessRuntimeProvider(
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
                RuntimeKey = StepRuntimeKeys.ArtifactDotnetProcess,
                DisplayName = "Artifact .NET process",
                Description = "Uploaded .NET artifact executed out of process through the Tempo SDK handler contract.",
                PackagingType = StepPackagingTypeEnum.Artifact,
                Availability = availability,
                SupportsArtifacts = true,
                SupportsVersioning = true,
                SecurityNotes = securityNotes,
                ConfigTypeName = nameof(ArtifactDotnetProcessRuntimeConfig),
                SupportedContractTypes = new List<StepContractTypeEnum> { StepContractTypeEnum.Loose, StepContractTypeEnum.Schema },
                ConfigProperties = new List<StepRuntimeConfigPropertyDescriptor>
                {
                    Prop("artifactId", "string", true),
                    Prop("artifactVersion", "string", false),
                    Prop("entrypoint", "string", false),
                    Prop("arguments", "array", false),
                    Prop("environmentReferences", "array", false)
                }
            };
        }

        public RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactDotnetProcess;
        public Type ConfigType => typeof(ArtifactDotnetProcessRuntimeConfig);
        public StepRuntimeDescriptor Describe() => _Descriptor;

        public async Task<StepConfigValidationResult> ValidateAsync(StepRuntimeValidationContext context, CancellationToken token = default)
        {
            if (_Descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available)
                return StepConfigValidationResult.Failure(new[] { "Runtime '" + RuntimeKey + "' is not available: " + _Descriptor.Availability + ". " + _Descriptor.SecurityNotes });
            if (context.Config is not ArtifactDotnetProcessRuntimeConfig config)
                return StepConfigValidationResult.Failure(new[] { "config type must be ArtifactDotnetProcessRuntimeConfig." });
            List<string> errors = new List<string>(config.Validate());
            await ArtifactRuntimePlan.AddArtifactReferenceValidationErrorsAsync(_Database, context.TenantId, config.ArtifactId, errors, token).ConfigureAwait(false);
            return errors.Count == 0 ? StepConfigValidationResult.Success() : StepConfigValidationResult.Failure(errors);
        }

        public async Task<StepRunner> CreateRunnerAsync(StepExecutionContext context, StepRecord step, StepRuntimeConfig config, CancellationToken token = default)
        {
            if (_Descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available)
                throw new NotSupportedException("Runtime '" + RuntimeKey + "' is not available: " + _Descriptor.Availability + ". " + _Descriptor.SecurityNotes);
            if (_BlobStore == null || _Capacity == null)
                throw new NotSupportedException("Artifact .NET process runtime requires artifact blob store and capacity manager services.");
            if (config is not ArtifactDotnetProcessRuntimeConfig dotnetConfig)
                throw new ArgumentException("config type must be ArtifactDotnetProcessRuntimeConfig.", nameof(config));

            ArtifactProcessRuntimeConfig processShape = new ArtifactProcessRuntimeConfig
            {
                ArtifactId = dotnetConfig.ArtifactId,
                ArtifactVersion = dotnetConfig.ArtifactVersion,
                Entrypoint = dotnetConfig.Entrypoint,
                Arguments = new List<string>(dotnetConfig.Arguments),
                EnvironmentReferences = new List<string>(dotnetConfig.EnvironmentReferences)
            };
            ArtifactRuntimePlan plan = _Database != null
                ? await ArtifactRuntimePlan.ResolveAsync(_Database, _BlobStore, _Settings, context, step, processShape, RuntimeKey, token).ConfigureAwait(false)
                : await ArtifactRuntimePlan.ResolveAsync(_BlobStore, _Settings, context, step, processShape, RuntimeKey, token).ConfigureAwait(false);
            ArtifactManifestEntrypoint entrypoint = plan.Entrypoint;
            string command = entrypoint.Command ?? throw new InvalidOperationException("Artifact.DotnetProcess entrypoint requires command.");
            if (string.IsNullOrWhiteSpace(entrypoint.HandlerType)) throw new InvalidOperationException("Artifact.DotnetProcess entrypoint requires handlerType.");
            List<string> args = new List<string>(entrypoint.Args);
            args.AddRange(dotnetConfig.Arguments);
            List<string> env = MergeEnvironment(dotnetConfig.EnvironmentReferences, plan.Manifest.EnvironmentAllowList, entrypoint.EnvironmentAllowList);
            return new ArtifactProcessStepRunner(context.TenantId, plan.Artifact, plan.ArtifactRoot, plan.EntrypointName, command, args, env, _Settings, _Capacity, context.RunLogSession, context.RunLogStep, step.MaxRuntimeMs);
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
