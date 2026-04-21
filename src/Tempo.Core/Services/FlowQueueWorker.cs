namespace Tempo.Core.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;
    using Tempo.Protocol;

    /// <summary>
    /// Background worker that pops queued flow runs and executes them via the runtime registry.
    /// </summary>
    public class FlowQueueWorker : IDisposable
    {
        private readonly DatabaseDriverBase _Database;
        private readonly StepManager _StepManager;
        private readonly StepRuntimeRegistry _RuntimeRegistry;
        private readonly IStepExecutionResolver _Resolver;
        private readonly EngineSettings _Settings;
        private readonly LoggingModule? _Logging;
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _Slots;
        private readonly string _Header = "[FlowQueueWorker] ";
        private Task? _Loop = null;
        private bool _Disposed = false;

        /// <summary>Instantiate.</summary>
        public FlowQueueWorker(
            DatabaseDriverBase database,
            StepManager stepManager,
            EngineSettings settings,
            LoggingModule? logging = null,
            StepRuntimeRegistry? runtimeRegistry = null,
            IStepExecutionResolver? resolver = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _StepManager = stepManager ?? throw new ArgumentNullException(nameof(stepManager));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging;
            _RuntimeRegistry = runtimeRegistry ?? StepRuntimeRegistry.CreateDefault(_StepManager);
            _Resolver = resolver ?? new CompositeStepExecutionResolver(new DatabaseStepExecutionResolver(_Database), new InMemoryStepExecutionResolver(_StepManager));
            _Slots = new SemaphoreSlim(_Settings.MaxConcurrentRuns, _Settings.MaxConcurrentRuns);
        }

        /// <summary>Start the background loop.</summary>
        public void Start()
        {
            if (!_Settings.QueueEnabled) return;
            if (_Loop != null) return;
            _Loop = Task.Run(() => RunLoopAsync(_Cts.Token));
        }

        /// <summary>Stop the background loop.</summary>
        public void Stop()
        {
            try { _Cts.Cancel(); } catch { /* ignore */ }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_Disposed) return;
            Stop();
            try { _Loop?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
            _Cts.Dispose();
            _Slots.Dispose();
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            _Logging?.Info(_Header + "started");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _Slots.WaitAsync(token).ConfigureAwait(false);
                    FlowRun? run = await _Database.FlowRuns.ClaimNextQueuedAsync(token).ConfigureAwait(false);
                    if (run == null)
                    {
                        _Slots.Release();
                        await Task.Delay(_Settings.PollIntervalMs, token).ConfigureAwait(false);
                        continue;
                    }

                    _ = Task.Run(async () =>
                    {
                        try { await ExecuteAsync(run, token).ConfigureAwait(false); }
                        finally { _Slots.Release(); }
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging?.Warn(LogMessages.WithoutTerminalPeriod(_Header + "loop error: " + ex.Message));
                    await Task.Delay(_Settings.PollIntervalMs, CancellationToken.None).ConfigureAwait(false);
                }
            }
            _Logging?.Info(_Header + "stopped");
        }

        private async Task ExecuteAsync(FlowRun run, CancellationToken token)
        {
            try
            {
                DataFlowRecord? record = await _Database.DataFlows.ReadGlobalAsync(run.DataFlowId, token).ConfigureAwait(false);
                if (record == null)
                {
                    run.State = FlowRunStateEnum.Failed;
                    run.ErrorMessage = "Flow not found.";
                    run.CompletedUtc = DateTime.UtcNow;
                    await _Database.FlowRuns.UpdateAsync(run, token).ConfigureAwait(false);
                    return;
                }

                Tempo.DataFlow flow = FlowDispatchService.Hydrate(record);
                FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(_Database, run, record, token).ConfigureAwait(false);
                run.ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(snapshot);
                await _Database.FlowRuns.UpdateAsync(run, token).ConfigureAwait(false);

                RegistryDataFlowRunner runner = new RegistryDataFlowRunner(_Resolver, _RuntimeRegistry)
                {
                    MetricsStore = new FlowMetricsBridge(_Database, run.Id, run.TenantId)
                };

                Tempo.StepRequest stepRequest = new Tempo.StepRequest
                {
                    ProtocolVersion = ProtocolVersions.Current,
                    TenantId = run.TenantId,
                    DataFlowId = flow.Identifier,
                    FlowRunId = run.Id,
                    RequestId = run.Id
                };

                if (!string.IsNullOrEmpty(run.InputData))
                {
                    try
                    {
                        JsonDocument doc = JsonDocument.Parse(run.InputData);
                        stepRequest.Data = doc.RootElement.Clone();
                    }
                    catch (JsonException) { stepRequest.Data = run.InputData; }
                }

                Tempo.StepResult result = await runner.Run(
                    flow,
                    stepRequest,
                    snapshot,
                    token).ConfigureAwait(false);

                run.State = result.Result switch
                {
                    Tempo.Enums.StepResultTypeEnum.Success => FlowRunStateEnum.Succeeded,
                    Tempo.Enums.StepResultTypeEnum.Error => FlowRunStateEnum.Failed,
                    Tempo.Enums.StepResultTypeEnum.Exception => FlowRunStateEnum.Exception,
                    Tempo.Enums.StepResultTypeEnum.Timeout => FlowRunStateEnum.Exception,
                    _ => FlowRunStateEnum.Failed
                };
                run.OutputData = SerializeOutput(result.Data);
                if (result.Exception != null) run.ErrorMessage = result.Exception.Message;
                run.ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(snapshot);
                run.CompletedUtc = DateTime.UtcNow;
                await _Database.FlowRuns.UpdateAsync(run, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                run.State = FlowRunStateEnum.Cancelled;
                run.CompletedUtc = DateTime.UtcNow;
                try { await _Database.FlowRuns.UpdateAsync(run, CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
            }
            catch (Exception ex)
            {
                run.State = FlowRunStateEnum.Exception;
                run.ErrorMessage = ex.Message;
                run.CompletedUtc = DateTime.UtcNow;
                try { await _Database.FlowRuns.UpdateAsync(run, CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
                _Logging?.Warn(LogMessages.WithoutTerminalPeriod(_Header + "run " + run.Id + " crashed: " + ex.Message));
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
