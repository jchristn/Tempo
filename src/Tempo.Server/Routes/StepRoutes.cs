namespace Tempo.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Step CRUD plus live attribute-step enumeration.</summary>
    public class StepRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public StepRoutes(TempoServer host) { _Host = host ?? throw new ArgumentNullException(nameof(host)); }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/steps", CreateAsync, null, openApiMetadata: CreateStepOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/steps/source", CreateFromSourceAsync, null, openApiMetadata: CreateSourceStepOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/steps", EnumerateAsync, null, openApiMetadata: ListStepsOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/steps/{id}", ReadAsync, null, openApiMetadata: ReadStepOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/steps/{id}", UpdateAsync, null, openApiMetadata: UpdateStepOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/steps/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete step", "Steps").WithResponse(204, OpenApiResponseMetadata.NoContent()));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/steps/registered", RegisteredAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List steps registered in the running process", "Steps"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/steps/bulk-delete", BulkDeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete multiple steps", "Steps"));
        }

        private static OpenApiRouteMetadata CreateStepOpenApi()
        {
            return OpenApiRouteMetadata.Create("Create step", "Steps")
                .WithDescription("Create a persisted step using a typed runtimeConfig discriminator payload.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.StepCreateRequest(), "Step create request.", true))
                .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaCatalog.StepResponse()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden());
        }

        private static OpenApiRouteMetadata CreateSourceStepOpenApi()
        {
            return OpenApiRouteMetadata.Create("Create source step", "Steps")
                .WithDescription("Package pasted Python, JavaScript, or C# source into an artifact and create an artifact-backed step.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.SourceStepCreateRequest(), "Source step create request.", true))
                .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaCatalog.SourceStepCreateResponse()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden());
        }

        private static OpenApiRouteMetadata ListStepsOpenApi()
        {
            return OpenApiRouteMetadata.Create("List steps", "Steps")
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.Enumeration(OpenApiSchemaCatalog.StepResponse())));
        }

        private static OpenApiRouteMetadata ReadStepOpenApi()
        {
            return OpenApiRouteMetadata.Create("Read step", "Steps")
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.StepResponse()))
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
        }

        private static OpenApiRouteMetadata UpdateStepOpenApi()
        {
            return OpenApiRouteMetadata.Create("Update step", "Steps")
                .WithDescription("Update a persisted step using a typed runtimeConfig discriminator payload.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.StepUpdateRequest(), "Step update request.", true))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.StepResponse()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
        }

        private async Task BulkDeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx, OperationTypeEnum.Delete);
            if (rc == null || tid == null) return;
            Tempo.Core.Requests.IdList? body = RouteHelpers.Body<Tempo.Core.Requests.IdList>(ctx);
            if (body == null || body.Ids == null || body.Ids.Count == 0) { await RouteHelpers.BadRequestAsync(ctx, "ids required."); return; }
            int deleted = 0;
            System.Collections.Generic.List<string> skipped = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<string> skippedInUse = new System.Collections.Generic.List<string>();
            foreach (string id in body.Ids)
            {
                Tempo.Core.Models.StepRecord? existing = await _Host.Database.Steps.ReadAsync(tid, id);
                if (existing == null) continue;
                if (existing.IsProtected) { skipped.Add(id); continue; }
                DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindStepReferencesAsync(tid, existing.ExecutionKey).ConfigureAwait(false);
                if (dependencies.IsBlocked) { skippedInUse.Add(id); continue; }
                await _Host.Database.Steps.DeleteAsync(tid, id);
                deleted++;
            }
            await RouteHelpers.JsonAsync(ctx, 200, new { deletedCount = deleted, skippedProtected = skipped, skippedInUse });
        }

        private async Task<(RequestContext?, string?)> AuthAsync(HttpContextBase ctx, OperationTypeEnum operation)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return (null, null); }
            string? tenantId = RouteHelpers.Path(ctx, "tenantId");
            if (string.IsNullOrEmpty(tenantId)) { await RouteHelpers.BadRequestAsync(ctx, "tenantId required."); return (null, null); }
            if (!_Host.Authorization.CanActOnTenant(rc, tenantId)) { await RouteHelpers.ForbiddenAsync(ctx); return (null, null); }
            bool authorized = await _Host.Authorization.AuthorizeAsync(rc, ResourceTypeEnum.Step, operation).ConfigureAwait(false);
            if (!authorized) { await RouteHelpers.ForbiddenAsync(ctx); return (null, null); }
            return (rc, tenantId);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx, OperationTypeEnum.Create);
            if (rc == null || tid == null) return;
            StepCreateRequest? body = RouteHelpers.Body<StepCreateRequest>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            IReadOnlyList<string> errors = body.Validate();
            if (errors.Count > 0) { await RouteHelpers.BadRequestAsync(ctx, string.Join("; ", errors)); return; }

            StepRecord record = body.ToRecord(tid);
            if (!await AuthorizeArtifactReferenceAsync(ctx, rc, record).ConfigureAwait(false)) return;
            if (!await ValidateRuntimeAsync(ctx, tid, record).ConfigureAwait(false)) return;

            StepRecord created = await _Host.Database.Steps.CreateAsync(record);
            await RouteHelpers.JsonAsync(ctx, 201, StepResponse.FromRecord(created));
        }

        private async Task CreateFromSourceAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx, OperationTypeEnum.Create);
            if (rc == null || tid == null) return;
            bool artifactCreate = await _Host.Authorization.AuthorizeAsync(rc, ResourceTypeEnum.Artifact, OperationTypeEnum.Create).ConfigureAwait(false);
            if (!artifactCreate) { await RouteHelpers.ForbiddenAsync(ctx); return; }

            SourceStepCreateRequest? body = RouteHelpers.Body<SourceStepCreateRequest>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            IReadOnlyList<string> errors = body.Validate();
            if (errors.Count > 0) { await RouteHelpers.BadRequestAsync(ctx, string.Join("; ", errors)); return; }
            RuntimeKey runtimeKey = RuntimeKeyForSourceLanguage(body.NormalizedLanguage);
            IStepRuntimeProvider? provider = _Host.RuntimeRegistry.Get(runtimeKey);
            StepRuntimeDescriptor? descriptor = provider?.Describe();
            if (descriptor == null || descriptor.Availability != StepRuntimeAvailabilityStateEnum.Available)
            {
                string reason = descriptor == null
                    ? "Runtime '" + runtimeKey + "' is not registered."
                    : "Runtime '" + runtimeKey + "' is not available: " + descriptor.Availability + ". " + descriptor.SecurityNotes;
                await RouteHelpers.BadRequestAsync(ctx, reason).ConfigureAwait(false);
                return;
            }

            try
            {
                SourceStepPackageService service = new SourceStepPackageService(_Host.Database, _Host.ArtifactBlobStore, _Host.Settings.Runtimes.ExternalExecution);
                SourceStepCreateResponse created = await service.CreateAsync(tid, body).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(ctx, 201, created).ConfigureAwait(false);
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

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx, OperationTypeEnum.Read);
            if (rc == null || tid == null) return;
            EnumerationFilter filter = new EnumerationFilter
            {
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                IncludeInactive = RouteHelpers.QueryBool(ctx, "includeInactive")
            };
            var result = await _Host.Database.Steps.EnumerateAsync(tid, filter);
            await RouteHelpers.JsonAsync(ctx, 200, StepListResponse.FromRecords(result));
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx, OperationTypeEnum.Read);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            StepRecord? s = await _Host.Database.Steps.ReadAsync(tid, id);
            if (s == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            await RouteHelpers.JsonAsync(ctx, 200, StepResponse.FromRecord(s));
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx, OperationTypeEnum.Update);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            StepRecord? existing = await _Host.Database.Steps.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }

            StepUpdateRequest? body = RouteHelpers.Body<StepUpdateRequest>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            IReadOnlyList<string> errors = body.Validate(existing);
            if (errors.Count > 0) { await RouteHelpers.BadRequestAsync(ctx, string.Join("; ", errors)); return; }

            StepRecord record = body.ApplyTo(existing);
            if (!await AuthorizeArtifactReferenceAsync(ctx, rc, record).ConfigureAwait(false)) return;
            if (!await ValidateRuntimeAsync(ctx, tid, record).ConfigureAwait(false)) return;

            StepRecord updated = await _Host.Database.Steps.UpdateAsync(record);
            await RouteHelpers.JsonAsync(ctx, 200, StepResponse.FromRecord(updated));
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tid) = await AuthAsync(ctx, OperationTypeEnum.Delete);
            if (rc == null || tid == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            StepRecord? existing = await _Host.Database.Steps.ReadAsync(tid, id);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx); return; }
            if (existing.IsProtected) { await RouteHelpers.ErrorAsync(ctx, 409, "Protected", "Step is protected."); return; }
            DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindStepReferencesAsync(tid, existing.ExecutionKey).ConfigureAwait(false);
            if (dependencies.IsBlocked) { await RouteHelpers.ErrorAsync(ctx, 409, "InUse", dependencies.ToMessage("Step")); return; }
            await _Host.Database.Steps.DeleteAsync(tid, id);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send();
        }

        private async Task<bool> AuthorizeArtifactReferenceAsync(HttpContextBase ctx, RequestContext rc, StepRecord body)
        {
            RuntimeKey key = body.RuntimeConfig?.RuntimeKey ?? body.RuntimeKey;
            if (key != StepRuntimeKeys.ArtifactProcess && key != StepRuntimeKeys.ArtifactPython && key != StepRuntimeKeys.ArtifactJavaScript && key != StepRuntimeKeys.ArtifactDotnetProcess) return true;
            bool ok = await _Host.Authorization.AuthorizeAsync(rc, ResourceTypeEnum.Artifact, OperationTypeEnum.Read).ConfigureAwait(false);
            if (!ok)
            {
                await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false);
                return false;
            }
            return true;
        }

        private static RuntimeKey RuntimeKeyForSourceLanguage(SourceStepLanguage language)
        {
            return language switch
            {
                SourceStepLanguage.Python => StepRuntimeKeys.ArtifactPython,
                SourceStepLanguage.JavaScript => StepRuntimeKeys.ArtifactJavaScript,
                SourceStepLanguage.CSharp => StepRuntimeKeys.ArtifactDotnetProcess,
                _ => StepRuntimeKeys.ArtifactProcess
            };
        }

        private async Task<bool> ValidateRuntimeAsync(HttpContextBase ctx, string tenantId, StepRecord body)
        {
            StepConfigValidationResult result = await _Host.RuntimeRegistry.ValidateAsync(tenantId, body.RuntimeKey, body.RuntimeConfig).ConfigureAwait(false);
            if (result.Valid) return true;
            await RouteHelpers.BadRequestAsync(ctx, string.Join("; ", result.Errors)).ConfigureAwait(false);
            return false;
        }

        private async Task RegisteredAsync(HttpContextBase ctx)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return; }

            List<object> names = new List<object>();
            try
            {
                foreach (Tempo.BuiltinStepRegistration registration in _Host.StepManager.Registrations())
                {
                    names.Add(new
                    {
                        identifier = registration.ExecutionKey,
                        tenantId = registration.TenantId,
                        name = string.IsNullOrWhiteSpace(registration.DisplayName) ? registration.ExecutionKey : registration.DisplayName,
                        source = registration.SourceKind.ToString().ToLowerInvariant(),
                        declaringType = registration.DeclaringType,
                        methodName = registration.MethodName,
                        signatureHash = registration.SignatureHash
                    });
                }
            }
            catch { /* ignore */ }
            await RouteHelpers.JsonAsync(ctx, 200, names);
        }
    }
}
