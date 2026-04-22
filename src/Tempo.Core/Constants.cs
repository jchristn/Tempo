namespace Tempo.Core
{
    /// <summary>
    /// Shared string and numeric constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>Logo displayed on startup.</summary>
        public const string Logo =
            "  _____                           \r\n" +
            " |_   _|__ _ __ ___  _ __   ___   \r\n" +
            "   | |/ _ \\ '_ ` _ \\| '_ \\ / _ \\  \r\n" +
            "   | |  __/ | | | | | |_) | (_) | \r\n" +
            "   |_|\\___|_| |_| |_| .__/ \\___/  \r\n" +
            "                    |_|           \r\n";

        /// <summary>Product name.</summary>
        public const string ProductName = "Tempo Server";

        /// <summary>Copyright notice.</summary>
        public const string Copyright = "(c)2026 Joel Christner";

        /// <summary>Default settings filename.</summary>
        public const string DefaultSettingsFile = "./tempo.json";

        /// <summary>Default API version prefix.</summary>
        public const string ApiPrefix = "/v1.0";

        /// <summary>Identifier prefix for accounts.</summary>
        public const string AccountIdPrefix = "acc_";

        /// <summary>Identifier prefix for administrators.</summary>
        public const string AdminIdPrefix = "adm_";

        /// <summary>Identifier prefix for tenants.</summary>
        public const string TenantIdPrefix = "ten_";

        /// <summary>Identifier prefix for users.</summary>
        public const string UserIdPrefix = "usr_";

        /// <summary>Identifier prefix for credentials.</summary>
        public const string CredentialIdPrefix = "crd_";

        /// <summary>Identifier prefix for roles.</summary>
        public const string RoleIdPrefix = "rol_";

        /// <summary>Identifier prefix for user role mappings.</summary>
        public const string UserRoleMapIdPrefix = "urm_";

        /// <summary>Identifier prefix for permissions.</summary>
        public const string PermissionIdPrefix = "prm_";

        /// <summary>Identifier prefix for role permission mappings.</summary>
        public const string RolePermissionMapIdPrefix = "rpm_";

        /// <summary>Identifier prefix for data flows.</summary>
        public const string DataFlowIdPrefix = "flow_";

        /// <summary>Identifier prefix for steps.</summary>
        public const string StepIdPrefix = "step_";

        /// <summary>Identifier prefix for triggers.</summary>
        public const string TriggerIdPrefix = "trg_";

        /// <summary>Identifier prefix for flow runs.</summary>
        public const string FlowRunIdPrefix = "run_";

        /// <summary>Identifier prefix for workers.</summary>
        public const string WorkerIdPrefix = "wrk_";

        /// <summary>Identifier prefix for worker sessions.</summary>
        public const string WorkerSessionIdPrefix = "wse_";

        /// <summary>Identifier prefix for run assignments.</summary>
        public const string RunAssignmentIdPrefix = "ras_";

        /// <summary>Identifier prefix for worker activity rows.</summary>
        public const string WorkerActivityIdPrefix = "wac_";

        /// <summary>Identifier prefix for server instance rows.</summary>
        public const string ServerInstanceIdPrefix = "srv_";

        /// <summary>Identifier prefix for step runs.</summary>
        public const string StepRunIdPrefix = "sru_";

        /// <summary>Identifier prefix for request history rows.</summary>
        public const string RequestHistoryIdPrefix = "req_";

        /// <summary>Identifier prefix for artifacts.</summary>
        public const string ArtifactIdPrefix = "art_";

        /// <summary>Identifier prefix for artifact versions.</summary>
        public const string ArtifactVersionIdPrefix = "arv_";

        /// <summary>Identifier prefix for authentication token nonces.</summary>
        public const string NonceIdPrefix = "non_";

        /// <summary>Mutable artifact snapshot version label.</summary>
        public const string MutableArtifactVersion = "current";

        /// <summary>Identifier prefix for credential access keys.</summary>
        public const string AccessKeyPrefix = "pub_";

        /// <summary>Identifier prefix for credential secret keys.</summary>
        public const string SecretKeyPrefix = "key_";

        /// <summary>Default PrettyId length used for generated identifiers (k-sortable, total length including prefix).</summary>
        public const int IdLength = 32;

        /// <summary>Legacy key random-length setting. Generated keys use <see cref="IdLength"/> as the total length.</summary>
        public const int KeyRandomLength = 32;

        /// <summary>Default token expiration in minutes.</summary>
        public const int DefaultTokenExpirationMinutes = 1440;

        /// <summary>Header for the API token.</summary>
        public const string HeaderToken = "x-token";

        /// <summary>Header for a system admin bypass API key.</summary>
        public const string HeaderApiKey = "x-api-key";

        /// <summary>Header for the tenant identifier.</summary>
        public const string HeaderTenantId = "x-tenant-id";

        /// <summary>Header for the worker identifier.</summary>
        public const string HeaderWorkerId = "x-worker-id";

        /// <summary>Header for the worker token.</summary>
        public const string HeaderWorkerToken = "x-worker-token";

        /// <summary>Response header for the flow run identifier.</summary>
        public const string HeaderRunId = "x-run-id";

        /// <summary>Response header for the data flow identifier.</summary>
        public const string HeaderDataFlowId = "x-dataflow-id";

        /// <summary>Response header for the trigger identifier.</summary>
        public const string HeaderTriggerId = "x-trigger-id";

        /// <summary>Response header for the flow run state.</summary>
        public const string HeaderRunState = "x-run-state";

        /// <summary>Response header for the flow run creation timestamp.</summary>
        public const string HeaderRunCreatedUtc = "x-run-created-utc";

        /// <summary>Response header for the flow run start timestamp.</summary>
        public const string HeaderRunStartedUtc = "x-run-started-utc";

        /// <summary>Response header for the flow run completion timestamp.</summary>
        public const string HeaderRunCompletedUtc = "x-run-completed-utc";

        /// <summary>Response header for the flow run last-update timestamp.</summary>
        public const string HeaderRunLastUpdateUtc = "x-run-last-update-utc";

        /// <summary>Response header for the flow run execution duration in milliseconds.</summary>
        public const string HeaderRuntimeMs = "x-runtime-ms";

        /// <summary>Response header for the flow run error message.</summary>
        public const string HeaderRunError = "x-run-error";

        /// <summary>CORS-exposed run metadata response headers.</summary>
        public const string HeaderRunMetadataExposeList =
            HeaderTenantId + ", " +
            HeaderWorkerId + ", " +
            HeaderRunId + ", " +
            HeaderDataFlowId + ", " +
            HeaderTriggerId + ", " +
            HeaderRunState + ", " +
            HeaderRunCreatedUtc + ", " +
            HeaderRunStartedUtc + ", " +
            HeaderRunCompletedUtc + ", " +
            HeaderRunLastUpdateUtc + ", " +
            HeaderRuntimeMs + ", " +
            HeaderRunError;

        /// <summary>Header for the user email (password-auth flow).</summary>
        public const string HeaderEmail = "x-email";

        /// <summary>Header for the user password SHA-256 (password-auth flow).</summary>
        public const string HeaderPassword = "x-password";

        /// <summary>Header for the credential access key.</summary>
        public const string HeaderAccessKey = "x-access-key";

        /// <summary>Header for the credential secret key.</summary>
        public const string HeaderSecretKey = "x-secret-key";

        /// <summary>Header for the request correlation identifier.</summary>
        public const string HeaderRequestId = "x-request";

        /// <summary>Redacted token placeholder returned in responses.</summary>
        public const string RedactedValue = "****";
    }
}
