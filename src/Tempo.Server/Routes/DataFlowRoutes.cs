namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using Tempo.Server.Helpers;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Data flow CRUD and enqueue-run route registrar.</summary>
    public class DataFlowRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public DataFlowRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/flows", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create flow", "Flows"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/flows", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List flows", "Flows"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/flows/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read flow", "Flows"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/flows/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update flow", "Flows"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/flows/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete flow", "Flows"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/flows/{id}/runs", EnqueueRunAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Enqueue flow run", "Flows"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/flows/{id}/ensure-steps", EnsureStepsAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Auto-create any referenced step IDs that do not yet exist", "Flows"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/flows/bulk-delete", BulkDeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete multiple flows by identifier", "Flows"));
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
            DataFlowRecord? body = RouteHelpers.Body<DataFlowRecord>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.TenantId = tid;
            DataFlowRecord created = await _Host.Database.DataFlows.CreateAsync(body);
            await new Tempo.Core.Runtime.StepCompatibilityMigrator(_Host.Database).MigrateFlowAsync(created);
            created = await _Host.Database.DataFlows.ReadAsync(tid, created.Id) ?? created;
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
            var result = await _Host.Database.DataFlows.EnumerateAsync(tid, filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            DataFlowRecord? record = await _Host.Database.DataFlows.ReadAsync(tid, id);
            if (record == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, record);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            DataFlowRecord? body = RouteHelpers.Body<DataFlowRecord>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            body.TenantId = tid;
            DataFlowRecord updated = await _Host.Database.DataFlows.UpdateAsync(body);
            await new Tempo.Core.Runtime.StepCompatibilityMigrator(_Host.Database).MigrateFlowAsync(updated);
            updated = await _Host.Database.DataFlows.ReadAsync(tid, updated.Id) ?? updated;
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            DataFlowRecord? existing = await _Host.Database.DataFlows.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Flow is protected."); return; }
            DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindDataFlowReferencesAsync(tid, id).ConfigureAwait(false);
            if (dependencies.IsBlocked) { await RouteHelpers.ErrorAsync(ctx, 409, "InUse", dependencies.ToMessage("Flow")); return; }
            await _Host.Database.DataFlows.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task EnqueueRunAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            FlowRunRequest? body = RouteHelpers.Body<FlowRunRequest>(ctx);
            string? inputJson = body != null ? FlowDispatchService.SerializeData(body.Data) : null;
            try
            {
                string? sourceIp = ClientIpResolver.Resolve(ctx);
                var run = await _Host.Dispatch.EnqueueAsync(tid, id, inputJson, rc.UserId, null, sourceIp);
                await RouteHelpers.JsonAsync(ctx, 202, run);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.ErrorAsync(ctx, 400, "CannotEnqueue", ex.Message);
            }
        }

        private async Task EnsureStepsAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }

            Tempo.Core.Models.DataFlowRecord? flow = await _Host.Database.DataFlows.ReadAsync(tid, id);
            if (flow == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await new Tempo.Core.Runtime.StepCompatibilityMigrator(_Host.Database).MigrateFlowAsync(flow);
            flow = await _Host.Database.DataFlows.ReadAsync(tid, id) ?? flow;

            System.Collections.Generic.List<Tempo.Core.Models.StepRecord> existing = await _Host.Database.Steps.AllAsync(tid);
            System.Collections.Generic.HashSet<string> existingExecutionKeys = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (Tempo.Core.Models.StepRecord s in existing) existingExecutionKeys.Add(s.ExecutionKey);

            System.Collections.Generic.List<string> created = new System.Collections.Generic.List<string>();
            foreach (System.Collections.Generic.KeyValuePair<string, Tempo.StepTransition> transition in flow.Transitions)
            {
                string executionKey = transition.Key;
                if (transition.Value.StepType == Tempo.Enums.StepTypeEnum.Rest || transition.Value.Rest != null) continue;
                if (!existingExecutionKeys.Contains(executionKey))
                {
                    Tempo.Core.Models.StepRecord rec = new Tempo.Core.Models.StepRecord
                    {
                        TenantId = tid,
                        ExecutionKey = executionKey,
                        Name = string.IsNullOrWhiteSpace(transition.Value.Name) ? executionKey : transition.Value.Name,
                        Description = "Auto-created by flow editor",
                        StepType = Tempo.Core.Enums.PersistedStepTypeEnum.Code,
                        Active = true
                    };
                    await _Host.Database.Steps.CreateAsync(rec);
                    existingExecutionKeys.Add(executionKey);
                    created.Add(executionKey);
                }
            }

            await RouteHelpers.JsonAsync(ctx, 200, new { createdCount = created.Count, created });
        }

        private async Task BulkDeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            Tempo.Core.Requests.IdList? body = RouteHelpers.Body<Tempo.Core.Requests.IdList>(ctx);
            if (body == null || body.Ids == null || body.Ids.Count == 0) { await RouteHelpers.BadRequestAsync(ctx, "ids required."); return; }
            int deleted = 0;
            System.Collections.Generic.List<string> skipped = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<string> skippedInUse = new System.Collections.Generic.List<string>();
            foreach (string id in body.Ids)
            {
                Tempo.Core.Models.DataFlowRecord? existing = await _Host.Database.DataFlows.ReadAsync(tid, id);
                if (existing == null) continue;
                if (existing.IsProtected) { skipped.Add(id); continue; }
                DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindDataFlowReferencesAsync(tid, id).ConfigureAwait(false);
                if (dependencies.IsBlocked) { skippedInUse.Add(id); continue; }
                await _Host.Database.DataFlows.DeleteAsync(tid, id);
                deleted++;
            }
            await RouteHelpers.JsonAsync(ctx, 200, new { deletedCount = deleted, skippedProtected = skipped, skippedInUse });
        }
    }
}
