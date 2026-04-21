namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;

    /// <summary>Resolves registered in-memory code steps for embedded and compatibility scenarios.</summary>
    public class InMemoryStepExecutionResolver : IStepExecutionResolver
    {
        private readonly StepManager _StepManager;

        /// <summary>Instantiate.</summary>
        public InMemoryStepExecutionResolver(StepManager stepManager)
        {
            _StepManager = stepManager ?? throw new ArgumentNullException(nameof(stepManager));
        }

        /// <inheritdoc/>
        public Task<ResolvedStepExecution> ResolveAsync(string tenantId, string executionKey, FlowRunExecutionSnapshot snapshot, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(executionKey)) throw new ArgumentNullException(nameof(executionKey));
            if (_StepManager.GetStepRunner(executionKey, tenantId) == null)
                throw new InvalidOperationException("Step '" + executionKey + "' was not found in step manager for tenant '" + tenantId + "'.");

            Tempo.BuiltinStepRegistration? registration = SelectRegistration(executionKey, tenantId);
            StepRuntimeConfig config = registration == null ? new BuiltinUnknownRuntimeConfig { Identifier = executionKey } : CreateConfig(registration);
            StepRecord step = new StepRecord
            {
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? "global" : tenantId,
                ExecutionKey = executionKey,
                Name = registration?.DisplayName ?? executionKey,
                RuntimeKey = config.RuntimeKey,
                RuntimeConfig = config,
                RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(config),
                StepType = PersistedStepTypeEnum.Code,
                MaxRuntimeMs = registration?.MaxRuntimeMs ?? _StepManager.GetMaxRuntimeMs(executionKey, tenantId),
                RuntimeBindingState = registration == null ? StepRuntimeBindingStateEnum.Unresolved : StepRuntimeBindingStateEnum.Resolved
            };
            return Task.FromResult(new ResolvedStepExecution { Step = step, Config = config });
        }

        private Tempo.BuiltinStepRegistration? SelectRegistration(string executionKey, string tenantId)
        {
            List<Tempo.BuiltinStepRegistration> registrations = _StepManager.Registrations(executionKey, tenantId);
            List<Tempo.BuiltinStepRegistration> exact = registrations
                .Where(r => string.Equals(r.TenantId, tenantId, StringComparison.Ordinal))
                .ToList();
            if (exact.Count == 1) return exact[0];
            if (exact.Count > 1) return null;

            List<Tempo.BuiltinStepRegistration> global = registrations.Where(r => r.IsGlobal).ToList();
            return global.Count == 1 ? global[0] : null;
        }

        private static StepRuntimeConfig CreateConfig(Tempo.BuiltinStepRegistration registration)
        {
            if (registration.SourceKind == Tempo.BuiltinStepSourceKind.Class)
            {
                return new BuiltinClassRuntimeConfig
                {
                    Identifier = registration.ExecutionKey,
                    TypeName = registration.DeclaringType,
                    AssemblyName = registration.AssemblyName,
                    AssemblyVersion = registration.AssemblyVersion,
                    SignatureHash = registration.SignatureHash
                };
            }

            return new BuiltinMethodRuntimeConfig
            {
                Identifier = registration.ExecutionKey,
                DeclaringType = registration.DeclaringType,
                MethodName = registration.MethodName,
                AssemblyName = registration.AssemblyName,
                AssemblyVersion = registration.AssemblyVersion,
                SignatureHash = registration.SignatureHash
            };
        }
    }
}
