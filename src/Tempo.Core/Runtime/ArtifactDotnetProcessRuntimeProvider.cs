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

        /// <summary>
        /// Initializes a new instance of the <see cref="ArtifactDotnetProcessRuntimeProvider"/> class.
        /// </summary>
        /// <param name="availability">Availability state of the runtime.</param>
        /// <param name="securityNotes">Operator-facing security notes for the runtime.</param>
        /// <param name="settings">External execution settings. Cannot be null.</param>
        /// <param name="database">Optional database driver used to resolve artifact references.</param>
        /// <param name="blobStore">Optional artifact blob store used to materialize artifacts.</param>
        /// <param name="capacity">Optional external runtime capacity manager.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
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

        /// <summary>Runtime key that uniquely identifies this provider.</summary>
        public RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactDotnetProcess;
        /// <summary>CLR type of the strongly-typed configuration this provider expects.</summary>
        public Type ConfigType => typeof(ArtifactDotnetProcessRuntimeConfig);
        /// <summary>Describes the runtime, including its availability, capabilities, and configuration properties.</summary>
        /// <returns>The runtime descriptor.</returns>
        public StepRuntimeDescriptor Describe() => _Descriptor;

        /// <summary>
        /// Validates the supplied runtime configuration for a step.
        /// </summary>
        /// <param name="context">The validation context, including tenant and configuration.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>The validation result indicating success or the collected errors.</returns>
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

        /// <summary>
        /// Creates a step runner for executing a step with the supplied configuration.
        /// </summary>
        /// <param name="context">The step execution context.</param>
        /// <param name="step">The step record being executed.</param>
        /// <param name="config">The runtime configuration for the step.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>A step runner capable of executing the step.</returns>
        /// <exception cref="NotSupportedException">Thrown when the runtime is unavailable or required services are missing.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="config"/> is not the expected configuration type.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the resolved entrypoint is missing required values.</exception>
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
