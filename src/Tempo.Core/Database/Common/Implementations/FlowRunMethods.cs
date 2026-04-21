namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using TempoStepResult = Tempo.Enums.StepResultTypeEnum;

    /// <summary>Driver-agnostic implementation of <see cref="IFlowRunMethods"/>.</summary>
    public class FlowRunMethods : IFlowRunMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public FlowRunMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<FlowRun> CreateAsync(FlowRun r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrWhiteSpace(r.Id)) r.Id = IdGenerator.GenerateFlowRunId();
            r.CreatedUtc = DateTime.UtcNow;
            r.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO flow_runs(id, tenant_id, data_flow_id, triggered_by_user_id, trigger_id, state, input_data, output_data, error_message, execution_snapshot_json, created_utc, started_utc, completed_utc, last_update_utc) VALUES (" +
                _D.Quote(r.Id) + ", " + _D.Quote(r.TenantId) + ", " + _D.Quote(r.DataFlowId) + ", " +
                _D.Quote(r.TriggeredByUserId) + ", " + _D.Quote(r.TriggerId) + ", " + _D.Quote(r.State.ToString()) + ", " +
                _D.Quote(r.InputData) + ", " + _D.Quote(r.OutputData) + ", " + _D.Quote(r.ErrorMessage) + ", " +
                _D.Quote(r.ExecutionSnapshotJson) + ", " + _D.Quote(r.CreatedUtc) + ", " + _D.Quote(r.StartedUtc) + ", " + _D.Quote(r.CompletedUtc) + ", " + _D.Quote(r.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<FlowRun> UpdateAsync(FlowRun r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            r.LastUpdateUtc = DateTime.UtcNow;
            await _Driver.ExecuteQueryAsync(
                "UPDATE flow_runs SET state = " + _D.Quote(r.State.ToString()) + ", input_data = " + _D.Quote(r.InputData) + ", " +
                "output_data = " + _D.Quote(r.OutputData) + ", error_message = " + _D.Quote(r.ErrorMessage) + ", " +
                "execution_snapshot_json = " + _D.Quote(r.ExecutionSnapshotJson) + ", " +
                "started_utc = " + _D.Quote(r.StartedUtc) + ", completed_utc = " + _D.Quote(r.CompletedUtc) + ", " +
                "last_update_utc = " + _D.Quote(r.LastUpdateUtc) +
                " WHERE id = " + _D.Quote(r.Id) + ";",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<bool> TransitionStateAsync(string id, FlowRunStateEnum newState, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            await _Driver.ExecuteQueryAsync(
                "UPDATE flow_runs SET state = " + _D.Quote(newState.ToString()) + ", last_update_utc = " + _D.Quote(DateTime.UtcNow) +
                " WHERE id = " + _D.Quote(id) + ";",
                false, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc/>
        public async Task<FlowRun?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM flow_runs WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<FlowRun?> ReadGlobalAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM flow_runs WHERE id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<FlowRun>> EnumerateAsync(FlowRunFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            List<string> clauses = new List<string>();
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenant_id = " + _D.Quote(filter.TenantId));
            if (!string.IsNullOrEmpty(filter.DataFlowId)) clauses.Add("data_flow_id = " + _D.Quote(filter.DataFlowId));
            if (filter.State.HasValue) clauses.Add("state = " + _D.Quote(filter.State.Value.ToString()));
            if (filter.FromUtc.HasValue) clauses.Add("created_utc >= " + _D.Quote(filter.FromUtc));
            if (filter.ToUtc.HasValue) clauses.Add("created_utc < " + _D.Quote(filter.ToUtc));
            string where = clauses.Count == 0 ? "" : " WHERE " + string.Join(" AND ", clauses);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM flow_runs" + where + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM flow_runs" + where + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<FlowRun> r = new EnumerationResult<FlowRun> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<FlowRun?> ClaimNextQueuedAsync(CancellationToken token = default)
        {
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM flow_runs WHERE state = 'Queued' ORDER BY created_utc ASC " + _D.Paging(1, 0) + ";",
                false, token).ConfigureAwait(false);
            if (dt.Rows.Count == 0) return null;

            FlowRun run = Map(dt.Rows[0]);
            await _Driver.ExecuteQueryAsync(
                "UPDATE flow_runs SET state = 'Running', started_utc = " + _D.Quote(DateTime.UtcNow) +
                ", last_update_utc = " + _D.Quote(DateTime.UtcNow) +
                " WHERE id = " + _D.Quote(run.Id) + " AND state = 'Queued';",
                false, token).ConfigureAwait(false);

            DataTable verify = await _Driver.ExecuteQueryAsync("SELECT state FROM flow_runs WHERE id = " + _D.Quote(run.Id) + ";", false, token).ConfigureAwait(false);
            if (verify.Rows.Count > 0 && verify.Rows[0][0].ToString() == "Running")
            {
                run.State = FlowRunStateEnum.Running;
                run.StartedUtc = DateTime.UtcNow;
                return run;
            }
            return null;
        }

        /// <inheritdoc/>
        public async Task<StepRun> CreateStepRunAsync(StepRun r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrWhiteSpace(r.Id)) r.Id = IdGenerator.GenerateStepRunId();
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO step_runs(id, tenant_id, flow_run_id, data_flow_id, step_id, sequence, result, next_step_id, input_data, output_data, error_message, artifact_id, artifact_version_id, artifact_version, artifact_sha256, manifest_entrypoint, execution_state, protocol_version, capacity_queued_utc, capacity_acquired_utc, capacity_wait_ms, started_utc, completed_utc) VALUES (" +
                _D.Quote(r.Id) + ", " + _D.Quote(r.TenantId) + ", " + _D.Quote(r.FlowRunId) + ", " + _D.Quote(r.DataFlowId) + ", " +
                _D.Quote(r.StepId) + ", " + r.Sequence + ", " + _D.Quote(r.Result.ToString()) + ", " + _D.Quote(r.NextStepId) + ", " +
                _D.Quote(r.InputData) + ", " + _D.Quote(r.OutputData) + ", " + _D.Quote(r.ErrorMessage) + ", " + _D.Quote(r.ArtifactId) + ", " +
                _D.Quote(r.ArtifactVersionId) + ", " + _D.Quote(r.ArtifactVersion) + ", " + _D.Quote(r.ArtifactSha256) + ", " + _D.Quote(r.ManifestEntrypoint) + ", " +
                _D.Quote(r.ExecutionState.ToString()) + ", " +
                _D.Quote(r.ProtocolVersion) + ", " + _D.Quote(r.CapacityQueuedUtc) + ", " + _D.Quote(r.CapacityAcquiredUtc) + ", " + _D.Quote(r.CapacityWaitMs) + ", " +
                _D.Quote(r.StartedUtc) + ", " + _D.Quote(r.CompletedUtc) + ");",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<StepRun> UpdateStepRunAsync(StepRun r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            await _Driver.ExecuteQueryAsync(
                "UPDATE step_runs SET result = " + _D.Quote(r.Result.ToString()) +
                ", next_step_id = " + _D.Quote(r.NextStepId) +
                ", input_data = " + _D.Quote(r.InputData) +
                ", output_data = " + _D.Quote(r.OutputData) +
                ", error_message = " + _D.Quote(r.ErrorMessage) +
                ", artifact_id = " + _D.Quote(r.ArtifactId) +
                ", artifact_version_id = " + _D.Quote(r.ArtifactVersionId) +
                ", artifact_version = " + _D.Quote(r.ArtifactVersion) +
                ", artifact_sha256 = " + _D.Quote(r.ArtifactSha256) +
                ", manifest_entrypoint = " + _D.Quote(r.ManifestEntrypoint) +
                ", execution_state = " + _D.Quote(r.ExecutionState.ToString()) +
                ", protocol_version = " + _D.Quote(r.ProtocolVersion) +
                ", capacity_queued_utc = " + _D.Quote(r.CapacityQueuedUtc) +
                ", capacity_acquired_utc = " + _D.Quote(r.CapacityAcquiredUtc) +
                ", capacity_wait_ms = " + _D.Quote(r.CapacityWaitMs) +
                ", started_utc = " + _D.Quote(r.StartedUtc) +
                ", completed_utc = " + _D.Quote(r.CompletedUtc) +
                " WHERE tenant_id = " + _D.Quote(r.TenantId) + " AND id = " + _D.Quote(r.Id) + ";",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<StepRun>> EnumerateStepRunsAsync(string tenantId, string flowRunId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(flowRunId)) throw new ArgumentNullException(nameof(flowRunId));
            DataTable dt = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM step_runs WHERE tenant_id = " + _D.Quote(tenantId) + " AND flow_run_id = " + _D.Quote(flowRunId) + " ORDER BY sequence ASC, started_utc ASC;",
                false, token).ConfigureAwait(false);
            List<StepRun> list = new List<StepRun>();
            foreach (DataRow row in dt.Rows) list.Add(MapStep(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string tq = _D.Quote(tenantId);
            string iq = _D.Quote(id);
            List<string> batch = new List<string>
            {
                "DELETE FROM step_runs WHERE tenant_id = " + tq + " AND flow_run_id = " + iq + ";",
                "DELETE FROM flow_runs WHERE tenant_id = " + tq + " AND id = " + iq + ";"
            };
            await _Driver.ExecuteQueriesAsync(batch, true, token).ConfigureAwait(false);
            return true;
        }

        private static FlowRun Map(DataRow row)
        {
            return new FlowRun
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                DataFlowId = Converters.String(row, "data_flow_id"),
                TriggeredByUserId = Converters.StringOrNull(row, "triggered_by_user_id"),
                TriggerId = Converters.StringOrNull(row, "trigger_id"),
                State = Converters.EnumValue<FlowRunStateEnum>(row, "state", FlowRunStateEnum.Queued),
                InputData = Converters.StringOrNull(row, "input_data"),
                OutputData = Converters.StringOrNull(row, "output_data"),
                ErrorMessage = Converters.StringOrNull(row, "error_message"),
                ExecutionSnapshotJson = Converters.StringOrNull(row, "execution_snapshot_json"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                StartedUtc = Converters.DateTimeOrNull(row, "started_utc"),
                CompletedUtc = Converters.DateTimeOrNull(row, "completed_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }

        private static StepRun MapStep(DataRow row)
        {
            return new StepRun
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                FlowRunId = Converters.String(row, "flow_run_id"),
                DataFlowId = Converters.String(row, "data_flow_id"),
                StepId = Converters.String(row, "step_id"),
                Sequence = Converters.Int(row, "sequence"),
                Result = Converters.EnumValue<TempoStepResult>(row, "result", TempoStepResult.Success),
                NextStepId = Converters.StringOrNull(row, "next_step_id"),
                InputData = Converters.StringOrNull(row, "input_data"),
                OutputData = Converters.StringOrNull(row, "output_data"),
                ErrorMessage = Converters.StringOrNull(row, "error_message"),
                ArtifactId = Converters.StringOrNull(row, "artifact_id"),
                ArtifactVersionId = Converters.StringOrNull(row, "artifact_version_id"),
                ArtifactVersion = Converters.StringOrNull(row, "artifact_version"),
                ArtifactSha256 = Converters.StringOrNull(row, "artifact_sha256"),
                ManifestEntrypoint = Converters.StringOrNull(row, "manifest_entrypoint"),
                ExecutionState = Converters.EnumValue<StepRunExecutionStateEnum>(row, "execution_state", StepRunExecutionStateEnum.Complete),
                ProtocolVersion = Converters.StringOrNull(row, "protocol_version") ?? Tempo.Protocol.ProtocolVersions.Current,
                CapacityQueuedUtc = Converters.DateTimeOrNull(row, "capacity_queued_utc"),
                CapacityAcquiredUtc = Converters.DateTimeOrNull(row, "capacity_acquired_utc"),
                CapacityWaitMs = Converters.LongOrNull(row, "capacity_wait_ms"),
                StartedUtc = Converters.DateTime(row, "started_utc"),
                CompletedUtc = Converters.DateTimeOrNull(row, "completed_utc")
            };
        }
    }
}
