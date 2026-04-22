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

    /// <summary>Flow run read + cancel route registrar.</summary>
    public class FlowRunRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public FlowRunRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/runs", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List flow runs", "Runs"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/runs/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read flow run", "Runs"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/runs/{id}/steps", StepsAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List step runs", "Runs"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/runs/{id}/cancel", CancelAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Cancel flow run", "Runs"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/runs/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete flow run", "Runs"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/runs/bulk-delete", BulkDeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete multiple flow runs", "Runs"));
        }

        private async Task BulkDeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            Tempo.Core.Requests.IdList? body = RouteHelpers.Body<Tempo.Core.Requests.IdList>(ctx);
            if (body == null || body.Ids == null || body.Ids.Count == 0) { await RouteHelpers.BadRequestAsync(ctx, "ids required."); return; }
            int deleted = 0;
            foreach (string id in body.Ids)
            {
                Tempo.Core.Models.FlowRun? existing = await _Host.Database.FlowRuns.ReadAsync(tid, id);
                if (existing == null) continue;
                await _Host.Database.FlowRuns.DeleteAsync(tid, id);
                deleted++;
            }
            await RouteHelpers.JsonAsync(ctx, 200, new { deletedCount = deleted });
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

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            FlowRunFilter filter = new FlowRunFilter
            {
                TenantId = tid,
                DataFlowId = RouteHelpers.Query(ctx, "dataFlowId"),
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                FromUtc = RouteHelpers.QueryDateTime(ctx, "fromUtc"),
                ToUtc = RouteHelpers.QueryDateTime(ctx, "toUtc")
            };
            string? s = RouteHelpers.Query(ctx, "state");
            if (!string.IsNullOrEmpty(s) && Enum.TryParse(s, true, out FlowRunStateEnum state)) filter.State = state;

            var result = await _Host.Database.FlowRuns.EnumerateAsync(filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            FlowRun? run = await _Host.Database.FlowRuns.ReadAsync(tid, id);
            if (run == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, run);
        }

        private async Task StepsAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            var steps = await _Host.Database.FlowRuns.EnumerateStepRunsAsync(tid, id);
            await RouteHelpers.JsonAsync(ctx, 200, steps);
        }

        private async Task CancelAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            FlowRun? existing = await _Host.Database.FlowRuns.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.State == FlowRunStateEnum.Queued)
            {
                bool cancelled = await _Host.Dispatch.CancelQueuedAsync(tid, id).ConfigureAwait(false);
                if (cancelled)
                {
                    await RouteHelpers.JsonAsync(ctx, 200, new { cancelled = true });
                    return;
                }
            }

            await RouteHelpers.ErrorAsync(ctx, 409, "CannotCancel", "Run is no longer queued.");
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            FlowRun? existing = await _Host.Database.FlowRuns.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await _Host.Database.FlowRuns.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }
    }
}
