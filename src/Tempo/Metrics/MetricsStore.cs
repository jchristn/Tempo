namespace Tempo.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Enumeration;

    /// <summary>
    /// Metrics store.
    /// </summary>
    public abstract class MetricsStore
    {
        /// <summary>
        /// Metrics store.
        /// </summary>
        public MetricsStore()
        {

        }

        /// <summary>
        /// Write data flow run details.
        /// </summary>
        /// <param name="details">Details.</param>
        /// <returns>Task.</returns>
        public abstract Task WriteDataFlowRun(DataFlowRunDetails details);

        /// <summary>
        /// Write step run details.
        /// </summary>
        /// <param name="details">Details.</param>
        /// <returns>Task.</returns>
        public abstract Task WriteStepRun(StepRunDetails details);

        /// <summary>
        /// Get data flow run details.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <returns>Data flow run details.</returns>
        public abstract Task<DataFlowRunDetails> GetDataFlowRun(string requestId);

        /// <summary>
        /// Get step run details for a given request.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <returns>List of step run details.</returns>
        public abstract Task<List<StepRunDetails>> GetDataFlowStepRuns(string requestId);

        /// <summary>
        /// Enumerate data flow runs.
        /// </summary>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        public abstract Task<EnumerationResult<DataFlowRunDetails>> EnumerateDataFlowRuns(EnumerationRequest request, CancellationToken token = default);

        /// <summary>
        /// Enumerate step runs.
        /// </summary>
        /// <param name="request">Enumeration request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        public abstract Task<EnumerationResult<StepRunDetails>> EnumerateStepRuns(EnumerationRequest request, CancellationToken token = default);
    }
}
