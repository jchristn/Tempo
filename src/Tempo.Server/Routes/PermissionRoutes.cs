namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Permission CRUD + role-permission maps.</summary>
    public class PermissionRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public PermissionRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/permissions", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create permission", "Permissions"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/permissions", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List permissions", "Permissions"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/permissions/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read permission", "Permissions"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/permissions/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update permission", "Permissions"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/permissions/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete permission", "Permissions"));

            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/role-permission-maps", CreateRolePermMapAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create role-permission map", "Permissions"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/role-permission-maps/{id}", DeleteRolePermMapAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete role-permission map", "Permissions"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/roles/{roleId}/permissions", EnumerateRolePermissionsAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List permissions for role", "Permissions"));
        }

        private async Task<(RequestContext?, string?)> AuthAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return (null, null); }
            string? tenantId = RouteHelpers.Path(ctx, "tenantId");
            if (string.IsNullOrEmpty(tenantId)) { await RouteHelpers.BadRequestAsync(ctx, "tenantId required."); return (null, null); }
            if (!_Host.Authorization.CanActOnTenant(rc, tenantId)) { await RouteHelpers.ForbiddenAsync(ctx); return (null, null); }
            return (rc, tenantId);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            Permission? body = RouteHelpers.Body<Permission>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.TenantId = tid;
            Permission created = await _Host.Database.Permissions.CreateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 201, created);
        }

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            EnumerationFilter filter = new EnumerationFilter
            {
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                IncludeInactive = RouteHelpers.QueryBool(ctx, "includeInactive")
            };
            var result = await _Host.Database.Permissions.EnumerateAsync(tid, filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Permission? p = await _Host.Database.Permissions.ReadAsync(tid, id);
            if (p == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, p);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Permission? body = RouteHelpers.Body<Permission>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            body.TenantId = tid;
            Permission updated = await _Host.Database.Permissions.UpdateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Permission? existing = await _Host.Database.Permissions.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Permission is protected."); return; }
            await _Host.Database.Permissions.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task CreateRolePermMapAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            RolePermissionMap? body = RouteHelpers.Body<RolePermissionMap>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.TenantId = tid;
            RolePermissionMap created = await _Host.Database.RolePermissionMaps.CreateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 201, created);
        }

        private async Task DeleteRolePermMapAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            RolePermissionMap? existing = await _Host.Database.RolePermissionMaps.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Role-permission mapping is protected."); return; }
            await _Host.Database.RolePermissionMaps.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task EnumerateRolePermissionsAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? roleId = RouteHelpers.Path(ctx, "roleId");
            if (string.IsNullOrEmpty(roleId)) { await RouteHelpers.BadRequestAsync(ctx, "roleId required."); return; }
            var list = await _Host.Database.RolePermissionMaps.EnumerateByRoleAsync(tid, roleId);
            await RouteHelpers.JsonAsync(ctx, 200, list);
        }
    }
}
