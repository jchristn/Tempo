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

    /// <summary>Role CRUD + user-role mapping.</summary>
    public class RoleRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public RoleRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/roles", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create role", "Roles"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/roles", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List roles", "Roles"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/roles/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read role", "Roles"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/roles/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update role", "Roles"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/roles/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete role", "Roles"));

            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/user-role-maps", CreateUserRoleMapAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create user-role map", "Roles"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/user-role-maps/{id}", DeleteUserRoleMapAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete user-role map", "Roles"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/users/{userId}/roles", EnumerateUserRolesAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List roles assigned to user", "Roles"));
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
            Role? body = RouteHelpers.Body<Role>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.TenantId = tid;
            Role created = await _Host.Database.Roles.CreateAsync(body);
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
            var result = await _Host.Database.Roles.EnumerateAsync(tid, filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Role? r = await _Host.Database.Roles.ReadAsync(tid, id);
            if (r == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, r);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Role? body = RouteHelpers.Body<Role>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            body.TenantId = tid;
            Role updated = await _Host.Database.Roles.UpdateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Role? existing = await _Host.Database.Roles.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Role is protected."); return; }
            await _Host.Database.Roles.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task CreateUserRoleMapAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            UserRoleMap? body = RouteHelpers.Body<UserRoleMap>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.TenantId = tid;
            UserRoleMap created = await _Host.Database.UserRoleMaps.CreateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 201, created);
        }

        private async Task DeleteUserRoleMapAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            UserRoleMap? existing = await _Host.Database.UserRoleMaps.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "User-role mapping is protected."); return; }
            await _Host.Database.UserRoleMaps.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task EnumerateUserRolesAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? userId = RouteHelpers.Path(ctx, "userId");
            if (string.IsNullOrEmpty(userId)) { await RouteHelpers.BadRequestAsync(ctx, "userId required."); return; }
            var list = await _Host.Database.UserRoleMaps.EnumerateByUserAsync(tid, userId);
            await RouteHelpers.JsonAsync(ctx, 200, list);
        }
    }
}
