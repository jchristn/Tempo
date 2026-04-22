namespace Tempo.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
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
            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/v1.0/tenants/{tenantId}/runs/{id}/activity",
                ActivityAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Read run activity", "Runs")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.RunActivityResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/v1.0/tenants/{tenantId}/runs/{id}/logs",
                LogsAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("List run logs", "Runs")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateArray(OpenApiSchemaCatalog.RunLogFileSummary())))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/v1.0/tenants/{tenantId}/runs/{id}/logs/content",
                ReadLogAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Read run log", "Runs")
                    .WithParameter(OpenApiParameterMetadata.Query("path", "Run-log path relative to the run directory.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("tailLines", "Optional tail line count. Defaults from runLogs settings.", false, OpenApiSchemaMetadata.Integer("int32")))
                    .WithParameter(OpenApiParameterMetadata.Query("maxBytes", "Optional maximum bytes returned. Defaults from runLogs settings.", false, OpenApiSchemaMetadata.Integer("int64")))
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.RunLogFileRead()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/v1.0/tenants/{tenantId}/runs/{id}/logs/download",
                DownloadLogAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Download run log", "Runs")
                    .WithParameter(OpenApiParameterMetadata.Query("path", "Run-log path relative to the run directory.", true, OpenApiSchemaMetadata.String(null)))
                    .WithResponse(200, OpenApiResponseMetadata.Binary("Complete run-log file bytes.", "text/plain"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE,
                "/v1.0/tenants/{tenantId}/runs/{id}/logs/content",
                DeleteLogAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Delete run log", "Runs")
                    .WithParameter(OpenApiParameterMetadata.Query("path", "Run-log path relative to the run directory.", true, OpenApiSchemaMetadata.String(null)))
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.RunLogDelete()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(
                HttpMethod.DELETE,
                "/v1.0/tenants/{tenantId}/runs/{id}/logs",
                DeleteLogsAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Delete all run logs", "Runs")
                    .WithResponse(204, OpenApiResponseMetadata.NoContent())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));
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
                await _Host.RunLogs.DeleteRunDirectoryAsync(id).ConfigureAwait(false);
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

        private async Task ActivityAsync(HttpContextBase ctx)
        {
            (_, string? tid) = await AuthAsync(ctx);
            if (tid == null) return;
            FlowRun? run = await ReadRunAsync(ctx, tid).ConfigureAwait(false);
            if (run == null) return;

            List<RunAssignmentRecord> assignments = await _Host.DispatchCoordinator.ListAssignmentsByRunAsync(run.Id).ConfigureAwait(false);
            List<WorkerActivityRecord> activity = await _Host.DispatchCoordinator.ListWorkerActivityByRunAsync(run.Id).ConfigureAwait(false);

            await RouteHelpers.JsonAsync(ctx, 200, new RunActivityResponse
            {
                Run = run,
                Assignments = assignments,
                Activity = activity
            }).ConfigureAwait(false);
        }

        private async Task LogsAsync(HttpContextBase ctx)
        {
            (_, string? tid) = await AuthAsync(ctx);
            if (tid == null) return;
            FlowRun? run = await ReadRunAsync(ctx, tid).ConfigureAwait(false);
            if (run == null) return;

            List<RunLogFileSummaryResponse> files = await _Host.RunLogs.ListFilesAsync(run.Id, IsActive(run)).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, files).ConfigureAwait(false);
        }

        private async Task ReadLogAsync(HttpContextBase ctx)
        {
            (_, string? tid) = await AuthAsync(ctx);
            if (tid == null) return;
            FlowRun? run = await ReadRunAsync(ctx, tid).ConfigureAwait(false);
            if (run == null) return;

            string? path = RouteHelpers.Query(ctx, "path");
            if (string.IsNullOrWhiteSpace(path))
            {
                await RouteHelpers.BadRequestAsync(ctx, "path required.").ConfigureAwait(false);
                return;
            }

            try
            {
                RunLogFileReadResponse response = await _Host.RunLogs.ReadAsync(
                    run.Id,
                    path,
                    IsActive(run),
                    RouteHelpers.QueryInt(ctx, "tailLines", 0) > 0 ? RouteHelpers.QueryInt(ctx, "tailLines", 0) : null,
                    RouteHelpers.Query(ctx, "maxBytes") != null ? (long?)long.Parse(RouteHelpers.Query(ctx, "maxBytes")!) : null).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(ctx, 200, response).ConfigureAwait(false);
            }
            catch (FormatException)
            {
                await RouteHelpers.BadRequestAsync(ctx, "maxBytes must be a valid integer.").ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (System.IO.FileNotFoundException)
            {
                await RouteHelpers.NotFoundAsync(ctx, "Run log file not found.").ConfigureAwait(false);
            }
        }

        private async Task DownloadLogAsync(HttpContextBase ctx)
        {
            (_, string? tid) = await AuthAsync(ctx);
            if (tid == null) return;
            FlowRun? run = await ReadRunAsync(ctx, tid).ConfigureAwait(false);
            if (run == null) return;

            string? path = RouteHelpers.Query(ctx, "path");
            if (string.IsNullOrWhiteSpace(path))
            {
                await RouteHelpers.BadRequestAsync(ctx, "path required.").ConfigureAwait(false);
                return;
            }

            try
            {
                (byte[] bytes, string contentType, string downloadFileName) = await _Host.RunLogs.DownloadAsync(run.Id, path).ConfigureAwait(false);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = contentType;
                ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=\"" + downloadFileName + "\"");
                await ctx.Response.Send(bytes).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (System.IO.FileNotFoundException)
            {
                await RouteHelpers.NotFoundAsync(ctx, "Run log file not found.").ConfigureAwait(false);
            }
        }

        private async Task DeleteLogAsync(HttpContextBase ctx)
        {
            (_, string? tid) = await AuthAsync(ctx);
            if (tid == null) return;
            FlowRun? run = await ReadRunAsync(ctx, tid).ConfigureAwait(false);
            if (run == null) return;

            string? path = RouteHelpers.Query(ctx, "path");
            if (string.IsNullOrWhiteSpace(path))
            {
                await RouteHelpers.BadRequestAsync(ctx, "path required.").ConfigureAwait(false);
                return;
            }

            try
            {
                RunLogDeleteResponse response = await _Host.RunLogs.DeleteFileAsync(run.Id, path, IsActive(run)).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(ctx, 200, response).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (System.IO.FileNotFoundException)
            {
                await RouteHelpers.NotFoundAsync(ctx, "Run log file not found.").ConfigureAwait(false);
            }
        }

        private async Task DeleteLogsAsync(HttpContextBase ctx)
        {
            (_, string? tid) = await AuthAsync(ctx);
            if (tid == null) return;
            FlowRun? run = await ReadRunAsync(ctx, tid).ConfigureAwait(false);
            if (run == null) return;
            if (IsActive(run))
            {
                await RouteHelpers.ErrorAsync(ctx, 409, "RunActive", "Run logs cannot be deleted while the run is active.").ConfigureAwait(false);
                return;
            }

            await _Host.RunLogs.DeleteRunDirectoryAsync(run.Id).ConfigureAwait(false);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send().ConfigureAwait(false);
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
            await _Host.RunLogs.DeleteRunDirectoryAsync(id).ConfigureAwait(false);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task<FlowRun?> ReadRunAsync(HttpContextBase ctx, string tenantId)
        {
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                await RouteHelpers.BadRequestAsync(ctx, "id required.").ConfigureAwait(false);
                return null;
            }

            FlowRun? run = await _Host.Database.FlowRuns.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (run == null)
            {
                await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false);
                return null;
            }

            return run;
        }

        private static bool IsActive(FlowRun run)
        {
            return run.State == FlowRunStateEnum.Queued || run.State == FlowRunStateEnum.Running;
        }
    }
}
