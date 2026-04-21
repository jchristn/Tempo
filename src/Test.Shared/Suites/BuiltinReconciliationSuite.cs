namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Enums;
    using Touchstone.Core;

    public static class BuiltinReconciliationSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "BuiltinReconciliation",
                displayName: "Built-in step reconciliation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("BuiltinReconciliation", "ClassRowResolves", "Builtin.Unknown class rows reconcile to Builtin.Class", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            StepManager manager = new StepManager();
                            manager.Add(new EchoStep("class_reconcile", tenant.Id, "Class Reconcile", 4321));
                            await CreateUnknownStepAsync(driver, tenant.Id, "class_reconcile", ct);

                            BuiltinStepReconciliationResult result = await new BuiltinStepReconciler(driver, manager).ReconcileTenantAsync(tenant.Id, ct);
                            StepRecord read = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "class_reconcile", ct))!;

                            Assert2.Equal(1, result.Resolved, "resolved count");
                            Assert2.Equal(StepRuntimeKeys.BuiltinClass, read.RuntimeKey, "runtime key");
                            Assert2.Equal(StepRuntimeBindingStateEnum.Resolved, read.RuntimeBindingState, "binding state");
                            Assert2.Equal(typeof(BuiltinClassRuntimeConfig), read.RuntimeConfig!.GetType(), "config type");
                            BuiltinClassRuntimeConfig config = (BuiltinClassRuntimeConfig)read.RuntimeConfig;
                            Assert2.True(!string.IsNullOrWhiteSpace(config.TypeName), "type name");
                            Assert2.True(!string.IsNullOrWhiteSpace(config.SignatureHash), "signature hash");
                            Assert2.Equal(4321, read.MaxRuntimeMs, "max runtime");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("BuiltinReconciliation", "MethodRowResolves", "Builtin.Unknown method rows reconcile to Builtin.Method", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            MethodInfo method = typeof(BuiltinReconciliationSuite).GetMethod(nameof(MethodStep), BindingFlags.Static | BindingFlags.NonPublic)!;
                            StepManager manager = new StepManager();
                            manager.RegisterMethod("method_reconcile", method, tenant.Id, 987);
                            await CreateUnknownStepAsync(driver, tenant.Id, "method_reconcile", ct);

                            BuiltinStepReconciliationResult result = await new BuiltinStepReconciler(driver, manager).ReconcileTenantAsync(tenant.Id, ct);
                            StepRecord read = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "method_reconcile", ct))!;

                            Assert2.Equal(1, result.Resolved, "resolved count");
                            Assert2.Equal(StepRuntimeKeys.BuiltinMethod, read.RuntimeKey, "runtime key");
                            Assert2.Equal(StepRuntimeBindingStateEnum.Resolved, read.RuntimeBindingState, "binding state");
                            Assert2.Equal(typeof(BuiltinMethodRuntimeConfig), read.RuntimeConfig!.GetType(), "config type");
                            BuiltinMethodRuntimeConfig config = (BuiltinMethodRuntimeConfig)read.RuntimeConfig;
                            Assert2.Equal(nameof(MethodStep), config.MethodName!, "method name");
                            Assert2.True(!string.IsNullOrWhiteSpace(config.SignatureHash), "signature hash");
                            Assert2.Equal(987, read.MaxRuntimeMs, "max runtime");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("BuiltinReconciliation", "GlobalMethodFallbackResolves", "Global built-in registrations resolve tenant step rows when no tenant-specific registration exists", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            MethodInfo method = typeof(BuiltinReconciliationSuite).GetMethod(nameof(MethodStep), BindingFlags.Static | BindingFlags.NonPublic)!;
                            StepManager manager = new StepManager();
                            manager.RegisterMethod("global_method", method);
                            await CreateUnknownStepAsync(driver, tenant.Id, "global_method", ct);

                            BuiltinStepReconciliationResult result = await new BuiltinStepReconciler(driver, manager).ReconcileTenantAsync(tenant.Id, ct);
                            StepRecord read = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "global_method", ct))!;

                            Assert2.Equal(1, result.Resolved, "resolved count");
                            Assert2.Equal(StepRuntimeKeys.BuiltinMethod, read.RuntimeKey, "runtime key");
                            Assert2.Equal(StepRuntimeBindingStateEnum.Resolved, read.RuntimeBindingState, "binding state");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("BuiltinReconciliation", "AmbiguousRowIsMarked", "Rows with multiple matching built-in registrations are marked ambiguous", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            MethodInfo method = typeof(BuiltinReconciliationSuite).GetMethod(nameof(MethodStep), BindingFlags.Static | BindingFlags.NonPublic)!;
                            StepManager manager = new StepManager();
                            manager.Add(new EchoStep("ambiguous_step", tenant.Id, "Ambiguous", 0));
                            manager.RegisterMethod("ambiguous_step", method, tenant.Id);
                            await CreateUnknownStepAsync(driver, tenant.Id, "ambiguous_step", ct);

                            BuiltinStepReconciliationResult result = await new BuiltinStepReconciler(driver, manager).ReconcileTenantAsync(tenant.Id, ct);
                            StepRecord read = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "ambiguous_step", ct))!;

                            Assert2.Equal(1, result.Ambiguous, "ambiguous count");
                            Assert2.Equal(StepRuntimeBindingStateEnum.Ambiguous, read.RuntimeBindingState, "binding state");
                            Assert2.Equal(StepRuntimeKeys.BuiltinUnknown, read.RuntimeKey, "runtime stays unknown");
                            Assert2.True(!string.IsNullOrWhiteSpace(read.RuntimeBindingMessage), "diagnostic message");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("BuiltinReconciliation", "MissingRowIsOrphaned", "Rows without matching built-in registrations are marked orphaned", async ct =>
                    {
                        Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tempo.Core.Models.Tenant tenant = await driver.Tenants.CreateAsync(new Tempo.Core.Models.Tenant { Name = "Tenant" }, ct);
                            await CreateUnknownStepAsync(driver, tenant.Id, "missing_step", ct);

                            BuiltinStepReconciliationResult result = await new BuiltinStepReconciler(driver, new StepManager()).ReconcileTenantAsync(tenant.Id, ct);
                            StepRecord read = (await driver.Steps.ReadByExecutionKeyAsync(tenant.Id, "missing_step", ct))!;

                            Assert2.Equal(1, result.Orphaned, "orphaned count");
                            Assert2.Equal(StepRuntimeBindingStateEnum.Orphaned, read.RuntimeBindingState, "binding state");
                            Assert2.Equal(StepRuntimeKeys.BuiltinUnknown, read.RuntimeKey, "runtime stays unknown");
                            Assert2.True(!string.IsNullOrWhiteSpace(read.RuntimeBindingMessage), "diagnostic message");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }

        private static Task<StepRecord> CreateUnknownStepAsync(Tempo.Core.Database.Sqlite.SqliteDatabaseDriver driver, string tenantId, string executionKey, System.Threading.CancellationToken token)
        {
            return driver.Steps.CreateAsync(new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = executionKey,
                Name = executionKey,
                RuntimeKey = StepRuntimeKeys.BuiltinUnknown,
                RuntimeConfig = new BuiltinUnknownRuntimeConfig { Identifier = executionKey },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Unresolved
            }, token);
        }

        private static Task<StepResult> MethodStep(StepRequest request)
        {
            return Task.FromResult(new StepResult
            {
                DataFlowId = request.DataFlowId,
                RequestId = request.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = "method:" + request.Data
            });
        }

        private sealed class EchoStep : Step
        {
            private readonly string _Prefix;

            public EchoStep(string identifier, string tenantId, string name, int maxRuntimeMs)
            {
                Identifier = identifier;
                TenantId = tenantId;
                Name = name;
                MaxRuntimeMs = maxRuntimeMs;
                _Prefix = name;
            }

            public override Task<StepResult> Run(StepRequest req)
            {
                return Task.FromResult(new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Success,
                    Data = _Prefix + ":" + req.Data
                });
            }
        }
    }
}
