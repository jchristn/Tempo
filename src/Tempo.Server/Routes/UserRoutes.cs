namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Tenant-scoped user CRUD.</summary>
    public class UserRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public UserRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/users", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create user", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/users", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List users", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/users/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read user", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/users/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update user", "Users"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/users/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete user", "Users"));
        }

        private async Task<(RequestContext? rc, string? tenantId)> AuthAsync(HttpContextBase ctx)
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
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            User? body = RouteHelpers.Body<User>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "User body required."); return; }
            body.TenantId = tenantId;
            if (!string.IsNullOrEmpty(body.PasswordSha256) && body.PasswordSha256.Length != 64)
                body.PasswordSha256 = Tempo.Core.Security.PasswordHasher.Hash(body.PasswordSha256);
            User created = await _Host.Database.Users.CreateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 201, created);
        }

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            EnumerationFilter filter = new EnumerationFilter
            {
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                IncludeInactive = RouteHelpers.QueryBool(ctx, "includeInactive")
            };
            var result = await _Host.Database.Users.EnumerateAsync(tenantId, filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            User? u = await _Host.Database.Users.ReadAsync(tenantId, id);
            if (u == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, u);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            User? body = RouteHelpers.Body<User>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            body.TenantId = tenantId;
            if (!string.IsNullOrEmpty(body.PasswordSha256) && body.PasswordSha256.Length != 64)
                body.PasswordSha256 = Tempo.Core.Security.PasswordHasher.Hash(body.PasswordSha256);
            User updated = await _Host.Database.Users.UpdateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            User? existing = await _Host.Database.Users.ReadAsync(tenantId, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "User is protected."); return; }
            await _Host.Database.Users.DeleteAsync(tenantId, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }
    }
}
