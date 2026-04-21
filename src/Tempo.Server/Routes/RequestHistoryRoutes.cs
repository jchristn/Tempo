namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Requests;
    using Tempo.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Request history read + delete route registrar.</summary>
    public class RequestHistoryRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public RequestHistoryRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/request-history", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List request history", "RequestHistory"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/request-history/summary", SummaryAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Summarize request history", "RequestHistory"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/request-history/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read request history entry", "RequestHistory"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/request-history/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete request history entry", "RequestHistory"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.DELETE, "/v1.0/api/request-history", DeleteManyAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Bulk delete request history", "RequestHistory"));
        }

        private RequestHistoryFilter BuildFilter(HttpContextBase ctx, RequestContext rc)
        {
            RequestHistoryFilter filter = new RequestHistoryFilter
            {
                Method = RouteHelpers.Query(ctx, "method"),
                PathContains = RouteHelpers.Query(ctx, "pathContains"),
                FromUtc = RouteHelpers.QueryDateTime(ctx, "fromUtc"),
                ToUtc = RouteHelpers.QueryDateTime(ctx, "toUtc"),
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                BucketMinutes = RouteHelpers.QueryInt(ctx, "bucketMinutes", 15)
            };

            string? sc = RouteHelpers.Query(ctx, "statusCode");
            if (!string.IsNullOrEmpty(sc) && int.TryParse(sc, out int code)) filter.StatusCode = code;

            if (rc.IsAdmin)
            {
                filter.TenantId = RouteHelpers.Query(ctx, "tenantId");
                filter.UserId = RouteHelpers.Query(ctx, "userId");
            }
            else
            {
                filter.TenantId = rc.TenantId;
                string? userId = RouteHelpers.Query(ctx, "userId");
                filter.UserId = string.IsNullOrEmpty(userId) ? rc.UserId : userId;
            }

            return filter;
        }

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            RequestHistoryFilter filter = BuildFilter(ctx, rc);
            var result = await _Host.Database.RequestHistory.EnumerateAsync(filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task SummaryAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            RequestHistoryFilter filter = BuildFilter(ctx, rc);
            if (!filter.FromUtc.HasValue || !filter.ToUtc.HasValue)
            {
                filter.ToUtc = DateTime.UtcNow;
                filter.FromUtc = DateTime.UtcNow.AddDays(-1);
            }
            var summary = await _Host.Database.RequestHistory.SummarizeAsync(filter);
            await RouteHelpers.JsonAsync(ctx, 200, summary);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            string? tenantScope = rc.IsAdmin ? RouteHelpers.Query(ctx, "tenantId") : rc.TenantId;
            var entry = await _Host.Database.RequestHistory.ReadAsync(tenantScope, id);
            if (entry == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, entry);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            string? tenantScope = rc.IsAdmin ? null : rc.TenantId;
            await _Host.Database.RequestHistory.DeleteAsync(tenantScope, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task DeleteManyAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            RequestHistoryFilter filter = BuildFilter(ctx, rc);
            int n = await _Host.Database.RequestHistory.DeleteManyAsync(filter);
            await RouteHelpers.JsonAsync(ctx, 200, new { deletedCount = n });
        }
    }
}
