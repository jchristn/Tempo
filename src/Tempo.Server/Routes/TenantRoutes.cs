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

    /// <summary>Tenant CRUD route registrar.</summary>
    public class TenantRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public TenantRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/tenants", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create tenant", "Tenants"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/tenants", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List tenants", "Tenants"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read tenant", "Tenants"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update tenant", "Tenants"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete tenant", "Tenants"));
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            if (!rc.IsAdmin) { await RouteHelpers.ForbiddenAsync(ctx); return; }
            Tenant? body = RouteHelpers.Body<Tenant>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "Tenant body required."); return; }
            Tenant created = await _Host.Database.Tenants.CreateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 201, created);
        }

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            EnumerationFilter filter = new EnumerationFilter
            {
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                IncludeInactive = RouteHelpers.QueryBool(ctx, "includeInactive")
            };
            var all = await _Host.Database.Tenants.EnumerateAsync(filter);

            if (!rc.IsAdmin)
            {
                if (string.IsNullOrEmpty(rc.TenantId)) { await RouteHelpers.ForbiddenAsync(ctx); return; }
                all.Items = all.Items.FindAll(t => t.Id == rc.TenantId);
                all.TotalCount = all.Items.Count;
            }
            await RouteHelpers.JsonAsync(ctx, 200, all);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            if (!_Host.Authorization.CanActOnTenant(rc, id)) { await RouteHelpers.ForbiddenAsync(ctx); return; }
            Tenant? t = await _Host.Database.Tenants.ReadAsync(id);
            if (t == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, t);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            if (!rc.IsAdmin) { await RouteHelpers.ForbiddenAsync(ctx); return; }
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Tenant? body = RouteHelpers.Body<Tenant>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            Tenant updated = await _Host.Database.Tenants.UpdateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            if (!rc.IsAdmin) { await RouteHelpers.ForbiddenAsync(ctx); return; }
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Tenant? existing = await _Host.Database.Tenants.ReadAsync(id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Tenant is protected."); return; }
            await _Host.Database.Tenants.DeleteAsync(id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }
    }
}
