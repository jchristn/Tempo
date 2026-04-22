namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Responses;
    using Tempo.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Admin-only routes for enumerating, reading, downloading, and deleting server-visible logs.
    /// </summary>
    public class LogRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public LogRoutes(TempoServer host)
        {
            _Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1.0/logs/sources",
                ListSourcesAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("List log sources", "Logs")
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateArray(OpenApiSchemaCatalog.LogSourceSummary())))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1.0/logs/files",
                ListFilesAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("List log files", "Logs")
                    .WithParameter(OpenApiParameterMetadata.Query("sourceKind", "Log source kind: server or worker.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("sourceId", "Log source identifier.", true, OpenApiSchemaMetadata.String(null)))
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateArray(OpenApiSchemaCatalog.LogFileSummary())))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden()));

            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1.0/logs/files/content",
                ReadFileAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Read log file", "Logs")
                    .WithParameter(OpenApiParameterMetadata.Query("sourceKind", "Log source kind: server or worker.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("sourceId", "Log source identifier.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("path", "Log file path relative to the source root.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("tailLines", "Optional tail line count. Defaults from settings when omitted.", false, OpenApiSchemaMetadata.Integer("int32")))
                    .WithParameter(OpenApiParameterMetadata.Query("maxBytes", "Optional maximum bytes returned. Defaults from settings when omitted.", false, OpenApiSchemaMetadata.Integer("int64")))
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.LogFileRead()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1.0/logs/files/download",
                DownloadFileAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Download log file", "Logs")
                    .WithParameter(OpenApiParameterMetadata.Query("sourceKind", "Log source kind: server or worker.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("sourceId", "Log source identifier.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("path", "Log file path relative to the source root.", true, OpenApiSchemaMetadata.String(null)))
                    .WithResponse(200, OpenApiResponseMetadata.Binary("Complete log file bytes.", "text/plain"))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));

            server.Routes.PostAuthentication.Static.Add(
                HttpMethod.DELETE,
                "/v1.0/logs/files/content",
                DeleteFileAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Delete log file", "Logs")
                    .WithParameter(OpenApiParameterMetadata.Query("sourceKind", "Log source kind: server or worker.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("sourceId", "Log source identifier.", true, OpenApiSchemaMetadata.String(null)))
                    .WithParameter(OpenApiParameterMetadata.Query("path", "Log file path relative to the source root.", true, OpenApiSchemaMetadata.String(null)))
                    .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.LogFileDelete()))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound()));
        }

        private async Task ListSourcesAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;
            await RouteHelpers.JsonAsync(ctx, 200, await _Host.LogFiles.ListSourcesAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task ListFilesAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? sourceKind = RouteHelpers.Query(ctx, "sourceKind");
            string? sourceId = RouteHelpers.Query(ctx, "sourceId");
            if (string.IsNullOrWhiteSpace(sourceKind) || string.IsNullOrWhiteSpace(sourceId))
            {
                await RouteHelpers.BadRequestAsync(ctx, "sourceKind and sourceId are required.").ConfigureAwait(false);
                return;
            }

            try
            {
                await RouteHelpers.JsonAsync(ctx, 200, await _Host.LogFiles.ListFilesAsync(sourceKind, sourceId).ConfigureAwait(false)).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
        }

        private async Task ReadFileAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? sourceKind = RouteHelpers.Query(ctx, "sourceKind");
            string? sourceId = RouteHelpers.Query(ctx, "sourceId");
            string? path = RouteHelpers.Query(ctx, "path");
            if (string.IsNullOrWhiteSpace(sourceKind) || string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(path))
            {
                await RouteHelpers.BadRequestAsync(ctx, "sourceKind, sourceId, and path are required.").ConfigureAwait(false);
                return;
            }

            try
            {
                LogFileReadResponse response = await _Host.LogFiles.ReadAsync(
                    sourceKind,
                    sourceId,
                    path,
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
                await RouteHelpers.NotFoundAsync(ctx, "Log file not found.").ConfigureAwait(false);
            }
        }

        private async Task DownloadFileAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? sourceKind = RouteHelpers.Query(ctx, "sourceKind");
            string? sourceId = RouteHelpers.Query(ctx, "sourceId");
            string? path = RouteHelpers.Query(ctx, "path");
            if (string.IsNullOrWhiteSpace(sourceKind) || string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(path))
            {
                await RouteHelpers.BadRequestAsync(ctx, "sourceKind, sourceId, and path are required.").ConfigureAwait(false);
                return;
            }

            try
            {
                (byte[] bytes, string contentType, string downloadFileName) = await _Host.LogFiles.DownloadAsync(sourceKind, sourceId, path).ConfigureAwait(false);
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
                await RouteHelpers.NotFoundAsync(ctx, "Log file not found.").ConfigureAwait(false);
            }
        }

        private async Task DeleteFileAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx).ConfigureAwait(false)) return;

            string? sourceKind = RouteHelpers.Query(ctx, "sourceKind");
            string? sourceId = RouteHelpers.Query(ctx, "sourceId");
            string? path = RouteHelpers.Query(ctx, "path");
            if (string.IsNullOrWhiteSpace(sourceKind) || string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(path))
            {
                await RouteHelpers.BadRequestAsync(ctx, "sourceKind, sourceId, and path are required.").ConfigureAwait(false);
                return;
            }

            try
            {
                await RouteHelpers.JsonAsync(ctx, 200, await _Host.LogFiles.DeleteAsync(sourceKind, sourceId, path).ConfigureAwait(false)).ConfigureAwait(false);
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
                await RouteHelpers.NotFoundAsync(ctx, "Log file not found.").ConfigureAwait(false);
            }
        }

        private static async Task<bool> RequireAdminAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated)
            {
                await RouteHelpers.UnauthorizedAsync(ctx).ConfigureAwait(false);
                return false;
            }
            if (!rc.IsAdmin)
            {
                await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false);
                return false;
            }
            return true;
        }
    }
}
