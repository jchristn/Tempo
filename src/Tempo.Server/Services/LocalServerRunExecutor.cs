namespace Tempo.Server.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Protocol;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Tempo.Protocol;

    /// <summary>
    /// In-process pseudo-worker that executes plans using the same contract as remote workers.
    /// </summary>
    public class LocalServerRunExecutor : IRunExecutor
    {
        /// <summary>Stable pseudo-worker identifier.</summary>
        public const string WorkerId = "wrk_local_server";

        private readonly DatabaseDriverBase _Database;
        private readonly StepRuntimeRegistry _RuntimeRegistry;
        private readonly LoggingModule? _Logging;
        private readonly string _Header = "[LocalServerRunExecutor] ";
        private int _ActiveRuns = 0;

        /// <summary>Instantiate.</summary>
        public LocalServerRunExecutor(DatabaseDriverBase database, StepRuntimeRegistry runtimeRegistry, EngineSettings settings, LoggingModule? logging = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _RuntimeRegistry = runtimeRegistry ?? throw new ArgumentNullException(nameof(runtimeRegistry));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Logging = logging;

            Descriptor = new RunExecutorDescriptor
            {
                WorkerId = WorkerId,
                WorkerSessionId = Tempo.Core.Helpers.IdGenerator.GenerateWorkerSessionId(),
                Name = "server-local",
                Kind = "Server",
                NodeKind = ExecutionNodeKindEnum.Server,
                State = "Online",
                Enabled = true,
                DrainMode = false,
                Version = typeof(LocalServerRunExecutor).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                HostName = Environment.MachineName,
                LabelsJson = "{}",
                MaxConcurrentRuns = settings.MaxConcurrentRuns,
                MaxTaskTimeoutMs = 0
            };
        }

        /// <inheritdoc/>
        public RunExecutorDescriptor Descriptor { get; }

        /// <inheritdoc/>
        public bool CanAcceptWork(FlowRunExecutionPlan plan)
        {
            return plan != null &&
                Descriptor.Enabled &&
                !Descriptor.DrainMode &&
                Volatile.Read(ref _ActiveRuns) < Descriptor.MaxConcurrentRuns;
        }

        /// <inheritdoc/>
        public async Task<RunCompletionReport?> ExecuteAsync(RunAssignmentRecord assignment, FlowRunExecutionPlan plan, CancellationToken token = default)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            int current = Interlocked.Increment(ref _ActiveRuns);
            Descriptor.CurrentRunCount = current;
            if (current > Descriptor.MaxConcurrentRuns)
            {
                Interlocked.Decrement(ref _ActiveRuns);
                Descriptor.CurrentRunCount = Volatile.Read(ref _ActiveRuns);
                throw new InvalidOperationException("Local executor oversubscribed.");
            }

            try
            {
                RegistryDataFlowRunner runner = new RegistryDataFlowRunner(new ExecutionPlanStepResolver(plan), _RuntimeRegistry)
                {
                    MetricsStore = new FlowMetricsBridge(_Database, plan.FlowRunId, plan.TenantId)
                };

                Tempo.StepRequest request = new Tempo.StepRequest
                {
                    ProtocolVersion = ProtocolVersions.Current,
                    TenantId = plan.TenantId,
                    DataFlowId = plan.Flow.Identifier,
                    FlowRunId = plan.FlowRunId,
                    RequestId = plan.FlowRunId
                };

                if (!string.IsNullOrWhiteSpace(plan.InitialInputData))
                {
                    try
                    {
                        JsonDocument doc = JsonDocument.Parse(plan.InitialInputData);
                        request.Data = doc.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        request.Data = plan.InitialInputData;
                    }
                }

                Tempo.StepResult result = await runner.Run(plan.Flow, request, plan.ExecutionSnapshot, token).ConfigureAwait(false);
                return new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = assignment.WorkerId,
                    WorkerSessionId = assignment.WorkerSessionId,
                    LeaseToken = assignment.LeaseToken,
                    FinalState = result.Result switch
                    {
                        Tempo.Enums.StepResultTypeEnum.Success => FlowRunStateEnum.Succeeded,
                        Tempo.Enums.StepResultTypeEnum.Error => FlowRunStateEnum.Failed,
                        Tempo.Enums.StepResultTypeEnum.Exception => FlowRunStateEnum.Exception,
                        Tempo.Enums.StepResultTypeEnum.Timeout => FlowRunStateEnum.Exception,
                        _ => FlowRunStateEnum.Failed
                    },
                    OutputData = SerializeOutput(result.Data),
                    ErrorMessage = result.Exception?.Message ?? result.ExceptionMessage,
                    ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                    CompletedUtc = DateTime.UtcNow
                };
            }
            catch (OperationCanceledException)
            {
                return new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = assignment.WorkerId,
                    WorkerSessionId = assignment.WorkerSessionId,
                    LeaseToken = assignment.LeaseToken,
                    FinalState = FlowRunStateEnum.Cancelled,
                    ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                    CompletedUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "run " + assignment.FlowRunId + " crashed: " + ex.Message);
                return new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = assignment.WorkerId,
                    WorkerSessionId = assignment.WorkerSessionId,
                    LeaseToken = assignment.LeaseToken,
                    FinalState = FlowRunStateEnum.Exception,
                    ErrorMessage = ex.Message,
                    ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                    CompletedUtc = DateTime.UtcNow
                };
            }
            finally
            {
                Interlocked.Decrement(ref _ActiveRuns);
                Descriptor.CurrentRunCount = Volatile.Read(ref _ActiveRuns);
            }
        }

        private static string? SerializeOutput(object? data)
        {
            if (data == null) return null;
            try { return JsonSerializer.Serialize(data); }
            catch (NotSupportedException) { return data.ToString(); }
        }
    }
}
