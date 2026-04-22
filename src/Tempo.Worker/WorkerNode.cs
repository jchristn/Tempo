namespace Tempo.Worker
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Protocol;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Workers;
    using Tempo.Protocol;

    /// <summary>
    /// Reconnecting worker daemon that executes assigned flow runs from a Tempo server.
    /// </summary>
    public sealed class WorkerNode
    {
        private readonly WorkerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly ExternalRuntimeCapacityManager _ExternalCapacity;
        private readonly RunLogService _RunLogs;
        private readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, CancellationTokenSource> _ActiveAssignments = new Dictionary<string, CancellationTokenSource>(StringComparer.Ordinal);
        private readonly object _AssignmentLock = new object();
        private readonly string _Header = "[Tempo.Worker] ";
        private string? _WorkerSessionId;
        private int _HeartbeatIntervalMs = 10000;
        private bool _DrainMode = false;

        /// <summary>Instantiate.</summary>
        public WorkerNode(WorkerSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _ExternalCapacity = new ExternalRuntimeCapacityManager(_Settings.Runtimes.ExternalExecution);
            _RunLogs = new RunLogService(_Settings.RunLogs);
        }

        /// <summary>Run until cancellation is requested.</summary>
        public async Task RunAsync(CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(_Settings.WorkerToken))
                throw new InvalidOperationException("workerToken is required. Issue one with POST /v1.0/workers/{id}/rotate-token.");

            while (!token.IsCancellationRequested)
            {
                using ClientWebSocket socket = new ClientWebSocket();
                socket.Options.SetRequestHeader(Tempo.Core.Constants.HeaderWorkerId, _Settings.WorkerId);
                socket.Options.SetRequestHeader(Tempo.Core.Constants.HeaderWorkerToken, _Settings.WorkerToken);

                CancellationTokenSource? connectionCts = null;
                Task? heartbeatTask = null;

                try
                {
                    Uri endpoint = BuildWebSocketEndpoint(_Settings.ServerEndpoint);
                    _Logging.Info(_Header + "connecting to " + endpoint);
                    await socket.ConnectAsync(endpoint, token).ConfigureAwait(false);

                    WorkerHelloMessage hello = BuildHello();
                    await SendFrameAsync(socket, hello, token).ConfigureAwait(false);
                    WorkerHelloAckMessage ack = await ReceiveHelloAckAsync(socket, token).ConfigureAwait(false);

                    _WorkerSessionId = ack.WorkerSessionId;
                    _HeartbeatIntervalMs = Math.Max(1000, ack.HeartbeatIntervalMs);
                    _DrainMode = ack.DrainMode;
                    _Logging.Info(_Header + "connected as " + _Settings.WorkerId + " session " + ack.WorkerSessionId);

                    connectionCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    heartbeatTask = Task.Run(() => HeartbeatLoopAsync(socket, connectionCts.Token), connectionCts.Token);

                    while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
                    {
                        string? text = await ReceiveTextAsync(socket, token).ConfigureAwait(false);
                        if (text == null) break;
                        await HandleFrameAsync(socket, text, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    _Logging.Warn(_Header + "socket error: " + ex.Message);
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "connection loop error: " + ex.Message);
                }
                finally
                {
                    try { connectionCts?.Cancel(); } catch { /* ignore */ }
                    CancelActiveAssignments();
                    if (heartbeatTask != null)
                    {
                        try { await heartbeatTask.ConfigureAwait(false); } catch { /* ignore */ }
                    }
                    connectionCts?.Dispose();
                    _WorkerSessionId = null;
                }

                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(_Settings.ReconnectDelayMs, token).ConfigureAwait(false);
                }
            }
        }

        private WorkerHelloMessage BuildHello()
        {
            return new WorkerHelloMessage
            {
                WorkerId = _Settings.WorkerId,
                Name = _Settings.Name,
                Kind = _Settings.Kind,
                Version = typeof(WorkerNode).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                HostName = Environment.MachineName,
                MaxConcurrentRuns = _Settings.MaxConcurrentRuns,
                MaxTaskTimeoutMs = _Settings.MaxTaskTimeoutMs,
                Labels = new List<string>(_Settings.Labels),
                Capabilities = BuildCapabilities()
            };
        }

        private List<WorkerCapabilityDescriptor> BuildCapabilities()
        {
            StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(runtimes: _Settings.Runtimes, externalCapacity: _ExternalCapacity);
            List<WorkerCapabilityDescriptor> capabilities = new List<WorkerCapabilityDescriptor>();

            foreach (StepRuntimeDescriptor descriptor in registry.DescribeAll())
            {
                if (descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available &&
                    descriptor.Availability != StepRuntimeAvailabilityStateEnum.Preview)
                {
                    continue;
                }

                switch (descriptor.RuntimeKey)
                {
                    case var key when key == StepRuntimeKeys.ArtifactProcess ||
                                       key == StepRuntimeKeys.ArtifactPython ||
                                       key == StepRuntimeKeys.ArtifactJavaScript ||
                                       key == StepRuntimeKeys.ArtifactDotnetProcess:
                        capabilities.Add(Capability("Artifact", descriptor.RuntimeKey));
                        break;
                    case var key when key == StepRuntimeKeys.ExternalRest:
                        capabilities.Add(Capability("Registry", descriptor.RuntimeKey));
                        break;
                    case var key when key == StepRuntimeKeys.LegacyInlineRest:
                        capabilities.Add(Capability("Inline", descriptor.RuntimeKey));
                        break;
                    case var key when key == StepRuntimeKeys.HostExecutable:
                        capabilities.Add(Capability("Registry", descriptor.RuntimeKey));
                        break;
                }
            }

            return capabilities
                .GroupBy(capability => capability.SourceKind + "|" + capability.RuntimeKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static WorkerCapabilityDescriptor Capability(string sourceKind, RuntimeKey runtimeKey)
        {
            return new WorkerCapabilityDescriptor
            {
                ExecutionKey = "*",
                TenantScope = "*",
                SourceKind = sourceKind,
                RuntimeKey = runtimeKey.ToString(),
                SignatureHash = "*"
            };
        }

        private async Task<WorkerHelloAckMessage> ReceiveHelloAckAsync(ClientWebSocket socket, CancellationToken token)
        {
            string? text = await ReceiveTextAsync(socket, token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Server closed the socket before sending hello-ack.");

            WorkerHelloAckMessage? ack = JsonSerializer.Deserialize<WorkerHelloAckMessage>(text, WorkerProtocolSerialization.Options);
            if (ack == null || !string.Equals(ack.Type, WorkerFrameTypes.HelloAck, StringComparison.Ordinal))
                throw new InvalidOperationException("Expected hello-ack from server.");
            return ack;
        }

        private async Task HandleFrameAsync(ClientWebSocket socket, string json, CancellationToken token)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("type", out JsonElement typeElement))
                throw new InvalidOperationException("Worker frame did not include a type field.");

            string? type = typeElement.GetString();
            switch (type)
            {
                case WorkerFrameTypes.Assign:
                {
                    WorkerAssignMessage? assign = JsonSerializer.Deserialize<WorkerAssignMessage>(json, WorkerProtocolSerialization.Options);
                    if (assign == null) throw new InvalidOperationException("Invalid assign frame.");
                    _ = Task.Run(() => HandleAssignmentAsync(socket, assign, token), token);
                    break;
                }
                case WorkerFrameTypes.Drain:
                    _DrainMode = true;
                    _Logging.Info(_Header + "entered drain mode");
                    break;
                case WorkerFrameTypes.Resume:
                    _DrainMode = false;
                    _Logging.Info(_Header + "resumed accepting assignments");
                    break;
                default:
                    throw new InvalidOperationException("Unsupported worker frame type '" + type + "'.");
            }
        }

        private async Task HandleAssignmentAsync(ClientWebSocket socket, WorkerAssignMessage message, CancellationToken token)
        {
            RunAssignmentRecord assignment = message.Assignment ?? throw new InvalidOperationException("assign frame missing assignment.");
            FlowRunExecutionPlan plan = message.Plan ?? throw new InvalidOperationException("assign frame missing plan.");
            string sessionId = _WorkerSessionId ?? string.Empty;
            bool accepted = false;
            string? rejectionMessage = null;
            CancellationTokenSource? assignmentCts = null;

            try
            {
                if (string.IsNullOrWhiteSpace(sessionId) || !string.Equals(assignment.WorkerSessionId, sessionId, StringComparison.Ordinal))
                {
                    rejectionMessage = "Assignment was issued for a different worker session.";
                }
                else if (_DrainMode)
                {
                    rejectionMessage = "Worker is draining.";
                }
                else if (!WorkerDescriptorJson.SupportsPlan(WorkerDescriptorJson.SerializeCapabilities(BuildCapabilities()), plan))
                {
                    rejectionMessage = "Worker capabilities do not satisfy the execution plan.";
                }
                else
                {
                    lock (_AssignmentLock)
                    {
                        if (_ActiveAssignments.Count >= _Settings.MaxConcurrentRuns)
                        {
                            rejectionMessage = "Worker is at max concurrency.";
                        }
                        else
                        {
                            assignmentCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                            _ActiveAssignments[assignment.Id] = assignmentCts;
                            accepted = true;
                        }
                    }
                }

                await SendFrameAsync(socket, new WorkerAssignAckMessage
                {
                    WorkerId = _Settings.WorkerId,
                    WorkerSessionId = sessionId,
                    RunAssignmentId = assignment.Id,
                    LeaseToken = assignment.LeaseToken,
                    Accepted = accepted,
                    Message = rejectionMessage
                }, token).ConfigureAwait(false);

                if (!accepted || assignmentCts == null) return;

                _Logging.Info(
                    _Header +
                    "received work from server: assignment " + assignment.Id +
                    " run " + assignment.FlowRunId +
                    " flow " + plan.DataFlowId +
                    " attempt " + assignment.AttemptNumber +
                    " (active " + ActiveAssignmentCount() + "/" + _Settings.MaxConcurrentRuns + ")");

                Stopwatch runtime = Stopwatch.StartNew();
                RunCompletionReport completion = await ExecuteAssignmentAsync(assignment, plan, assignmentCts.Token).ConfigureAwait(false);
                runtime.Stop();

                _Logging.Info(
                    _Header +
                    "completed assignment " + assignment.Id +
                    " run " + assignment.FlowRunId +
                    " flow " + plan.DataFlowId +
                    " with state " + completion.FinalState +
                    " in " + FormatMilliseconds(runtime.Elapsed.TotalMilliseconds) + "ms");

                if (socket.State == WebSocketState.Open)
                {
                    await SendFrameAsync(socket, new WorkerRunCompletedMessage { Completion = completion }, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Assignment cancellation is expected during disconnect/shutdown.
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "assignment " + assignment.Id + " failed before completion send: " + ex.Message);
                if (accepted && socket.State == WebSocketState.Open)
                {
                    RunCompletionReport completion = new RunCompletionReport
                    {
                        FlowRunId = assignment.FlowRunId,
                        RunAssignmentId = assignment.Id,
                        WorkerId = _Settings.WorkerId,
                        WorkerSessionId = _WorkerSessionId,
                        LeaseToken = assignment.LeaseToken,
                        FinalState = FlowRunStateEnum.Exception,
                        ErrorMessage = ex.Message,
                        ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                        CompletedUtc = DateTime.UtcNow
                    };
                    await SendFrameAsync(socket, new WorkerRunCompletedMessage { Completion = completion }, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                if (assignmentCts != null)
                {
                    lock (_AssignmentLock)
                    {
                        _ActiveAssignments.Remove(assignment.Id);
                    }
                    assignmentCts.Dispose();
                }
            }
        }

        private async Task<RunCompletionReport> ExecuteAssignmentAsync(RunAssignmentRecord assignment, FlowRunExecutionPlan plan, CancellationToken token)
        {
            BufferedFlowMetricsStore bufferedMetrics = new BufferedFlowMetricsStore(plan.TenantId, plan.FlowRunId);
            RunLogSession? runLogs = await _RunLogs.CreateSessionAsync(new RunLogSessionContext
            {
                FlowRunId = assignment.FlowRunId,
                TenantId = plan.TenantId,
                DataFlowId = plan.DataFlowId,
                AttemptNumber = assignment.AttemptNumber,
                RunAssignmentId = assignment.Id,
                WorkerId = _Settings.WorkerId,
                NodeKind = ExecutionNodeKindEnum.Worker.ToString()
            }, token).ConfigureAwait(false);

            using RemoteArtifactBlobStore blobStore = new RemoteArtifactBlobStore(
                _Settings.ServerEndpoint,
                _Settings.WorkerId,
                _Settings.WorkerToken,
                assignment.Id,
                assignment.LeaseToken,
                _Settings.RequestTimeoutMs);

            StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(
                runtimes: _Settings.Runtimes,
                artifactBlobStore: blobStore,
                externalCapacity: _ExternalCapacity);

            RegistryDataFlowRunner runner = new RegistryDataFlowRunner(new ExecutionPlanStepResolver(plan), registry)
            {
                MetricsStore = bufferedMetrics,
                RunLogs = runLogs
            };

            StepRequest request = new StepRequest
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
                    using JsonDocument input = JsonDocument.Parse(plan.InitialInputData);
                    request.Data = input.RootElement.Clone();
                }
                catch (JsonException)
                {
                    request.Data = plan.InitialInputData;
                }
            }

            using CancellationTokenSource executionCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            Stopwatch runtime = Stopwatch.StartNew();
            if (runLogs != null)
            {
                await runLogs.AppendWorkerAsync("Info", "Worker accepted the assignment and started execution.", token).ConfigureAwait(false);
            }

            Task<StepResult> runTask = runner.Run(plan.Flow, request, plan.ExecutionSnapshot, executionCts.Token);
            Task timeoutTask = _Settings.MaxTaskTimeoutMs > 0
                ? Task.Delay(_Settings.MaxTaskTimeoutMs)
                : Task.Delay(Timeout.InfiniteTimeSpan);
            Task cancelTask = Task.Delay(Timeout.InfiniteTimeSpan, token);

            try
            {
                Task completedTask = await Task.WhenAny(runTask, timeoutTask, cancelTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    try { executionCts.Cancel(); } catch { /* ignore */ }
                    ObserveTaskCompletion(runTask);
                    runtime.Stop();
                    if (runLogs != null)
                    {
                        await runLogs.AppendWorkerAsync(
                            "Error",
                            "Assignment exceeded maxTaskTimeoutMs of " + _Settings.MaxTaskTimeoutMs + " after " + FormatMilliseconds(runtime.Elapsed.TotalMilliseconds) + "ms.",
                            token).ConfigureAwait(false);
                    }

                    return new RunCompletionReport
                    {
                        FlowRunId = assignment.FlowRunId,
                        RunAssignmentId = assignment.Id,
                        WorkerId = _Settings.WorkerId,
                        WorkerSessionId = _WorkerSessionId,
                        LeaseToken = assignment.LeaseToken,
                        FinalState = FlowRunStateEnum.Exception,
                        ErrorMessage = "Worker task exceeded maxTaskTimeoutMs of " + _Settings.MaxTaskTimeoutMs + ".",
                        ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                        StepRuns = bufferedMetrics.Snapshot(),
                        CompletedUtc = DateTime.UtcNow
                    };
                }

                if (completedTask == cancelTask)
                {
                    try { executionCts.Cancel(); } catch { /* ignore */ }
                    ObserveTaskCompletion(runTask);
                    runtime.Stop();
                    if (runLogs != null)
                    {
                        await runLogs.AppendWorkerAsync(
                            "Warning",
                            "Assignment was cancelled after " + FormatMilliseconds(runtime.Elapsed.TotalMilliseconds) + "ms.",
                            token).ConfigureAwait(false);
                    }

                    return new RunCompletionReport
                    {
                        FlowRunId = assignment.FlowRunId,
                        RunAssignmentId = assignment.Id,
                        WorkerId = _Settings.WorkerId,
                        WorkerSessionId = _WorkerSessionId,
                        LeaseToken = assignment.LeaseToken,
                        FinalState = FlowRunStateEnum.Cancelled,
                        ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                        StepRuns = bufferedMetrics.Snapshot(),
                        CompletedUtc = DateTime.UtcNow
                    };
                }

                StepResult result = await runTask.ConfigureAwait(false);
                runtime.Stop();
                if (runLogs != null)
                {
                    await runLogs.AppendWorkerAsync(
                        "Info",
                        "Assignment completed with result " + result.Result + " in " + FormatMilliseconds(runtime.Elapsed.TotalMilliseconds) + "ms.",
                        token).ConfigureAwait(false);
                }
                return new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = _Settings.WorkerId,
                    WorkerSessionId = _WorkerSessionId,
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
                    StepRuns = bufferedMetrics.Snapshot(),
                    CompletedUtc = DateTime.UtcNow
                };
            }
            catch (OperationCanceledException)
            {
                runtime.Stop();
                if (runLogs != null)
                {
                    await runLogs.AppendWorkerAsync(
                        "Warning",
                        "Assignment was cancelled after " + FormatMilliseconds(runtime.Elapsed.TotalMilliseconds) + "ms.",
                        CancellationToken.None).ConfigureAwait(false);
                }
                return new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = _Settings.WorkerId,
                    WorkerSessionId = _WorkerSessionId,
                    LeaseToken = assignment.LeaseToken,
                    FinalState = FlowRunStateEnum.Cancelled,
                    ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                    StepRuns = bufferedMetrics.Snapshot(),
                    CompletedUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                runtime.Stop();
                if (runLogs != null)
                {
                    await runLogs.AppendWorkerAsync(
                        "Error",
                        "Assignment crashed after " + FormatMilliseconds(runtime.Elapsed.TotalMilliseconds) + "ms: " + ex.Message,
                        CancellationToken.None).ConfigureAwait(false);
                }
                return new RunCompletionReport
                {
                    FlowRunId = assignment.FlowRunId,
                    RunAssignmentId = assignment.Id,
                    WorkerId = _Settings.WorkerId,
                    WorkerSessionId = _WorkerSessionId,
                    LeaseToken = assignment.LeaseToken,
                    FinalState = FlowRunStateEnum.Exception,
                    ErrorMessage = ex.Message,
                    ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot),
                    StepRuns = bufferedMetrics.Snapshot(),
                    CompletedUtc = DateTime.UtcNow
                };
            }
        }

        private async Task HeartbeatLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                try
                {
                    string? sessionId = _WorkerSessionId;
                    if (string.IsNullOrWhiteSpace(sessionId)) return;

                    await SendFrameAsync(socket, new WorkerHeartbeatMessage
                    {
                        WorkerId = _Settings.WorkerId,
                        WorkerSessionId = sessionId,
                        ActiveRuns = ActiveAssignmentCount(),
                        SentUtc = DateTime.UtcNow
                    }, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "heartbeat send failed: " + ex.Message);
                    break;
                }

                await Task.Delay(_HeartbeatIntervalMs, token).ConfigureAwait(false);
            }
        }

        private async Task SendFrameAsync(ClientWebSocket socket, object frame, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(frame, WorkerProtocolSerialization.Options);
            byte[] payload = Encoding.UTF8.GetBytes(json);
            await _SendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, endOfMessage: true, token).ConfigureAwait(false);
            }
            finally
            {
                _SendLock.Release();
            }
        }

        private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, CancellationToken token)
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);
            using MemoryStream ms = new MemoryStream();

            while (true)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.Count > 0) ms.Write(buffer.Array!, buffer.Offset, result.Count);
                if (result.EndOfMessage)
                {
                    if (result.MessageType != WebSocketMessageType.Text)
                        throw new InvalidOperationException("Worker only supports text frames.");
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }

        private int ActiveAssignmentCount()
        {
            lock (_AssignmentLock)
            {
                return _ActiveAssignments.Count;
            }
        }

        private void CancelActiveAssignments()
        {
            List<CancellationTokenSource> sources;
            lock (_AssignmentLock)
            {
                sources = _ActiveAssignments.Values.ToList();
                _ActiveAssignments.Clear();
            }

            foreach (CancellationTokenSource source in sources)
            {
                try { source.Cancel(); } catch { /* ignore */ }
                source.Dispose();
            }
        }

        private static string? SerializeOutput(object? data)
        {
            if (data == null) return null;
            try { return JsonSerializer.Serialize(data); }
            catch (NotSupportedException) { return data.ToString(); }
        }

        private static string FormatMilliseconds(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static void ObserveTaskCompletion(Task task)
        {
            _ = task.ContinueWith(
                completed =>
                {
                    try
                    {
                        _ = completed.Exception;
                    }
                    catch
                    {
                        // Ignore background completion observation failures.
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private static Uri BuildWebSocketEndpoint(string serverEndpoint)
        {
            Uri http = new Uri(serverEndpoint.TrimEnd('/'), UriKind.Absolute);
            string scheme = string.Equals(http.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            UriBuilder builder = new UriBuilder(http)
            {
                Scheme = scheme,
                Path = "/v1.0/workers/connect"
            };
            return builder.Uri;
        }
    }
}
