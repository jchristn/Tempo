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

    /// <summary>Runtime provider for Artifact.Python.</summary>
    public class ArtifactPythonRuntimeProvider : IStepRuntimeProvider
    {
        private readonly StepRuntimeDescriptor _Descriptor;
        private readonly DatabaseDriverBase? _Database;
        private readonly IArtifactBlobStore? _BlobStore;
        private readonly ExternalExecutionSettings _Settings;
        private readonly ExternalRuntimeCapacityManager? _Capacity;

        public ArtifactPythonRuntimeProvider(
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
                RuntimeKey = StepRuntimeKeys.ArtifactPython,
                DisplayName = "Artifact Python",
                Description = "Uploaded Python artifact executed through the Tempo SDK envelope.",
                PackagingType = StepPackagingTypeEnum.Artifact,
                Availability = availability,
                SupportsArtifacts = true,
                SupportsVersioning = true,
                SecurityNotes = securityNotes,
                ConfigTypeName = nameof(ArtifactPythonRuntimeConfig),
                SupportedContractTypes = new List<StepContractTypeEnum> { StepContractTypeEnum.Loose, StepContractTypeEnum.Schema },
                ConfigProperties = new List<StepRuntimeConfigPropertyDescriptor>
                {
                    Prop("artifactId", "string", true),
                    Prop("artifactVersion", "string", false),
                    Prop("entrypoint", "string", false),
                    Prop("module", "string", false),
                    Prop("function", "string", false),
                    Prop("pythonVersion", "string", false),
                    Prop("arguments", "array", false),
                    Prop("environmentReferences", "array", false)
                }
            };
        }

        public RuntimeKey RuntimeKey => StepRuntimeKeys.ArtifactPython;
        public Type ConfigType => typeof(ArtifactPythonRuntimeConfig);
        public StepRuntimeDescriptor Describe() => _Descriptor;

        public async Task<StepConfigValidationResult> ValidateAsync(StepRuntimeValidationContext context, CancellationToken token = default)
        {
            if (_Descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available)
                return StepConfigValidationResult.Failure(new[] { "Runtime '" + RuntimeKey + "' is not available: " + _Descriptor.Availability + ". " + _Descriptor.SecurityNotes });
            if (context.Config is not ArtifactPythonRuntimeConfig config)
                return StepConfigValidationResult.Failure(new[] { "config type must be ArtifactPythonRuntimeConfig." });
            List<string> errors = new List<string>(config.Validate());
            await ArtifactRuntimePlan.AddArtifactReferenceValidationErrorsAsync(_Database, context.TenantId, config.ArtifactId, errors, token).ConfigureAwait(false);
            return errors.Count == 0 ? StepConfigValidationResult.Success() : StepConfigValidationResult.Failure(errors);
        }

        public async Task<StepRunner> CreateRunnerAsync(StepExecutionContext context, StepRecord step, StepRuntimeConfig config, CancellationToken token = default)
        {
            if (_Descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available)
                throw new NotSupportedException("Runtime '" + RuntimeKey + "' is not available: " + _Descriptor.Availability + ". " + _Descriptor.SecurityNotes);
            if (_BlobStore == null || _Capacity == null)
                throw new NotSupportedException("Artifact Python runtime requires artifact blob store and capacity manager services.");
            if (config is not ArtifactPythonRuntimeConfig pythonConfig)
                throw new ArgumentException("config type must be ArtifactPythonRuntimeConfig.", nameof(config));

            ArtifactProcessRuntimeConfig processShape = new ArtifactProcessRuntimeConfig
            {
                ArtifactId = pythonConfig.ArtifactId,
                ArtifactVersion = pythonConfig.ArtifactVersion,
                Entrypoint = pythonConfig.Entrypoint,
                Arguments = new List<string>(pythonConfig.Arguments),
                EnvironmentReferences = new List<string>(pythonConfig.EnvironmentReferences)
            };
            ArtifactRuntimePlan plan = _Database != null
                ? await ArtifactRuntimePlan.ResolveAsync(_Database, _BlobStore, _Settings, context, step, processShape, RuntimeKey, token).ConfigureAwait(false)
                : await ArtifactRuntimePlan.ResolveAsync(_BlobStore, _Settings, context, step, processShape, RuntimeKey, token).ConfigureAwait(false);
            ArtifactManifestEntrypoint entry = plan.Entrypoint;
            string module = string.IsNullOrWhiteSpace(pythonConfig.Module) ? entry.Module ?? string.Empty : pythonConfig.Module!;
            string function = string.IsNullOrWhiteSpace(pythonConfig.Function) ? entry.Function : pythonConfig.Function;
            if (string.IsNullOrWhiteSpace(module)) throw new InvalidOperationException("Artifact.Python entrypoint requires module.");
            if (string.IsNullOrWhiteSpace(function)) throw new InvalidOperationException("Artifact.Python entrypoint requires function.");

            PythonEnvironmentCache pythonCache = new PythonEnvironmentCache(_Settings);
            string pythonExecutable = await pythonCache.PrepareAsync(plan, pythonConfig.PythonVersion, token).ConfigureAwait(false);
            List<string> args = new List<string>(entry.Args);
            args.AddRange(pythonConfig.Arguments);
            List<string> env = MergeEnvironment(pythonConfig.EnvironmentReferences, plan.Manifest.EnvironmentAllowList, entry.EnvironmentAllowList);
            return new ArtifactPythonStepRunner(context.TenantId, plan.Artifact, plan.ArtifactRoot, plan.EntrypointName, pythonExecutable, module, function, args, env, _Settings, _Capacity, context.RunLogSession, context.RunLogStep, step.MaxRuntimeMs);
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
