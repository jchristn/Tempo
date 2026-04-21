namespace Tempo.Core.Helpers
{
    /// <summary>
    /// Generates application identifiers using PrettyId K-sortable IDs with stable prefixes.
    /// Thread-safe.
    /// </summary>
    public static class IdGenerator
    {
        private static readonly PrettyId.IdGenerator _PrettyId = new PrettyId.IdGenerator();

        /// <summary>Generate a k-sortable identifier with a total maximum length of 32 characters.</summary>
        private static string Generate(string prefix)
        {
            return _PrettyId.GenerateKSortable(prefix, Constants.IdLength);
        }

        /// <summary>Generate an account identifier.</summary>
        public static string GenerateAccountId()
        {
            return Generate(Constants.AccountIdPrefix);
        }

        /// <summary>Generate an administrator identifier.</summary>
        public static string GenerateAdminId()
        {
            return Generate(Constants.AdminIdPrefix);
        }

        /// <summary>Generate a tenant identifier.</summary>
        public static string GenerateTenantId()
        {
            return Generate(Constants.TenantIdPrefix);
        }

        /// <summary>Generate a user identifier.</summary>
        public static string GenerateUserId()
        {
            return Generate(Constants.UserIdPrefix);
        }

        /// <summary>Generate a credential identifier.</summary>
        public static string GenerateCredentialId()
        {
            return Generate(Constants.CredentialIdPrefix);
        }

        /// <summary>Generate a role identifier.</summary>
        public static string GenerateRoleId()
        {
            return Generate(Constants.RoleIdPrefix);
        }

        /// <summary>Generate a user role map identifier.</summary>
        public static string GenerateUserRoleMapId()
        {
            return Generate(Constants.UserRoleMapIdPrefix);
        }

        /// <summary>Generate a permission identifier.</summary>
        public static string GeneratePermissionId()
        {
            return Generate(Constants.PermissionIdPrefix);
        }

        /// <summary>Generate a role permission map identifier.</summary>
        public static string GenerateRolePermissionMapId()
        {
            return Generate(Constants.RolePermissionMapIdPrefix);
        }

        /// <summary>Generate a data flow identifier.</summary>
        public static string GenerateDataFlowId()
        {
            return Generate(Constants.DataFlowIdPrefix);
        }

        /// <summary>Generate a step identifier.</summary>
        public static string GenerateStepId()
        {
            return Generate(Constants.StepIdPrefix);
        }

        /// <summary>Generate a trigger identifier.</summary>
        public static string GenerateTriggerId()
        {
            return Generate(Constants.TriggerIdPrefix);
        }

        /// <summary>Generate a flow run identifier.</summary>
        public static string GenerateFlowRunId()
        {
            return Generate(Constants.FlowRunIdPrefix);
        }

        /// <summary>Generate a step run identifier.</summary>
        public static string GenerateStepRunId()
        {
            return Generate(Constants.StepRunIdPrefix);
        }

        /// <summary>Generate a request history identifier.</summary>
        public static string GenerateRequestHistoryId()
        {
            return Generate(Constants.RequestHistoryIdPrefix);
        }

        /// <summary>Generate an artifact identifier.</summary>
        public static string GenerateArtifactId()
        {
            return Generate(Constants.ArtifactIdPrefix);
        }

        /// <summary>Generate an artifact version identifier.</summary>
        public static string GenerateArtifactVersionId()
        {
            return Generate(Constants.ArtifactVersionIdPrefix);
        }

        /// <summary>Generate a token nonce identifier.</summary>
        public static string GenerateNonceId()
        {
            return Generate(Constants.NonceIdPrefix);
        }

        /// <summary>
        /// Generate a k-sortable credential access key (prefix <c>pub_</c>, total length 32).
        /// </summary>
        public static string GenerateAccessKey()
        {
            return Generate(Constants.AccessKeyPrefix);
        }

        /// <summary>
        /// Generate a k-sortable credential secret key (prefix <c>key_</c>, total length 32).
        /// </summary>
        public static string GenerateSecretKey()
        {
            return Generate(Constants.SecretKeyPrefix);
        }
    }
}
