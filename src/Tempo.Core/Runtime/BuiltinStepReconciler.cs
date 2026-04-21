namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;

    /// <summary>Reconciles legacy built-in step rows to concrete class or method runtime bindings.</summary>
    public class BuiltinStepReconciler
    {
        private readonly DatabaseDriverBase _Database;
        private readonly StepManager _StepManager;
        private readonly string _GlobalTenantId;

        /// <summary>Instantiate.</summary>
        public BuiltinStepReconciler(DatabaseDriverBase database, StepManager stepManager, string globalTenantId = "global")
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _StepManager = stepManager ?? throw new ArgumentNullException(nameof(stepManager));
            _GlobalTenantId = string.IsNullOrWhiteSpace(globalTenantId) ? "global" : globalTenantId;
        }

        /// <summary>Reconcile built-in steps across global and all persisted tenants.</summary>
        public async Task<BuiltinStepReconciliationResult> ReconcileAllTenantsAsync(CancellationToken token = default)
        {
            BuiltinStepReconciliationResult result = new BuiltinStepReconciliationResult();
            HashSet<string> tenantIds = new HashSet<string>(StringComparer.Ordinal) { _GlobalTenantId };
            foreach (Tempo.Core.Models.Tenant tenant in await _Database.Tenants.AllAsync(token).ConfigureAwait(false))
            {
                tenantIds.Add(tenant.Id);
            }

            foreach (string tenantId in tenantIds)
            {
                result.Merge(await ReconcileTenantAsync(tenantId, token).ConfigureAwait(false));
            }

            return result;
        }

        /// <summary>Reconcile built-in steps for one tenant.</summary>
        public async Task<BuiltinStepReconciliationResult> ReconcileTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            BuiltinStepReconciliationResult result = new BuiltinStepReconciliationResult();
            List<StepRecord> steps = await _Database.Steps.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (StepRecord step in steps)
            {
                if (!IsBuiltInRuntime(step.RuntimeKey)) continue;
                BuiltinStepReconciliationEntry entry = await ReconcileStepAsync(step, token).ConfigureAwait(false);
                result.Add(entry, true);
            }

            return result;
        }

        private async Task<BuiltinStepReconciliationEntry> ReconcileStepAsync(StepRecord step, CancellationToken token)
        {
            List<BuiltinStepRegistration> candidates = SelectCandidates(step);
            BuiltinStepReconciliationEntry entry = new BuiltinStepReconciliationEntry
            {
                StepId = step.Id,
                TenantId = step.TenantId,
                ExecutionKey = step.ExecutionKey,
                CandidateCount = candidates.Count
            };

            if (candidates.Count == 0)
            {
                step.RuntimeBindingState = StepRuntimeBindingStateEnum.Orphaned;
                step.RuntimeBindingMessage = "No registered built-in step matches execution key '" + step.ExecutionKey + "' for tenant '" + step.TenantId + "'.";
            }
            else if (candidates.Count > 1)
            {
                step.RuntimeBindingState = StepRuntimeBindingStateEnum.Ambiguous;
                step.RuntimeBindingMessage = "Multiple registered built-in steps match execution key '" + step.ExecutionKey + "': " +
                    string.Join(", ", candidates.Select(c => c.SourceKind + ":" + c.DeclaringType + (string.IsNullOrWhiteSpace(c.MethodName) ? "" : "." + c.MethodName)));
            }
            else
            {
                ApplyResolvedRegistration(step, candidates[0]);
            }

            StepRecord updated = await _Database.Steps.UpdateAsync(step, token).ConfigureAwait(false);
            entry.RuntimeKey = updated.RuntimeKey;
            entry.State = updated.RuntimeBindingState;
            entry.Message = updated.RuntimeBindingMessage ?? string.Empty;
            return entry;
        }

        private List<BuiltinStepRegistration> SelectCandidates(StepRecord step)
        {
            List<BuiltinStepRegistration> registrations = _StepManager.Registrations(step.ExecutionKey, step.TenantId);
            List<BuiltinStepRegistration> exact = registrations
                .Where(r => string.Equals(r.TenantId, step.TenantId, StringComparison.Ordinal))
                .ToList();
            return exact.Count > 0 ? exact : registrations.Where(r => r.IsGlobal).ToList();
        }

        private static bool IsBuiltInRuntime(RuntimeKey runtimeKey)
        {
            return runtimeKey == StepRuntimeKeys.BuiltinUnknown ||
                   runtimeKey == StepRuntimeKeys.BuiltinClass ||
                   runtimeKey == StepRuntimeKeys.BuiltinMethod;
        }

        private static void ApplyResolvedRegistration(StepRecord step, BuiltinStepRegistration registration)
        {
            step.StepType = PersistedStepTypeEnum.Code;
            step.RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved;
            step.RuntimeBindingMessage = null;
            if (registration.MaxRuntimeMs > 0) step.MaxRuntimeMs = registration.MaxRuntimeMs;

            if (registration.SourceKind == BuiltinStepSourceKind.Class)
            {
                BuiltinClassRuntimeConfig config = new BuiltinClassRuntimeConfig
                {
                    Identifier = registration.ExecutionKey,
                    TypeName = registration.DeclaringType,
                    AssemblyName = registration.AssemblyName,
                    AssemblyVersion = registration.AssemblyVersion,
                    SignatureHash = registration.SignatureHash
                };
                step.RuntimeKey = StepRuntimeKeys.BuiltinClass;
                step.RuntimeConfig = config;
                step.RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(config);
            }
            else
            {
                BuiltinMethodRuntimeConfig config = new BuiltinMethodRuntimeConfig
                {
                    Identifier = registration.ExecutionKey,
                    DeclaringType = registration.DeclaringType,
                    MethodName = registration.MethodName,
                    AssemblyName = registration.AssemblyName,
                    AssemblyVersion = registration.AssemblyVersion,
                    SignatureHash = registration.SignatureHash
                };
                step.RuntimeKey = StepRuntimeKeys.BuiltinMethod;
                step.RuntimeConfig = config;
                step.RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(config);
            }
        }
    }
}
