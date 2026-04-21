namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using Touchstone.Core;

    public static class RbacSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "RBAC",
                displayName: "Roles, permissions, and authorization evaluation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("RBAC", "AdminBypass", "Global admin is authorized for anything", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, IsAdmin = true };
                            Assert2.True(await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Delete, ct), "permitted");
                            Assert2.Equal(AuthorizationResultEnum.Permitted, rc.AuthorizationResult, "result");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "TenantAdminBypass", "Tenant admin is authorized within tenant scope", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, IsTenantAdmin = true, TenantId = "ten_a", UserId = "usr_a" };
                            Assert2.True(await svc.AuthorizeAsync(rc, ResourceTypeEnum.User, OperationTypeEnum.Create, ct), "tenant admin permitted");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "Unauthenticated", "Unauthenticated requests are denied", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = false };
                            Assert2.False(await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Read, ct), "denied");
                            Assert2.Equal(AuthorizationResultEnum.DeniedImplicit, rc.AuthorizationResult, "implicit");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "ImplicitDeny", "Regular user with no permissions is denied", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t", PasswordSha256 = "x" }, ct);
                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            bool ok = await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Delete, ct);
                            Assert2.False(ok, "denied");
                            Assert2.Equal(AuthorizationResultEnum.DeniedImplicit, rc.AuthorizationResult, "implicit");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "PermitMatch", "Specific permit matches specific operation", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t", PasswordSha256 = "x" }, ct);
                            Role r = await driver.Roles.CreateAsync(new Role { TenantId = t.Id, Name = "reader" }, ct);
                            Permission p = await driver.Permissions.CreateAsync(new Permission
                            {
                                TenantId = t.Id, Name = "read tenants",
                                ResourceTypes = new List<ResourceTypeEnum> { ResourceTypeEnum.Tenant },
                                OperationTypes = new List<OperationTypeEnum> { OperationTypeEnum.Read },
                                PermissionType = PermissionTypeEnum.Permit
                            }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = t.Id, UserId = u.Id, RoleId = r.Id }, ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = t.Id, RoleId = r.Id, PermissionId = p.Id }, ct);

                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            Assert2.True(await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Read, ct), "permitted read");
                            rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            Assert2.False(await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Delete, ct), "denied delete");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "WildcardResource", "Resource wildcard 'All' matches any resource", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t", PasswordSha256 = "x" }, ct);
                            Role r = await driver.Roles.CreateAsync(new Role { TenantId = t.Id, Name = "power" }, ct);
                            Permission p = await driver.Permissions.CreateAsync(new Permission
                            {
                                TenantId = t.Id, Name = "all reads",
                                ResourceTypes = new List<ResourceTypeEnum> { ResourceTypeEnum.All },
                                OperationTypes = new List<OperationTypeEnum> { OperationTypeEnum.Read },
                                PermissionType = PermissionTypeEnum.Permit
                            }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = t.Id, UserId = u.Id, RoleId = r.Id }, ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = t.Id, RoleId = r.Id, PermissionId = p.Id }, ct);

                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            Assert2.True(await svc.AuthorizeAsync(rc, ResourceTypeEnum.User, OperationTypeEnum.Read, ct), "user read");
                            rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            Assert2.True(await svc.AuthorizeAsync(rc, ResourceTypeEnum.DataFlow, OperationTypeEnum.Read, ct), "flow read");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "DenyWins", "Explicit deny wins over permit", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t", PasswordSha256 = "x" }, ct);
                            Role r = await driver.Roles.CreateAsync(new Role { TenantId = t.Id, Name = "mixed" }, ct);
                            Permission permit = await driver.Permissions.CreateAsync(new Permission
                            {
                                TenantId = t.Id, Name = "permit all",
                                ResourceTypes = new List<ResourceTypeEnum> { ResourceTypeEnum.All },
                                OperationTypes = new List<OperationTypeEnum> { OperationTypeEnum.All },
                                PermissionType = PermissionTypeEnum.Permit
                            }, ct);
                            Permission deny = await driver.Permissions.CreateAsync(new Permission
                            {
                                TenantId = t.Id, Name = "deny tenant delete",
                                ResourceTypes = new List<ResourceTypeEnum> { ResourceTypeEnum.Tenant },
                                OperationTypes = new List<OperationTypeEnum> { OperationTypeEnum.Delete },
                                PermissionType = PermissionTypeEnum.Deny
                            }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = t.Id, UserId = u.Id, RoleId = r.Id }, ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = t.Id, RoleId = r.Id, PermissionId = permit.Id }, ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = t.Id, RoleId = r.Id, PermissionId = deny.Id }, ct);

                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            bool ok = await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Delete, ct);
                            Assert2.False(ok, "denied by explicit deny");
                            Assert2.Equal(AuthorizationResultEnum.DeniedExplicit, rc.AuthorizationResult, "explicit");
                            rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            Assert2.True(await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Read, ct), "other ops permitted");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "InactivePermission", "Inactive permissions are ignored", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant t = await driver.Tenants.CreateAsync(new Tenant { Name = "T" }, ct);
                            User u = await driver.Users.CreateAsync(new User { TenantId = t.Id, Email = "u@t", PasswordSha256 = "x" }, ct);
                            Role r = await driver.Roles.CreateAsync(new Role { TenantId = t.Id, Name = "r" }, ct);
                            Permission p = await driver.Permissions.CreateAsync(new Permission
                            {
                                TenantId = t.Id, Name = "read all (inactive)",
                                ResourceTypes = new List<ResourceTypeEnum> { ResourceTypeEnum.All },
                                OperationTypes = new List<OperationTypeEnum> { OperationTypeEnum.All },
                                PermissionType = PermissionTypeEnum.Permit,
                                Active = false
                            }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = t.Id, UserId = u.Id, RoleId = r.Id }, ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = t.Id, RoleId = r.Id, PermissionId = p.Id }, ct);

                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, TenantId = t.Id, UserId = u.Id };
                            Assert2.False(await svc.AuthorizeAsync(rc, ResourceTypeEnum.Tenant, OperationTypeEnum.Read, ct), "inactive permission ignored");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("RBAC", "CanActOnTenantOwn", "Same tenant can act", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            AuthorizationService svc = new AuthorizationService(driver);
                            RequestContext rc = new RequestContext { IsAuthenticated = true, TenantId = "ten_1" };
                            Assert2.True(svc.CanActOnTenant(rc, "ten_1"), "same tenant");
                            Assert2.False(svc.CanActOnTenant(rc, "ten_2"), "cross tenant");
                            RequestContext admin = new RequestContext { IsAuthenticated = true, IsAdmin = true };
                            Assert2.True(svc.CanActOnTenant(admin, "ten_anything"), "admin any");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    })
                });
        }
    }
}
