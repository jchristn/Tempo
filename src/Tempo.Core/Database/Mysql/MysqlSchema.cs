namespace Tempo.Core.Database.Mysql
{
    using System.Collections.Generic;

    /// <summary>MySQL schema DDL (TINYINT booleans, VARCHAR/TEXT, DATETIME(3)).</summary>
    public static class MysqlSchema
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
                    applied_utc DATETIME(3) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS accounts (
                    id VARCHAR(64) PRIMARY KEY, name VARCHAR(500) NOT NULL, additional_data TEXT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS administrators (
                    id VARCHAR(64) PRIMARY KEY, account_id VARCHAR(64) NULL,
                    first_name VARCHAR(255) NULL, last_name VARCHAR(255) NULL,
                    email VARCHAR(255) NOT NULL UNIQUE, password_sha256 VARCHAR(64) NOT NULL,
                    telephone VARCHAR(64) NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS tenants (
                    id VARCHAR(64) PRIMARY KEY, account_id VARCHAR(64) NULL, name VARCHAR(500) NOT NULL,
                    region VARCHAR(64) NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS users (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    first_name VARCHAR(255) NULL, last_name VARCHAR(255) NULL,
                    email VARCHAR(255) NOT NULL, password_sha256 VARCHAR(64) NOT NULL,
                    is_admin TINYINT NOT NULL DEFAULT 0, is_tenant_admin TINYINT NOT NULL DEFAULT 0,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    UNIQUE KEY idx_users_tenant_email (tenant_id, email)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS credentials (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL, user_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL,
                    access_key VARCHAR(128) NOT NULL UNIQUE, secret_key VARCHAR(128) NOT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_credentials_tenant_user (tenant_id, user_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS roles (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    UNIQUE KEY idx_roles_tenant_name (tenant_id, name)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS user_role_maps (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    user_id VARCHAR(64) NOT NULL, role_id VARCHAR(64) NOT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_urm_user (tenant_id, user_id), KEY idx_urm_role (tenant_id, role_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS permissions (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL,
                    resource_types TEXT NOT NULL, operation_types TEXT NOT NULL,
                    permission_type VARCHAR(16) NOT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_permissions_tenant (tenant_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS role_permission_maps (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    role_id VARCHAR(64) NOT NULL, permission_id VARCHAR(64) NOT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_rpm_role (tenant_id, role_id), KEY idx_rpm_perm (tenant_id, permission_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS data_flows (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    trigger_id VARCHAR(64) NULL, start_step_id VARCHAR(255) NOT NULL,
                    max_runtime_ms INT NOT NULL DEFAULT 0, transitions LONGTEXT NOT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_flows_tenant (tenant_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS steps (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    step_type VARCHAR(16) NOT NULL, max_runtime_ms INT NOT NULL DEFAULT 0,
                    rest_config LONGTEXT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_steps_tenant (tenant_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS triggers (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    name VARCHAR(255) NOT NULL, description VARCHAR(1000) NULL,
                    trigger_type VARCHAR(32) NOT NULL, data_flow_id VARCHAR(64) NULL,
                    configuration LONGTEXT NULL,
                    active TINYINT NOT NULL DEFAULT 1, is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_triggers_tenant (tenant_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS flow_runs (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    data_flow_id VARCHAR(64) NOT NULL,
                    triggered_by_user_id VARCHAR(64) NULL, trigger_id VARCHAR(64) NULL,
                    state VARCHAR(32) NOT NULL, input_data LONGTEXT NULL, output_data LONGTEXT NULL,
                    error_message TEXT NULL,
                    created_utc DATETIME(3) NOT NULL, started_utc DATETIME(3) NULL, completed_utc DATETIME(3) NULL,
                    last_update_utc DATETIME(3) NOT NULL,
                    KEY idx_runs_tenant_flow (tenant_id, data_flow_id),
                    KEY idx_runs_state (state), KEY idx_runs_created (created_utc)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS step_runs (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NOT NULL,
                    flow_run_id VARCHAR(64) NOT NULL, data_flow_id VARCHAR(64) NOT NULL,
                    step_id VARCHAR(255) NOT NULL, sequence INT NOT NULL DEFAULT 0,
                    result VARCHAR(32) NOT NULL, next_step_id VARCHAR(255) NULL,
                    input_data LONGTEXT NULL, output_data LONGTEXT NULL, error_message TEXT NULL,
                    started_utc DATETIME(3) NOT NULL, completed_utc DATETIME(3) NULL,
                    KEY idx_step_runs_flow (tenant_id, flow_run_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS request_history (
                    id VARCHAR(64) PRIMARY KEY, tenant_id VARCHAR(64) NULL, user_id VARCHAR(64) NULL,
                    principal_name VARCHAR(255) NULL,
                    method VARCHAR(16) NOT NULL, path VARCHAR(1024) NOT NULL, url VARCHAR(2048) NOT NULL,
                    status_code INT NOT NULL, duration_ms DOUBLE NOT NULL, source_ip VARCHAR(64) NULL,
                    request_headers LONGTEXT NULL, request_body LONGTEXT NULL,
                    request_body_bytes BIGINT NOT NULL DEFAULT 0, request_body_truncated TINYINT NOT NULL DEFAULT 0,
                    response_headers LONGTEXT NULL, response_body LONGTEXT NULL,
                    response_body_bytes BIGINT NOT NULL DEFAULT 0, response_body_truncated TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL, completed_utc DATETIME(3) NULL,
                    KEY idx_req_created (created_utc), KEY idx_req_tenant (tenant_id, created_utc)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
            });
            list.Add(m1);

            SchemaMigration m2 = new SchemaMigration { Version = 2, Description = "step execution keys" };
            m2.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN execution_key VARCHAR(255) NULL AFTER tenant_id;",
                "UPDATE steps SET execution_key = name WHERE execution_key IS NULL OR TRIM(execution_key) = '';",
                "CREATE UNIQUE INDEX idx_steps_tenant_execution_key ON steps(tenant_id, execution_key);"
            });
            list.Add(m2);

            SchemaMigration m3 = new SchemaMigration { Version = 3, Description = "canonical step runtime model" };
            m3.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN runtime_key VARCHAR(128) NULL AFTER description;",
                "ALTER TABLE steps ADD COLUMN runtime_config LONGTEXT NULL AFTER runtime_key;",
                "ALTER TABLE steps ADD COLUMN contract_type VARCHAR(16) NULL AFTER runtime_config;",
                "ALTER TABLE steps ADD COLUMN input_schema LONGTEXT NULL AFTER contract_type;",
                "ALTER TABLE steps ADD COLUMN output_schema LONGTEXT NULL AFTER input_schema;",
                "ALTER TABLE steps ADD COLUMN validate_input TINYINT NOT NULL DEFAULT 0 AFTER output_schema;",
                "ALTER TABLE steps ADD COLUMN validate_output TINYINT NOT NULL DEFAULT 0 AFTER validate_input;",
                "ALTER TABLE steps ADD COLUMN artifact_id VARCHAR(64) NULL AFTER validate_output;",
                "ALTER TABLE steps ADD COLUMN artifact_version VARCHAR(64) NULL AFTER artifact_id;",
                "UPDATE steps SET runtime_key = CASE WHEN step_type = 'Rest' THEN 'External.Rest' ELSE 'Builtin.Unknown' END WHERE runtime_key IS NULL OR TRIM(runtime_key) = '';",
                "UPDATE steps SET contract_type = 'Loose' WHERE contract_type IS NULL OR TRIM(contract_type) = '';"
            });
            list.Add(m3);

            SchemaMigration m4 = new SchemaMigration { Version = 4, Description = "step runtime binding state" };
            m4.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN runtime_binding_state VARCHAR(32) NULL AFTER artifact_version;",
                "ALTER TABLE steps ADD COLUMN runtime_binding_message TEXT NULL AFTER runtime_binding_state;",
                "UPDATE steps SET runtime_binding_state = CASE WHEN runtime_key = 'Builtin.Unknown' THEN 'Unresolved' ELSE 'Resolved' END WHERE runtime_binding_state IS NULL OR TRIM(runtime_binding_state) = '';"
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
                    active TINYINT NOT NULL DEFAULT 1,
                    is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL,
                    last_update_utc DATETIME(3) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                "CREATE UNIQUE INDEX idx_artifacts_tenant_name ON artifacts(tenant_id, name);",
                @"CREATE TABLE IF NOT EXISTS artifact_versions (
                    id VARCHAR(64) PRIMARY KEY,
                    tenant_id VARCHAR(64) NOT NULL,
                    artifact_id VARCHAR(64) NOT NULL,
                    version VARCHAR(128) NOT NULL,
                    sha256 VARCHAR(64) NOT NULL,
                    byte_length BIGINT NOT NULL DEFAULT 0,
                    content_type VARCHAR(255) NULL,
                    original_file_name VARCHAR(1024) NULL,
                    manifest_json LONGTEXT NULL,
                    storage_key VARCHAR(1024) NULL,
                    active TINYINT NOT NULL DEFAULT 1,
                    is_protected TINYINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL,
                    last_update_utc DATETIME(3) NOT NULL,
                    deleted_utc DATETIME(3) NULL,
                    gc_eligible_utc DATETIME(3) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                "CREATE UNIQUE INDEX idx_artifact_versions_artifact_version ON artifact_versions(tenant_id, artifact_id, version);",
                "CREATE INDEX idx_artifact_versions_sha ON artifact_versions(tenant_id, sha256);",
                "CREATE INDEX idx_artifact_versions_gc ON artifact_versions(gc_eligible_utc);"
            });
            list.Add(m5);

            SchemaMigration m6 = new SchemaMigration { Version = 6, Description = "step run protocol version" };
            m6.Statements.AddRange(new[]
            {
                "ALTER TABLE step_runs ADD COLUMN protocol_version VARCHAR(32) NULL AFTER error_message;",
                "UPDATE step_runs SET protocol_version = '1.0' WHERE protocol_version IS NULL OR TRIM(protocol_version) = '';"
            });
            list.Add(m6);

            SchemaMigration m7 = new SchemaMigration { Version = 7, Description = "step run capacity wait state" };
            m7.Statements.AddRange(new[]
            {
                "ALTER TABLE step_runs ADD COLUMN execution_state VARCHAR(32) NULL AFTER error_message;",
                "ALTER TABLE step_runs ADD COLUMN capacity_queued_utc DATETIME(3) NULL AFTER protocol_version;",
                "ALTER TABLE step_runs ADD COLUMN capacity_acquired_utc DATETIME(3) NULL AFTER capacity_queued_utc;",
                "ALTER TABLE step_runs ADD COLUMN capacity_wait_ms BIGINT NULL AFTER capacity_acquired_utc;",
                "UPDATE step_runs SET execution_state = 'Complete' WHERE execution_state IS NULL OR TRIM(execution_state) = '';"
            });
            list.Add(m7);

            SchemaMigration m8 = new SchemaMigration { Version = 8, Description = "artifact run snapshots" };
            m8.Statements.AddRange(new[]
            {
                "ALTER TABLE flow_runs ADD COLUMN execution_snapshot_json LONGTEXT NULL AFTER error_message;",
                "ALTER TABLE step_runs ADD COLUMN artifact_id VARCHAR(64) NULL AFTER error_message;",
                "ALTER TABLE step_runs ADD COLUMN artifact_version_id VARCHAR(64) NULL AFTER artifact_id;",
                "ALTER TABLE step_runs ADD COLUMN artifact_version VARCHAR(128) NULL AFTER artifact_version_id;",
                "ALTER TABLE step_runs ADD COLUMN artifact_sha256 VARCHAR(64) NULL AFTER artifact_version;",
                "ALTER TABLE step_runs ADD COLUMN manifest_entrypoint VARCHAR(128) NULL AFTER artifact_sha256;"
            });
            list.Add(m8);

            SchemaMigration m9 = new SchemaMigration { Version = 9, Description = "mutable artifact files" };
            m9.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS artifact_files (
                    tenant_id VARCHAR(64) NOT NULL,
                    artifact_id VARCHAR(64) NOT NULL,
                    path VARCHAR(1024) NOT NULL,
                    content LONGTEXT NOT NULL,
                    content_type VARCHAR(255) NULL,
                    is_binary TINYINT NOT NULL DEFAULT 0,
                    sha256 VARCHAR(64) NOT NULL,
                    byte_length BIGINT NOT NULL DEFAULT 0,
                    created_utc DATETIME(3) NOT NULL,
                    last_update_utc DATETIME(3) NOT NULL,
                    PRIMARY KEY (tenant_id, artifact_id, path),
                    KEY idx_artifact_files_artifact (tenant_id, artifact_id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
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
                    enabled TINYINT NOT NULL DEFAULT 1,
                    drain_mode TINYINT NOT NULL DEFAULT 0,
                    version VARCHAR(64) NULL,
                    host_name VARCHAR(255) NULL,
                    labels_json LONGTEXT NULL,
                    max_concurrent_runs INT NOT NULL DEFAULT 1,
                    last_heartbeat_utc DATETIME(3) NULL,
                    created_utc DATETIME(3) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS worker_sessions (
                    id VARCHAR(64) PRIMARY KEY,
                    worker_id VARCHAR(64) NOT NULL,
                    connected_utc DATETIME(3) NOT NULL,
                    disconnected_utc DATETIME(3) NULL,
                    disconnect_reason TEXT NULL,
                    protocol_version VARCHAR(32) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS run_assignments (
                    id VARCHAR(64) PRIMARY KEY,
                    flow_run_id VARCHAR(64) NOT NULL,
                    worker_id VARCHAR(64) NOT NULL,
                    worker_session_id VARCHAR(64) NULL,
                    attempt_number INT NOT NULL DEFAULT 1,
                    state VARCHAR(32) NOT NULL,
                    lease_token VARCHAR(64) NOT NULL,
                    lease_expires_utc DATETIME(3) NOT NULL,
                    assigned_utc DATETIME(3) NOT NULL,
                    completed_utc DATETIME(3) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS worker_activity (
                    id VARCHAR(64) PRIMARY KEY,
                    worker_id VARCHAR(64) NOT NULL,
                    worker_session_id VARCHAR(64) NULL,
                    flow_run_id VARCHAR(64) NULL,
                    run_assignment_id VARCHAR(64) NULL,
                    event_type VARCHAR(64) NOT NULL,
                    severity VARCHAR(32) NULL,
                    message TEXT NULL,
                    payload_json LONGTEXT NULL,
                    created_utc DATETIME(3) NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS server_instances (
                    id VARCHAR(64) PRIMARY KEY,
                    started_utc DATETIME(3) NOT NULL,
                    last_heartbeat_utc DATETIME(3) NOT NULL,
                    version VARCHAR(64) NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                "ALTER TABLE flow_runs ADD COLUMN dispatch_state VARCHAR(32) NULL AFTER execution_snapshot_json;",
                "ALTER TABLE flow_runs ADD COLUMN dispatch_attempt INT NOT NULL DEFAULT 0 AFTER dispatch_state;",
                "ALTER TABLE flow_runs ADD COLUMN assigned_worker_id VARCHAR(64) NULL AFTER dispatch_attempt;",
                "ALTER TABLE flow_runs ADD COLUMN run_assignment_id VARCHAR(64) NULL AFTER assigned_worker_id;",
                "ALTER TABLE flow_runs ADD COLUMN queue_wait_ms BIGINT NULL AFTER run_assignment_id;",
                "ALTER TABLE flow_runs ADD COLUMN assigned_utc DATETIME(3) NULL AFTER queue_wait_ms;",
                "ALTER TABLE flow_runs ADD COLUMN lease_expires_utc DATETIME(3) NULL AFTER assigned_utc;",
                "ALTER TABLE flow_runs ADD COLUMN execution_node_kind VARCHAR(32) NULL AFTER lease_expires_utc;",
                "UPDATE flow_runs SET dispatch_state = 'Pending' WHERE dispatch_state IS NULL OR TRIM(dispatch_state) = '';",
                "CREATE INDEX idx_flow_runs_dispatch_pending ON flow_runs(dispatch_state, state, created_utc);",
                "CREATE INDEX idx_workers_online ON workers(enabled, drain_mode, state, last_heartbeat_utc);",
                "CREATE INDEX idx_worker_sessions_stale ON worker_sessions(worker_id, disconnected_utc, connected_utc);",
                "CREATE INDEX idx_run_assignments_lease ON run_assignments(state, lease_expires_utc);",
                "CREATE INDEX idx_run_assignments_flow_run ON run_assignments(flow_run_id, attempt_number);",
                "CREATE INDEX idx_worker_activity_worker ON worker_activity(worker_id, created_utc);",
                "CREATE INDEX idx_worker_activity_run ON worker_activity(flow_run_id, created_utc);",
                "CREATE INDEX idx_server_instances_heartbeat ON server_instances(last_heartbeat_utc);"
            });
            list.Add(m10);

            SchemaMigration m11 = new SchemaMigration { Version = 11, Description = "distributed execution worker auth and placement" };
            m11.Statements.AddRange(new[]
            {
                "ALTER TABLE workers ADD COLUMN capabilities_json LONGTEXT NULL AFTER labels_json;",
                "ALTER TABLE workers ADD COLUMN token_hash VARCHAR(128) NULL AFTER capabilities_json;",
                "ALTER TABLE workers ADD COLUMN token_last_rotated_utc DATETIME(3) NULL AFTER token_hash;",
                "ALTER TABLE data_flows ADD COLUMN routing_hint_label VARCHAR(255) NULL AFTER active;",
                "ALTER TABLE server_instances ADD COLUMN host_name VARCHAR(255) NULL AFTER id;",
                "UPDATE workers SET capabilities_json = '[]' WHERE capabilities_json IS NULL OR TRIM(capabilities_json) = '';",
                "CREATE INDEX idx_workers_token_hash ON workers(token_hash);",
                "CREATE INDEX idx_data_flows_routing_label ON data_flows(routing_hint_label);"
            });
            list.Add(m11);

            SchemaMigration m12 = new SchemaMigration { Version = 12, Description = "worker task timeout metadata" };
            m12.Statements.AddRange(new[]
            {
                "ALTER TABLE workers ADD COLUMN max_task_timeout_ms INT NOT NULL DEFAULT 0 AFTER max_concurrent_runs;",
                "UPDATE workers SET max_task_timeout_ms = 0 WHERE max_task_timeout_ms IS NULL;"
            });
            list.Add(m12);

            SchemaMigration m13 = new SchemaMigration { Version = 13, Description = "flow run source ip" };
            m13.Statements.AddRange(new[]
            {
                "ALTER TABLE flow_runs ADD COLUMN source_ip VARCHAR(64) NULL AFTER trigger_id;"
            });
            list.Add(m13);

            SchemaMigration m14 = new SchemaMigration { Version = 14, Description = "flow invocation authentication policy" };
            m14.Statements.AddRange(new[]
            {
                "ALTER TABLE data_flows ADD COLUMN invocation_auth_mode VARCHAR(32) NOT NULL DEFAULT 'Public' AFTER routing_hint_label;",
                "UPDATE data_flows SET invocation_auth_mode = 'Public' WHERE invocation_auth_mode IS NULL OR TRIM(invocation_auth_mode) = '';"
            });
            list.Add(m14);

            return list;
        }
    }
}
