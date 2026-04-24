namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Touchstone.Core;

    public static class ModelsSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Models",
                displayName: "Model validation and defaults",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Models", "TenantDefaults", "New tenant has auto id and defaults", async _ =>
                    {
                        await Task.CompletedTask;
                        Tenant t = new Tenant();
                        Assert2.StartsWith("ten_", t.Id, "id prefix");
                        Assert2.True(t.Active, "active default true");
                        Assert2.False(t.IsProtected, "not protected default");
                    }),
                    new TestCaseDescriptor("Models", "UserEmailRequired", "User.Email setter rejects empty", async _ =>
                    {
                        await Task.CompletedTask;
                        Assert2.Throws<ArgumentNullException>(() => { User u = new User(); u.Email = ""; }, "empty email");
                        Assert2.Throws<ArgumentNullException>(() => { User u = new User(); u.Email = null!; }, "null email");
                    }),
                    new TestCaseDescriptor("Models", "DataFlowRecordDefaults", "DataFlowRecord has transitions map", async _ =>
                    {
                        await Task.CompletedTask;
                        DataFlowRecord r = new DataFlowRecord();
                        Assert2.NotNull(r.Transitions, "transitions not null");
                        Assert2.Equal(0, r.Transitions.Count, "empty");
                        Assert2.Equal(DataFlowInvocationAuthModeEnum.Public, r.InvocationAuthMode, "public invocation auth default");
                    }),
                    new TestCaseDescriptor("Models", "EnumerationFilterClamps", "Filter clamps paging values", async _ =>
                    {
                        await Task.CompletedTask;
                        EnumerationFilter f = new EnumerationFilter();
                        f.PageNumber = -3;
                        Assert2.Equal(1, f.PageNumber, "page number clamped to 1");
                        f.PageSize = 0;
                        Assert2.Equal(1, f.PageSize, "page size clamped to 1");
                        f.PageSize = 99999;
                        Assert2.Equal(1000, f.PageSize, "page size clamp max");
                    }),
                    new TestCaseDescriptor("Models", "RequestHistoryFilterBucketClamp", "Bucket minutes clamp", async _ =>
                    {
                        await Task.CompletedTask;
                        RequestHistoryFilter f = new RequestHistoryFilter();
                        f.BucketMinutes = 0;
                        Assert2.Equal(1, f.BucketMinutes, "clamp low");
                        f.BucketMinutes = 100000;
                        Assert2.Equal(10080, f.BucketMinutes, "clamp high");
                    }),
                    new TestCaseDescriptor("Models", "PermissionDefaults", "Permission defaults to Permit + All/All", async _ =>
                    {
                        await Task.CompletedTask;
                        Permission p = new Permission();
                        Assert2.Equal(Tempo.Core.Enums.PermissionTypeEnum.Permit, p.PermissionType, "permit default");
                        Assert2.Equal(1, p.ResourceTypes.Count, "one resource default");
                        Assert2.Equal(Tempo.Core.Enums.ResourceTypeEnum.All, p.ResourceTypes[0], "resource=All");
                    })
                });
        }
    }
}
