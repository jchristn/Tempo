namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core;
    using Tempo.Core.Helpers;
    using Touchstone.Core;

    /// <summary>Identifier generation tests.</summary>
    public static class IdentifierSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Identifier",
                displayName: "Identifier generation",
                cases: new List<TestCaseDescriptor>
                {
                    Case("AccountPrefix", "Account IDs start with acc_", Constants.AccountIdPrefix, IdGenerator.GenerateAccountId),
                    Case("AdminPrefix", "Administrator IDs start with adm_", Constants.AdminIdPrefix, IdGenerator.GenerateAdminId),
                    Case("TenantPrefix", "Tenant IDs start with ten_", Constants.TenantIdPrefix, IdGenerator.GenerateTenantId),
                    Case("UserPrefix", "User IDs start with usr_", Constants.UserIdPrefix, IdGenerator.GenerateUserId),
                    Case("CredentialPrefix", "Credential IDs start with crd_", Constants.CredentialIdPrefix, IdGenerator.GenerateCredentialId),
                    Case("RolePrefix", "Role IDs start with rol_", Constants.RoleIdPrefix, IdGenerator.GenerateRoleId),
                    Case("UserRoleMapPrefix", "User-role map IDs start with urm_", Constants.UserRoleMapIdPrefix, IdGenerator.GenerateUserRoleMapId),
                    Case("PermissionPrefix", "Permission IDs start with prm_", Constants.PermissionIdPrefix, IdGenerator.GeneratePermissionId),
                    Case("RolePermissionMapPrefix", "Role-permission map IDs start with rpm_", Constants.RolePermissionMapIdPrefix, IdGenerator.GenerateRolePermissionMapId),
                    Case("DataFlowPrefix", "Flow IDs start with flow_", Constants.DataFlowIdPrefix, IdGenerator.GenerateDataFlowId),
                    Case("StepPrefix", "Step IDs start with step_", Constants.StepIdPrefix, IdGenerator.GenerateStepId),
                    Case("TriggerPrefix", "Trigger IDs start with trg_", Constants.TriggerIdPrefix, IdGenerator.GenerateTriggerId),
                    Case("FlowRunPrefix", "Flow run IDs start with run_", Constants.FlowRunIdPrefix, IdGenerator.GenerateFlowRunId),
                    Case("StepRunPrefix", "Step run IDs start with sru_", Constants.StepRunIdPrefix, IdGenerator.GenerateStepRunId),
                    Case("RequestHistoryPrefix", "Request history IDs start with req_", Constants.RequestHistoryIdPrefix, IdGenerator.GenerateRequestHistoryId),
                    Case("ArtifactPrefix", "Artifact IDs start with art_", Constants.ArtifactIdPrefix, IdGenerator.GenerateArtifactId),
                    Case("ArtifactVersionPrefix", "Artifact version IDs start with arv_", Constants.ArtifactVersionIdPrefix, IdGenerator.GenerateArtifactVersionId),
                    Case("NoncePrefix", "Nonce IDs start with non_", Constants.NonceIdPrefix, IdGenerator.GenerateNonceId),

                    new TestCaseDescriptor("Identifier", "AccessKey", "Access keys are PrettyId k-sortable with prefix pub_ at total length 32", async _ =>
                    {
                        await Task.CompletedTask;
                        string key = IdGenerator.GenerateAccessKey();
                        AssertKSortableId(key, Constants.AccessKeyPrefix, "access key");
                    }),
                    new TestCaseDescriptor("Identifier", "SecretKey", "Secret keys are PrettyId k-sortable with prefix key_ at total length 32", async _ =>
                    {
                        await Task.CompletedTask;
                        string key = IdGenerator.GenerateSecretKey();
                        AssertKSortableId(key, Constants.SecretKeyPrefix, "secret key");
                    }),
                    new TestCaseDescriptor("Identifier", "Uniqueness", "Sequential IDs are unique", async _ =>
                    {
                        await Task.CompletedTask;
                        HashSet<string> set = new HashSet<string>();
                        for (int i = 0; i < 1000; i++) set.Add(IdGenerator.GenerateTenantId());
                        Assert2.Equal(1000, set.Count, "1000 tenant ids unique");
                    }),
                    new TestCaseDescriptor("Identifier", "KSortableChronology", "IDs with the same prefix sort chronologically", async _ =>
                    {
                        string first = IdGenerator.GenerateTenantId();
                        await Task.Delay(5);
                        string second = IdGenerator.GenerateTenantId();
                        Assert2.True(string.CompareOrdinal(first, second) < 0, "later tenant id sorts after earlier tenant id");
                    }),
                    new TestCaseDescriptor("Identifier", "AccessKeyCharset", "Access key k-sortable and random segments are alphanumeric", async _ =>
                    {
                        await Task.CompletedTask;
                        AssertKSortableId(IdGenerator.GenerateAccessKey(), Constants.AccessKeyPrefix, "access key charset");
                    })
                });
        }

        private static TestCaseDescriptor Case(string id, string displayName, string expectedPrefix, Func<string> gen)
        {
            return new TestCaseDescriptor("Identifier", id, displayName, async _ =>
            {
                await Task.CompletedTask;
                string value = gen();
                AssertKSortableId(value, expectedPrefix, id);
            });
        }

        private static void AssertKSortableId(string value, string expectedPrefix, string message)
        {
            Assert2.StartsWith(expectedPrefix, value, message + " prefix");
            Assert2.Equal(Constants.IdLength, value.Length, message + " total length");
            string suffix = value.Substring(expectedPrefix.Length);
            string[] parts = suffix.Split('_');
            Assert2.Equal(2, parts.Length, message + " has k-sortable and random segments");
            Assert2.True(parts[0].Length > 0, message + " k-sortable segment present");
            Assert2.True(parts[1].Length > 0, message + " random segment present");
            AssertAlphanumeric(parts[0], message + " k-sortable segment");
            AssertAlphanumeric(parts[1], message + " random segment");
        }

        private static void AssertAlphanumeric(string value, string message)
        {
            foreach (char c in value)
            {
                bool ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                Assert2.True(ok, message + " char '" + c + "' is alphanumeric");
            }
        }
    }
}
