namespace Tempo.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Tempo.Core;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using Tempo.Server.Helpers;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Trigger CRUD plus public HTTP trigger intake.</summary>
    public class TriggerRoutes
    {
        private const int DefaultHttpTriggerWaitMs = 30000;
        private const int MaxHttpTriggerWaitMs = 600000;
        private const int HttpTriggerPollMs = 100;
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public TriggerRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/triggers", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create trigger", "Triggers"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/triggers", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List triggers", "Triggers"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/triggers/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read trigger", "Triggers"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/triggers/{id}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update trigger", "Triggers"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/triggers/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete trigger", "Triggers"));
            server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/triggers/http/{id}", FireHttpTriggerAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Fire an HTTP trigger", "Triggers"));
            server.Routes.PreAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/triggers/http/{id}", FireHttpTriggerAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Fire an HTTP trigger", "Triggers"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/triggers/bulk-delete", BulkDeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete multiple triggers", "Triggers"));
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
                Tempo.Core.Models.TriggerRecord? existing = await _Host.Database.Triggers.ReadAsync(tid, id);
                if (existing == null) continue;
                if (existing.IsProtected) { skipped.Add(id); continue; }
                DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindTriggerReferencesAsync(tid, id).ConfigureAwait(false);
                if (dependencies.IsBlocked) { skippedInUse.Add(id); continue; }
                await _Host.Database.Triggers.DeleteAsync(tid, id);
                deleted++;
            }
            await RouteHelpers.JsonAsync(ctx, 200, new { deletedCount = deleted, skippedProtected = skipped, skippedInUse });
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
            TriggerRecord? body = RouteHelpers.Body<TriggerRecord>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.TenantId = tid;
            TriggerRecord created = await _Host.Database.Triggers.CreateAsync(body);
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
            var result = await _Host.Database.Triggers.EnumerateAsync(tid, filter);
            await RouteHelpers.JsonAsync(ctx, 200, result);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            TriggerRecord? t = await _Host.Database.Triggers.ReadAsync(tid, id);
            if (t == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, t);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            TriggerRecord? body = RouteHelpers.Body<TriggerRecord>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            body.Id = id;
            body.TenantId = tid;
            TriggerRecord updated = await _Host.Database.Triggers.UpdateAsync(body);
            await RouteHelpers.JsonAsync(ctx, 200, updated);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            TriggerRecord? existing = await _Host.Database.Triggers.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Trigger is protected."); return; }
            DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindTriggerReferencesAsync(tid, id).ConfigureAwait(false);
            if (dependencies.IsBlocked) { await RouteHelpers.ErrorAsync(ctx, 409, "InUse", dependencies.ToMessage("Trigger")); return; }
            await _Host.Database.Triggers.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task FireHttpTriggerAsync(HttpContextBase ctx)
        {
            if (RouteHelpers.HasUnsupportedSecretKeyHeader(ctx))
            {
                await RouteHelpers.UnsupportedSecretKeyAsync(ctx).ConfigureAwait(false);
                return;
            }

            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }

            TriggerRecord? trigger = await _Host.Database.Triggers.ReadGlobalAsync(id);
            if (trigger == null) { await RouteHelpers.NotFoundAsync(ctx, "Trigger not found."); return; }
            if (!trigger.Active) { await RouteHelpers.ErrorAsync(ctx, 409, "Inactive", "Trigger is inactive."); return; }
            if (string.IsNullOrEmpty(trigger.DataFlowId)) { await RouteHelpers.ErrorAsync(ctx, 409, "NoFlow", "Trigger has no associated flow."); return; }
            string method = ctx.Request.Method.ToString().ToUpperInvariant();
            if (!IsHttpMethodAllowed(trigger, method, out string allowHeader))
            {
                SetHeader(ctx, "Allow", allowHeader);
                await RouteHelpers.ErrorAsync(ctx, 405, "MethodNotAllowed", "HTTP method " + method + " is not allowed for this trigger.");
                return;
            }

            DataFlowRecord? flow = await _Host.Database.DataFlows.ReadAsync(trigger.TenantId, trigger.DataFlowId!).ConfigureAwait(false);
            if (flow == null) { await RouteHelpers.NotFoundAsync(ctx, "Associated flow not found."); return; }

            RequestContext? invocationContext = await AuthorizeTriggerInvocationAsync(ctx, flow).ConfigureAwait(false);
            if (flow.InvocationAuthMode == DataFlowInvocationAuthModeEnum.ApiAuthenticated && invocationContext == null) return;

            string body = method == "GET" ? string.Empty : ctx.Request.DataAsString ?? string.Empty;
            try
            {
                string? sourceIp = ClientIpResolver.Resolve(ctx);
                string? triggeredByUserId = flow.InvocationAuthMode == DataFlowInvocationAuthModeEnum.ApiAuthenticated ? invocationContext?.UserId : null;
                var run = await _Host.Dispatch.EnqueueAsync(trigger.TenantId, trigger.DataFlowId!, string.IsNullOrEmpty(body) ? null : body, triggeredByUserId, trigger.Id, sourceIp);
                run = await WaitForRunCompletionAsync(run, CalculateWaitMs(flow)).ConfigureAwait(false);

                ApplyRunHeaders(ctx, run);
                await SendRunResponseAsync(ctx, run).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.ErrorAsync(ctx, 400, "CannotEnqueue", ex.Message);
            }
        }

        private async Task<RequestContext?> AuthorizeTriggerInvocationAsync(HttpContextBase ctx, DataFlowRecord flow)
        {
            if (flow.InvocationAuthMode != DataFlowInvocationAuthModeEnum.ApiAuthenticated) return RouteHelpers.Context(ctx);

            RequestContext? rc = await ResolveRequestContextAsync(ctx).ConfigureAwait(false);

            if (rc == null || !rc.IsAuthenticated)
            {
                await RouteHelpers.UnauthorizedAsync(ctx).ConfigureAwait(false);
                return null;
            }

            if (!_Host.Authorization.CanActOnTenant(rc, flow.TenantId))
            {
                await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false);
                return null;
            }

            return rc;
        }

        private async Task<RequestContext?> ResolveRequestContextAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc != null) return rc;

            string? authz = ctx.Request.Headers["Authorization"];
            string? bearerToken = null;
            if (!string.IsNullOrEmpty(authz) && authz.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                bearerToken = authz.Substring(7).Trim();
            }

            rc = await _Host.Authentication.AuthenticateAsync(
                ctx.Request.Headers[Constants.HeaderToken],
                bearerToken,
                ctx.Request.Headers[Constants.HeaderApiKey],
                ctx.Request.Headers[Constants.HeaderAccessKey],
                ctx.Request.Headers[Constants.HeaderTenantId],
                ctx.Request.Headers[Constants.HeaderEmail],
                ctx.Request.Headers[Constants.HeaderPassword],
                containsUnsupportedSecretKeyHeader: !string.IsNullOrWhiteSpace(ctx.Request.Headers[Constants.HeaderSecretKey])).ConfigureAwait(false);
            ctx.Metadata = rc;
            return rc;
        }

        private async Task<FlowRun> WaitForRunCompletionAsync(FlowRun run, int maxWaitMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            FlowRun current = run;
            while (DateTime.UtcNow <= deadline)
            {
                FlowRun? latest = await _Host.Database.FlowRuns.ReadAsync(run.TenantId, run.Id).ConfigureAwait(false);
                if (latest != null) current = latest;
                if (IsTerminal(current.State)) return current;
                await Task.Delay(HttpTriggerPollMs).ConfigureAwait(false);
            }
            return current;
        }

        private static int CalculateWaitMs(DataFlowRecord? flow)
        {
            if (flow?.MaxRuntimeMs > 0) return Math.Clamp(flow.MaxRuntimeMs + 5000, 1000, MaxHttpTriggerWaitMs);
            return DefaultHttpTriggerWaitMs;
        }

        private static bool IsHttpMethodAllowed(TriggerRecord trigger, string method, out string allowHeader)
        {
            List<string> allowed = ReadAllowedHttpMethods(trigger.Configuration);
            allowHeader = string.Join(", ", allowed);
            return allowed.Any(item => string.Equals(item, method, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> ReadAllowedHttpMethods(string? configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration)) return new List<string> { "POST" };

            try
            {
                using JsonDocument doc = JsonDocument.Parse(configuration);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return new List<string> { "POST" };

                if (!root.TryGetProperty("allowedMethods", out JsonElement methods) && !root.TryGetProperty("AllowedMethods", out methods))
                    return new List<string> { "POST" };

                if (methods.ValueKind != JsonValueKind.Array) return new List<string> { "POST" };

                List<string> result = methods.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return result.Count > 0 ? result : new List<string> { "POST" };
            }
            catch (JsonException)
            {
                return new List<string> { "POST" };
            }
        }

        private static bool IsTerminal(FlowRunStateEnum state)
        {
            return state != FlowRunStateEnum.Queued && state != FlowRunStateEnum.Running;
        }

        private static void ApplyRunHeaders(HttpContextBase ctx, FlowRun run)
        {
            SetHeader(ctx, "Access-Control-Expose-Headers", Constants.HeaderRunMetadataExposeList);
            SetHeader(ctx, Constants.HeaderTenantId, run.TenantId);
            SetHeader(ctx, Constants.HeaderRunId, run.Id);
            SetHeader(ctx, Constants.HeaderDataFlowId, run.DataFlowId);
            SetHeader(ctx, Constants.HeaderRunState, run.State.ToString());
            SetHeader(ctx, Constants.HeaderRunCreatedUtc, run.CreatedUtc.ToString("o"));
            SetHeader(ctx, Constants.HeaderRunLastUpdateUtc, run.LastUpdateUtc.ToString("o"));
            if (!string.IsNullOrWhiteSpace(run.AssignedWorkerId)) SetHeader(ctx, Constants.HeaderWorkerId, run.AssignedWorkerId);
            if (!string.IsNullOrWhiteSpace(run.TriggerId)) SetHeader(ctx, Constants.HeaderTriggerId, run.TriggerId);
            if (run.StartedUtc.HasValue) SetHeader(ctx, Constants.HeaderRunStartedUtc, run.StartedUtc.Value.ToString("o"));
            if (run.CompletedUtc.HasValue) SetHeader(ctx, Constants.HeaderRunCompletedUtc, run.CompletedUtc.Value.ToString("o"));
            if (run.StartedUtc.HasValue && run.CompletedUtc.HasValue)
            {
                double runtimeMs = Math.Max(0, (run.CompletedUtc.Value - run.StartedUtc.Value).TotalMilliseconds);
                SetHeader(ctx, Constants.HeaderRuntimeMs, runtimeMs.ToString("0.##", CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(run.ErrorMessage)) SetHeader(ctx, Constants.HeaderRunError, run.ErrorMessage);
        }

        private static void SetHeader(HttpContextBase ctx, string key, string value)
        {
            ctx.Response.Headers.Remove(key);
            ctx.Response.Headers.Add(key, value);
        }

        private static async Task SendRunResponseAsync(HttpContextBase ctx, FlowRun run)
        {
            if (run.State == FlowRunStateEnum.Succeeded)
            {
                await SendJsonPayloadAsync(ctx, 200, run.OutputData).ConfigureAwait(false);
                return;
            }

            if (!IsTerminal(run.State))
            {
                await SendJsonPayloadAsync(ctx, 202, run.OutputData).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(run.OutputData))
            {
                await SendJsonPayloadAsync(ctx, 500, run.OutputData).ConfigureAwait(false);
                return;
            }

            string message = string.IsNullOrWhiteSpace(run.ErrorMessage) ? "Flow run ended with state " + run.State + "." : run.ErrorMessage;
            await RouteHelpers.ErrorAsync(ctx, run.State == FlowRunStateEnum.Cancelled ? 409 : 500, run.State.ToString(), message).ConfigureAwait(false);
        }

        private static async Task SendJsonPayloadAsync(HttpContextBase ctx, int statusCode, string? json)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = RouteHelpers.ContentTypeJson;
            await ctx.Response.Send(string.IsNullOrWhiteSpace(json) ? "null" : json).ConfigureAwait(false);
        }
    }
}
