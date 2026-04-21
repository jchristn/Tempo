namespace Tempo.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
    using Tempo.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Runtime catalog and validation route registrar.</summary>
    public class RuntimeRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public RuntimeRoutes(TempoServer host)
        {
            _Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/runtimes", ListServerRuntimesAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List server runtime providers", "Runtimes").WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.RuntimeDescriptorArray())));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/runtimes/{runtimeKey}", ReadServerRuntimeAsync, null, openApiMetadata: ReadRuntimeOpenApi());
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/runtimes/external-execution", ServerExternalExecutionStatusAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read external execution status and capacity pressure", "Runtimes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/runtimes", ListTenantRuntimesAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List tenant runtime providers", "Runtimes").WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.RuntimeDescriptorArray())));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/runtimes/external-execution", TenantExternalExecutionStatusAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read tenant external execution status and capacity pressure", "Runtimes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/runtimes/validate", ValidateRuntimeAsync, null, openApiMetadata: ValidateRuntimeOpenApi());
        }

        private static OpenApiRouteMetadata ReadRuntimeOpenApi()
        {
            return OpenApiRouteMetadata.Create("Read server runtime provider", "Runtimes")
                .WithParameter(OpenApiParameterMetadata.Path("runtimeKey", "Runtime provider key.", OpenApiSchemaCatalog.RuntimeDescriptor().Properties["runtimeKey"]))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.RuntimeDescriptor()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
        }

        private static OpenApiRouteMetadata ValidateRuntimeOpenApi()
        {
            return OpenApiRouteMetadata.Create("Validate runtime configuration", "Runtimes")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.RuntimeValidationRequest(), "Runtime validation request.", true))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.RuntimeValidationResponse()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.RuntimeValidationResponse()))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden());
        }

        private async Task ListServerRuntimesAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, _Host.RuntimeRegistry.DescribeAll());
        }

        private async Task ReadServerRuntimeAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            string? value = RouteHelpers.Path(ctx, "runtimeKey");
            if (string.IsNullOrWhiteSpace(value)) { await RouteHelpers.BadRequestAsync(ctx, "runtimeKey required."); return; }
            RuntimeKey runtimeKey;
            try { runtimeKey = new RuntimeKey(value); }
            catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException || ex is ArgumentOutOfRangeException)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message);
                return;
            }

            IStepRuntimeProvider? provider = _Host.RuntimeRegistry.Get(runtimeKey);
            if (provider == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, provider.Describe());
        }

        private async Task ServerExternalExecutionStatusAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }
            ExternalExecutionStatusResponse response = ExternalExecutionStatusResponse.From(_Host.Settings, _Host.ExternalCapacity.Snapshot());
            await RouteHelpers.JsonAsync(ctx, 200, response);
        }

        private async Task<(RequestContext?, string?)> TenantAuthAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return (null, null); }
            string? tenantId = RouteHelpers.Path(ctx, "tenantId");
            if (string.IsNullOrEmpty(tenantId)) { await RouteHelpers.BadRequestAsync(ctx, "tenantId required."); return (null, null); }
            if (!_Host.Authorization.CanActOnTenant(rc, tenantId)) { await RouteHelpers.ForbiddenAsync(ctx); return (null, null); }
            return (rc, tenantId);
        }

        private async Task ListTenantRuntimesAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            await RouteHelpers.JsonAsync(ctx, 200, _Host.RuntimeRegistry.DescribeAll());
        }

        private async Task TenantExternalExecutionStatusAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx);
            if (rc == null || tenantId == null) return;
            ExternalExecutionStatusResponse response = ExternalExecutionStatusResponse.From(_Host.Settings, _Host.ExternalCapacity.Snapshot(), tenantId);
            await RouteHelpers.JsonAsync(ctx, 200, response);
        }

        private async Task ValidateRuntimeAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx);
            if (rc == null || tenantId == null) return;

            bool authorized = await _Host.Authorization.AuthorizeAsync(rc, ResourceTypeEnum.Step, OperationTypeEnum.Update);
            if (!authorized) { await RouteHelpers.ForbiddenAsync(ctx); return; }

            RuntimeValidationRequest? body = RouteHelpers.Body<RuntimeValidationRequest>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            if (body.RuntimeKey.IsEmpty) { await RouteHelpers.BadRequestAsync(ctx, "runtimeKey required."); return; }
            if (!await AuthorizeArtifactValidationAsync(ctx, rc, body).ConfigureAwait(false)) return;

            StepConfigValidationResult result = await _Host.RuntimeRegistry.ValidateAsync(tenantId, body.RuntimeKey, body.Config);
            await RouteHelpers.JsonAsync(ctx, result.Valid ? 200 : 400, result);
        }

        private async Task<bool> AuthorizeArtifactValidationAsync(HttpContextBase ctx, RequestContext rc, RuntimeValidationRequest body)
        {
            RuntimeKey runtimeKey = body.Config?.RuntimeKey ?? body.RuntimeKey;
            if (runtimeKey != StepRuntimeKeys.ArtifactProcess && runtimeKey != StepRuntimeKeys.ArtifactPython && runtimeKey != StepRuntimeKeys.ArtifactJavaScript && runtimeKey != StepRuntimeKeys.ArtifactDotnetProcess) return true;

            bool authorized = await _Host.Authorization.AuthorizeAsync(rc, ResourceTypeEnum.Artifact, OperationTypeEnum.Read).ConfigureAwait(false);
            if (authorized) return true;
            await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false);
            return false;
        }
    }
}
