namespace Tempo.Core.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;

    /// <summary>
    /// Enqueues flow runs and hydrates <see cref="Tempo.DataFlow"/> instances from persisted records.
    /// </summary>
    public class FlowDispatchService
    {
        private readonly DatabaseDriverBase _Database;

        /// <summary>Instantiate.</summary>
        public FlowDispatchService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Enqueue a new run of the given flow.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="dataFlowId">Data flow identifier.</param>
        /// <param name="inputData">Optional JSON input data.</param>
        /// <param name="triggeredByUserId">User that initiated the run.</param>
        /// <param name="triggerId">Trigger identifier, if any.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task<FlowRun> EnqueueAsync(
            string tenantId,
            string dataFlowId,
            string? inputData = null,
            string? triggeredByUserId = null,
            string? triggerId = null,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(dataFlowId)) throw new ArgumentNullException(nameof(dataFlowId));

            DataFlowRecord? flow = await _Database.DataFlows.ReadAsync(tenantId, dataFlowId, token).ConfigureAwait(false);
            if (flow == null) throw new InvalidOperationException("Flow '" + dataFlowId + "' not found in tenant '" + tenantId + "'.");
            if (!flow.Active) throw new InvalidOperationException("Flow '" + dataFlowId + "' is inactive.");

            FlowRun run = new FlowRun
            {
                TenantId = tenantId,
                DataFlowId = flow.Id,
                TriggeredByUserId = triggeredByUserId,
                TriggerId = triggerId,
                State = FlowRunStateEnum.Queued,
                InputData = inputData
            };
            return await _Database.FlowRuns.CreateAsync(run, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Hydrate a persisted record into the in-memory <see cref="Tempo.DataFlow"/>.
        /// </summary>
        public static Tempo.DataFlow Hydrate(DataFlowRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            Tempo.DataFlow flow = new Tempo.DataFlow
            {
                Identifier = record.Id,
                TenantId = record.TenantId,
                Name = record.Name,
                StartStepId = record.StartStepId,
                MaxRuntimeMs = record.MaxRuntimeMs,
                Steps = record.Transitions ?? new System.Collections.Generic.Dictionary<string, Tempo.StepTransition>()
            };
            if (!string.IsNullOrEmpty(record.TriggerId)) flow.TriggerId = record.TriggerId;
            return flow;
        }

        /// <summary>Serialize arbitrary JSON data for storage in a flow run row.</summary>
        public static string? SerializeData(JsonElement? data)
        {
            if (!data.HasValue) return null;
            return data.Value.GetRawText();
        }
    }
}
