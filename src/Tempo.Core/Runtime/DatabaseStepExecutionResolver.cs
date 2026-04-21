namespace Tempo.Core.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Models;

    /// <summary>Resolves step execution metadata from persisted step records.</summary>
    public class DatabaseStepExecutionResolver : IStepExecutionResolver
    {
        private readonly DatabaseDriverBase _Database;
        private readonly string _GlobalTenantId;

        /// <summary>Instantiate.</summary>
        public DatabaseStepExecutionResolver(DatabaseDriverBase database, string globalTenantId = "global")
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _GlobalTenantId = string.IsNullOrWhiteSpace(globalTenantId) ? "global" : globalTenantId;
        }

        /// <inheritdoc/>
        public async Task<ResolvedStepExecution> ResolveAsync(string tenantId, string executionKey, FlowRunExecutionSnapshot snapshot, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(executionKey)) throw new ArgumentNullException(nameof(executionKey));

            StepRecord? step = await _Database.Steps.ReadByExecutionKeyAsync(tenantId, executionKey, token).ConfigureAwait(false);
            if (step == null && !string.Equals(tenantId, _GlobalTenantId, StringComparison.Ordinal))
            {
                step = await _Database.Steps.ReadByExecutionKeyAsync(_GlobalTenantId, executionKey, token).ConfigureAwait(false);
            }

            if (step == null) throw new InvalidOperationException("Step '" + executionKey + "' was not found for tenant '" + tenantId + "'.");
            return new ResolvedStepExecution { Step = step, Config = step.RuntimeConfig };
        }
    }
}
