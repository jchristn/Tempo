namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Enums;

    /// <summary>Migrates legacy inline flow step definitions to persisted step records.</summary>
    public class StepCompatibilityMigrator
    {
        private readonly DatabaseDriverBase _Database;

        /// <summary>Instantiate.</summary>
        public StepCompatibilityMigrator(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>Migrate inline REST transitions across all tenants.</summary>
        public async Task<StepCompatibilityMigrationResult> MigrateAllTenantsAsync(CancellationToken token = default)
        {
            StepCompatibilityMigrationResult result = new StepCompatibilityMigrationResult();
            foreach (Tenant tenant in await _Database.Tenants.AllAsync(token).ConfigureAwait(false))
            {
                result.Merge(await MigrateTenantAsync(tenant.Id, token).ConfigureAwait(false));
            }

            return result;
        }

        /// <summary>Migrate inline REST transitions in one tenant.</summary>
        public async Task<StepCompatibilityMigrationResult> MigrateTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            StepCompatibilityMigrationResult result = new StepCompatibilityMigrationResult();
            List<DataFlowRecord> flows = await _Database.DataFlows.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (DataFlowRecord flow in flows)
            {
                result.Merge(await MigrateFlowAsync(flow, token).ConfigureAwait(false));
            }

            return result;
        }

        /// <summary>Migrate inline REST transitions in one persisted flow.</summary>
        public async Task<StepCompatibilityMigrationResult> MigrateFlowAsync(DataFlowRecord flow, CancellationToken token = default)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));

            StepCompatibilityMigrationResult result = new StepCompatibilityMigrationResult { FlowsScanned = 1 };
            if (flow.Transitions == null || flow.Transitions.Count == 0) return result;

            Dictionary<string, string> keyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, Tempo.StepTransition> migrated = new Dictionary<string, Tempo.StepTransition>(StringComparer.Ordinal);
            HashSet<string> reservedKeys = new HashSet<string>(flow.Transitions.Keys, StringComparer.Ordinal);
            bool flowChanged = false;

            foreach (KeyValuePair<string, Tempo.StepTransition> item in flow.Transitions)
            {
                string sourceKey = item.Key;
                Tempo.StepTransition transition = item.Value ?? new Tempo.StepTransition();
                if (!IsInlineRest(transition))
                {
                    migrated[sourceKey] = CloneTransition(transition);
                    continue;
                }

                if (transition.Rest == null) throw new InvalidOperationException("Inline REST transition '" + sourceKey + "' has no REST configuration.");

                ExternalRestRuntimeConfig config = ExternalRestRuntimeConfig.FromLegacy(transition.Rest);
                (StepRecord step, bool created) = await EnsureRestStepAsync(flow, sourceKey, transition, config, reservedKeys, token).ConfigureAwait(false);
                string targetKey = step.ExecutionKey;
                if (!string.Equals(sourceKey, targetKey, StringComparison.Ordinal))
                {
                    keyMap[sourceKey] = targetKey;
                    reservedKeys.Add(targetKey);
                }

                Tempo.StepTransition cleaned = CloneTransition(transition);
                cleaned.StepType = null;
                cleaned.Rest = null!;
                migrated[targetKey] = cleaned;
                flowChanged = true;

                result.Add(new StepCompatibilityMigrationEntry
                {
                    TenantId = flow.TenantId,
                    FlowId = flow.Id,
                    OriginalExecutionKey = sourceKey,
                    ExecutionKey = targetKey,
                    StepId = step.Id,
                    StepCreated = created,
                    FlowUpdated = true,
                    Message = string.Equals(sourceKey, targetKey, StringComparison.Ordinal)
                        ? "Inline REST transition migrated in place."
                        : "Inline REST transition migrated to non-conflicting execution key."
                });
            }

            if (!flowChanged) return result;

            if (keyMap.Count > 0)
            {
                foreach (Tempo.StepTransition transition in migrated.Values)
                {
                    transition.OnSuccess = RewriteReference(transition.OnSuccess, keyMap)!;
                    transition.OnFailure = RewriteReference(transition.OnFailure, keyMap)!;
                    transition.OnException = RewriteReference(transition.OnException, keyMap)!;
                }

                flow.StartStepId = RewriteReference(flow.StartStepId, keyMap) ?? flow.StartStepId;
            }

            flow.Transitions = migrated;
            await _Database.DataFlows.UpdateAsync(flow, token).ConfigureAwait(false);
            result.FlowsUpdated = 1;
            foreach (StepCompatibilityMigrationEntry entry in result.Entries) entry.FlowUpdated = true;
            return result;
        }

        private async Task<(StepRecord Step, bool Created)> EnsureRestStepAsync(
            DataFlowRecord flow,
            string sourceKey,
            Tempo.StepTransition transition,
            ExternalRestRuntimeConfig config,
            HashSet<string> reservedKeys,
            CancellationToken token)
        {
            string preferredKey = NormalizeExecutionKey(sourceKey);
            StepRecord? existing = await _Database.Steps.ReadByExecutionKeyAsync(flow.TenantId, preferredKey, token).ConfigureAwait(false);
            if (CanReuse(existing, config)) return (existing!, false);

            string executionKey = preferredKey;
            if (existing != null)
            {
                executionKey = BuildDeterministicRestKey(flow, sourceKey, config, reservedKeys);
                existing = await _Database.Steps.ReadByExecutionKeyAsync(flow.TenantId, executionKey, token).ConfigureAwait(false);
                if (CanReuse(existing, config)) return (existing!, false);
                if (existing != null) throw new InvalidOperationException("Deterministic inline REST migration key '" + executionKey + "' already exists with a different runtime config.");
            }

            StepRecord record = new StepRecord
            {
                TenantId = flow.TenantId,
                ExecutionKey = executionKey,
                Name = string.IsNullOrWhiteSpace(transition.Name) ? sourceKey : transition.Name,
                Description = MigrationDescription(flow.Id, sourceKey),
                RuntimeKey = StepRuntimeKeys.ExternalRest,
                RuntimeConfig = config,
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                StepType = PersistedStepTypeEnum.Rest,
                Active = true
            };
            return (await _Database.Steps.CreateAsync(record, token).ConfigureAwait(false), true);
        }

        private static bool IsInlineRest(Tempo.StepTransition transition)
        {
            return transition.StepType == StepTypeEnum.Rest || transition.Rest != null;
        }

        private static bool CanReuse(StepRecord? step, ExternalRestRuntimeConfig config)
        {
            if (step == null) return false;
            if (step.RuntimeKey != StepRuntimeKeys.ExternalRest) return false;
            if (step.RuntimeConfig is not ExternalRestRuntimeConfig existing) return false;
            return RestConfigEquivalent(existing, config);
        }

        private static bool RestConfigEquivalent(ExternalRestRuntimeConfig left, ExternalRestRuntimeConfig right)
        {
            string leftJson = JsonSerializer.Serialize(left, StepRuntimeSerialization.Options);
            string rightJson = JsonSerializer.Serialize(right, StepRuntimeSerialization.Options);
            return string.Equals(leftJson, rightJson, StringComparison.Ordinal);
        }

        private static string NormalizeExecutionKey(string executionKey)
        {
            string trimmed = string.IsNullOrWhiteSpace(executionKey) ? "inline_rest" : executionKey.Trim();
            StringBuilder sb = new StringBuilder(trimmed.Length);
            foreach (char ch in trimmed)
            {
                if (!char.IsControl(ch)) sb.Append(ch);
            }

            string normalized = sb.Length == 0 ? "inline_rest" : sb.ToString();
            return normalized.Length <= StepRecord.ExecutionKeyMaxLength
                ? normalized
                : normalized.Substring(0, StepRecord.ExecutionKeyMaxLength);
        }

        private static string BuildDeterministicRestKey(DataFlowRecord flow, string sourceKey, ExternalRestRuntimeConfig config, HashSet<string> reservedKeys)
        {
            string hash = ShortHash(flow.TenantId + "\n" + flow.Id + "\n" + sourceKey + "\n" + JsonSerializer.Serialize(config, StepRuntimeSerialization.Options));
            string baseKey = NormalizeExecutionKey(sourceKey);
            int maxBaseLength = Math.Max(1, StepRecord.ExecutionKeyMaxLength - hash.Length - 6);
            if (baseKey.Length > maxBaseLength) baseKey = baseKey.Substring(0, maxBaseLength);
            string candidate = baseKey + "_rest_" + hash;
            if (!reservedKeys.Contains(candidate)) return candidate;

            for (int i = 2; i < 1000; i++)
            {
                string suffix = "_" + i;
                string nextBase = baseKey;
                if (candidate.Length + suffix.Length > StepRecord.ExecutionKeyMaxLength)
                {
                    nextBase = baseKey.Substring(0, Math.Max(1, baseKey.Length - suffix.Length));
                }

                string next = nextBase + "_rest_" + hash + suffix;
                if (!reservedKeys.Contains(next)) return next;
            }

            throw new InvalidOperationException("Could not allocate a deterministic inline REST migration key for '" + sourceKey + "'.");
        }

        private static string ShortHash(string input)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).Substring(0, 12).ToLowerInvariant();
        }

        private static Tempo.StepTransition CloneTransition(Tempo.StepTransition source)
        {
            return new Tempo.StepTransition
            {
                Name = source.Name,
                OnSuccess = source.OnSuccess,
                OnFailure = source.OnFailure,
                OnException = source.OnException,
                MaxTransitions = source.MaxTransitions,
                StepType = source.StepType,
                Rest = source.Rest == null ? null! : new Tempo.RestStepConfiguration
                {
                    Method = source.Rest.Method,
                    Url = source.Rest.Url,
                    Headers = new Dictionary<string, string>(source.Rest.Headers),
                    TimeoutMs = source.Rest.TimeoutMs
                }
            };
        }

        private static string? RewriteReference(string? value, Dictionary<string, string> keyMap)
        {
            if (value == null) return null;
            return keyMap.TryGetValue(value, out string? rewritten) ? rewritten : value;
        }

        private static string MigrationDescription(string flowId, string sourceKey)
        {
            return "Migrated from inline REST transition '" + sourceKey + "' in flow '" + flowId + "'.";
        }
    }
}
