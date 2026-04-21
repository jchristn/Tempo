namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Suites;
    using Touchstone.Core;

    /// <summary>
    /// Master registry of Tempo test suites consumed by the CLI/xUnit/NUnit runners.
    /// </summary>
    public static class TempoSuites
    {
        /// <summary>Every suite in a stable order.</summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    IdentifierSuite.Build(),
                    SecuritySuite.Build(),
                    SettingsSuite.Build(),
                    ModelsSuite.Build(),
                    SchemaSuite.Build(),
                    AccountsSuite.Build(),
                    TenantsSuite.Build(),
                    UsersSuite.Build(),
                    CredentialsSuite.Build(),
                    RbacSuite.Build(),
                    StepIdentitySuite.Build(),
                    RuntimeConfigSuite.Build(),
                    RuntimeRegistrySuite.Build(),
                    ExternalRuntimeCapacitySuite.Build(),
                    StepResolverSuite.Build(),
                    BuiltinReconciliationSuite.Build(),
                    InlineRestMigrationSuite.Build(),
                    ArtifactSuite.Build(),
                    ArtifactProcessSuite.Build(),
                    ProtocolSuite.Build(),
                    DataFlowSuite.Build(),
                    DeletionProtectionSuite.Build(),
                    RequestHistorySuite.Build(),
                    HydrationSuite.Build()
                };
            }
        }
    }
}
