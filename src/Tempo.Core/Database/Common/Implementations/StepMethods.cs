namespace Tempo.Core.Database.Common.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;

    /// <summary>Driver-agnostic implementation of <see cref="IStepMethods"/>.</summary>
    public class StepMethods : IStepMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly SqlDialect _D;

        /// <summary>Instantiate.</summary>
        public StepMethods(DatabaseDriverBase driver, SqlDialect dialect)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _D = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        /// <inheritdoc/>
        public async Task<StepRecord> CreateAsync(StepRecord r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrWhiteSpace(r.Id)) r.Id = IdGenerator.GenerateStepId();
            NormalizeExecutionKey(r);
            NormalizeRuntime(r);
            r.CreatedUtc = DateTime.UtcNow;
            r.LastUpdateUtc = DateTime.UtcNow;
            string? restJson = r.Rest == null ? null : JsonSerializer.Serialize(r.Rest);
            await _Driver.ExecuteQueryAsync(
                "INSERT INTO steps(id, tenant_id, execution_key, name, description, runtime_key, runtime_config, contract_type, input_schema, output_schema, validate_input, validate_output, artifact_id, artifact_version, runtime_binding_state, runtime_binding_message, step_type, max_runtime_ms, rest_config, active, is_protected, created_utc, last_update_utc) VALUES (" +
                _D.Quote(r.Id) + ", " + _D.Quote(r.TenantId) + ", " + _D.Quote(r.ExecutionKey) + ", " + _D.Quote(r.Name) + ", " + _D.Quote(r.Description) + ", " +
                _D.Quote(r.RuntimeKey.ToString()) + ", " + _D.Quote(r.RuntimeConfigJson) + ", " + _D.Quote(r.ContractType.ToString()) + ", " +
                _D.Quote(r.InputSchema) + ", " + _D.Quote(r.OutputSchema) + ", " + _D.Bit(r.ValidateInput) + ", " + _D.Bit(r.ValidateOutput) + ", " +
                _D.Quote(r.ArtifactId) + ", " + _D.Quote(r.ArtifactVersion) + ", " + _D.Quote(r.RuntimeBindingState.ToString()) + ", " + _D.Quote(r.RuntimeBindingMessage) + ", " +
                _D.Quote(r.StepType.ToString()) + ", " + r.MaxRuntimeMs + ", " + _D.Quote(restJson) + ", " +
                _D.Bit(r.Active) + ", " + _D.Bit(r.IsProtected) + ", " + _D.Quote(r.CreatedUtc) + ", " + _D.Quote(r.LastUpdateUtc) + ");",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<StepRecord> UpdateAsync(StepRecord r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrWhiteSpace(r.ExecutionKey))
            {
                StepRecord? existing = await ReadAsync(r.TenantId, r.Id, token).ConfigureAwait(false);
                if (existing != null) r.ExecutionKey = existing.ExecutionKey;
            }
            NormalizeExecutionKey(r);
            NormalizeRuntime(r);
            r.LastUpdateUtc = DateTime.UtcNow;
            string? restJson = r.Rest == null ? null : JsonSerializer.Serialize(r.Rest);
            await _Driver.ExecuteQueryAsync(
                "UPDATE steps SET execution_key = " + _D.Quote(r.ExecutionKey) + ", name = " + _D.Quote(r.Name) + ", description = " + _D.Quote(r.Description) + ", " +
                "runtime_key = " + _D.Quote(r.RuntimeKey.ToString()) + ", runtime_config = " + _D.Quote(r.RuntimeConfigJson) + ", " +
                "contract_type = " + _D.Quote(r.ContractType.ToString()) + ", input_schema = " + _D.Quote(r.InputSchema) + ", output_schema = " + _D.Quote(r.OutputSchema) + ", " +
                "validate_input = " + _D.Bit(r.ValidateInput) + ", validate_output = " + _D.Bit(r.ValidateOutput) + ", " +
                "artifact_id = " + _D.Quote(r.ArtifactId) + ", artifact_version = " + _D.Quote(r.ArtifactVersion) + ", " +
                "runtime_binding_state = " + _D.Quote(r.RuntimeBindingState.ToString()) + ", runtime_binding_message = " + _D.Quote(r.RuntimeBindingMessage) + ", " +
                "step_type = " + _D.Quote(r.StepType.ToString()) + ", max_runtime_ms = " + r.MaxRuntimeMs + ", " +
                "rest_config = " + _D.Quote(restJson) + ", active = " + _D.Bit(r.Active) + ", " +
                "is_protected = " + _D.Bit(r.IsProtected) + ", last_update_utc = " + _D.Quote(r.LastUpdateUtc) +
                " WHERE tenant_id = " + _D.Quote(r.TenantId) + " AND id = " + _D.Quote(r.Id) + ";",
                false, token).ConfigureAwait(false);
            return r;
        }

        /// <inheritdoc/>
        public async Task<StepRecord> UpsertAsync(StepRecord r, CancellationToken token = default)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            NormalizeExecutionKey(r);
            StepRecord? existing = await ReadByExecutionKeyAsync(r.TenantId, r.ExecutionKey, token).ConfigureAwait(false);
            if (existing == null) return await CreateAsync(r, token).ConfigureAwait(false);
            r.Id = existing.Id;
            r.CreatedUtc = existing.CreatedUtc;
            return await UpdateAsync(r, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<StepRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM steps WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<StepRecord?> ReadByExecutionKeyAsync(string tenantId, string executionKey, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(executionKey)) throw new ArgumentNullException(nameof(executionKey));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM steps WHERE tenant_id = " + _D.Quote(tenantId) + " AND execution_key = " + _D.Quote(executionKey.Trim()) + ";", false, token).ConfigureAwait(false);
            return dt.Rows.Count == 0 ? null : Map(dt.Rows[0]);
        }

        /// <inheritdoc/>
        public async Task<EnumerationResult<StepRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string active = filter.IncludeInactive ? "" : " AND active = " + _D.BoolLiteral(true);
            DataTable c = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) AS n FROM steps WHERE tenant_id = " + _D.Quote(tenantId) + active + ";", false, token).ConfigureAwait(false);
            int total = c.Rows.Count == 0 ? 0 : Convert.ToInt32(c.Rows[0][0]);
            int offset = (filter.PageNumber - 1) * filter.PageSize;
            DataTable page = await _Driver.ExecuteQueryAsync(
                "SELECT * FROM steps WHERE tenant_id = " + _D.Quote(tenantId) + active + " ORDER BY created_utc DESC " + _D.Paging(filter.PageSize, offset) + ";",
                false, token).ConfigureAwait(false);
            EnumerationResult<StepRecord> r = new EnumerationResult<StepRecord> { PageNumber = filter.PageNumber, PageSize = filter.PageSize, TotalCount = total };
            foreach (DataRow row in page.Rows) r.Items.Add(Map(row));
            return r;
        }

        /// <inheritdoc/>
        public async Task<List<StepRecord>> AllAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            DataTable dt = await _Driver.ExecuteQueryAsync("SELECT * FROM steps WHERE tenant_id = " + _D.Quote(tenantId) + " ORDER BY created_utc DESC;", false, token).ConfigureAwait(false);
            List<StepRecord> list = new List<StepRecord>();
            foreach (DataRow row in dt.Rows) list.Add(Map(row));
            return list;
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            await _Driver.ExecuteQueryAsync("DELETE FROM steps WHERE tenant_id = " + _D.Quote(tenantId) + " AND id = " + _D.Quote(id) + ";", false, token).ConfigureAwait(false);
            return true;
        }

        private static StepRecord Map(DataRow row)
        {
            string? restJson = Converters.StringOrNull(row, "rest_config");
            Tempo.RestStepConfiguration? rest = null;
            if (!string.IsNullOrEmpty(restJson))
            {
                try { rest = JsonSerializer.Deserialize<Tempo.RestStepConfiguration>(restJson); }
                catch (JsonException) { rest = null; }
            }
            StepRecord record = new StepRecord
            {
                Id = Converters.String(row, "id"),
                TenantId = Converters.String(row, "tenant_id"),
                Name = Converters.String(row, "name"),
                Description = Converters.StringOrNull(row, "description"),
                RuntimeConfigJson = Converters.StringOrNull(row, "runtime_config"),
                ContractType = Converters.EnumValue<StepContractTypeEnum>(row, "contract_type", StepContractTypeEnum.Loose),
                InputSchema = Converters.StringOrNull(row, "input_schema"),
                OutputSchema = Converters.StringOrNull(row, "output_schema"),
                ValidateInput = Converters.Bool(row, "validate_input"),
                ValidateOutput = Converters.Bool(row, "validate_output"),
                ArtifactId = Converters.StringOrNull(row, "artifact_id"),
                ArtifactVersion = Converters.StringOrNull(row, "artifact_version"),
                RuntimeBindingState = Converters.EnumValue<StepRuntimeBindingStateEnum>(row, "runtime_binding_state", StepRuntimeBindingStateEnum.Unresolved),
                RuntimeBindingMessage = Converters.StringOrNull(row, "runtime_binding_message"),
                StepType = Converters.EnumValue<PersistedStepTypeEnum>(row, "step_type", PersistedStepTypeEnum.Code),
                MaxRuntimeMs = Converters.Int(row, "max_runtime_ms"),
                Rest = rest,
                Active = Converters.Bool(row, "active"),
                IsProtected = Converters.Bool(row, "is_protected"),
                CreatedUtc = Converters.DateTime(row, "created_utc"),
                LastUpdateUtc = Converters.DateTime(row, "last_update_utc")
            };
            record.ExecutionKey = Converters.StringOrNull(row, "execution_key") ?? record.Name;
            string? runtimeKey = Converters.StringOrNull(row, "runtime_key");
            record.RuntimeKey = string.IsNullOrWhiteSpace(runtimeKey) ? LegacyRuntimeKey(record) : new RuntimeKey(runtimeKey);
            if (!string.IsNullOrWhiteSpace(record.RuntimeConfigJson))
            {
                record.RuntimeConfig = StepRuntimeSerialization.DeserializeConfig(record.RuntimeKey, record.RuntimeConfigJson);
            }
            else if (record.RuntimeKey == StepRuntimeKeys.ExternalRest && record.Rest != null)
            {
                record.RuntimeConfig = ExternalRestRuntimeConfig.FromLegacy(record.Rest);
                record.RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(record.RuntimeConfig);
            }
            return record;
        }

        private static void NormalizeExecutionKey(StepRecord record)
        {
            record.EnsureExecutionKey();
        }

        private static void NormalizeRuntime(StepRecord record)
        {
            if (record.RuntimeConfig != null)
            {
                if (record.RuntimeKey.IsEmpty) record.RuntimeKey = record.RuntimeConfig.RuntimeKey;
                if (record.RuntimeKey != record.RuntimeConfig.RuntimeKey)
                    throw new InvalidOperationException("RuntimeKey '" + record.RuntimeKey + "' does not match runtime config key '" + record.RuntimeConfig.RuntimeKey + "'.");

                record.RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(record.RuntimeConfig);
                if (record.RuntimeConfig is ExternalRestRuntimeConfig restConfig)
                {
                    record.StepType = PersistedStepTypeEnum.Rest;
                    record.Rest = restConfig.ToLegacy();
                }
                else if (record.RuntimeConfig is ArtifactProcessRuntimeConfig processConfig)
                {
                    record.ArtifactId = processConfig.ArtifactId;
                    record.ArtifactVersion = string.IsNullOrWhiteSpace(processConfig.ArtifactVersion) ? "latest" : processConfig.ArtifactVersion;
                }
                else if (record.RuntimeConfig is ArtifactPythonRuntimeConfig pythonConfig)
                {
                    record.ArtifactId = pythonConfig.ArtifactId;
                    record.ArtifactVersion = string.IsNullOrWhiteSpace(pythonConfig.ArtifactVersion) ? "latest" : pythonConfig.ArtifactVersion;
                }
                else if (record.RuntimeConfig is ArtifactJavaScriptRuntimeConfig javaScriptConfig)
                {
                    record.ArtifactId = javaScriptConfig.ArtifactId;
                    record.ArtifactVersion = string.IsNullOrWhiteSpace(javaScriptConfig.ArtifactVersion) ? "latest" : javaScriptConfig.ArtifactVersion;
                }
                else if (record.RuntimeConfig is ArtifactDotnetProcessRuntimeConfig dotnetConfig)
                {
                    record.ArtifactId = dotnetConfig.ArtifactId;
                    record.ArtifactVersion = string.IsNullOrWhiteSpace(dotnetConfig.ArtifactVersion) ? "latest" : dotnetConfig.ArtifactVersion;
                }
            }
            else if (!string.IsNullOrWhiteSpace(record.RuntimeConfigJson) && !record.RuntimeKey.IsEmpty)
            {
                record.RuntimeConfig = StepRuntimeSerialization.DeserializeConfig(record.RuntimeKey, record.RuntimeConfigJson);
            }
            else if (record.StepType == PersistedStepTypeEnum.Rest && record.Rest != null)
            {
                record.RuntimeKey = StepRuntimeKeys.ExternalRest;
                record.RuntimeConfig = ExternalRestRuntimeConfig.FromLegacy(record.Rest);
                record.RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(record.RuntimeConfig);
            }
            else if (record.RuntimeKey.IsEmpty)
            {
                record.RuntimeKey = StepRuntimeKeys.BuiltinUnknown;
                record.RuntimeConfig = new BuiltinUnknownRuntimeConfig { Identifier = record.ExecutionKey };
                record.RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(record.RuntimeConfig);
            }
        }

        private static RuntimeKey LegacyRuntimeKey(StepRecord record)
        {
            return record.StepType == PersistedStepTypeEnum.Rest ? StepRuntimeKeys.ExternalRest : StepRuntimeKeys.BuiltinUnknown;
        }
    }
}
