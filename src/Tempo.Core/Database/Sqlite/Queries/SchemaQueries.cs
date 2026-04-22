namespace Tempo.Core.Database.Sqlite.Queries
{
    using System.Collections.Generic;
    using Tempo.Core.Database;

    /// <summary>
    /// SQLite schema migrations. Each migration is idempotent.
    /// </summary>
    public static class SchemaQueries
    {
        /// <summary>Return all migrations in ascending version order.</summary>
        public static IReadOnlyList<SchemaMigration> All()
        {
            List<SchemaMigration> list = new List<SchemaMigration>();

            SchemaMigration m1 = new SchemaMigration { Version = 1, Description = "initial schema" };
            m1.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS schema_migrations (
                    version INTEGER PRIMARY KEY,
                    description TEXT NOT NULL,
                    applied_utc TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS accounts (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    additional_data TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS administrators (
                    id TEXT PRIMARY KEY,
                    account_id TEXT NULL,
                    first_name TEXT NULL,
                    last_name TEXT NULL,
                    email TEXT NOT NULL UNIQUE,
                    password_sha256 TEXT NOT NULL,
                    telephone TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS tenants (
                    id TEXT PRIMARY KEY,
                    account_id TEXT NULL,
                    name TEXT NOT NULL,
                    region TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS users (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    first_name TEXT NULL,
                    last_name TEXT NULL,
                    email TEXT NOT NULL,
                    password_sha256 TEXT NOT NULL,
                    is_admin INTEGER NOT NULL DEFAULT 0,
                    is_tenant_admin INTEGER NOT NULL DEFAULT 0,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenant_email ON users(tenant_id, email);",
                @"CREATE TABLE IF NOT EXISTS credentials (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    user_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    access_key TEXT NOT NULL UNIQUE,
                    secret_key TEXT NOT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_credentials_tenant_user ON credentials(tenant_id, user_id);",
                @"CREATE TABLE IF NOT EXISTS roles (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_roles_tenant_name ON roles(tenant_id, name);",
                @"CREATE TABLE IF NOT EXISTS user_role_maps (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    user_id TEXT NOT NULL,
                    role_id TEXT NOT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_user_role_maps_user ON user_role_maps(tenant_id, user_id);",
                "CREATE INDEX IF NOT EXISTS idx_user_role_maps_role ON user_role_maps(tenant_id, role_id);",
                @"CREATE TABLE IF NOT EXISTS permissions (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    resource_types TEXT NOT NULL,
                    operation_types TEXT NOT NULL,
                    permission_type TEXT NOT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_permissions_tenant ON permissions(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS role_permission_maps (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    role_id TEXT NOT NULL,
                    permission_id TEXT NOT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_rpm_role ON role_permission_maps(tenant_id, role_id);",
                "CREATE INDEX IF NOT EXISTS idx_rpm_permission ON role_permission_maps(tenant_id, permission_id);",
                @"CREATE TABLE IF NOT EXISTS data_flows (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT NULL,
                    trigger_id TEXT NULL,
                    start_step_id TEXT NOT NULL,
                    max_runtime_ms INTEGER NOT NULL DEFAULT 0,
                    transitions TEXT NOT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_flows_tenant ON data_flows(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS steps (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT NULL,
                    step_type TEXT NOT NULL,
                    max_runtime_ms INTEGER NOT NULL DEFAULT 0,
                    rest_config TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_steps_tenant ON steps(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS triggers (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT NULL,
                    trigger_type TEXT NOT NULL,
                    data_flow_id TEXT NULL,
                    configuration TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_triggers_tenant ON triggers(tenant_id);",
                @"CREATE TABLE IF NOT EXISTS flow_runs (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    data_flow_id TEXT NOT NULL,
                    triggered_by_user_id TEXT NULL,
                    trigger_id TEXT NULL,
                    state TEXT NOT NULL,
                    input_data TEXT NULL,
                    output_data TEXT NULL,
                    error_message TEXT NULL,
                    created_utc TEXT NOT NULL,
                    started_utc TEXT NULL,
                    completed_utc TEXT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_flow_runs_tenant_flow ON flow_runs(tenant_id, data_flow_id);",
                "CREATE INDEX IF NOT EXISTS idx_flow_runs_state ON flow_runs(state);",
                "CREATE INDEX IF NOT EXISTS idx_flow_runs_created ON flow_runs(created_utc);",
                @"CREATE TABLE IF NOT EXISTS step_runs (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    flow_run_id TEXT NOT NULL,
                    data_flow_id TEXT NOT NULL,
                    step_id TEXT NOT NULL,
                    sequence INTEGER NOT NULL DEFAULT 0,
                    result TEXT NOT NULL,
                    next_step_id TEXT NULL,
                    input_data TEXT NULL,
                    output_data TEXT NULL,
                    error_message TEXT NULL,
                    started_utc TEXT NOT NULL,
                    completed_utc TEXT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_step_runs_flow_run ON step_runs(tenant_id, flow_run_id);",
                @"CREATE TABLE IF NOT EXISTS request_history (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NULL,
                    user_id TEXT NULL,
                    principal_name TEXT NULL,
                    method TEXT NOT NULL,
                    path TEXT NOT NULL,
                    url TEXT NOT NULL,
                    status_code INTEGER NOT NULL,
                    duration_ms REAL NOT NULL,
                    source_ip TEXT NULL,
                    request_headers TEXT NULL,
                    request_body TEXT NULL,
                    request_body_bytes INTEGER NOT NULL DEFAULT 0,
                    request_body_truncated INTEGER NOT NULL DEFAULT 0,
                    response_headers TEXT NULL,
                    response_body TEXT NULL,
                    response_body_bytes INTEGER NOT NULL DEFAULT 0,
                    response_body_truncated INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    completed_utc TEXT NULL
                );",
                "CREATE INDEX IF NOT EXISTS idx_req_created ON request_history(created_utc);",
                "CREATE INDEX IF NOT EXISTS idx_req_tenant ON request_history(tenant_id, created_utc);"
            });

            list.Add(m1);

            SchemaMigration m2 = new SchemaMigration { Version = 2, Description = "step execution keys" };
            m2.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN execution_key TEXT NULL;",
                "UPDATE steps SET execution_key = name WHERE execution_key IS NULL OR length(trim(execution_key)) = 0;",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_steps_tenant_execution_key ON steps(tenant_id, execution_key);"
            });
            list.Add(m2);

            SchemaMigration m3 = new SchemaMigration { Version = 3, Description = "canonical step runtime model" };
            m3.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN runtime_key TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN runtime_config TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN contract_type TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN input_schema TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN output_schema TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN validate_input INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE steps ADD COLUMN validate_output INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE steps ADD COLUMN artifact_id TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN artifact_version TEXT NULL;",
                "UPDATE steps SET runtime_key = CASE WHEN step_type = 'Rest' THEN 'External.Rest' ELSE 'Builtin.Unknown' END WHERE runtime_key IS NULL OR length(trim(runtime_key)) = 0;",
                "UPDATE steps SET contract_type = 'Loose' WHERE contract_type IS NULL OR length(trim(contract_type)) = 0;"
            });
            list.Add(m3);

            SchemaMigration m4 = new SchemaMigration { Version = 4, Description = "step runtime binding state" };
            m4.Statements.AddRange(new[]
            {
                "ALTER TABLE steps ADD COLUMN runtime_binding_state TEXT NULL;",
                "ALTER TABLE steps ADD COLUMN runtime_binding_message TEXT NULL;",
                "UPDATE steps SET runtime_binding_state = CASE WHEN runtime_key = 'Builtin.Unknown' THEN 'Unresolved' ELSE 'Resolved' END WHERE runtime_binding_state IS NULL OR length(trim(runtime_binding_state)) = 0;"
            });
            list.Add(m4);

            SchemaMigration m5 = new SchemaMigration { Version = 5, Description = "artifact metadata" };
            m5.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS artifacts (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_artifacts_tenant_name ON artifacts(tenant_id, name);",
                @"CREATE TABLE IF NOT EXISTS artifact_versions (
                    id TEXT PRIMARY KEY,
                    tenant_id TEXT NOT NULL,
                    artifact_id TEXT NOT NULL,
                    version TEXT NOT NULL,
                    sha256 TEXT NOT NULL,
                    byte_length INTEGER NOT NULL DEFAULT 0,
                    content_type TEXT NULL,
                    original_file_name TEXT NULL,
                    manifest_json TEXT NULL,
                    storage_key TEXT NULL,
                    active INTEGER NOT NULL DEFAULT 1,
                    is_protected INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL,
                    deleted_utc TEXT NULL,
                    gc_eligible_utc TEXT NULL
                );",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_artifact_versions_artifact_version ON artifact_versions(tenant_id, artifact_id, version);",
                "CREATE INDEX IF NOT EXISTS idx_artifact_versions_sha ON artifact_versions(tenant_id, sha256);",
                "CREATE INDEX IF NOT EXISTS idx_artifact_versions_gc ON artifact_versions(gc_eligible_utc);"
            });
            list.Add(m5);

            SchemaMigration m6 = new SchemaMigration { Version = 6, Description = "step run protocol version" };
            m6.Statements.AddRange(new[]
            {
                "ALTER TABLE step_runs ADD COLUMN protocol_version TEXT NULL;",
                "UPDATE step_runs SET protocol_version = '1.0' WHERE protocol_version IS NULL OR length(trim(protocol_version)) = 0;"
            });
            list.Add(m6);

            SchemaMigration m7 = new SchemaMigration { Version = 7, Description = "step run capacity wait state" };
            m7.Statements.AddRange(new[]
            {
                "ALTER TABLE step_runs ADD COLUMN execution_state TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN capacity_queued_utc TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN capacity_acquired_utc TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN capacity_wait_ms INTEGER NULL;",
                "UPDATE step_runs SET execution_state = 'Complete' WHERE execution_state IS NULL OR length(trim(execution_state)) = 0;"
            });
            list.Add(m7);

            SchemaMigration m8 = new SchemaMigration { Version = 8, Description = "artifact run snapshots" };
            m8.Statements.AddRange(new[]
            {
                "ALTER TABLE flow_runs ADD COLUMN execution_snapshot_json TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN artifact_id TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN artifact_version_id TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN artifact_version TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN artifact_sha256 TEXT NULL;",
                "ALTER TABLE step_runs ADD COLUMN manifest_entrypoint TEXT NULL;"
            });
            list.Add(m8);

            SchemaMigration m9 = new SchemaMigration { Version = 9, Description = "mutable artifact files" };
            m9.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS artifact_files (
                    tenant_id TEXT NOT NULL,
                    artifact_id TEXT NOT NULL,
                    path TEXT NOT NULL,
                    content TEXT NOT NULL,
                    content_type TEXT NULL,
                    is_binary INTEGER NOT NULL DEFAULT 0,
                    sha256 TEXT NOT NULL,
                    byte_length INTEGER NOT NULL DEFAULT 0,
                    created_utc TEXT NOT NULL,
                    last_update_utc TEXT NOT NULL,
                    PRIMARY KEY (tenant_id, artifact_id, path)
                );",
                "CREATE INDEX IF NOT EXISTS idx_artifact_files_artifact ON artifact_files(tenant_id, artifact_id);"
            });
            list.Add(m9);

            SchemaMigration m10 = new SchemaMigration { Version = 10, Description = "distributed execution foundation" };
            m10.Statements.AddRange(new[]
            {
                @"CREATE TABLE IF NOT EXISTS workers (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    state TEXT NOT NULL,
                    enabled INTEGER NOT NULL DEFAULT 1,
                    drain_mode INTEGER NOT NULL DEFAULT 0,
                    version TEXT NULL,
                    host_name TEXT NULL,
                    labels_json TEXT NULL,
                    max_concurrent_runs INTEGER NOT NULL DEFAULT 1,
                    last_heartbeat_utc TEXT NULL,
                    created_utc TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS worker_sessions (
                    id TEXT PRIMARY KEY,
                    worker_id TEXT NOT NULL,
                    connected_utc TEXT NOT NULL,
                    disconnected_utc TEXT NULL,
                    disconnect_reason TEXT NULL,
                    protocol_version TEXT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS run_assignments (
                    id TEXT PRIMARY KEY,
                    flow_run_id TEXT NOT NULL,
                    worker_id TEXT NOT NULL,
                    worker_session_id TEXT NULL,
                    attempt_number INTEGER NOT NULL DEFAULT 1,
                    state TEXT NOT NULL,
                    lease_token TEXT NOT NULL,
                    lease_expires_utc TEXT NOT NULL,
                    assigned_utc TEXT NOT NULL,
                    completed_utc TEXT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS worker_activity (
                    id TEXT PRIMARY KEY,
                    worker_id TEXT NOT NULL,
                    worker_session_id TEXT NULL,
                    flow_run_id TEXT NULL,
                    run_assignment_id TEXT NULL,
                    event_type TEXT NOT NULL,
                    severity TEXT NULL,
                    message TEXT NULL,
                    payload_json TEXT NULL,
                    created_utc TEXT NOT NULL
                );",
                @"CREATE TABLE IF NOT EXISTS server_instances (
                    id TEXT PRIMARY KEY,
                    started_utc TEXT NOT NULL,
                    last_heartbeat_utc TEXT NOT NULL,
                    version TEXT NULL
                );",
                "ALTER TABLE flow_runs ADD COLUMN dispatch_state TEXT NULL;",
                "ALTER TABLE flow_runs ADD COLUMN dispatch_attempt INTEGER NOT NULL DEFAULT 0;",
                "ALTER TABLE flow_runs ADD COLUMN assigned_worker_id TEXT NULL;",
                "ALTER TABLE flow_runs ADD COLUMN run_assignment_id TEXT NULL;",
                "ALTER TABLE flow_runs ADD COLUMN queue_wait_ms INTEGER NULL;",
                "ALTER TABLE flow_runs ADD COLUMN assigned_utc TEXT NULL;",
                "ALTER TABLE flow_runs ADD COLUMN lease_expires_utc TEXT NULL;",
                "ALTER TABLE flow_runs ADD COLUMN execution_node_kind TEXT NULL;",
                "UPDATE flow_runs SET dispatch_state = 'Pending' WHERE dispatch_state IS NULL OR length(trim(dispatch_state)) = 0;",
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
                "ALTER TABLE workers ADD COLUMN capabilities_json TEXT NULL;",
                "ALTER TABLE workers ADD COLUMN token_hash TEXT NULL;",
                "ALTER TABLE workers ADD COLUMN token_last_rotated_utc TEXT NULL;",
                "ALTER TABLE data_flows ADD COLUMN routing_hint_label TEXT NULL;",
                "ALTER TABLE server_instances ADD COLUMN host_name TEXT NULL;",
                "UPDATE workers SET capabilities_json = '[]' WHERE capabilities_json IS NULL OR length(trim(capabilities_json)) = 0;",
                "CREATE INDEX IF NOT EXISTS idx_workers_token_hash ON workers(token_hash);",
                "CREATE INDEX IF NOT EXISTS idx_data_flows_routing_label ON data_flows(routing_hint_label);"
            });
            list.Add(m11);

            SchemaMigration m12 = new SchemaMigration { Version = 12, Description = "worker task timeout metadata" };
            m12.Statements.AddRange(new[]
            {
                "ALTER TABLE workers ADD COLUMN max_task_timeout_ms INTEGER NOT NULL DEFAULT 0;",
                "UPDATE workers SET max_task_timeout_ms = 0 WHERE max_task_timeout_ms IS NULL;"
            });
            list.Add(m12);

            SchemaMigration m13 = new SchemaMigration { Version = 13, Description = "flow run source ip" };
            m13.Statements.AddRange(new[]
            {
                "ALTER TABLE flow_runs ADD COLUMN source_ip TEXT NULL;"
            });
            list.Add(m13);

            return list;
        }
    }
}
