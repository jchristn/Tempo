namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Enumeration;
    using Tempo.Metrics;
    using Tempo.Protocol;

    /// <summary>
    /// In-memory metrics store used by remote workers to return step-run rows to the server on completion.
    /// </summary>
    public sealed class BufferedFlowMetricsStore : MetricsStore
    {
        private readonly object _Lock = new object();
        private readonly string _TenantId;
        private readonly string _FlowRunId;
        private int _Sequence = 0;
        private readonly List<StepRun> _StepRuns = new List<StepRun>();

        /// <summary>Instantiate.</summary>
        public BufferedFlowMetricsStore(string tenantId, string flowRunId)
        {
            _TenantId = string.IsNullOrWhiteSpace(tenantId) ? throw new ArgumentNullException(nameof(tenantId)) : tenantId;
            _FlowRunId = string.IsNullOrWhiteSpace(flowRunId) ? throw new ArgumentNullException(nameof(flowRunId)) : flowRunId;
        }

        /// <inheritdoc/>
        public override Task WriteDataFlowRun(DataFlowRunDetails details)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override Task WriteStepRun(StepRunDetails details)
        {
            if (details == null) return Task.CompletedTask;

            StepRun run = new StepRun
            {
                Id = string.IsNullOrWhiteSpace(details.RowId) ? Tempo.Core.Helpers.IdGenerator.GenerateStepRunId() : details.RowId,
                TenantId = _TenantId,
                FlowRunId = _FlowRunId,
                DataFlowId = details.DataFlowId,
                StepId = details.StepId,
                Sequence = Interlocked.Increment(ref _Sequence),
                Result = details.Result,
                NextStepId = string.IsNullOrWhiteSpace(details.NextStepId) ? null : details.NextStepId,
                ErrorMessage = string.IsNullOrWhiteSpace(details.ExceptionMessage) ? null : details.ExceptionMessage,
                ExecutionState = Enum.TryParse(details.ExecutionState, true, out StepRunExecutionStateEnum parsed) ? parsed : StepRunExecutionStateEnum.Complete,
                ProtocolVersion = string.IsNullOrWhiteSpace(details.ProtocolVersion) ? ProtocolVersions.Current : details.ProtocolVersion,
                ArtifactId = string.IsNullOrWhiteSpace(details.ArtifactId) ? null : details.ArtifactId,
                ArtifactVersionId = string.IsNullOrWhiteSpace(details.ArtifactVersionId) ? null : details.ArtifactVersionId,
                ArtifactVersion = string.IsNullOrWhiteSpace(details.ArtifactVersion) ? null : details.ArtifactVersion,
                ArtifactSha256 = string.IsNullOrWhiteSpace(details.ArtifactSha256) ? null : details.ArtifactSha256,
                ManifestEntrypoint = string.IsNullOrWhiteSpace(details.ManifestEntrypoint) ? null : details.ManifestEntrypoint,
                StartedUtc = details.StartUtc,
                CapacityQueuedUtc = details.CapacityQueuedUtc,
                CapacityAcquiredUtc = details.CapacityAcquiredUtc,
                CapacityWaitMs = details.CapacityWaitMs,
                CompletedUtc = details.EndUtc
            };

            lock (_Lock)
            {
                _StepRuns.Add(run);
            }

            return Task.CompletedTask;
        }

        /// <summary>Snapshot buffered step runs.</summary>
        public List<StepRun> Snapshot()
        {
            lock (_Lock)
            {
                List<StepRun> copy = new List<StepRun>(_StepRuns.Count);
                foreach (StepRun run in _StepRuns)
                {
                    copy.Add(new StepRun
                    {
                        Id = run.Id,
                        TenantId = run.TenantId,
                        FlowRunId = run.FlowRunId,
                        DataFlowId = run.DataFlowId,
                        StepId = run.StepId,
                        Result = run.Result,
                        NextStepId = run.NextStepId,
                        InputData = run.InputData,
                        OutputData = run.OutputData,
                        ErrorMessage = run.ErrorMessage,
                        ArtifactId = run.ArtifactId,
                        ArtifactVersionId = run.ArtifactVersionId,
                        ArtifactVersion = run.ArtifactVersion,
                        ArtifactSha256 = run.ArtifactSha256,
                        ManifestEntrypoint = run.ManifestEntrypoint,
                        ExecutionState = run.ExecutionState,
                        ProtocolVersion = run.ProtocolVersion,
                        Sequence = run.Sequence,
                        StartedUtc = run.StartedUtc,
                        CapacityQueuedUtc = run.CapacityQueuedUtc,
                        CapacityAcquiredUtc = run.CapacityAcquiredUtc,
                        CapacityWaitMs = run.CapacityWaitMs,
                        CompletedUtc = run.CompletedUtc
                    });
                }

                return copy;
            }
        }

        /// <inheritdoc/>
        public override Task<DataFlowRunDetails> GetDataFlowRun(string requestId) =>
            throw new NotSupportedException("BufferedFlowMetricsStore is write-only.");

        /// <inheritdoc/>
        public override Task<List<StepRunDetails>> GetDataFlowStepRuns(string requestId) =>
            throw new NotSupportedException("BufferedFlowMetricsStore is write-only.");

        /// <inheritdoc/>
        public override Task<EnumerationResult<DataFlowRunDetails>> EnumerateDataFlowRuns(EnumerationRequest request, CancellationToken token = default) =>
            throw new NotSupportedException("BufferedFlowMetricsStore is write-only.");

        /// <inheritdoc/>
        public override Task<EnumerationResult<StepRunDetails>> EnumerateStepRuns(EnumerationRequest request, CancellationToken token = default) =>
            throw new NotSupportedException("BufferedFlowMetricsStore is write-only.");
    }
}
