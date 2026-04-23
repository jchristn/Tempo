namespace Tempo.Core.Database.Postgresql
{
    using System.Collections.Generic;

    /// <summary>PostgreSQL schema DDL (BOOLEAN, TIMESTAMP, TEXT).</summary>
    public static class PostgresqlSchema
    {
        /// <summary>All migrations in version order.</summary>
        public static IReadOnlyList<SchemaMigration> All()
        {
            List<SchemaMigration> list = new List<SchemaMigration>();
            SchemaMigration m1 = new SchemaMigration { Version = 1, Description = "initial schema" };
            m1.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS schema_migrations (
                    version INT PRIMARY KEY,
                    description VARCHAR(500) NOT NULL,
                    applied_utc TIMESTAMP NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS accounts (
                    id VARCHAR(64) PRIMARY KEY, name VARCHAR(500) NOT NULL, additional_data TEXT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS administrators (
                    id VARCHAR(64) PRIMARY KEY, account_id VARCHAR(64) NULL,
                    first_name VARCHAR(255) NULL, last_name VARCHAR(255) NULL,
                    email VARCHAR(255) NOT NULL UNIQUE, password_sha256 VARCHAR(64) NOT NULL,
                    telephone VARCHAR(64) NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS tenants (
                    id VARCHAR(64) PRIMARY KEY, account_id VARCHAR(64) NULL, name VARCHAR(500) NOT NULL,
                    region VARCHAR(64) NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS users (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    first_name VARCHAR(255) NULL, last_name VARCHAR(255) NULL,
                    email VARCHAR(255) NOT NULL, password_sha256 VARCHAR(64) NOT NULL,
                    is_admin BOOLEAN NOT NULL DEFAULT FALSE, is_tenant_admin BOOLEAN NOT NULL DEFAULT FALSE,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenant_email ON users(tenant_id, email);",
                @"CREATE TABLE IF NOT EXISTS credentials (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL, user_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL,
                    access_key VARCHAR(128) NOT NULL UNIQUE, secret_key VARCHAR(128) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_credentials_tenant_user ON credentials(tenant_id, user_id);",
                @"CREATE TABLE IF NOT EXISTS roles (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_roles_tenant_name ON roles(tenant_id, name);",
                @"CREATE TABLE IF NOT EXISTS user_role_maps (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    user_id VARCHAR(64) NOT NULL, role_id VARCHAR(64) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_urm_user ON user_role_maps(tenant_id, user_id);",
                "CREATE INDEX IF NOT EXISTS idx_urm_role ON user_role_maps(tenant_id, role_id);",
                @"CREATE TABLE IF NOT EXISTS permissions (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL,
                    resource_types TEXT NOT NULL, operation_types TEXT NOT NULL,
                    permission_type VARCHAR(16) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_permissions_tenant ON permissions(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS role_permission_maps (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    role_id VARCHAR(64) NOT NULL, permission_id VARCHAR(64) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_rpm_role ON role_permission_maps(tenant_id, role_id);",
                "CREATE INDEX IF NOT EXISTS idx_rpm_perm ON role_permission_maps(tenant_id, permission_id);",
                @"CREATE TABLE IF NOT EXISTS data_flows (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    trigger_id VARCHAR(64) NULL, start_step_id VARCHAR(255) NOT NULL,
                    max_runtime_ms INT NOT NULL DEFAULT 0, transitions TEXT NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_flows_tenant ON data_flows(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS steps (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    step_type VARCHAR(16) NOT NULL, max_runtime_ms INT NOT NULL DEFAULT 0,
                    rest_config TEXT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_steps_tenant ON steps(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS triggers (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    trigger_type VARCHAR(32) NOT NULL, data_flow_id VARCHAR(64) NULL,
                    configuration TEXT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE, is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_triggers_tenant ON triggers(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS flow_runs (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    data_flow_id VARCHAR(64) NOT NULL,
                    triggered_by_user_id VARCHAR(64) NULL, trigger_id VARCHAR(64) NULL,
                    state VARCHAR(32) NOT NULL, input_data TEXT NULL, output_data TEXT NULL,
                    error_message TEXT NULL,
                    created_utc TIMESTAMP NOT NULL, started_utc TIMESTAMP NULL, completed_utc TIMESTAMP NULL,
                    last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_runs_tenant_flow ON flow_runs(tenant_id, data_flow_id);",
                "CREATE INDEX IF NOT EXISTS idx_runs_state ON flow_runs(state);",
                "CREATE INDEX IF NOT EXISTS idx_runs_created ON flow_runs(created_utc);",
                @"CREATE TABLE IF NOT EXISTS step_runs (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    flow_run_id VARCHAR(64) NOT NULL, data_flow_id VARCHAR(64) NOT NULL,
                    step_id VARCHAR(255) NOT NULL, sequence INT NOT NULL DEFAULT 0,
                    result VARCHAR(32) NOT NULL, next_step_id VARCHAR(255) NULL,
                    input_data TEXT NULL, output_data TEXT NULL, error_message TEXT NULL,
                    started_utc TIMESTAMP NOT NULL, completed_utc TIMESTAMP NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_step_runs_flow ON step_runs(tenant_id, flow_run_id);",
                @"CREATE TABLE IF NOT EXISTS request_history (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NULL, user_id VARCHAR(64) NULL,
                    principal_name VARCHAR(255) NULL,
                    method VARCHAR(16) NOT NULL, path VARCHAR(1024) NOT NULL, url VARCHAR(2048) NOT NULL,
                    status_code INT NOT NULL, duration_ms DOUBLE PRECISION NOT NULL, source_ip VARCHAR(64) NULL,
                    request_headers TEXT NULL, request_body TEXT NULL,
                    request_body_bytes BIGINT NOT NULL DEFAULT 0, request_body_truncated BOOLEAN NOT NULL DEFAULT FALSE,
                    response_headers TEXT NULL, response_body TEXT NULL,
                    response_body_bytes BIGINT NOT NULL DEFAULT 0, response_body_truncated BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL, completed_utc TIMESTAMP NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_req_created ON request_history(created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_req_tenant ON request_history(tenant_id, created_utc);"
            });
            list.Add(m1);

            SchemaMigration m2 = new SchemaMigration { Version = 2, Description = "step execution keys" };
            m2.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS execution_key VARCHAR(255) NULL;",
                "UPDATE steps SET execution_key = name WHERE execution_key IS NULL OR btrim(execution_key) = '';",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_steps_tenant_execution_key ON steps(tenant_id, execution_key);"
            });
            list.Add(m2);

            SchemaMigration m3 = new SchemaMigration { Version = 3, Description = "canonical step runtime model" };
            m3.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS runtime_key VARCHAR(128) NULL;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS runtime_config TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS contract_type VARCHAR(16) NULL;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS input_schema TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS output_schema TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS validate_input BOOLEAN NOT NULL DEFAULT FALSE;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS validate_output BOOLEAN NOT NULL DEFAULT FALSE;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS artifact_id VARCHAR(64) NULL;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS artifact_version VARCHAR(64) NULL;",
                "UPDATE steps SET runtime_key = CASE WHEN step_type = 'Rest' THEN 'External.Rest' ELSE 'Builtin.Unknown' END WHERE runtime_key IS NULL OR btrim(runtime_key) = '';",
                "UPDATE steps SET contract_type = 'Loose' WHERE contract_type IS NULL OR btrim(contract_type) = '';"
            });
            list.Add(m3);

            SchemaMigration m4 = new SchemaMigration { Version = 4, Description = "step runtime binding state" };
            m4.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS runtime_binding_state VARCHAR(32) NULL;",
                "ALTER TABLE steps ADD COLUMN IF NOT EXISTS runtime_binding_message TEXT NULL;",
                "UPDATE steps SET runtime_binding_state = CASE WHEN runtime_key = 'Builtin.Unknown' THEN 'Unresolved' ELSE 'Resolved' END WHERE runtime_binding_state IS NULL OR btrim(runtime_binding_state) = '';"
            });
            list.Add(m4);

            SchemaMigration m5 = new SchemaMigration { Version = 5, Description = "artifact metadata" };
            m5.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS artifacts (
                    id VARCHAR(64) PRIMARY KEY,
                    tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL,
                    description VARCHAR(1000) NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL,
                    last_update_utc TIMESTAMP NOT NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_artifacts_tenant_name ON artifacts(tenant_id, name);",
                @"CREATE TABLE IF NOT EXISTS artifact_versions (
                    id VARCHAR(64) PRIMARY KEY,
                    tenant_id VARCHAR(64) NOT NULL,
                    artifact_id VARCHAR(64) NOT NULL,
                    version VARCHAR(128) NOT NULL,
                    sha256 VARCHAR(64) NOT NULL,
                    byte_length BIGINT NOT NULL DEFAULT 0,
                    content_type VARCHAR(255) NULL,
                    original_file_name VARCHAR(1024) NULL,
                    manifest_json TEXT NULL,
                    storage_key VARCHAR(1024) NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    is_protected BOOLEAN NOT NULL DEFAULT FALSE,
                    created_utc TIMESTAMP NOT NULL,
                    last_update_utc TIMESTAMP NOT NULL,
                    deleted_utc TIMESTAMP NULL,
                    gc_eligible_utc TIMESTAMP NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_artifact_versions_artifact_version ON artifact_versions(tenant_id, artifact_id, version);",
                "CREATE INDEX IF NOT EXISTS idx_artifact_versions_sha ON artifact_versions(tenant_id, sha256);",
                "CREATE INDEX IF NOT EXISTS idx_artifact_versions_gc ON artifact_versions(gc_eligible_utc);"
            });
            list.Add(m5);

            SchemaMigration m6 = new SchemaMigration { Version = 6, Description = "step run protocol version" };
            m6.Statements.AddRange(new[]
            {
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS protocol_version VARCHAR(32) NULL;",
                "UPDATE step_runs SET protocol_version = '1.0' WHERE protocol_version IS NULL OR btrim(protocol_version) = '';"
            });
            list.Add(m6);

            SchemaMigration m7 = new SchemaMigration { Version = 7, Description = "step run capacity wait state" };
            m7.Statements.AddRange(new[]
            {
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS execution_state VARCHAR(32) NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS capacity_queued_utc TIMESTAMP NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS capacity_acquired_utc TIMESTAMP NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS capacity_wait_ms BIGINT NULL;",
                "UPDATE step_runs SET execution_state = 'Complete' WHERE execution_state IS NULL OR btrim(execution_state) = '';"
            });
            list.Add(m7);

            SchemaMigration m8 = new SchemaMigration { Version = 8, Description = "artifact run snapshots" };
            m8.Statements.AddRange(new[]
            {
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS execution_snapshot_json TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS artifact_id VARCHAR(64) NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS artifact_version_id VARCHAR(64) NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS artifact_version VARCHAR(128) NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS artifact_sha256 VARCHAR(64) NULL;",
                "ALTER TABLE step_runs ADD COLUMN IF NOT EXISTS manifest_entrypoint VARCHAR(128) NULL;"
            });
            list.Add(m8);

            SchemaMigration m9 = new SchemaMigration { Version = 9, Description = "mutable artifact files" };
            m9.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS artifact_files (
                    tenant_id VARCHAR(64) NOT NULL,
                    artifact_id VARCHAR(64) NOT NULL,
                    path VARCHAR(1024) NOT NULL,
                    content TEXT NOT NULL,
                    content_type VARCHAR(255) NULL,
                    is_binary BOOLEAN NOT NULL DEFAULT FALSE,
                    sha256 VARCHAR(64) NOT NULL,
                    byte_length BIGINT NOT NULL DEFAULT 0,
                    created_utc TIMESTAMP NOT NULL,
                    last_update_utc TIMESTAMP NOT NULL,
                    PRIMARY KEY (tenant_id, artifact_id, path)
                );",
                "CREATE INDEX IF NOT EXISTS idx_artifact_files_artifact ON artifact_files(tenant_id, artifact_id);"
            });
            list.Add(m9);

            SchemaMigration m10 = new SchemaMigration { Version = 10, Description = "distributed execution foundation" };
            m10.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS workers (
                    id VARCHAR(64) PRIMARY KEY,
                    name VARCHAR(255) NOT NULL,
                    kind VARCHAR(64) NOT NULL,
                    state VARCHAR(64) NOT NULL,
                    enabled BOOLEAN NOT NULL DEFAULT TRUE,
                    drain_mode BOOLEAN NOT NULL DEFAULT FALSE,
                    version VARCHAR(64) NULL,
                    host_name VARCHAR(255) NULL,
                    labels_json TEXT NULL,
                    max_concurrent_runs INT NOT NULL DEFAULT 1,
                    last_heartbeat_utc TIMESTAMP NULL,
                    created_utc TIMESTAMP NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS worker_sessions (
                    id VARCHAR(64) PRIMARY KEY,
                    worker_id VARCHAR(64) NOT NULL,
                    connected_utc TIMESTAMP NOT NULL,
                    disconnected_utc TIMESTAMP NULL,
                    disconnect_reason TEXT NULL,
                    protocol_version VARCHAR(32) NULL
                );",
                @"CREATE TABLE IF NOT EXISTS run_assignments (
                    id VARCHAR(64) PRIMARY KEY,
                    flow_run_id VARCHAR(64) NOT NULL,
                    worker_id VARCHAR(64) NOT NULL,
                    worker_session_id VARCHAR(64) NULL,
                    attempt_number INT NOT NULL DEFAULT 1,
                    state VARCHAR(32) NOT NULL,
                    lease_token VARCHAR(64) NOT NULL,
                    lease_expires_utc TIMESTAMP NOT NULL,
                    assigned_utc TIMESTAMP NOT NULL,
                    completed_utc TIMESTAMP NULL
                );",
                @"CREATE TABLE IF NOT EXISTS worker_activity (
                    id VARCHAR(64) PRIMARY KEY,
                    worker_id VARCHAR(64) NOT NULL,
                    worker_session_id VARCHAR(64) NULL,
                    flow_run_id VARCHAR(64) NULL,
                    run_assignment_id VARCHAR(64) NULL,
                    event_type VARCHAR(64) NOT NULL,
                    severity VARCHAR(32) NULL,
                    message TEXT NULL,
                    payload_json TEXT NULL,
                    created_utc TIMESTAMP NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS server_instances (
                    id VARCHAR(64) PRIMARY KEY,
                    started_utc TIMESTAMP NOT NULL,
                    last_heartbeat_utc TIMESTAMP NOT NULL,
                    version VARCHAR(64) NULL
                );",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS dispatch_state VARCHAR(32) NULL;",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS dispatch_attempt INT NOT NULL DEFAULT 0;",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS assigned_worker_id VARCHAR(64) NULL;",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS run_assignment_id VARCHAR(64) NULL;",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS queue_wait_ms BIGINT NULL;",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS assigned_utc TIMESTAMP NULL;",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS lease_expires_utc TIMESTAMP NULL;",
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS execution_node_kind VARCHAR(32) NULL;",
                "UPDATE flow_runs SET dispatch_state = 'Pending' WHERE dispatch_state IS NULL OR btrim(dispatch_state) = '';",
                "CREATE INDEX IF NOT EXISTS idx_flow_runs_dispatch_pending ON flow_runs(dispatch_state, state, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_workers_online ON workers(enabled, drain_mode, state, last_heartbeat_utc);",
                "CREATE INDEX IF NOT EXISTS idx_worker_sessions_stale ON worker_sessions(worker_id, disconnected_utc, connected_utc);",
                "CREATE INDEX IF NOT EXISTS idx_run_assignments_lease ON run_assignments(state, lease_expires_utc);",
                "CREATE INDEX IF NOT EXISTS idx_run_assignments_flow_run ON run_assignments(flow_run_id, attempt_number);",
                "CREATE INDEX IF NOT EXISTS idx_worker_activity_worker ON worker_activity(worker_id, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_worker_activity_run ON worker_activity(flow_run_id, created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_server_instances_heartbeat ON server_instances(last_heartbeat_utc);"
            });
            list.Add(m10);

            SchemaMigration m11 = new SchemaMigration { Version = 11, Description = "distributed execution worker auth and placement" };
            m11.Statements.AddRange(new[]
            {
                "ALTER TABLE workers ADD COLUMN IF NOT EXISTS capabilities_json TEXT NULL;",
                "ALTER TABLE workers ADD COLUMN IF NOT EXISTS token_hash VARCHAR(128) NULL;",
                "ALTER TABLE workers ADD COLUMN IF NOT EXISTS token_last_rotated_utc TIMESTAMP NULL;",
                "ALTER TABLE data_flows ADD COLUMN IF NOT EXISTS routing_hint_label VARCHAR(255) NULL;",
                "ALTER TABLE server_instances ADD COLUMN IF NOT EXISTS host_name VARCHAR(255) NULL;",
                "UPDATE workers SET capabilities_json = '[]' WHERE capabilities_json IS NULL OR btrim(capabilities_json) = '';",
                "CREATE INDEX IF NOT EXISTS idx_workers_token_hash ON workers(token_hash);",
                "CREATE INDEX IF NOT EXISTS idx_data_flows_routing_label ON data_flows(routing_hint_label);"
            });
            list.Add(m11);

            SchemaMigration m12 = new SchemaMigration { Version = 12, Description = "worker task timeout metadata" };
            m12.Statements.AddRange(new[]
            {
                "ALTER TABLE workers ADD COLUMN IF NOT EXISTS max_task_timeout_ms INT NOT NULL DEFAULT 0;",
                "UPDATE workers SET max_task_timeout_ms = 0 WHERE max_task_timeout_ms IS NULL;"
            });
            list.Add(m12);

            SchemaMigration m13 = new SchemaMigration { Version = 13, Description = "flow run source ip" };
            m13.Statements.AddRange(new[]
            {
                "ALTER TABLE flow_runs ADD COLUMN IF NOT EXISTS source_ip VARCHAR(64) NULL;"
            });
            list.Add(m13);

            SchemaMigration m14 = new SchemaMigration { Version = 14, Description = "flow invocation authentication policy" };
            m14.Statements.AddRange(new[]
            {
                "ALTER TABLE data_flows ADD COLUMN IF NOT EXISTS invocation_auth_mode VARCHAR(32) NOT NULL DEFAULT 'Public';",
                "UPDATE data_flows SET invocation_auth_mode = 'Public' WHERE invocation_auth_mode IS NULL OR btrim(invocation_auth_mode) = '';"
            });
            list.Add(m14);

            return list;
        }
    }
}
