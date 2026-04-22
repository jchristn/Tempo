namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Enumeration;
    using Tempo.Metrics;
    using Tempo.Protocol;

    /// <summary>
    /// Bridges the in-memory <see cref="Tempo.Metrics.MetricsStore"/> callback surface to our
    /// persistent <c>flow_runs</c> / <c>step_runs</c> tables. Only write paths are used by the runner.
    /// </summary>
    public class FlowMetricsBridge : MetricsStore
    {
        private readonly DatabaseDriverBase _Database;
        private readonly string _FlowRunId;
        private readonly string _TenantId;
        private int _Sequence = 0;

        /// <summary>Instantiate.</summary>
        /// <param name="database">Database driver.</param>
        /// <param name="flowRunId">Parent flow run identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        public FlowMetricsBridge(DatabaseDriverBase database, string flowRunId, string tenantId)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _FlowRunId = string.IsNullOrWhiteSpace(flowRunId) ? throw new ArgumentNullException(nameof(flowRunId)) : flowRunId;
            _TenantId = string.IsNullOrWhiteSpace(tenantId) ? throw new ArgumentNullException(nameof(tenantId)) : tenantId;
        }

        /// <inheritdoc/>
        public override async Task WriteDataFlowRun(DataFlowRunDetails details)
        {
            // Flow-level update happens when the dispatch coordinator handles terminal completion.
            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task WriteStepRun(StepRunDetails details)
        {
            if (details == null) return;
            int sequence = System.Threading.Interlocked.Increment(ref _Sequence);
            StepRun run = new StepRun
            {
                TenantId = _TenantId,
                FlowRunId = _FlowRunId,
                DataFlowId = details.DataFlowId,
                StepId = details.StepId,
                Sequence = sequence,
                Result = details.Result,
                NextStepId = string.IsNullOrEmpty(details.NextStepId) ? null : details.NextStepId,
                InputData = null,
                OutputData = null,
                ErrorMessage = string.IsNullOrEmpty(details.ExceptionMessage) ? null : details.ExceptionMessage,
                ExecutionState = Enum.TryParse(details.ExecutionState, true, out StepRunExecutionStateEnum executionState) ? executionState : StepRunExecutionStateEnum.Complete,
                ProtocolVersion = string.IsNullOrWhiteSpace(details.ProtocolVersion) ? ProtocolVersions.Current : details.ProtocolVersion,
                ArtifactId = string.IsNullOrEmpty(details.ArtifactId) ? null : details.ArtifactId,
                ArtifactVersionId = string.IsNullOrEmpty(details.ArtifactVersionId) ? null : details.ArtifactVersionId,
                ArtifactVersion = string.IsNullOrEmpty(details.ArtifactVersion) ? null : details.ArtifactVersion,
                ArtifactSha256 = string.IsNullOrEmpty(details.ArtifactSha256) ? null : details.ArtifactSha256,
                ManifestEntrypoint = string.IsNullOrEmpty(details.ManifestEntrypoint) ? null : details.ManifestEntrypoint,
                StartedUtc = details.StartUtc,
                CapacityQueuedUtc = details.CapacityQueuedUtc,
                CapacityAcquiredUtc = details.CapacityAcquiredUtc,
                CapacityWaitMs = details.CapacityWaitMs,
                CompletedUtc = details.EndUtc
            };
            if (!string.IsNullOrEmpty(details.RowId)) run.Id = details.RowId;
            await _Database.FlowRuns.CreateStepRunAsync(run).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override Task<DataFlowRunDetails> GetDataFlowRun(string requestId) =>
            throw new NotSupportedException("FlowMetricsBridge is write-only.");

        /// <inheritdoc/>
        public override Task<List<StepRunDetails>> GetDataFlowStepRuns(string requestId) =>
            throw new NotSupportedException("FlowMetricsBridge is write-only.");

        /// <inheritdoc/>
        public override Task<EnumerationResult<DataFlowRunDetails>> EnumerateDataFlowRuns(EnumerationRequest request, CancellationToken token = default) =>
            throw new NotSupportedException("FlowMetricsBridge is write-only.");

        /// <inheritdoc/>
        public override Task<EnumerationResult<StepRunDetails>> EnumerateStepRuns(EnumerationRequest request, CancellationToken token = default) =>
            throw new NotSupportedException("FlowMetricsBridge is write-only.");
    }
}
