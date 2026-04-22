namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;

    /// <summary>
    /// Metadata describing an executor that can accept flow-run assignments.
    /// </summary>
    public class RunExecutorDescriptor
    {
        /// <summary>Stable worker identifier.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Human-readable worker name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Worker kind persisted to the workers table.</summary>
        public string Kind { get; set; } = "Server";

        /// <summary>Execution node kind for assignments handled by this executor.</summary>
        public ExecutionNodeKindEnum NodeKind { get; set; } = ExecutionNodeKindEnum.Server;

        /// <summary>Worker state persisted to the workers table.</summary>
        public string State { get; set; } = "Online";

        /// <summary>Whether the worker is enabled for scheduling.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Whether the worker is draining.</summary>
        public bool DrainMode { get; set; } = false;

        /// <summary>Runtime or product version.</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>Host name where the worker is running.</summary>
        public string HostName { get; set; } = string.Empty;

        /// <summary>Serialized worker labels.</summary>
        public string LabelsJson { get; set; } = "{}";

        /// <summary>Serialized worker capabilities.</summary>
        public string CapabilitiesJson { get; set; } = "[]";

        /// <summary>Maximum concurrent runs this executor can handle.</summary>
        public int MaxConcurrentRuns { get; set; } = 1;

        /// <summary>Maximum worker-enforced assignment runtime in milliseconds. Zero means no explicit worker timeout.</summary>
        public int MaxTaskTimeoutMs { get; set; } = 0;

        /// <summary>Current active-run count, when known.</summary>
        public int CurrentRunCount { get; set; } = 0;

        /// <summary>Current worker-session identifier. Null for sessionless executors.</summary>
        public string? WorkerSessionId { get; set; } = null;
    }

    /// <summary>
    /// Terminal completion frame for a run assignment.
    /// </summary>
    public class RunCompletionReport
    {
        /// <summary>Flow-run identifier.</summary>
        public string FlowRunId { get; set; } = string.Empty;

        /// <summary>Run-assignment identifier.</summary>
        public string RunAssignmentId { get; set; } = string.Empty;

        /// <summary>Worker identifier.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Worker-session identifier.</summary>
        public string? WorkerSessionId { get; set; } = null;

        /// <summary>Lease token associated with the completion.</summary>
        public string LeaseToken { get; set; } = string.Empty;

        /// <summary>Final coarse flow-run state.</summary>
        public FlowRunStateEnum FinalState { get; set; } = FlowRunStateEnum.Succeeded;

        /// <summary>Serialized output payload.</summary>
        public string? OutputData { get; set; } = null;

        /// <summary>Error or exception message.</summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>Serialized run-start execution snapshot.</summary>
        public string? ExecutionSnapshotJson { get; set; } = null;

        /// <summary>Buffered step-run rows recorded during execution.</summary>
        public List<StepRun> StepRuns { get; set; } = new List<StepRun>();

        /// <summary>Completion timestamp in UTC.</summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Authoritative server-side coordinator for scheduling and assignment state.
    /// </summary>
    public interface IRunDispatchCoordinator : IDisposable
    {
        /// <summary>Start background dispatch work.</summary>
        void Start();

        /// <summary>Stop background dispatch work.</summary>
        void Stop();

        /// <summary>Enqueue a new flow run.</summary>
        Task<FlowRun> EnqueueAsync(
            string tenantId,
            string dataFlowId,
            string? inputData = null,
            string? triggeredByUserId = null,
            string? triggerId = null,
            string? sourceIp = null,
            CancellationToken token = default);

        /// <summary>Cancel a queued run before assignment.</summary>
        Task<bool> CancelQueuedAsync(string tenantId, string flowRunId, CancellationToken token = default);

        /// <summary>Handle terminal completion for an assignment.</summary>
        Task HandleCompletionAsync(RunCompletionReport completion, CancellationToken token = default);

        /// <summary>Recover any expired leases that should be retried.</summary>
        Task<int> HandleLeaseExpiryAsync(CancellationToken token = default);
    }

    /// <summary>
    /// Persistence boundary for distributed run assignment state.
    /// </summary>
    public interface IRunAssignmentStore
    {
        /// <summary>Read the next schedulable queued run.</summary>
        Task<FlowRun?> ReadNextPendingAsync(CancellationToken token = default);

        /// <summary>Create an assignment and persist the associated flow-run dispatch metadata.</summary>
        Task<RunAssignmentRecord> CreateAssignmentAsync(FlowRun run, RunExecutorDescriptor executor, FlowRunExecutionPlan plan, CancellationToken token = default);

        /// <summary>Cancel a queued run.</summary>
        Task<bool> CancelQueuedAsync(string tenantId, string flowRunId, CancellationToken token = default);

        /// <summary>Persist terminal completion for an assignment.</summary>
        Task<bool> CompleteAssignmentAsync(RunCompletionReport completion, CancellationToken token = default);

        /// <summary>Fail a pending run before any assignment could be created.</summary>
        Task FailPendingRunAsync(FlowRun run, FlowRunStateEnum finalState, string errorMessage, CancellationToken token = default);

        /// <summary>Recover expired non-local assignments.</summary>
        Task<int> RecoverExpiredAssignmentsAsync(DateTime utcNow, CancellationToken token = default);

        /// <summary>Ensure a worker/session row exists for an executor.</summary>
        Task EnsureWorkerAsync(RunExecutorDescriptor executor, CancellationToken token = default);

        /// <summary>Refresh the worker heartbeat.</summary>
        Task TouchWorkerHeartbeatAsync(RunExecutorDescriptor executor, DateTime utcNow, CancellationToken token = default);

        /// <summary>Mark the worker session disconnected.</summary>
        Task MarkWorkerDisconnectedAsync(RunExecutorDescriptor executor, string reason, CancellationToken token = default);
    }

    /// <summary>
    /// Component that attempts to schedule one queued run onto an eligible executor.
    /// </summary>
    public interface IRunScheduler
    {
        /// <summary>Attempt to schedule one queued run.</summary>
        Task<bool> TryScheduleNextAsync(CancellationToken token = default);
    }

    /// <summary>
    /// Placement strategy for choosing an executor for a run.
    /// </summary>
    public interface ILoadBalancer
    {
        /// <summary>Select an eligible executor, or null when none are available.</summary>
        Task<IRunExecutor?> SelectExecutorAsync(FlowRunExecutionPlan plan, CancellationToken token = default);
    }

    /// <summary>
    /// Runtime executor that can run a serialized execution plan.
    /// </summary>
    public interface IRunExecutor
    {
        /// <summary>Persisted metadata for this executor.</summary>
        RunExecutorDescriptor Descriptor { get; }

        /// <summary>Whether this executor can currently accept the supplied plan.</summary>
        bool CanAcceptWork(FlowRunExecutionPlan plan);

        /// <summary>Execute or dispatch the supplied assignment. Remote executors may return null and report completion asynchronously.</summary>
        Task<RunCompletionReport?> ExecuteAsync(RunAssignmentRecord assignment, FlowRunExecutionPlan plan, CancellationToken token = default);
    }
}
