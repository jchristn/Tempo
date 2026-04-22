namespace Tempo.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Database.Common;
    using Tempo.Core.Database.Postgresql;
    using Tempo.Core.Database.SqlServer;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Security;
    using Tempo.Core.Services;

    /// <summary>
    /// Database-backed assignment and worker-state persistence.
    /// </summary>
    public class RunAssignmentStore : IRunAssignmentStore
    {
        private readonly DatabaseDriverBase _Database;
        private readonly SqlDialect _Dialect;
        private readonly Tempo.Core.Settings.EngineSettings _Settings;

        /// <summary>Instantiate.</summary>
        public RunAssignmentStore(DatabaseDriverBase database, Tempo.Core.Settings.EngineSettings settings)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Dialect = ResolveDialect(database);
        }

        /// <inheritdoc/>
        public async Task<FlowRun?> ReadNextPendingAsync(CancellationToken token = default)
        {
            DataTable dt = await _Database.ExecuteQueryAsync(
                "SELECT id FROM flow_runs WHERE state = 'Queued' AND (dispatch_state IS NULL OR dispatch_state = 'Pending') AND dispatch_attempt < " +
                _Settings.MaxAssignmentAttempts.ToString(CultureInfo.InvariantCulture) +
                " ORDER BY created_utc ASC " + _Dialect.Paging(1, 0) + ";",
                false,
                token).ConfigureAwait(false);

            if (dt.Rows.Count < 1) return null;
            string id = dt.Rows[0][0]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) return null;
            return await _Database.FlowRuns.ReadGlobalAsync(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<RunAssignmentRecord> CreateAssignmentAsync(FlowRun run, RunExecutorDescriptor executor, FlowRunExecutionPlan plan, CancellationToken token = default)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            DateTime now = DateTime.UtcNow;
            RunAssignmentRecord assignment = new RunAssignmentRecord
            {
                FlowRunId = run.Id,
                WorkerId = executor.WorkerId,
                WorkerSessionId = executor.NodeKind == ExecutionNodeKindEnum.Server ? null : executor.WorkerSessionId,
                AttemptNumber = run.DispatchAttempt + 1,
                State = RunAssignmentStateEnum.Assigned,
                LeaseToken = IdGenerator.GenerateNonceId(),
                AssignedUtc = now,
                LeaseExpiresUtc = now.AddMilliseconds(_Settings.LeaseDurationMs)
            };

            long queueWaitMs = Math.Max(0, (long)(now - run.CreatedUtc).TotalMilliseconds);
            string snapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(plan.ExecutionSnapshot);

            List<string> batch = new List<string>
            {
                "INSERT INTO run_assignments(id, flow_run_id, worker_id, worker_session_id, attempt_number, state, lease_token, lease_expires_utc, assigned_utc, completed_utc) VALUES (" +
                _Dialect.Quote(assignment.Id) + ", " + _Dialect.Quote(assignment.FlowRunId) + ", " + _Dialect.Quote(assignment.WorkerId) + ", " +
                _Dialect.Quote(assignment.WorkerSessionId) + ", " + assignment.AttemptNumber.ToString(CultureInfo.InvariantCulture) + ", " +
                _Dialect.Quote(assignment.State.ToString()) + ", " + _Dialect.Quote(assignment.LeaseToken) + ", " + _Dialect.Quote(assignment.LeaseExpiresUtc) + ", " +
                _Dialect.Quote(assignment.AssignedUtc) + ", NULL);",

                "UPDATE flow_runs SET state = 'Running', dispatch_state = 'Assigned', dispatch_attempt = " + assignment.AttemptNumber.ToString(CultureInfo.InvariantCulture) +
                ", assigned_worker_id = " + _Dialect.Quote(executor.WorkerId) +
                ", run_assignment_id = " + _Dialect.Quote(assignment.Id) +
                ", queue_wait_ms = " + queueWaitMs.ToString(CultureInfo.InvariantCulture) +
                ", assigned_utc = " + _Dialect.Quote(now) +
                ", lease_expires_utc = " + _Dialect.Quote(assignment.LeaseExpiresUtc) +
                ", execution_node_kind = " + _Dialect.Quote(executor.NodeKind.ToString()) +
                ", execution_snapshot_json = " + _Dialect.Quote(snapshotJson) +
                ", started_utc = " + _Dialect.Quote(now) +
                ", last_update_utc = " + _Dialect.Quote(now) +
                " WHERE id = " + _Dialect.Quote(run.Id) + " AND state = 'Queued' AND (dispatch_state IS NULL OR dispatch_state = 'Pending');",

                "SELECT * FROM flow_runs WHERE id = " + _Dialect.Quote(run.Id) + ";"
            };

            DataTable verify = await _Database.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            if (verify.Rows.Count < 1) throw new InvalidOperationException("Assignment verification failed for run '" + run.Id + "'.");

            FlowRun current = MapFlowRun(verify.Rows[0]);
            if (!string.Equals(current.RunAssignmentId, assignment.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Assignment '" + assignment.Id + "' was not persisted for run '" + run.Id + "'.");
            }

            return assignment;
        }

        /// <inheritdoc/>
        public async Task<bool> CancelQueuedAsync(string tenantId, string flowRunId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(flowRunId)) throw new ArgumentNullException(nameof(flowRunId));

            DateTime now = DateTime.UtcNow;
            DataTable verify = await _Database.ExecuteQueriesAsync(new[]
            {
                "UPDATE flow_runs SET state = 'Cancelled', dispatch_state = 'Cancelled', completed_utc = " + _Dialect.Quote(now) +
                ", last_update_utc = " + _Dialect.Quote(now) +
                " WHERE tenant_id = " + _Dialect.Quote(tenantId) + " AND id = " + _Dialect.Quote(flowRunId) +
                " AND state = 'Queued' AND (dispatch_state IS NULL OR dispatch_state = 'Pending');",
                "SELECT state, dispatch_state FROM flow_runs WHERE tenant_id = " + _Dialect.Quote(tenantId) + " AND id = " + _Dialect.Quote(flowRunId) + ";"
            }, true, token).ConfigureAwait(false);

            if (verify.Rows.Count < 1) return false;
            string? state = verify.Rows[0]["state"]?.ToString();
            string? dispatchState = verify.Rows[0]["dispatch_state"]?.ToString();
            return string.Equals(state, FlowRunStateEnum.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dispatchState, FlowRunDispatchStateEnum.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<bool> CompleteAssignmentAsync(RunCompletionReport completion, CancellationToken token = default)
        {
            if (completion == null) throw new ArgumentNullException(nameof(completion));

            DataTable current = await _Database.ExecuteQueryAsync(
                "SELECT * FROM run_assignments WHERE id = " + _Dialect.Quote(completion.RunAssignmentId) + ";",
                false,
                token).ConfigureAwait(false);

            if (current.Rows.Count < 1) return false;

            RunAssignmentRecord assignment = MapAssignment(current.Rows[0]);
            if (!string.Equals(assignment.FlowRunId, completion.FlowRunId, StringComparison.Ordinal)) return false;
            if (!string.Equals(assignment.WorkerId, completion.WorkerId, StringComparison.Ordinal)) return false;
            if (!string.Equals(assignment.LeaseToken, completion.LeaseToken, StringComparison.Ordinal)) return false;
            if (!string.Equals(assignment.WorkerSessionId, completion.WorkerSessionId, StringComparison.Ordinal)) return false;
            if (assignment.CompletedUtc.HasValue) return false;

            if (completion.StepRuns.Count > 0)
            {
                foreach (StepRun stepRun in completion.StepRuns.OrderBy(r => r.Sequence))
                {
                    StepRun toPersist = stepRun;
                    if (string.IsNullOrWhiteSpace(toPersist.TenantId) ||
                        string.IsNullOrWhiteSpace(toPersist.FlowRunId) ||
                        string.IsNullOrWhiteSpace(toPersist.DataFlowId) ||
                        string.IsNullOrWhiteSpace(toPersist.StepId))
                    {
                        continue;
                    }

                    await _Database.FlowRuns.CreateStepRunAsync(toPersist, token).ConfigureAwait(false);
                }
            }

            RunAssignmentStateEnum assignmentState = completion.FinalState switch
            {
                FlowRunStateEnum.Succeeded => RunAssignmentStateEnum.Succeeded,
                FlowRunStateEnum.Cancelled => RunAssignmentStateEnum.Cancelled,
                FlowRunStateEnum.Failed => RunAssignmentStateEnum.Failed,
                FlowRunStateEnum.Exception => RunAssignmentStateEnum.Exception,
                _ => RunAssignmentStateEnum.Failed
            };

            FlowRunDispatchStateEnum dispatchState = completion.FinalState switch
            {
                FlowRunStateEnum.Succeeded => FlowRunDispatchStateEnum.Completed,
                FlowRunStateEnum.Cancelled => FlowRunDispatchStateEnum.Cancelled,
                FlowRunStateEnum.Failed => FlowRunDispatchStateEnum.Failed,
                FlowRunStateEnum.Exception => FlowRunDispatchStateEnum.Failed,
                _ => FlowRunDispatchStateEnum.Failed
            };

            DataTable verify = await _Database.ExecuteQueriesAsync(new[]
            {
                "UPDATE run_assignments SET state = " + _Dialect.Quote(assignmentState.ToString()) +
                ", completed_utc = " + _Dialect.Quote(completion.CompletedUtc) +
                " WHERE id = " + _Dialect.Quote(completion.RunAssignmentId) + ";",

                "UPDATE flow_runs SET state = " + _Dialect.Quote(completion.FinalState.ToString()) +
                ", dispatch_state = " + _Dialect.Quote(dispatchState.ToString()) +
                ", output_data = " + _Dialect.Quote(completion.OutputData) +
                ", error_message = " + _Dialect.Quote(completion.ErrorMessage) +
                ", execution_snapshot_json = " + _Dialect.Quote(completion.ExecutionSnapshotJson) +
                ", completed_utc = " + _Dialect.Quote(completion.CompletedUtc) +
                ", lease_expires_utc = NULL, last_update_utc = " + _Dialect.Quote(completion.CompletedUtc) +
                " WHERE id = " + _Dialect.Quote(completion.FlowRunId) + " AND run_assignment_id = " + _Dialect.Quote(completion.RunAssignmentId) + ";",

                "SELECT * FROM run_assignments WHERE id = " + _Dialect.Quote(completion.RunAssignmentId) + ";"
            }, true, token).ConfigureAwait(false);

            if (verify.Rows.Count < 1) return false;
            RunAssignmentRecord updated = MapAssignment(verify.Rows[0]);
            return updated.CompletedUtc.HasValue && updated.State == assignmentState;
        }

        /// <inheritdoc/>
        public async Task FailPendingRunAsync(FlowRun run, FlowRunStateEnum finalState, string errorMessage, CancellationToken token = default)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));

            DateTime now = DateTime.UtcNow;
            await _Database.ExecuteQueryAsync(
                "UPDATE flow_runs SET state = " + _Dialect.Quote(finalState.ToString()) +
                ", dispatch_state = " + _Dialect.Quote(FlowRunDispatchStateEnum.Failed.ToString()) +
                ", error_message = " + _Dialect.Quote(errorMessage) +
                ", completed_utc = " + _Dialect.Quote(now) +
                ", last_update_utc = " + _Dialect.Quote(now) +
                " WHERE id = " + _Dialect.Quote(run.Id) + " AND state = 'Queued' AND (dispatch_state IS NULL OR dispatch_state = 'Pending');",
                false,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> RecoverExpiredAssignmentsAsync(DateTime utcNow, CancellationToken token = default)
        {
            DataTable dt = await _Database.ExecuteQueryAsync(
                "SELECT * FROM run_assignments WHERE completed_utc IS NULL AND state = 'Assigned' AND lease_expires_utc < " + _Dialect.Quote(utcNow) + ";",
                false,
                token).ConfigureAwait(false);

            int recovered = 0;
            foreach (DataRow row in dt.Rows)
            {
                if (await RecoverAssignmentAsync(MapAssignment(row), utcNow, "lease_expired", token).ConfigureAwait(false))
                {
                    recovered++;
                }
            }

            return recovered;
        }

        /// <inheritdoc/>
        public async Task EnsureWorkerAsync(RunExecutorDescriptor executor, CancellationToken token = default)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            DateTime now = DateTime.UtcNow;
            DataTable existing = await _Database.ExecuteQueryAsync(
                "SELECT id FROM workers WHERE id = " + _Dialect.Quote(executor.WorkerId) + ";",
                false,
                token).ConfigureAwait(false);

            List<string> batch = new List<string>();
            if (existing.Rows.Count < 1)
            {
                batch.Add(
                    "INSERT INTO workers(id, name, kind, state, enabled, drain_mode, version, host_name, labels_json, capabilities_json, max_concurrent_runs, max_task_timeout_ms, last_heartbeat_utc, created_utc) VALUES (" +
                    _Dialect.Quote(executor.WorkerId) + ", " + _Dialect.Quote(executor.Name) + ", " + _Dialect.Quote(executor.Kind) + ", " + _Dialect.Quote(executor.State) + ", " +
                    _Dialect.Bit(executor.Enabled) + ", " + _Dialect.Bit(executor.DrainMode) + ", " + _Dialect.Quote(executor.Version) + ", " + _Dialect.Quote(executor.HostName) + ", " +
                    _Dialect.Quote(executor.LabelsJson) + ", " + _Dialect.Quote(executor.CapabilitiesJson) + ", " + executor.MaxConcurrentRuns.ToString(CultureInfo.InvariantCulture) + ", " +
                    executor.MaxTaskTimeoutMs.ToString(CultureInfo.InvariantCulture) + ", " +
                    _Dialect.Quote(now) + ", " + _Dialect.Quote(now) + ");");
            }
            else
            {
                batch.Add(
                    "UPDATE workers SET name = " + _Dialect.Quote(executor.Name) + ", kind = " + _Dialect.Quote(executor.Kind) + ", state = 'Online', enabled = " + _Dialect.Bit(executor.Enabled) +
                    ", drain_mode = " + _Dialect.Bit(executor.DrainMode) + ", version = " + _Dialect.Quote(executor.Version) + ", host_name = " + _Dialect.Quote(executor.HostName) +
                    ", labels_json = " + _Dialect.Quote(executor.LabelsJson) + ", capabilities_json = " + _Dialect.Quote(executor.CapabilitiesJson) +
                    ", max_concurrent_runs = " + executor.MaxConcurrentRuns.ToString(CultureInfo.InvariantCulture) +
                    ", max_task_timeout_ms = " + executor.MaxTaskTimeoutMs.ToString(CultureInfo.InvariantCulture) +
                    ", last_heartbeat_utc = " + _Dialect.Quote(now) + " WHERE id = " + _Dialect.Quote(executor.WorkerId) + ";");
            }

            if (!string.IsNullOrWhiteSpace(executor.WorkerSessionId))
            {
                batch.Add(
                    "INSERT INTO worker_sessions(id, worker_id, connected_utc, disconnected_utc, disconnect_reason, protocol_version) VALUES (" +
                    _Dialect.Quote(executor.WorkerSessionId) + ", " + _Dialect.Quote(executor.WorkerId) + ", " + _Dialect.Quote(now) + ", NULL, NULL, " + _Dialect.Quote("1.0") + ");");
            }

            if (batch.Count > 0)
            {
                await _Database.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task TouchWorkerHeartbeatAsync(RunExecutorDescriptor executor, DateTime utcNow, CancellationToken token = default)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            return _Database.ExecuteQueryAsync(
                "UPDATE workers SET state = 'Online', last_heartbeat_utc = " + _Dialect.Quote(utcNow) +
                ", max_concurrent_runs = " + executor.MaxConcurrentRuns.ToString(CultureInfo.InvariantCulture) +
                ", max_task_timeout_ms = " + executor.MaxTaskTimeoutMs.ToString(CultureInfo.InvariantCulture) +
                ", labels_json = " + _Dialect.Quote(executor.LabelsJson) +
                ", capabilities_json = " + _Dialect.Quote(executor.CapabilitiesJson) +
                " WHERE id = " + _Dialect.Quote(executor.WorkerId) + " AND enabled = " + _Dialect.Bit(true) + ";",
                false,
                token);
        }

        /// <inheritdoc/>
        public async Task MarkWorkerDisconnectedAsync(RunExecutorDescriptor executor, string reason, CancellationToken token = default)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            DateTime now = DateTime.UtcNow;
            List<string> batch = new List<string>
            {
                "UPDATE workers SET state = 'Offline', last_heartbeat_utc = " + _Dialect.Quote(now) + " WHERE id = " + _Dialect.Quote(executor.WorkerId) + ";"
            };

            if (!string.IsNullOrWhiteSpace(executor.WorkerSessionId))
            {
                batch.Add(
                    "UPDATE worker_sessions SET disconnected_utc = COALESCE(disconnected_utc, " + _Dialect.Quote(now) + "), disconnect_reason = " + _Dialect.Quote(reason) +
                    " WHERE id = " + _Dialect.Quote(executor.WorkerSessionId) + ";");
            }

            await _Database.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
        }

        /// <summary>Read all workers in creation order.</summary>
        public async Task<List<WorkerRecord>> ListWorkersAsync(CancellationToken token = default)
        {
            DataTable dt = await _Database.ExecuteQueryAsync(
                "SELECT * FROM workers ORDER BY created_utc ASC;",
                false,
                token).ConfigureAwait(false);

            List<WorkerRecord> workers = new List<WorkerRecord>();
            foreach (DataRow row in dt.Rows) workers.Add(MapWorker(row));
            return workers;
        }

        /// <summary>Read one worker by identifier.</summary>
        public async Task<WorkerRecord?> ReadWorkerAsync(string workerId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentNullException(nameof(workerId));
            DataTable dt = await _Database.ExecuteQueryAsync(
                "SELECT * FROM workers WHERE id = " + _Dialect.Quote(workerId) + ";",
                false,
                token).ConfigureAwait(false);
            if (dt.Rows.Count < 1) return null;
            return MapWorker(dt.Rows[0]);
        }

        /// <summary>Read the most recent worker session.</summary>
        public async Task<WorkerSessionRecord?> ReadLatestWorkerSessionAsync(string workerId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentNullException(nameof(workerId));
            DataTable dt = await _Database.ExecuteQueryAsync(
                "SELECT * FROM worker_sessions WHERE worker_id = " + _Dialect.Quote(workerId) + " ORDER BY connected_utc DESC " + _Dialect.Paging(1, 0) + ";",
                false,
                token).ConfigureAwait(false);
            if (dt.Rows.Count < 1) return null;
            return MapWorkerSession(dt.Rows[0]);
        }

        /// <summary>Read active assignment counts keyed by worker id.</summary>
        public async Task<Dictionary<string, int>> ReadActiveAssignmentCountsAsync(CancellationToken token = default)
        {
            DataTable dt = await _Database.ExecuteQueryAsync(
                "SELECT worker_id, COUNT(*) AS assignment_count FROM run_assignments WHERE completed_utc IS NULL AND state = 'Assigned' GROUP BY worker_id;",
                false,
                token).ConfigureAwait(false);

            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (DataRow row in dt.Rows)
            {
                string workerId = row["worker_id"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(workerId)) continue;
                counts[workerId] = Converters.Int(row, "assignment_count");
            }
            return counts;
        }

        /// <summary>Set worker drain mode.</summary>
        public async Task<bool> SetWorkerDrainModeAsync(string workerId, bool drainMode, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentNullException(nameof(workerId));
            await _Database.ExecuteQueryAsync(
                "UPDATE workers SET drain_mode = " + _Dialect.Bit(drainMode) + " WHERE id = " + _Dialect.Quote(workerId) + ";",
                false,
                token).ConfigureAwait(false);
            return await ReadWorkerAsync(workerId, token).ConfigureAwait(false) != null;
        }

        /// <summary>Set whether a worker is enabled to connect and accept work.</summary>
        public async Task<bool> SetWorkerEnabledAsync(string workerId, bool enabled, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentNullException(nameof(workerId));

            string sql = "UPDATE workers SET enabled = " + _Dialect.Bit(enabled);
            if (!enabled)
            {
                sql += ", state = 'Offline'";
            }
            sql += " WHERE id = " + _Dialect.Quote(workerId) + ";";

            await _Database.ExecuteQueryAsync(sql, false, token).ConfigureAwait(false);
            return await ReadWorkerAsync(workerId, token).ConfigureAwait(false) != null;
        }

        /// <summary>Issue a new worker token and store only its hash.</summary>
        public async Task<WorkerTokenIssueResult> RotateWorkerTokenAsync(string workerId, string? workerName = null, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentNullException(nameof(workerId));

            string plaintext = IdGenerator.GenerateSecretKey();
            string hash = PasswordHasher.Hash(plaintext);
            DateTime now = DateTime.UtcNow;
            WorkerRecord? existing = await ReadWorkerAsync(workerId, token).ConfigureAwait(false);

            if (existing == null)
            {
                string name = string.IsNullOrWhiteSpace(workerName) ? workerId : workerName.Trim();
                await _Database.ExecuteQueryAsync(
                    "INSERT INTO workers(id, name, kind, state, enabled, drain_mode, version, host_name, labels_json, capabilities_json, max_concurrent_runs, max_task_timeout_ms, token_hash, token_last_rotated_utc, last_heartbeat_utc, created_utc) VALUES (" +
                    _Dialect.Quote(workerId) + ", " + _Dialect.Quote(name) + ", " + _Dialect.Quote("Worker") + ", " + _Dialect.Quote("Offline") + ", " +
                    _Dialect.Bit(true) + ", " + _Dialect.Bit(false) + ", NULL, NULL, " + _Dialect.Quote("[]") + ", " + _Dialect.Quote("[]") + ", 1, " +
                    "0, " +
                    _Dialect.Quote(hash) + ", " + _Dialect.Quote(now) + ", NULL, " + _Dialect.Quote(now) + ");",
                    false,
                    token).ConfigureAwait(false);
            }
            else
            {
                await _Database.ExecuteQueryAsync(
                    "UPDATE workers SET token_hash = " + _Dialect.Quote(hash) + ", token_last_rotated_utc = " + _Dialect.Quote(now) +
                    " WHERE id = " + _Dialect.Quote(workerId) + ";",
                    false,
                    token).ConfigureAwait(false);
            }

            return new WorkerTokenIssueResult
            {
                WorkerId = workerId,
                Token = plaintext,
                IssuedUtc = now
            };
        }

        /// <summary>Authenticate a worker using its persisted token hash.</summary>
        public async Task<WorkerRecord?> AuthenticateWorkerAsync(string workerId, string workerToken, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(workerToken)) return null;
            WorkerRecord? worker = await ReadWorkerAsync(workerId, token).ConfigureAwait(false);
            if (worker == null) return null;
            if (!worker.Enabled) return null;
            if (string.IsNullOrWhiteSpace(worker.TokenHash)) return null;
            return PasswordHasher.Verify(workerToken, worker.TokenHash) ? worker : null;
        }

        /// <summary>Record worker activity.</summary>
        public Task RecordWorkerActivityAsync(WorkerActivityRecord activity, CancellationToken token = default)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            return _Database.ExecuteQueryAsync(
                "INSERT INTO worker_activity(id, worker_id, worker_session_id, flow_run_id, run_assignment_id, event_type, severity, message, payload_json, created_utc) VALUES (" +
                _Dialect.Quote(activity.Id) + ", " + _Dialect.Quote(activity.WorkerId) + ", " + _Dialect.Quote(activity.WorkerSessionId) + ", " +
                _Dialect.Quote(activity.FlowRunId) + ", " + _Dialect.Quote(activity.RunAssignmentId) + ", " + _Dialect.Quote(activity.EventType) + ", " +
                _Dialect.Quote(activity.Severity) + ", " + _Dialect.Quote(activity.Message) + ", " + _Dialect.Quote(activity.PayloadJson) + ", " +
                _Dialect.Quote(activity.CreatedUtc) + ");",
                false,
                token);
        }

        /// <summary>Upsert the current server instance heartbeat row.</summary>
        public async Task TouchServerInstanceAsync(ServerInstanceRecord instance, DateTime utcNow, CancellationToken token = default)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            DataTable existing = await _Database.ExecuteQueryAsync(
                "SELECT id FROM server_instances WHERE id = " + _Dialect.Quote(instance.Id) + ";",
                false,
                token).ConfigureAwait(false);

            if (existing.Rows.Count < 1)
            {
                await _Database.ExecuteQueryAsync(
                    "INSERT INTO server_instances(id, host_name, started_utc, last_heartbeat_utc, version) VALUES (" +
                    _Dialect.Quote(instance.Id) + ", " + _Dialect.Quote(instance.HostName) + ", " + _Dialect.Quote(instance.StartedUtc) + ", " +
                    _Dialect.Quote(utcNow) + ", " + _Dialect.Quote(instance.Version) + ");",
                    false,
                    token).ConfigureAwait(false);
            }
            else
            {
                await _Database.ExecuteQueryAsync(
                    "UPDATE server_instances SET host_name = " + _Dialect.Quote(instance.HostName) + ", last_heartbeat_utc = " + _Dialect.Quote(utcNow) +
                    ", version = " + _Dialect.Quote(instance.Version) + " WHERE id = " + _Dialect.Quote(instance.Id) + ";",
                    false,
                    token).ConfigureAwait(false);
            }
        }

        /// <summary>Read active server instances whose heartbeats are newer than the cutoff.</summary>
        public async Task<List<ServerInstanceRecord>> ListActiveServerInstancesAsync(DateTime cutoffUtc, CancellationToken token = default)
        {
            DataTable dt = await _Database.ExecuteQueryAsync(
                "SELECT * FROM server_instances WHERE last_heartbeat_utc >= " + _Dialect.Quote(cutoffUtc) + " ORDER BY started_utc ASC;",
                false,
                token).ConfigureAwait(false);

            List<ServerInstanceRecord> instances = new List<ServerInstanceRecord>();
            foreach (DataRow row in dt.Rows) instances.Add(MapServerInstance(row));
            return instances;
        }

        /// <summary>Recover active assignments owned by a disconnected worker session.</summary>
        public async Task<int> RecoverAssignmentsForWorkerSessionAsync(string workerId, string? workerSessionId, DateTime utcNow, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentNullException(nameof(workerId));

            string sql = "SELECT * FROM run_assignments WHERE completed_utc IS NULL AND state = 'Assigned' AND worker_id = " + _Dialect.Quote(workerId);
            if (!string.IsNullOrWhiteSpace(workerSessionId))
            {
                sql += " AND worker_session_id = " + _Dialect.Quote(workerSessionId);
            }
            sql += ";";

            DataTable dt = await _Database.ExecuteQueryAsync(sql, false, token).ConfigureAwait(false);
            int recovered = 0;
            foreach (DataRow row in dt.Rows)
            {
                if (await RecoverAssignmentAsync(MapAssignment(row), utcNow, "worker_disconnected", token).ConfigureAwait(false))
                {
                    recovered++;
                }
            }

            return recovered;
        }

        /// <summary>Validate worker access to an artifact download for one active assignment.</summary>
        public async Task<bool> ValidateWorkerArtifactAccessAsync(
            string workerId,
            string runAssignmentId,
            string leaseToken,
            string tenantId,
            string sha256,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(workerId) ||
                string.IsNullOrWhiteSpace(runAssignmentId) ||
                string.IsNullOrWhiteSpace(leaseToken) ||
                string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(sha256))
            {
                return false;
            }

            DataTable assignments = await _Database.ExecuteQueryAsync(
                "SELECT * FROM run_assignments WHERE id = " + _Dialect.Quote(runAssignmentId) + " AND worker_id = " + _Dialect.Quote(workerId) +
                " AND lease_token = " + _Dialect.Quote(leaseToken) + " AND completed_utc IS NULL;",
                false,
                token).ConfigureAwait(false);

            if (assignments.Rows.Count < 1) return false;
            RunAssignmentRecord assignment = MapAssignment(assignments.Rows[0]);

            FlowRun? run = await _Database.FlowRuns.ReadGlobalAsync(assignment.FlowRunId, token).ConfigureAwait(false);
            if (run == null || !string.Equals(run.TenantId, tenantId, StringComparison.Ordinal)) return false;

            FlowRunExecutionSnapshot snapshot = FlowRunExecutionSnapshotSerializer.Deserialize(run.ExecutionSnapshotJson, run.Id);
            return snapshot.ArtifactVersions.Values.Any(candidate => string.Equals(candidate.Sha256, sha256, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> RecoverAssignmentAsync(RunAssignmentRecord assignment, DateTime utcNow, string reason, CancellationToken token)
        {
            string failureMessage = "Run assignment '" + assignment.Id + "' was recovered after " + reason.Replace('_', ' ') + ".";
            bool exhausted = assignment.AttemptNumber >= _Settings.MaxAssignmentAttempts;

            List<string> batch = new List<string>
            {
                "UPDATE run_assignments SET state = " + _Dialect.Quote(RunAssignmentStateEnum.LeaseExpired.ToString()) +
                ", completed_utc = " + _Dialect.Quote(utcNow) +
                " WHERE id = " + _Dialect.Quote(assignment.Id) + " AND completed_utc IS NULL;"
            };

            if (exhausted)
            {
                batch.Add(
                    "UPDATE flow_runs SET state = " + _Dialect.Quote(FlowRunStateEnum.Failed.ToString()) +
                    ", dispatch_state = " + _Dialect.Quote(FlowRunDispatchStateEnum.Failed.ToString()) +
                    ", error_message = " + _Dialect.Quote("Maximum assignment attempts reached. " + failureMessage) +
                    ", completed_utc = " + _Dialect.Quote(utcNow) +
                    ", assigned_worker_id = NULL, run_assignment_id = NULL, assigned_utc = NULL, lease_expires_utc = NULL, execution_node_kind = NULL, started_utc = NULL, last_update_utc = " + _Dialect.Quote(utcNow) +
                    " WHERE id = " + _Dialect.Quote(assignment.FlowRunId) + " AND run_assignment_id = " + _Dialect.Quote(assignment.Id) + ";");
            }
            else
            {
                batch.Add(
                    "UPDATE flow_runs SET state = 'Queued', dispatch_state = 'Pending', assigned_worker_id = NULL, run_assignment_id = NULL, assigned_utc = NULL, lease_expires_utc = NULL, execution_node_kind = NULL, started_utc = NULL, last_update_utc = " + _Dialect.Quote(utcNow) +
                    " WHERE id = " + _Dialect.Quote(assignment.FlowRunId) + " AND run_assignment_id = " + _Dialect.Quote(assignment.Id) + ";");
            }

            batch.Add("SELECT completed_utc FROM run_assignments WHERE id = " + _Dialect.Quote(assignment.Id) + ";");
            DataTable verify = await _Database.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            return verify.Rows.Count > 0 && Converters.DateTimeOrNull(verify.Rows[0], "completed_utc").HasValue;
        }

        private static SqlDialect ResolveDialect(DatabaseDriverBase database)
        {
            return database.DatabaseType switch
            {
                DatabaseTypeEnum.Postgresql => new PostgresqlDialect(),
                DatabaseTypeEnum.SqlServer => new SqlServerDialect(),
                _ => SqlDialect.Ansi
            };
        }

        private static bool HasColumn(DataRow row, string name)
        {
            return row.Table.Columns.Contains(name);
        }

        private static FlowRun MapFlowRun(DataRow row)
        {
            FlowRun run = new FlowRun
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                DataFlowId = Converters.String(row, "data_flow_id"),
                TriggeredByUserId = Converters.StringOrNull(row, "triggered_by_user_id"),
                TriggerId = Converters.StringOrNull(row, "trigger_id"),
                SourceIp = Converters.StringOrNull(row, "source_ip"),
                State = Converters.EnumValue<FlowRunStateEnum>(row, "state", FlowRunStateEnum.Queued),
                InputData = Converters.StringOrNull(row, "input_data"),
                OutputData = Converters.StringOrNull(row, "output_data"),
                ErrorMessage = Converters.StringOrNull(row, "error_message"),
                ExecutionSnapshotJson = Converters.StringOrNull(row, "execution_snapshot_json"),
                DispatchState = Converters.EnumValue<FlowRunDispatchStateEnum>(row, "dispatch_state", FlowRunDispatchStateEnum.Pending),
                DispatchAttempt = Converters.Int(row, "dispatch_attempt"),
                AssignedWorkerId = Converters.StringOrNull(row, "assigned_worker_id"),
                RunAssignmentId = Converters.StringOrNull(row, "run_assignment_id"),
                QueueWaitMs = Converters.LongOrNull(row, "queue_wait_ms"),
                AssignedUtc = Converters.DateTimeOrNull(row, "assigned_utc"),
                LeaseExpiresUtc = Converters.DateTimeOrNull(row, "lease_expires_utc"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                StartedUtc = Converters.DateTimeOrNull(row, "started_utc"),
                CompletedUtc = Converters.DateTimeOrNull(row, "completed_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };

            string? executionNodeKind = Converters.StringOrNull(row, "execution_node_kind");
            if (!string.IsNullOrWhiteSpace(executionNodeKind) && Enum.TryParse(executionNodeKind, true, out ExecutionNodeKindEnum parsed))
            {
                run.ExecutionNodeKind = parsed;
            }

            return run;
        }

        private static RunAssignmentRecord MapAssignment(DataRow row)
        {
            return new RunAssignmentRecord
            {
                Id = Converters.String(row, "id"),
                FlowRunId = Converters.String(row, "flow_run_id"),
                WorkerId = Converters.String(row, "worker_id"),
                WorkerSessionId = Converters.StringOrNull(row, "worker_session_id"),
                AttemptNumber = Converters.Int(row, "attempt_number", 1),
                State = Converters.EnumValue<RunAssignmentStateEnum>(row, "state", RunAssignmentStateEnum.Assigned),
                LeaseToken = Converters.String(row, "lease_token"),
                LeaseExpiresUtc = Converters.DateTime(row, "lease_expires_utc"),
                AssignedUtc = Converters.DateTime(row, "assigned_utc"),
                CompletedUtc = Converters.DateTimeOrNull(row, "completed_utc")
            };
        }

        private static WorkerRecord MapWorker(DataRow row)
        {
            return new WorkerRecord
            {
                Id = Converters.String(row, "id"),
                Name = Converters.String(row, "name"),
                Kind = Converters.StringOrNull(row, "kind") ?? "Worker",
                State = Converters.StringOrNull(row, "state") ?? "Offline",
                Enabled = Converters.Bool(row, "enabled"),
                DrainMode = Converters.Bool(row, "drain_mode"),
                Version = Converters.StringOrNull(row, "version"),
                HostName = Converters.StringOrNull(row, "host_name"),
                LabelsJson = Converters.StringOrNull(row, "labels_json") ?? "[]",
                CapabilitiesJson = HasColumn(row, "capabilities_json") ? (Converters.StringOrNull(row, "capabilities_json") ?? "[]") : "[]",
                MaxConcurrentRuns = Converters.Int(row, "max_concurrent_runs", 1),
                MaxTaskTimeoutMs = HasColumn(row, "max_task_timeout_ms") ? Converters.Int(row, "max_task_timeout_ms", 0) : 0,
                TokenHash = HasColumn(row, "token_hash") ? Converters.StringOrNull(row, "token_hash") : null,
                TokenLastRotatedUtc = HasColumn(row, "token_last_rotated_utc") ? Converters.DateTimeOrNull(row, "token_last_rotated_utc") : null,
                LastHeartbeatUtc = Converters.DateTimeOrNull(row, "last_heartbeat_utc"),
                CreatedUtc = Converters.DateTime(row, "created_utc")
            };
        }

        private static WorkerSessionRecord MapWorkerSession(DataRow row)
        {
            return new WorkerSessionRecord
            {
                Id = Converters.String(row, "id"),
                WorkerId = Converters.String(row, "worker_id"),
                ConnectedUtc = Converters.DateTime(row, "connected_utc"),
                DisconnectedUtc = Converters.DateTimeOrNull(row, "disconnected_utc"),
                DisconnectReason = Converters.StringOrNull(row, "disconnect_reason"),
                ProtocolVersion = Converters.StringOrNull(row, "protocol_version")
            };
        }

        private static ServerInstanceRecord MapServerInstance(DataRow row)
        {
            return new ServerInstanceRecord
            {
                Id = Converters.String(row, "id"),
                HostName = HasColumn(row, "host_name") ? Converters.StringOrNull(row, "host_name") : null,
                StartedUtc = Converters.DateTime(row, "started_utc"),
                LastHeartbeatUtc = Converters.DateTime(row, "last_heartbeat_utc"),
                Version = Converters.StringOrNull(row, "version")
            };
        }
    }
}
