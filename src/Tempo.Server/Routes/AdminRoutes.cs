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

    /// <summary>Administrator CRUD route registrar. Admin-only.</summary>
    public class AdminRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public AdminRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/admins", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create administrator", "Administrators"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/admins", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List administrators", "Administrators"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/admins/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read administrator", "Administrators"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/admins/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update administrator", "Administrators"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/admins/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete administrator", "Administrators"));
        }

        private async Task<bool> RequireAdminAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return false; }
            if (!rc.IsAdmin) { await RouteHelpers.ForbiddenAsync(ctx); return false; }
            return true;
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            Administrator? body = RouteHelpers.Body<Administrator>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "Administrator body required."); return; }
            if (!string.IsNullOrEmpty(body.PasswordSha256) && body.PasswordSha256.Length != 64)
                body.PasswordSha256 = Tempo.Core.Security.PasswordHasher.Hash(body.PasswordSha256);
            Administrator created = await _Host.Database.Administrators.CreateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 201, created);
        }

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            EnumerationFilter filter = new EnumerationFilter
            {
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                IncludeInactive = RouteHelpers.QueryBool(ctx, "includeInactive")
            };
            var result = await _Host.Database.Administrators.EnumerateAsync(filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Administrator? a = await _Host.Database.Administrators.ReadAsync(id);
            if (a == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, a);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Administrator? body = RouteHelpers.Body<Administrator>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            if (!string.IsNullOrEmpty(body.PasswordSha256) && body.PasswordSha256.Length != 64)
                body.PasswordSha256 = Tempo.Core.Security.PasswordHasher.Hash(body.PasswordSha256);
            Administrator updated = await _Host.Database.Administrators.UpdateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Administrator? existing = await _Host.Database.Administrators.ReadAsync(id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Administrator is protected."); return; }
            await _Host.Database.Administrators.DeleteAsync(id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }
    }
}
