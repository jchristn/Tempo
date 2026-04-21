namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Helpers;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Credential CRUD.</summary>
    public class CredentialRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public CredentialRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/credentials", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create credential", "Credentials"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/credentials", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List credentials", "Credentials"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/credentials/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read credential", "Credentials"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/credentials/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update credential", "Credentials"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/credentials/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete credential", "Credentials"));
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
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            Credential? body = RouteHelpers.Body<Credential>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.TenantId = tenantId;
            if (string.IsNullOrEmpty(body.AccessKey)) body.AccessKey = IdGenerator.GenerateAccessKey();
            if (string.IsNullOrEmpty(body.SecretKey)) body.SecretKey = IdGenerator.GenerateSecretKey();
            if (string.IsNullOrEmpty(body.UserId) && !string.IsNullOrEmpty(rc.UserId)) body.UserId = rc.UserId;
            Credential created = await _Host.Database.Credentials.CreateAsync(body);
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
            var result = await _Host.Database.Credentials.EnumerateAsync(tenantId, filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Credential? c = await _Host.Database.Credentials.ReadAsync(tenantId, id);
            if (c == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, c);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Credential? body = RouteHelpers.Body<Credential>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            body.TenantId = tenantId;
            Credential updated = await _Host.Database.Credentials.UpdateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await AuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Credential? existing = await _Host.Database.Credentials.ReadAsync(tenantId, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Credential is protected."); return; }
            await _Host.Database.Credentials.DeleteAsync(tenantId, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }
    }
}
