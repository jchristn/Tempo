namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Operator-triggered compatibility migrations. Admin-only.</summary>
    public class MigrationRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public MigrationRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/migrations/inline-rest", MigrateInlineRestAsync, null, openApiMetadata: InlineRestOpenApi());
        }

        private static OpenApiRouteMetadata InlineRestOpenApi()
        {
            return OpenApiRouteMetadata.Create("Migrate legacy inline REST transitions to persisted External.Rest steps", "Migrations")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.InlineRestMigrationRequest(), "Optional migration scope. Omit the body to scan all tenants.", false))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.StepCompatibilityMigrationResult()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
        }

        private async Task<bool> RequireAdminAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return false; }
            if (!rc.IsAdmin) { await RouteHelpers.ForbiddenAsync(ctx); return false; }
            return true;
        }

        private async Task MigrateInlineRestAsync(HttpContextBase ctx)
        {
            if (!await RequireAdminAsync(ctx)) return;

            InlineRestMigrationRequest body = RouteHelpers.Body<InlineRestMigrationRequest>(ctx) ?? new InlineRestMigrationRequest();
            StepCompatibilityMigrator migrator = new StepCompatibilityMigrator(_Host.Database);
            try
            {
                StepCompatibilityMigrationResult result;
                if (!string.IsNullOrWhiteSpace(body.FlowId))
                {
                    if (string.IsNullOrWhiteSpace(body.TenantId)) { await RouteHelpers.BadRequestAsync(ctx, "tenantId is required when flowId is provided."); return; }
                    DataFlowRecord? flow = await _Host.Database.DataFlows.ReadAsync(body.TenantId, body.FlowId).ConfigureAwait(false);
                    if (flow == null) { await RouteHelpers.NotFoundAsync(ctx, "Flow not found."); return; }
                    result = await migrator.MigrateFlowAsync(flow).ConfigureAwait(false);
                }
                else if (!string.IsNullOrWhiteSpace(body.TenantId))
                {
                    result = await migrator.MigrateTenantAsync(body.TenantId).ConfigureAwait(false);
                }
                else
                {
                    result = await migrator.MigrateAllTenantsAsync().ConfigureAwait(false);
                }

                _Host.Logger.Info("[MigrationRoutes] inline REST migration scanned " + result.FlowsScanned + " flows and updated " + result.FlowsUpdated);
                await RouteHelpers.JsonAsync(ctx, 200, result).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.ErrorAsync(ctx, 409, "MigrationConflict", ex.Message).ConfigureAwait(false);
            }
        }

        /// <summary>Optional inline REST migration scope.</summary>
        public class InlineRestMigrationRequest
        {
            /// <summary>Tenant to scan. Omit with flowId omitted to scan all tenants.</summary>
            public string? TenantId { get; set; }

            /// <summary>Single flow to scan. Requires tenantId.</summary>
            public string? FlowId { get; set; }
        }
    }
}
