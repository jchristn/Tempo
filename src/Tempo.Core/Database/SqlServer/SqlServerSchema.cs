namespace Tempo.Core.Database.SqlServer
{
    using System.Collections.Generic;

    /// <summary>SQL Server schema DDL (BIT booleans, NVARCHAR, DATETIME2).</summary>
    public static class SqlServerSchema
    {
        /// <summary>All migrations in version order.</summary>
        public static IReadOnlyList<SchemaMigration> All()
        {
            List<SchemaMigration> list = new List<SchemaMigration>();
            SchemaMigration m1 = new SchemaMigration { Version = 1, Description = "initial schema" };
            m1.Statements.AddRange(new[]
            {
                @"IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
                  CREATE TABLE schema_migrations (
                    version INT PRIMARY KEY,
                    description NVARCHAR(500) NOT NULL,
                    applied_utc DATETIME2 NOT NULL
                  );",
                @"IF OBJECT_ID(N'dbo.accounts', N'U') IS NULL
                  CREATE TABLE accounts (
                    id NVARCHAR(64) PRIMARY KEY, name NVARCHAR(500) NOT NULL, additional_data NVARCHAR(MAX) NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF OBJECT_ID(N'dbo.administrators', N'U') IS NULL
                  CREATE TABLE administrators (
                    id NVARCHAR(64) PRIMARY KEY, account_id NVARCHAR(64) NULL,
                    first_name NVARCHAR(255) NULL, last_name NVARCHAR(255) NULL,
                    email NVARCHAR(255) NOT NULL UNIQUE, password_sha256 NVARCHAR(64) NOT NULL,
                    telephone NVARCHAR(64) NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF OBJECT_ID(N'dbo.tenants', N'U') IS NULL
                  CREATE TABLE tenants (
                    id NVARCHAR(64) PRIMARY KEY, account_id NVARCHAR(64) NULL, name NVARCHAR(500) NOT NULL,
                    region NVARCHAR(64) NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF OBJECT_ID(N'dbo.users', N'U') IS NULL
                  CREATE TABLE users (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    first_name NVARCHAR(255) NULL, last_name NVARCHAR(255) NULL,
                    email NVARCHAR(255) NOT NULL, password_sha256 NVARCHAR(64) NOT NULL,
                    is_admin BIT NOT NULL DEFAULT 0, is_tenant_admin BIT NOT NULL DEFAULT 0,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_users_tenant_email')
                  CREATE UNIQUE INDEX idx_users_tenant_email ON users(tenant_id, email);",
                @"IF OBJECT_ID(N'dbo.credentials', N'U') IS NULL
                  CREATE TABLE credentials (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL, user_id NVARCHAR(64) NOT NULL,
                    name NVARCHAR(255) NOT NULL,
                    access_key NVARCHAR(128) NOT NULL UNIQUE, secret_key NVARCHAR(128) NOT NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_credentials_tenant_user')
                  CREATE INDEX idx_credentials_tenant_user ON credentials(tenant_id, user_id);",
                @"IF OBJECT_ID(N'dbo.roles', N'U') IS NULL
                  CREATE TABLE roles (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    name NVARCHAR(255) NOT NULL, description NVARCHAR(1000) NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_roles_tenant_name')
                  CREATE UNIQUE INDEX idx_roles_tenant_name ON roles(tenant_id, name);",
                @"IF OBJECT_ID(N'dbo.user_role_maps', N'U') IS NULL
                  CREATE TABLE user_role_maps (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    user_id NVARCHAR(64) NOT NULL, role_id NVARCHAR(64) NOT NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_urm_user')
                  CREATE INDEX idx_urm_user ON user_role_maps(tenant_id, user_id);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_urm_role')
                  CREATE INDEX idx_urm_role ON user_role_maps(tenant_id, role_id);",
                @"IF OBJECT_ID(N'dbo.permissions', N'U') IS NULL
                  CREATE TABLE permissions (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    name NVARCHAR(255) NOT NULL,
                    resource_types NVARCHAR(MAX) NOT NULL, operation_types NVARCHAR(MAX) NOT NULL,
                    permission_type NVARCHAR(16) NOT NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_permissions_tenant')
                  CREATE INDEX idx_permissions_tenant ON permissions(tenant_id);",
                @"IF OBJECT_ID(N'dbo.role_permission_maps', N'U') IS NULL
                  CREATE TABLE role_permission_maps (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    role_id NVARCHAR(64) NOT NULL, permission_id NVARCHAR(64) NOT NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_rpm_role')
                  CREATE INDEX idx_rpm_role ON role_permission_maps(tenant_id, role_id);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_rpm_perm')
                  CREATE INDEX idx_rpm_perm ON role_permission_maps(tenant_id, permission_id);",
                @"IF OBJECT_ID(N'dbo.data_flows', N'U') IS NULL
                  CREATE TABLE data_flows (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    name NVARCHAR(255) NOT NULL, description NVARCHAR(1000) NULL,
                    trigger_id NVARCHAR(64) NULL, start_step_id NVARCHAR(255) NOT NULL,
                    max_runtime_ms INT NOT NULL DEFAULT 0, transitions NVARCHAR(MAX) NOT NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_flows_tenant')
                  CREATE INDEX idx_flows_tenant ON data_flows(tenant_id);",
                @"IF OBJECT_ID(N'dbo.steps', N'U') IS NULL
                  CREATE TABLE steps (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    name NVARCHAR(255) NOT NULL, description NVARCHAR(1000) NULL,
                    step_type NVARCHAR(16) NOT NULL, max_runtime_ms INT NOT NULL DEFAULT 0,
                    rest_config NVARCHAR(MAX) NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_steps_tenant')
                  CREATE INDEX idx_steps_tenant ON steps(tenant_id);",
                @"IF OBJECT_ID(N'dbo.triggers', N'U') IS NULL
                  CREATE TABLE triggers (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    name NVARCHAR(255) NOT NULL, description NVARCHAR(1000) NULL,
                    trigger_type NVARCHAR(32) NOT NULL, data_flow_id NVARCHAR(64) NULL,
                    configuration NVARCHAR(MAX) NULL,
                    active BIT NOT NULL DEFAULT 1, is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_triggers_tenant')
                  CREATE INDEX idx_triggers_tenant ON triggers(tenant_id);",
                @"IF OBJECT_ID(N'dbo.flow_runs', N'U') IS NULL
                  CREATE TABLE flow_runs (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    data_flow_id NVARCHAR(64) NOT NULL,
                    triggered_by_user_id NVARCHAR(64) NULL, trigger_id NVARCHAR(64) NULL,
                    state NVARCHAR(32) NOT NULL, input_data NVARCHAR(MAX) NULL, output_data NVARCHAR(MAX) NULL,
                    error_message NVARCHAR(MAX) NULL,
                    created_utc DATETIME2 NOT NULL, started_utc DATETIME2 NULL, completed_utc DATETIME2 NULL,
                    last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_runs_tenant_flow')
                  CREATE INDEX idx_runs_tenant_flow ON flow_runs(tenant_id, data_flow_id);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_runs_state')
                  CREATE INDEX idx_runs_state ON flow_runs(state);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_runs_created')
                  CREATE INDEX idx_runs_created ON flow_runs(created_utc);",
                @"IF OBJECT_ID(N'dbo.step_runs', N'U') IS NULL
                  CREATE TABLE step_runs (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NOT NULL,
                    flow_run_id NVARCHAR(64) NOT NULL, data_flow_id NVARCHAR(64) NOT NULL,
                    step_id NVARCHAR(255) NOT NULL, sequence INT NOT NULL DEFAULT 0,
                    result NVARCHAR(32) NOT NULL, next_step_id NVARCHAR(255) NULL,
                    input_data NVARCHAR(MAX) NULL, output_data NVARCHAR(MAX) NULL, error_message NVARCHAR(MAX) NULL,
                    started_utc DATETIME2 NOT NULL, completed_utc DATETIME2 NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_step_runs_flow')
                  CREATE INDEX idx_step_runs_flow ON step_runs(tenant_id, flow_run_id);",
                @"IF OBJECT_ID(N'dbo.request_history', N'U') IS NULL
                  CREATE TABLE request_history (
                    id NVARCHAR(64) PRIMARY KEY, tenant_id NVARCHAR(64) NULL, user_id NVARCHAR(64) NULL,
                    principal_name NVARCHAR(255) NULL,
                    method NVARCHAR(16) NOT NULL, path NVARCHAR(1024) NOT NULL, url NVARCHAR(2048) NOT NULL,
                    status_code INT NOT NULL, duration_ms FLOAT NOT NULL, source_ip NVARCHAR(64) NULL,
                    request_headers NVARCHAR(MAX) NULL, request_body NVARCHAR(MAX) NULL,
                    request_body_bytes BIGINT NOT NULL DEFAULT 0, request_body_truncated BIT NOT NULL DEFAULT 0,
                    response_headers NVARCHAR(MAX) NULL, response_body NVARCHAR(MAX) NULL,
                    response_body_bytes BIGINT NOT NULL DEFAULT 0, response_body_truncated BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL, completed_utc DATETIME2 NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_req_created')
                  CREATE INDEX idx_req_created ON request_history(created_utc);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_req_tenant')
                  CREATE INDEX idx_req_tenant ON request_history(tenant_id, created_utc);"
            });
            list.Add(m1);

            SchemaMigration m2 = new SchemaMigration { Version = 2, Description = "step execution keys" };
            m2.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.steps', 'execution_key') IS NULL
                  ALTER TABLE steps ADD execution_key NVARCHAR(255) NULL;",
                @"UPDATE steps SET execution_key = name WHERE execution_key IS NULL OR LTRIM(RTRIM(execution_key)) = '';",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_steps_tenant_execution_key')
                  CREATE UNIQUE INDEX idx_steps_tenant_execution_key ON steps(tenant_id, execution_key);"
            });
            list.Add(m2);

            SchemaMigration m3 = new SchemaMigration { Version = 3, Description = "canonical step runtime model" };
            m3.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.steps', 'runtime_key') IS NULL
                  ALTER TABLE steps ADD runtime_key NVARCHAR(128) NULL;",
                @"IF COL_LENGTH('dbo.steps', 'runtime_config') IS NULL
                  ALTER TABLE steps ADD runtime_config NVARCHAR(MAX) NULL;",
                @"IF COL_LENGTH('dbo.steps', 'contract_type') IS NULL
                  ALTER TABLE steps ADD contract_type NVARCHAR(16) NULL;",
                @"IF COL_LENGTH('dbo.steps', 'input_schema') IS NULL
                  ALTER TABLE steps ADD input_schema NVARCHAR(MAX) NULL;",
                @"IF COL_LENGTH('dbo.steps', 'output_schema') IS NULL
                  ALTER TABLE steps ADD output_schema NVARCHAR(MAX) NULL;",
                @"IF COL_LENGTH('dbo.steps', 'validate_input') IS NULL
                  ALTER TABLE steps ADD validate_input BIT NOT NULL DEFAULT 0;",
                @"IF COL_LENGTH('dbo.steps', 'validate_output') IS NULL
                  ALTER TABLE steps ADD validate_output BIT NOT NULL DEFAULT 0;",
                @"IF COL_LENGTH('dbo.steps', 'artifact_id') IS NULL
                  ALTER TABLE steps ADD artifact_id NVARCHAR(64) NULL;",
                @"IF COL_LENGTH('dbo.steps', 'artifact_version') IS NULL
                  ALTER TABLE steps ADD artifact_version NVARCHAR(64) NULL;",
                @"UPDATE steps SET runtime_key = CASE WHEN step_type = 'Rest' THEN 'External.Rest' ELSE 'Builtin.Unknown' END WHERE runtime_key IS NULL OR LTRIM(RTRIM(runtime_key)) = '';",
                @"UPDATE steps SET contract_type = 'Loose' WHERE contract_type IS NULL OR LTRIM(RTRIM(contract_type)) = '';"
            });
            list.Add(m3);

            SchemaMigration m4 = new SchemaMigration { Version = 4, Description = "step runtime binding state" };
            m4.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.steps', 'runtime_binding_state') IS NULL
                  ALTER TABLE steps ADD runtime_binding_state NVARCHAR(32) NULL;",
                @"IF COL_LENGTH('dbo.steps', 'runtime_binding_message') IS NULL
                  ALTER TABLE steps ADD runtime_binding_message NVARCHAR(MAX) NULL;",
                @"UPDATE steps SET runtime_binding_state = CASE WHEN runtime_key = 'Builtin.Unknown' THEN 'Unresolved' ELSE 'Resolved' END WHERE runtime_binding_state IS NULL OR LTRIM(RTRIM(runtime_binding_state)) = '';"
            });
            list.Add(m4);

            SchemaMigration m5 = new SchemaMigration { Version = 5, Description = "artifact metadata" };
            m5.Statements.AddRange(new[]
            {
                @"IF OBJECT_ID(N'dbo.artifacts', N'U') IS NULL
                  CREATE TABLE artifacts (
                    id NVARCHAR(64) PRIMARY KEY,
                    tenant_id NVARCHAR(64) NOT NULL,
                    name NVARCHAR(255) NOT NULL,
                    description NVARCHAR(1000) NULL,
                    active BIT NOT NULL DEFAULT 1,
                    is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL,
                    last_update_utc DATETIME2 NOT NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_artifacts_tenant_name')
                  CREATE UNIQUE INDEX idx_artifacts_tenant_name ON artifacts(tenant_id, name);",
                @"IF OBJECT_ID(N'dbo.artifact_versions', N'U') IS NULL
                  CREATE TABLE artifact_versions (
                    id NVARCHAR(64) PRIMARY KEY,
                    tenant_id NVARCHAR(64) NOT NULL,
                    artifact_id NVARCHAR(64) NOT NULL,
                    version NVARCHAR(128) NOT NULL,
                    sha256 NVARCHAR(64) NOT NULL,
                    byte_length BIGINT NOT NULL DEFAULT 0,
                    content_type NVARCHAR(255) NULL,
                    original_file_name NVARCHAR(1024) NULL,
                    manifest_json NVARCHAR(MAX) NULL,
                    storage_key NVARCHAR(1024) NULL,
                    active BIT NOT NULL DEFAULT 1,
                    is_protected BIT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL,
                    last_update_utc DATETIME2 NOT NULL,
                    deleted_utc DATETIME2 NULL,
                    gc_eligible_utc DATETIME2 NULL
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_artifact_versions_artifact_version')
                  CREATE UNIQUE INDEX idx_artifact_versions_artifact_version ON artifact_versions(tenant_id, artifact_id, version);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_artifact_versions_sha')
                  CREATE INDEX idx_artifact_versions_sha ON artifact_versions(tenant_id, sha256);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_artifact_versions_gc')
                  CREATE INDEX idx_artifact_versions_gc ON artifact_versions(gc_eligible_utc);"
            });
            list.Add(m5);

            SchemaMigration m6 = new SchemaMigration { Version = 6, Description = "step run protocol version" };
            m6.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.step_runs', 'protocol_version') IS NULL
                  ALTER TABLE step_runs ADD protocol_version NVARCHAR(32) NULL;",
                @"UPDATE step_runs SET protocol_version = '1.0' WHERE protocol_version IS NULL OR LTRIM(RTRIM(protocol_version)) = '';"
            });
            list.Add(m6);

            SchemaMigration m7 = new SchemaMigration { Version = 7, Description = "step run capacity wait state" };
            m7.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.step_runs', 'execution_state') IS NULL
                  ALTER TABLE step_runs ADD execution_state NVARCHAR(32) NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'capacity_queued_utc') IS NULL
                  ALTER TABLE step_runs ADD capacity_queued_utc DATETIME2 NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'capacity_acquired_utc') IS NULL
                  ALTER TABLE step_runs ADD capacity_acquired_utc DATETIME2 NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'capacity_wait_ms') IS NULL
                  ALTER TABLE step_runs ADD capacity_wait_ms BIGINT NULL;",
                @"UPDATE step_runs SET execution_state = 'Complete' WHERE execution_state IS NULL OR LTRIM(RTRIM(execution_state)) = '';"
            });
            list.Add(m7);

            SchemaMigration m8 = new SchemaMigration { Version = 8, Description = "artifact run snapshots" };
            m8.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.flow_runs', 'execution_snapshot_json') IS NULL
                  ALTER TABLE flow_runs ADD execution_snapshot_json NVARCHAR(MAX) NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'artifact_id') IS NULL
                  ALTER TABLE step_runs ADD artifact_id NVARCHAR(64) NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'artifact_version_id') IS NULL
                  ALTER TABLE step_runs ADD artifact_version_id NVARCHAR(64) NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'artifact_version') IS NULL
                  ALTER TABLE step_runs ADD artifact_version NVARCHAR(128) NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'artifact_sha256') IS NULL
                  ALTER TABLE step_runs ADD artifact_sha256 NVARCHAR(64) NULL;",
                @"IF COL_LENGTH('dbo.step_runs', 'manifest_entrypoint') IS NULL
                  ALTER TABLE step_runs ADD manifest_entrypoint NVARCHAR(128) NULL;"
            });
            list.Add(m8);

            SchemaMigration m9 = new SchemaMigration { Version = 9, Description = "mutable artifact files" };
            m9.Statements.AddRange(new[]
            {
                @"IF OBJECT_ID(N'dbo.artifact_files', N'U') IS NULL
                  CREATE TABLE artifact_files (
                    tenant_id NVARCHAR(64) NOT NULL,
                    artifact_id NVARCHAR(64) NOT NULL,
                    path NVARCHAR(1024) NOT NULL,
                    content NVARCHAR(MAX) NOT NULL,
                    content_type NVARCHAR(255) NULL,
                    is_binary BIT NOT NULL DEFAULT 0,
                    sha256 NVARCHAR(64) NOT NULL,
                    byte_length BIGINT NOT NULL DEFAULT 0,
                    created_utc DATETIME2 NOT NULL,
                    last_update_utc DATETIME2 NOT NULL,
                    CONSTRAINT pk_artifact_files PRIMARY KEY (tenant_id, artifact_id, path)
                  );",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_artifact_files_artifact')
                  CREATE INDEX idx_artifact_files_artifact ON artifact_files(tenant_id, artifact_id);"
            });
            list.Add(m9);

            SchemaMigration m10 = new SchemaMigration { Version = 10, Description = "distributed execution foundation" };
            m10.Statements.AddRange(new[]
            {
                @"IF OBJECT_ID(N'dbo.workers', N'U') IS NULL
                  CREATE TABLE workers (
                    id NVARCHAR(64) PRIMARY KEY,
                    name NVARCHAR(255) NOT NULL,
                    kind NVARCHAR(64) NOT NULL,
                    state NVARCHAR(64) NOT NULL,
                    enabled BIT NOT NULL DEFAULT 1,
                    drain_mode BIT NOT NULL DEFAULT 0,
                    version NVARCHAR(64) NULL,
                    host_name NVARCHAR(255) NULL,
                    labels_json NVARCHAR(MAX) NULL,
                    max_concurrent_runs INT NOT NULL DEFAULT 1,
                    last_heartbeat_utc DATETIME2 NULL,
                    created_utc DATETIME2 NOT NULL
                  );",
                @"IF OBJECT_ID(N'dbo.worker_sessions', N'U') IS NULL
                  CREATE TABLE worker_sessions (
                    id NVARCHAR(64) PRIMARY KEY,
                    worker_id NVARCHAR(64) NOT NULL,
                    connected_utc DATETIME2 NOT NULL,
                    disconnected_utc DATETIME2 NULL,
                    disconnect_reason NVARCHAR(MAX) NULL,
                    protocol_version NVARCHAR(32) NULL
                  );",
                @"IF OBJECT_ID(N'dbo.run_assignments', N'U') IS NULL
                  CREATE TABLE run_assignments (
                    id NVARCHAR(64) PRIMARY KEY,
                    flow_run_id NVARCHAR(64) NOT NULL,
                    worker_id NVARCHAR(64) NOT NULL,
                    worker_session_id NVARCHAR(64) NULL,
                    attempt_number INT NOT NULL DEFAULT 1,
                    state NVARCHAR(32) NOT NULL,
                    lease_token NVARCHAR(64) NOT NULL,
                    lease_expires_utc DATETIME2 NOT NULL,
                    assigned_utc DATETIME2 NOT NULL,
                    completed_utc DATETIME2 NULL
                  );",
                @"IF OBJECT_ID(N'dbo.worker_activity', N'U') IS NULL
                  CREATE TABLE worker_activity (
                    id NVARCHAR(64) PRIMARY KEY,
                    worker_id NVARCHAR(64) NOT NULL,
                    worker_session_id NVARCHAR(64) NULL,
                    flow_run_id NVARCHAR(64) NULL,
                    run_assignment_id NVARCHAR(64) NULL,
                    event_type NVARCHAR(64) NOT NULL,
                    severity NVARCHAR(32) NULL,
                    message NVARCHAR(MAX) NULL,
                    payload_json NVARCHAR(MAX) NULL,
                    created_utc DATETIME2 NOT NULL
                  );",
                @"IF OBJECT_ID(N'dbo.server_instances', N'U') IS NULL
                  CREATE TABLE server_instances (
                    id NVARCHAR(64) PRIMARY KEY,
                    started_utc DATETIME2 NOT NULL,
                    last_heartbeat_utc DATETIME2 NOT NULL,
                    version NVARCHAR(64) NULL
                  );",
                @"IF COL_LENGTH('dbo.flow_runs', 'dispatch_state') IS NULL
                  ALTER TABLE flow_runs ADD dispatch_state NVARCHAR(32) NULL;",
                @"IF COL_LENGTH('dbo.flow_runs', 'dispatch_attempt') IS NULL
                  ALTER TABLE flow_runs ADD dispatch_attempt INT NOT NULL DEFAULT 0;",
                @"IF COL_LENGTH('dbo.flow_runs', 'assigned_worker_id') IS NULL
                  ALTER TABLE flow_runs ADD assigned_worker_id NVARCHAR(64) NULL;",
                @"IF COL_LENGTH('dbo.flow_runs', 'run_assignment_id') IS NULL
                  ALTER TABLE flow_runs ADD run_assignment_id NVARCHAR(64) NULL;",
                @"IF COL_LENGTH('dbo.flow_runs', 'queue_wait_ms') IS NULL
                  ALTER TABLE flow_runs ADD queue_wait_ms BIGINT NULL;",
                @"IF COL_LENGTH('dbo.flow_runs', 'assigned_utc') IS NULL
                  ALTER TABLE flow_runs ADD assigned_utc DATETIME2 NULL;",
                @"IF COL_LENGTH('dbo.flow_runs', 'lease_expires_utc') IS NULL
                  ALTER TABLE flow_runs ADD lease_expires_utc DATETIME2 NULL;",
                @"IF COL_LENGTH('dbo.flow_runs', 'execution_node_kind') IS NULL
                  ALTER TABLE flow_runs ADD execution_node_kind NVARCHAR(32) NULL;",
                @"UPDATE flow_runs SET dispatch_state = 'Pending' WHERE dispatch_state IS NULL OR LTRIM(RTRIM(dispatch_state)) = '';",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_flow_runs_dispatch_pending')
                  CREATE INDEX idx_flow_runs_dispatch_pending ON flow_runs(dispatch_state, state, created_utc);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_workers_online')
                  CREATE INDEX idx_workers_online ON workers(enabled, drain_mode, state, last_heartbeat_utc);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_worker_sessions_stale')
                  CREATE INDEX idx_worker_sessions_stale ON worker_sessions(worker_id, disconnected_utc, connected_utc);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_run_assignments_lease')
                  CREATE INDEX idx_run_assignments_lease ON run_assignments(state, lease_expires_utc);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_run_assignments_flow_run')
                  CREATE INDEX idx_run_assignments_flow_run ON run_assignments(flow_run_id, attempt_number);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_worker_activity_worker')
                  CREATE INDEX idx_worker_activity_worker ON worker_activity(worker_id, created_utc);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_worker_activity_run')
                  CREATE INDEX idx_worker_activity_run ON worker_activity(flow_run_id, created_utc);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_server_instances_heartbeat')
                  CREATE INDEX idx_server_instances_heartbeat ON server_instances(last_heartbeat_utc);"
            });
            list.Add(m10);

            SchemaMigration m11 = new SchemaMigration { Version = 11, Description = "distributed execution worker auth and placement" };
            m11.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.workers', 'capabilities_json') IS NULL
                  ALTER TABLE workers ADD capabilities_json NVARCHAR(MAX) NULL;",
                @"IF COL_LENGTH('dbo.workers', 'token_hash') IS NULL
                  ALTER TABLE workers ADD token_hash NVARCHAR(128) NULL;",
                @"IF COL_LENGTH('dbo.workers', 'token_last_rotated_utc') IS NULL
                  ALTER TABLE workers ADD token_last_rotated_utc DATETIME2 NULL;",
                @"IF COL_LENGTH('dbo.data_flows', 'routing_hint_label') IS NULL
                  ALTER TABLE data_flows ADD routing_hint_label NVARCHAR(255) NULL;",
                @"IF COL_LENGTH('dbo.server_instances', 'host_name') IS NULL
                  ALTER TABLE server_instances ADD host_name NVARCHAR(255) NULL;",
                @"UPDATE workers SET capabilities_json = '[]' WHERE capabilities_json IS NULL OR LTRIM(RTRIM(capabilities_json)) = '';",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_workers_token_hash')
                  CREATE INDEX idx_workers_token_hash ON workers(token_hash);",
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_data_flows_routing_label')
                  CREATE INDEX idx_data_flows_routing_label ON data_flows(routing_hint_label);"
            });
            list.Add(m11);

            SchemaMigration m12 = new SchemaMigration { Version = 12, Description = "worker task timeout metadata" };
            m12.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.workers', 'max_task_timeout_ms') IS NULL
                  ALTER TABLE workers ADD max_task_timeout_ms INT NOT NULL CONSTRAINT DF_workers_max_task_timeout_ms DEFAULT 0;",
                @"UPDATE workers SET max_task_timeout_ms = 0 WHERE max_task_timeout_ms IS NULL;"
            });
            list.Add(m12);

            SchemaMigration m13 = new SchemaMigration { Version = 13, Description = "flow run source ip" };
            m13.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.flow_runs', 'source_ip') IS NULL
                  ALTER TABLE flow_runs ADD source_ip NVARCHAR(64) NULL;"
            });
            list.Add(m13);

            SchemaMigration m14 = new SchemaMigration { Version = 14, Description = "flow invocation authentication policy" };
            m14.Statements.AddRange(new[]
            {
                @"IF COL_LENGTH('dbo.data_flows', 'invocation_auth_mode') IS NULL
                  ALTER TABLE data_flows ADD invocation_auth_mode NVARCHAR(32) NOT NULL CONSTRAINT DF_data_flows_invocation_auth_mode DEFAULT 'Public';",
                @"UPDATE data_flows SET invocation_auth_mode = 'Public' WHERE invocation_auth_mode IS NULL OR LTRIM(RTRIM(invocation_auth_mode)) = '';"
            });
            list.Add(m14);

            return list;
        }
    }
}
