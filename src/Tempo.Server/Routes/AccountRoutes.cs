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

    /// <summary>Account CRUD route registrar. Admin-only.</summary>
    public class AccountRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public AccountRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/accounts", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create account", "Accounts"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/accounts", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List accounts", "Accounts"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/accounts/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read account", "Accounts"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/accounts/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update account", "Accounts"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/accounts/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete account", "Accounts"));
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
            Account? body = RouteHelpers.Body<Account>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "Account body required."); return; }
            Account created = await _Host.Database.Accounts.CreateAsync(body, System.Threading.CancellationToken.None);
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
            var result = await _Host.Database.Accounts.EnumerateAsync(filter, System.Threading.CancellationToken.None);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Account? account = await _Host.Database.Accounts.ReadAsync(id, System.Threading.CancellationToken.None);
            if (account == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, account);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Account? body = RouteHelpers.Body<Account>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "Account body required."); return; }
            body.Id = id;
            Account updated = await _Host.Database.Accounts.UpdateAsync(body, System.Threading.CancellationToken.None);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            Account? existing = await _Host.Database.Accounts.ReadAsync(id, System.Threading.CancellationToken.None);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Account is protected."); return; }
            await _Host.Database.Accounts.DeleteAsync(id, System.Threading.CancellationToken.None);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }
    }
}
