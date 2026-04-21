namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Security;
    using Tempo.Core.Settings;
    using TempoStepManager = Tempo.StepManager;

    /// <summary>
    /// Seeds a fresh database with a default account/tenant/admin, and optionally loads
    /// flow/step/trigger definitions from a hydration JSON file.
    /// </summary>
    public class HydrationService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly HydrationSettings _Settings;
        private readonly LoggingModule? _Logging;
        private readonly DefaultRuntimeStepSeeder? _RuntimeStepSeeder;
        private readonly string _Header = "[Hydration] ";

        /// <summary>Default administrator credential displayed after seeding.</summary>
        public Credential? DefaultCredential { get; private set; } = null;

        /// <summary>Default tenant.</summary>
        public Tenant? DefaultTenant { get; private set; } = null;

        /// <summary>Default administrator.</summary>
        public Administrator? DefaultAdministrator { get; private set; } = null;

        /// <summary>Default user.</summary>
        public User? DefaultUser { get; private set; } = null;

        /// <summary>Result of runtime sample step seeding for the most recent hydrate pass.</summary>
        public DefaultRuntimeStepSeedResult? RuntimeStepSeedResult { get; private set; } = null;

        /// <summary>Instantiate.</summary>
        public HydrationService(
            DatabaseDriverBase database,
            HydrationSettings settings,
            LoggingModule? logging = null,
            ArtifactSettings? artifactSettings = null,
            RuntimeSettings? runtimeSettings = null,
            TempoStepManager? stepManager = null,
            IArtifactBlobStore? artifactBlobStore = null,
            RestSettings? restSettings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging;
            if (artifactSettings != null && runtimeSettings != null)
            {
                _RuntimeStepSeeder = new DefaultRuntimeStepSeeder(_Database, artifactSettings, runtimeSettings, stepManager, artifactBlobStore, restSettings);
            }
        }

        /// <summary>
        /// Seed the database if it is empty. Idempotent — running twice is safe.
        /// </summary>
        public async Task HydrateAsync(CancellationToken token = default)
        {
            if (!_Settings.SeedDefaults) return;

            List<Tenant> existingTenants = await _Database.Tenants.AllAsync(token).ConfigureAwait(false);
            bool empty = existingTenants.Count == 0;

            if (empty)
            {
                _Logging?.Info(_Header + "seeding defaults");

                Tenant tenant = new Tenant
                {
                    Name = _Settings.DefaultTenantName,
                    IsProtected = true
                };
                tenant = await _Database.Tenants.CreateAsync(tenant, token).ConfigureAwait(false);
                DefaultTenant = tenant;

                Administrator admin = new Administrator
                {
                    Email = _Settings.DefaultAdminEmail.ToLowerInvariant(),
                    PasswordSha256 = PasswordHasher.Hash(_Settings.DefaultAdminPassword),
                    FirstName = "System",
                    LastName = "Admin",
                    IsProtected = true
                };
                admin = await _Database.Administrators.CreateAsync(admin, token).ConfigureAwait(false);
                DefaultAdministrator = admin;

                User user = new User
                {
                    TenantId = tenant.Id,
                    Email = _Settings.DefaultUserEmail.ToLowerInvariant(),
                    PasswordSha256 = PasswordHasher.Hash(_Settings.DefaultUserPassword),
                    FirstName = "Default",
                    LastName = "User",
                    IsTenantAdmin = true,
                    IsProtected = true
                };
                user = await _Database.Users.CreateAsync(user, token).ConfigureAwait(false);
                DefaultUser = user;

                Credential credential = new Credential
                {
                    TenantId = tenant.Id,
                    UserId = user.Id,
                    Name = "default",
                    IsProtected = true
                };
                credential = await _Database.Credentials.CreateAsync(credential, token).ConfigureAwait(false);
                DefaultCredential = credential;

                await SeedDefaultRbacAsync(tenant.Id, user.Id, token).ConfigureAwait(false);
                await EnsureDefaultRuntimeStepsAsync(tenant.Id, token).ConfigureAwait(false);

                _Logging?.Info(_Header + "seeded tenant " + tenant.Id + ", admin " + admin.Id + ", user " + user.Id);
            }
            else
            {
                // Existing tenants: still seed RBAC defaults per tenant when absent.
                foreach (Tenant t in existingTenants)
                {
                    await EnsureTenantRbacAsync(t.Id, token).ConfigureAwait(false);
                    await EnsureDefaultRuntimeStepsAsync(t.Id, token).ConfigureAwait(false);
                }
            }

            if (!string.IsNullOrWhiteSpace(_Settings.HydrationFile) && File.Exists(_Settings.HydrationFile))
            {
                await ApplyHydrationFileAsync(_Settings.HydrationFile!, token).ConfigureAwait(false);
            }
        }

        private async Task EnsureDefaultRuntimeStepsAsync(string tenantId, CancellationToken token)
        {
            if (_RuntimeStepSeeder == null) return;
            RuntimeStepSeedResult = await _RuntimeStepSeeder.EnsureAsync(tenantId, token).ConfigureAwait(false);
            if (RuntimeStepSeedResult.StepsCreated.Count > 0 ||
                RuntimeStepSeedResult.ArtifactsCreated.Count > 0 ||
                RuntimeStepSeedResult.ArtifactVersionsCreated.Count > 0)
            {
                _Logging?.Info(_Header + "seeded runtime samples for tenant " + tenantId +
                    ": steps=" + RuntimeStepSeedResult.StepsCreated.Count +
                    ", artifacts=" + RuntimeStepSeedResult.ArtifactsCreated.Count +
                    ", versions=" + RuntimeStepSeedResult.ArtifactVersionsCreated.Count);
            }
            foreach (string note in RuntimeStepSeedResult.Notes)
            {
                _Logging?.Warn(LogMessages.WithoutTerminalPeriod(_Header + note));
            }
        }

        private async Task ApplyHydrationFileAsync(string path, CancellationToken token)
        {
            try
            {
                string json = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
                HydrationFile? file = JsonSerializer.Deserialize<HydrationFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (file == null) return;

                if (file.Flows != null)
                {
                    foreach (DataFlowRecord flow in file.Flows)
                    {
                        if (string.IsNullOrEmpty(flow.TenantId) && DefaultTenant != null) flow.TenantId = DefaultTenant.Id;
                        DataFlowRecord? existing = await _Database.DataFlows.ReadAsync(flow.TenantId, flow.Id, token).ConfigureAwait(false);
                        if (existing == null) await _Database.DataFlows.CreateAsync(flow, token).ConfigureAwait(false);
                    }
                }

                if (file.Steps != null)
                {
                    foreach (StepRecord step in file.Steps)
                    {
                        if (string.IsNullOrEmpty(step.TenantId) && DefaultTenant != null) step.TenantId = DefaultTenant.Id;
                        await _Database.Steps.UpsertAsync(step, token).ConfigureAwait(false);
                    }
                }

                if (file.Triggers != null)
                {
                    foreach (TriggerRecord trg in file.Triggers)
                    {
                        if (string.IsNullOrEmpty(trg.TenantId) && DefaultTenant != null) trg.TenantId = DefaultTenant.Id;
                        TriggerRecord? existing = await _Database.Triggers.ReadAsync(trg.TenantId, trg.Id, token).ConfigureAwait(false);
                        if (existing == null) await _Database.Triggers.CreateAsync(trg, token).ConfigureAwait(false);
                    }
                }

                _Logging?.Info(LogMessages.WithoutTerminalPeriod(_Header + "hydration file applied: " + path));
            }
            catch (Exception ex)
            {
                _Logging?.Warn(LogMessages.WithoutTerminalPeriod(_Header + "hydration file error: " + ex.Message));
            }
        }

        /// <summary>Built-in role names. Protected — cannot be deleted.</summary>
        public const string RoleAdministrator = "Administrator";
        /// <summary>Built-in role name for Editor.</summary>
        public const string RoleEditor = "Editor";
        /// <summary>Built-in role name for Operator.</summary>
        public const string RoleOperator = "Operator";
        /// <summary>Built-in role name for ReadOnly.</summary>
        public const string RoleReadOnly = "ReadOnly";

        private async Task SeedDefaultRbacAsync(string tenantId, string firstUserId, CancellationToken token)
        {
            Role admin = await EnsureRoleAsync(tenantId, RoleAdministrator, "Full access to all resources.", token).ConfigureAwait(false);
            Role editor = await EnsureRoleAsync(tenantId, RoleEditor, "Create, read, update, and execute; cannot delete tenants, users, or credentials.", token).ConfigureAwait(false);
            Role operatorRole = await EnsureRoleAsync(tenantId, RoleOperator, "Read, execute, and view runs. Cannot modify definitions.", token).ConfigureAwait(false);
            Role readOnly = await EnsureRoleAsync(tenantId, RoleReadOnly, "Read-only access.", token).ConfigureAwait(false);

            Permission permitAll = await EnsurePermissionAsync(tenantId, "permit all",
                new List<ResourceTypeEnum> { ResourceTypeEnum.All },
                new List<OperationTypeEnum> { OperationTypeEnum.All },
                PermissionTypeEnum.Permit, token).ConfigureAwait(false);

            Permission permitEditor = await EnsurePermissionAsync(tenantId, "editor - create/read/update/execute",
                new List<ResourceTypeEnum> { ResourceTypeEnum.All },
                new List<OperationTypeEnum> { OperationTypeEnum.Create, OperationTypeEnum.Read, OperationTypeEnum.Update, OperationTypeEnum.Execute },
                PermissionTypeEnum.Permit, token).ConfigureAwait(false);

            Permission permitOperator = await EnsurePermissionAsync(tenantId, "operator - read/execute",
                new List<ResourceTypeEnum> { ResourceTypeEnum.All },
                new List<OperationTypeEnum> { OperationTypeEnum.Read, OperationTypeEnum.Execute },
                PermissionTypeEnum.Permit, token).ConfigureAwait(false);

            Permission permitReader = await EnsurePermissionAsync(tenantId, "readonly - read",
                new List<ResourceTypeEnum> { ResourceTypeEnum.All },
                new List<OperationTypeEnum> { OperationTypeEnum.Read },
                PermissionTypeEnum.Permit, token).ConfigureAwait(false);

            await EnsureRolePermissionAsync(tenantId, admin.Id, permitAll.Id, token).ConfigureAwait(false);
            await EnsureRolePermissionAsync(tenantId, editor.Id, permitEditor.Id, token).ConfigureAwait(false);
            await EnsureRolePermissionAsync(tenantId, operatorRole.Id, permitOperator.Id, token).ConfigureAwait(false);
            await EnsureRolePermissionAsync(tenantId, readOnly.Id, permitReader.Id, token).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(firstUserId))
            {
                List<UserRoleMap> existing = await _Database.UserRoleMaps.EnumerateByUserAsync(tenantId, firstUserId, token).ConfigureAwait(false);
                bool hasAdmin = false;
                foreach (UserRoleMap m in existing) if (m.RoleId == admin.Id) { hasAdmin = true; break; }
                if (!hasAdmin)
                {
                    await _Database.UserRoleMaps.CreateAsync(new UserRoleMap
                    {
                        TenantId = tenantId,
                        UserId = firstUserId,
                        RoleId = admin.Id,
                        IsProtected = true
                    }, token).ConfigureAwait(false);
                }
            }
        }

        private async Task EnsureTenantRbacAsync(string tenantId, CancellationToken token)
        {
            List<Role> existing = await _Database.Roles.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (string name in new[] { RoleAdministrator, RoleEditor, RoleOperator, RoleReadOnly })
            {
                bool has = false;
                foreach (Role r in existing) if (r.Name == name) { has = true; break; }
                if (!has)
                {
                    await SeedDefaultRbacAsync(tenantId, firstUserId: string.Empty, token).ConfigureAwait(false);
                    return;
                }
            }
        }

        private async Task<Role> EnsureRoleAsync(string tenantId, string name, string description, CancellationToken token)
        {
            List<Role> roles = await _Database.Roles.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (Role r in roles) if (r.Name == name) return r;
            return await _Database.Roles.CreateAsync(new Role
            {
                TenantId = tenantId,
                Name = name,
                Description = description,
                IsProtected = true
            }, token).ConfigureAwait(false);
        }

        private async Task<Permission> EnsurePermissionAsync(string tenantId, string name, List<ResourceTypeEnum> resources, List<OperationTypeEnum> operations, PermissionTypeEnum type, CancellationToken token)
        {
            List<Permission> existing = await _Database.Permissions.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (Permission p in existing) if (p.Name == name) return p;
            return await _Database.Permissions.CreateAsync(new Permission
            {
                TenantId = tenantId,
                Name = name,
                ResourceTypes = resources,
                OperationTypes = operations,
                PermissionType = type,
                IsProtected = true
            }, token).ConfigureAwait(false);
        }

        private async Task EnsureRolePermissionAsync(string tenantId, string roleId, string permissionId, CancellationToken token)
        {
            List<RolePermissionMap> existing = await _Database.RolePermissionMaps.EnumerateByRoleAsync(tenantId, roleId, token).ConfigureAwait(false);
            foreach (RolePermissionMap m in existing) if (m.PermissionId == permissionId) return;
            await _Database.RolePermissionMaps.CreateAsync(new RolePermissionMap
            {
                TenantId = tenantId,
                RoleId = roleId,
                PermissionId = permissionId,
                IsProtected = true
            }, token).ConfigureAwait(false);
        }

        private sealed class HydrationFile
        {
            public List<DataFlowRecord>? Flows { get; set; } = null;
            public List<StepRecord>? Steps { get; set; } = null;
            public List<TriggerRecord>? Triggers { get; set; } = null;
        }
    }
}
