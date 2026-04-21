namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Flow run and step run persistence methods.
    /// </summary>
    public interface IFlowRunMethods
    {
        /// <summary>Create a flow run.</summary>
        Task<FlowRun> CreateAsync(FlowRun run, CancellationToken token = default);

        /// <summary>Update a flow run.</summary>
        Task<FlowRun> UpdateAsync(FlowRun run, CancellationToken token = default);

        /// <summary>Transition a flow run's state.</summary>
        Task<bool> TransitionStateAsync(string id, FlowRunStateEnum newState, CancellationToken token = default);

        /// <summary>Read a flow run (tenant-scoped).</summary>
        Task<FlowRun?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Read a flow run globally (used by worker).</summary>
        Task<FlowRun?> ReadGlobalAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate flow runs.</summary>
        Task<EnumerationResult<FlowRun>> EnumerateAsync(FlowRunFilter filter, CancellationToken token = default);

        /// <summary>Pop the next queued run for execution (sets state to Running).</summary>
        Task<FlowRun?> ClaimNextQueuedAsync(CancellationToken token = default);

        /// <summary>Record a step run.</summary>
        Task<StepRun> CreateStepRunAsync(StepRun run, CancellationToken token = default);

        /// <summary>Update a step run.</summary>
        Task<StepRun> UpdateStepRunAsync(StepRun run, CancellationToken token = default);

        /// <summary>Enumerate step runs for a flow run.</summary>
        Task<List<StepRun>> EnumerateStepRunsAsync(string tenantId, string flowRunId, CancellationToken token = default);

        /// <summary>Delete a flow run and its step runs.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
