namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Settings;
    using Tempo.Runners;

    /// <summary>Runtime provider for operator allowlisted host executables.</summary>
    public class HostExecutableRuntimeProvider : IStepRuntimeProvider
    {
        private readonly StepRuntimeDescriptor _Descriptor;
        private readonly ExternalExecutionSettings _ExternalSettings;
        private readonly HostExecutableSettings _HostSettings;
        private readonly ExternalRuntimeCapacityManager? _Capacity;

        public HostExecutableRuntimeProvider(
            StepRuntimeAvailabilityStateEnum availability,
            string securityNotes,
            ExternalExecutionSettings externalSettings,
            HostExecutableSettings hostSettings,
            ExternalRuntimeCapacityManager? capacity = null)
        {
            _ExternalSettings = externalSettings ?? throw new ArgumentNullException(nameof(externalSettings));
            _HostSettings = hostSettings ?? throw new ArgumentNullException(nameof(hostSettings));
            _Capacity = capacity;
            _Descriptor = new StepRuntimeDescriptor
            {
                RuntimeKey = StepRuntimeKeys.HostExecutable,
                DisplayName = "Host executable",
                Description = "Operator allowlisted executable runtime.",
                PackagingType = StepPackagingTypeEnum.Host,
                Availability = availability,
                SupportsArtifacts = false,
                SupportsVersioning = false,
                SecurityNotes = securityNotes,
                ConfigTypeName = nameof(HostExecutableRuntimeConfig),
                SupportedContractTypes = new List<StepContractTypeEnum> { StepContractTypeEnum.Loose, StepContractTypeEnum.Schema },
                ConfigProperties = new List<StepRuntimeConfigPropertyDescriptor>
                {
                    Prop("allowListKey", "string", true),
                    Prop("arguments", "array", false)
                }
            };
        }

        public RuntimeKey RuntimeKey => StepRuntimeKeys.HostExecutable;
        public Type ConfigType => typeof(HostExecutableRuntimeConfig);
        public StepRuntimeDescriptor Describe() => _Descriptor;

        public Task<StepConfigValidationResult> ValidateAsync(StepRuntimeValidationContext context, CancellationToken token = default)
        {
            if (_Descriptor.Availability == StepRuntimeAvailabilityStateEnum.DisabledBySettings)
                return Task.FromResult(StepConfigValidationResult.Failure(new[] { "Runtime '" + RuntimeKey + "' is disabled by settings." }));
            if (context.Config is not HostExecutableRuntimeConfig config)
                return Task.FromResult(StepConfigValidationResult.Failure(new[] { "config type must be HostExecutableRuntimeConfig." }));

            List<string> errors = new List<string>(config.Validate());
            HostExecutableAllowListEntry? entry = _HostSettings.Find(config.AllowListKey);
            if (entry == null)
            {
                errors.Add("allowListKey is not configured.");
            }
            else
            {
                errors.AddRange(ValidateEntry(entry));
                errors.AddRange(ValidateArguments(entry, config.Arguments));
            }

            return Task.FromResult(errors.Count == 0 ? StepConfigValidationResult.Success() : StepConfigValidationResult.Failure(errors));
        }

        public async Task<StepRunner> CreateRunnerAsync(StepExecutionContext context, StepRecord step, StepRuntimeConfig config, CancellationToken token = default)
        {
            if (_Descriptor.Availability == StepRuntimeAvailabilityStateEnum.DisabledBySettings)
                throw new NotSupportedException("Runtime '" + RuntimeKey + "' is disabled by settings.");
            if (_Capacity == null)
                throw new NotSupportedException("Host executable runtime requires the external runtime capacity manager service.");
            if (config is not HostExecutableRuntimeConfig hostConfig)
                throw new ArgumentException("config type must be HostExecutableRuntimeConfig.", nameof(config));

            StepConfigValidationResult validation = await ValidateAsync(new StepRuntimeValidationContext
            {
                TenantId = context.TenantId,
                RuntimeKey = RuntimeKey,
                Config = config
            }, token).ConfigureAwait(false);
            if (!validation.Valid) throw new InvalidOperationException(string.Join("; ", validation.Errors));

            HostExecutableAllowListEntry entry = _HostSettings.Find(hostConfig.AllowListKey)!;
            string executable = Path.GetFullPath(entry.ExecutablePath);
            string workingDirectory = ResolveWorkingDirectory(entry, executable);
            List<string> args = new List<string>(entry.Arguments);
            args.AddRange(hostConfig.Arguments);
            int maxRuntimeMs = step.MaxRuntimeMs > 0 ? step.MaxRuntimeMs : entry.MaxRuntimeMs;

            return new HostExecutableStepRunner(
                context.TenantId,
                entry.Key,
                executable,
                workingDirectory,
                args,
                entry.EnvironmentAllowList,
                _ExternalSettings,
                _Capacity,
                maxRuntimeMs);
        }

        private static IEnumerable<string> ValidateEntry(HostExecutableAllowListEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)) yield return "allowlist entry key is required.";
            if (ContainsUnsafeKeyCharacter(entry.Key)) yield return "allowlist entry key contains invalid characters.";
            if (string.IsNullOrWhiteSpace(entry.ExecutablePath)) yield return "allowlist executablePath is required.";
            else if (!Path.IsPathFullyQualified(entry.ExecutablePath)) yield return "allowlist executablePath must be absolute.";
            if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory) && !Path.IsPathFullyQualified(entry.WorkingDirectory!))
                yield return "allowlist workingDirectory must be absolute when provided.";
            foreach (string? arg in entry.Arguments)
            {
                if (arg == null) { yield return "allowlist fixed arguments cannot contain null values."; continue; }
                if (arg.Any(char.IsControl)) yield return "allowlist fixed arguments cannot contain control characters.";
            }
            foreach (string? name in entry.EnvironmentAllowList)
            {
                if (string.IsNullOrWhiteSpace(name)) { yield return "allowlist environment names cannot be empty."; continue; }
                if (name.Contains("=")) yield return "allowlist environment names must contain names only, not values.";
            }
        }

        private static IEnumerable<string> ValidateArguments(HostExecutableAllowListEntry entry, IEnumerable<string> arguments)
        {
            List<string> args = arguments?.ToList() ?? new List<string>();
            HostExecutableArgumentPolicy policy = entry.ArgumentPolicy;
            if (args.Count == 0) yield break;
            if (!policy.AllowAdditionalArguments)
            {
                yield return "arguments are not allowed for allowListKey '" + entry.Key + "'.";
                yield break;
            }
            if (args.Count > policy.MaxArguments)
                yield return "arguments exceed maxArguments for allowListKey '" + entry.Key + "'.";

            bool restricted = policy.AllowedValues.Count > 0 || policy.AllowedPrefixes.Count > 0;
            foreach (string arg in args)
            {
                if (!restricted) continue;
                bool allowed = policy.AllowedValues.Contains(arg, StringComparer.Ordinal) ||
                    policy.AllowedPrefixes.Any(prefix => arg.StartsWith(prefix, StringComparison.Ordinal));
                if (!allowed) yield return "argument '" + arg + "' is not allowed for allowListKey '" + entry.Key + "'.";
            }
        }

        private static string ResolveWorkingDirectory(HostExecutableAllowListEntry entry, string executable)
        {
            string? configured = string.IsNullOrWhiteSpace(entry.WorkingDirectory) ? null : entry.WorkingDirectory;
            if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured!);
            return Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory;
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

        private static StepRuntimeConfigPropertyDescriptor Prop(string name, string type, bool required)
        {
            return new StepRuntimeConfigPropertyDescriptor { Name = name, Type = type, Required = required };
        }
    }
}
