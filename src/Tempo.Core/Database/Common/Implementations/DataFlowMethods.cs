namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>Driver-agnostic implementation of <see cref="IDataFlowMethods"/>.</summary>
    public class DataFlowMethods : IDataFlowMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public DataFlowMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<DataFlowRecord> CreateAsync(DataFlowRecord r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrWhiteSpace(r.Id)) r.Id = IdGenerator.GenerateDataFlowId();
            r.CreatedUtc = DateTime.UtcNow;
            r.LastUpdateUtc = DateTime.UtcNow;
            string transitionsJson = JsonSerializer.Serialize(r.Transitions);
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO data_flows(id, tenant_id, name, description, trigger_id, start_step_id, routing_hint_label, max_runtime_ms, transitions, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(r.Id) + ", " + _D.Quote(r.TenantId) + ", " + _D.Quote(r.Name) + ", " + _D.Quote(r.Description) + ", " +
                _D.Quote(r.TriggerId) + ", " + _D.Quote(r.StartStepId) + ", " + _D.Quote(r.RoutingHintLabel) + ", " + r.MaxRuntimeMs + ", " +
                _D.Quote(transitionsJson) + ", " + _D.Bit(r.Active) + ", " + _D.Bit(r.IsProtected) + ", " +
                _D.Quote(r.CreatedUtc) + ", " + _D.Quote(r.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<DataFlowRecord> UpdateAsync(DataFlowRecord r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            r.LastUpdateUtc = DateTime.UtcNow;
            string transitionsJson = JsonSerializer.Serialize(r.Transitions);
            await _Driver.ExecuteQueryAsync(
                "UPDATE data_flows SET name = " + _D.Quote(r.Name) + ", description = " + _D.Quote(r.Description) + ", " +
                "trigger_id = " + _D.Quote(r.TriggerId) + ", start_step_id = " + _D.Quote(r.StartStepId) + ", " +
                "routing_hint_label = " + _D.Quote(r.RoutingHintLabel) + ", " +
                "max_runtime_ms = " + r.MaxRuntimeMs + ", transitions = " + _D.Quote(transitionsJson) + ", " +
                "active = " + _D.Bit(r.Active) + ", is_protected = " + _D.Bit(r.IsProtected) + ", " +
                "last_update_utc = " + _D.Quote(r.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(r.TenantId) + " AND id = " + _D.Quote(r.Id) + ";",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<DataFlowRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM data_flows WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<DataFlowRecord?> ReadGlobalAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM data_flows WHERE id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<DataFlowRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM data_flows WHERE tenant_id = " + _D.Quote(tenantId) + active + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM data_flows WHERE tenant_id = " + _D.Quote(tenantId) + active + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<DataFlowRecord> r = new EnumerationResult<DataFlowRecord> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<DataFlowRecord>> AllAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM data_flows WHERE tenant_id = " + _D.Quote(tenantId) + " ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<DataFlowRecord> list = new List<DataFlowRecord>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string t = _D.Quote(tenantId);
            string f = _D.Quote(id);
            await _Driver.ExecuteQueriesAsync(new[]
            {
                "DELETE FROM step_runs WHERE tenant_id = " + t + " AND data_flow_id = " + f + ";",
                "DELETE FROM flow_runs WHERE tenant_id = " + t + " AND data_flow_id = " + f + ";",
                "DELETE FROM data_flows WHERE tenant_id = " + t + " AND id = " + f + ";"
            }, true, token).ConfigureAwait(false);
            return true;
        }

        private static DataFlowRecord Map(DataRow row)
        {
            string transitionsJson = Converters.String(row, "transitions");
            Dictionary<string, Tempo.StepTransition> transitions;
            try
            {
                transitions = string.IsNullOrEmpty(transitionsJson)
                    ? new Dictionary<string, Tempo.StepTransition>()
                    : JsonSerializer.Deserialize<Dictionary<string, Tempo.StepTransition>>(transitionsJson) ?? new Dictionary<string, Tempo.StepTransition>();
            }
            catch (JsonException) { transitions = new Dictionary<string, Tempo.StepTransition>(); }

            return new DataFlowRecord
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                Name = Converters.String(row, "name"),
                Description = Converters.StringOrNull(row, "description"),
                TriggerId = Converters.StringOrNull(row, "trigger_id"),
                StartStepId = Converters.String(row, "start_step_id"),
                RoutingHintLabel = row.Table.Columns.Contains("routing_hint_label") ? Converters.StringOrNull(row, "routing_hint_label") : null,
                MaxRuntimeMs = Converters.Int(row, "max_runtime_ms"),
                Transitions = transitions,
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
        }
    }
}
