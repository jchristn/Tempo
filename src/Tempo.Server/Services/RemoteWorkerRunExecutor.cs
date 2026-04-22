namespace Tempo.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Workers;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Live remote worker session exposed through the same executor contract as the local pseudo-worker.
    /// </summary>
    public sealed class RemoteWorkerRunExecutor : IRunExecutor
    {
        private readonly WebSocketSession _Session;
        private readonly object _Lock = new object();
        private readonly HashSet<string> _ActiveAssignments = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Instantiate.</summary>
        public RemoteWorkerRunExecutor(RunExecutorDescriptor descriptor, WebSocketSession session)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _Session = session ?? throw new ArgumentNullException(nameof(session));
            LastHeartbeatUtc = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        public RunExecutorDescriptor Descriptor { get; }

        /// <summary>UTC time of the last received heartbeat.</summary>
        public DateTime LastHeartbeatUtc { get; private set; }

        /// <summary>Whether the underlying websocket session is still connected.</summary>
        public bool IsConnected => _Session.IsConnected;

        /// <summary>Worker session identifier.</summary>
        public string WorkerSessionId => Descriptor.WorkerSessionId ?? string.Empty;

        /// <summary>Update heartbeat metadata from the worker.</summary>
        public void TouchHeartbeat(int activeRuns, DateTime utcNow)
        {
            lock (_Lock)
            {
                Descriptor.CurrentRunCount = Math.Max(ActiveAssignmentCountUnsafe(), Math.Max(0, activeRuns));
                LastHeartbeatUtc = utcNow;
            }
        }

        /// <summary>Mark an assignment as no longer active for this worker.</summary>
        public void CompleteAssignment(string runAssignmentId)
        {
            if (string.IsNullOrWhiteSpace(runAssignmentId)) return;

            lock (_Lock)
            {
                _ActiveAssignments.Remove(runAssignmentId);
                Descriptor.CurrentRunCount = ActiveAssignmentCountUnsafe();
            }
        }

        /// <summary>Mark the worker draining state locally.</summary>
        public void SetDrainMode(bool drainMode)
        {
            Descriptor.DrainMode = drainMode;
        }

        /// <summary>Mark the worker enabled or blocked locally.</summary>
        public void SetEnabled(bool enabled)
        {
            Descriptor.Enabled = enabled;
        }

        /// <summary>Send a drain command to the worker.</summary>
        public Task SendDrainAsync(string? message, CancellationToken token = default)
        {
            return SendAsync(new WorkerDrainMessage
            {
                WorkerId = Descriptor.WorkerId,
                Message = message
            }, token);
        }

        /// <summary>Send a resume command to the worker.</summary>
        public Task SendResumeAsync(string? message, CancellationToken token = default)
        {
            return SendAsync(new WorkerResumeMessage
            {
                WorkerId = Descriptor.WorkerId,
                Message = message
            }, token);
        }

        /// <summary>Close the underlying websocket session.</summary>
        public Task DisconnectAsync(string reason, CancellationToken token = default)
        {
            return _Session.CloseAsync(WebSocketCloseStatus.PolicyViolation, reason, token);
        }

        /// <inheritdoc/>
        public bool CanAcceptWork(FlowRunExecutionPlan plan)
        {
            if (plan == null) return false;
            if (!IsConnected) return false;
            if (!Descriptor.Enabled || Descriptor.DrainMode) return false;
            if (ActiveAssignmentCount() >= Descriptor.MaxConcurrentRuns) return false;
            if (!WorkerDescriptorJson.HasLabel(Descriptor.LabelsJson, plan.PlacementLabel)) return false;
            return WorkerDescriptorJson.SupportsPlan(Descriptor.CapabilitiesJson, plan);
        }

        /// <inheritdoc/>
        public async Task<RunCompletionReport?> ExecuteAsync(RunAssignmentRecord assignment, FlowRunExecutionPlan plan, CancellationToken token = default)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            if (!IsConnected) throw new InvalidOperationException("Worker session is not connected.");

            WorkerAssignMessage frame = new WorkerAssignMessage
            {
                Assignment = assignment,
                Plan = plan
            };

            lock (_Lock)
            {
                _ActiveAssignments.Add(assignment.Id);
                Descriptor.CurrentRunCount = ActiveAssignmentCountUnsafe();
            }

            try
            {
                await SendAsync(frame, token).ConfigureAwait(false);
                return null;
            }
            catch
            {
                CompleteAssignment(assignment.Id);
                throw;
            }
        }

        private int ActiveAssignmentCount()
        {
            lock (_Lock)
            {
                return ActiveAssignmentCountUnsafe();
            }
        }

        private int ActiveAssignmentCountUnsafe()
        {
            return _ActiveAssignments.Count;
        }

        private Task SendAsync(object frame, CancellationToken token)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(frame, WorkerProtocolSerialization.Options);
            return _Session.SendTextAsync(json, token);
        }
    }
}
