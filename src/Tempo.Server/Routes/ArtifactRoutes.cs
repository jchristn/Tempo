namespace Tempo.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Tempo.Core;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Artifact metadata and blob upload/download routes.</summary>
    public class ArtifactRoutes
    {
        private readonly TempoServer _Host;

        /// <summary>Instantiate.</summary>
        public ArtifactRoutes(TempoServer host)
        {
            _Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>Register routes.</summary>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/artifacts", CreateAsync, null, openApiMetadata: CreateArtifactOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/artifacts", EnumerateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List artifacts", "Artifacts").WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.Enumeration(OpenApiSchemaCatalog.ArtifactRecord()))));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/artifacts/{id}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read artifact", "Artifacts").WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.ArtifactRecord())).WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/artifacts/{id}", UpdateAsync, null, openApiMetadata: UpdateArtifactOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/artifacts/{id}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete artifact", "Artifacts").WithResponse(204, OpenApiResponseMetadata.NoContent()).WithResponse(403, OpenApiResponseMetadata.Forbidden()).WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/artifacts/{id}/files", EnumerateFilesAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List artifact files", "Artifacts").WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateArray(OpenApiSchemaCatalog.ArtifactFileRecord()))).WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/artifacts/{id}/files/content", ReadFileAsync, null, openApiMetadata: FileContentOpenApi("Read artifact file", false));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/tenants/{tenantId}/artifacts/{id}/files/content", SaveFileAsync, null, openApiMetadata: FileContentOpenApi("Save artifact file", true));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/artifacts/{id}/files/content", DeleteFileAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete artifact file", "Artifacts").WithParameter(OpenApiParameterMetadata.Query("path", "Artifact-relative file path.", true, OpenApiSchemaMetadata.String(null))).WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.ArtifactFileWriteResponse())).WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/tenants/{tenantId}/artifacts/{id}/versions", UploadVersionAsync, null, openApiMetadata: UploadVersionOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/artifacts/{id}/versions", EnumerateVersionsAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List artifact versions", "Artifacts").WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.Enumeration(OpenApiSchemaCatalog.ArtifactVersionRecord()))));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/artifacts/{id}/versions/{version}/download", DownloadVersionAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Download artifact version", "Artifacts").WithResponse(200, OpenApiResponseMetadata.Binary("Artifact package bytes.", "application/octet-stream")).WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/workers/artifacts/{tenantId}/blobs/{sha256}/download", WorkerDownloadVersionAsync, null, openApiMetadata: WorkerDownloadOpenApi());
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/tenants/{tenantId}/artifacts/{id}/versions/{version}", ReadVersionAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read artifact version", "Artifacts").WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.ArtifactVersionRecord())).WithResponse(404, OpenApiResponseMetadata.NotFound()));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/tenants/{tenantId}/artifacts/{id}/versions/{version}", DeleteVersionAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete artifact version", "Artifacts").WithResponse(204, OpenApiResponseMetadata.NoContent()).WithResponse(403, OpenApiResponseMetadata.Forbidden()).WithResponse(404, OpenApiResponseMetadata.NotFound()));
        }

        private static OpenApiRouteMetadata CreateArtifactOpenApi()
        {
            return OpenApiRouteMetadata.Create("Create artifact", "Artifacts")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.ArtifactCreateRequest(), "Artifact create request.", true))
                .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaCatalog.ArtifactRecord()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()));
        }

        private static OpenApiRouteMetadata UpdateArtifactOpenApi()
        {
            return OpenApiRouteMetadata.Create("Update artifact", "Artifacts")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.ArtifactUpdateRequest(), "Artifact update request.", true))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaCatalog.ArtifactRecord()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
        }

        private static OpenApiRouteMetadata UploadVersionOpenApi()
        {
            OpenApiRequestBodyMetadata body = new OpenApiRequestBodyMetadata
            {
                Description = "Raw artifact package bytes. Metadata is supplied through query parameters or headers.",
                Required = true,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["application/octet-stream"] = new OpenApiMediaTypeMetadata { Schema = OpenApiSchemaCatalog.BinaryBody() },
                    ["application/zip"] = new OpenApiMediaTypeMetadata { Schema = OpenApiSchemaCatalog.BinaryBody() }
                }
            };

            return OpenApiRouteMetadata.Create("Upload artifact version", "Artifacts")
                .WithParameter(OpenApiParameterMetadata.Query("version", "Artifact version label.", true, OpenApiSchemaMetadata.String(null)))
                .WithParameter(OpenApiParameterMetadata.Query("sha256", "Optional expected SHA-256 digest. When omitted, the server computes it.", false, OpenApiSchemaMetadata.String(null)))
                .WithParameter(OpenApiParameterMetadata.Query("originalFileName", "Original file name.", false, OpenApiSchemaMetadata.String(null)))
                .WithParameter(OpenApiParameterMetadata.Query("contentType", "Content type.", false, OpenApiSchemaMetadata.String(null)))
                .WithParameter(OpenApiParameterMetadata.Query("manifestJson", "Optional manifest JSON.", false, OpenApiSchemaMetadata.String(null)))
                .WithRequestBody(body)
                .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaCatalog.ArtifactVersionRecord()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
        }

        private static OpenApiRouteMetadata FileContentOpenApi(string summary, bool includeBody)
        {
            OpenApiRouteMetadata metadata = OpenApiRouteMetadata.Create(summary, "Artifacts")
                .WithParameter(OpenApiParameterMetadata.Query("path", "Artifact-relative file path.", true, OpenApiSchemaMetadata.String(null)))
                .WithResponse(200, OpenApiResponseMetadata.Ok(includeBody ? OpenApiSchemaCatalog.ArtifactFileWriteResponse() : OpenApiSchemaCatalog.ArtifactFileRecord()))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest(OpenApiSchemaCatalog.ErrorResponse()))
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
            if (includeBody)
                metadata.WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaCatalog.ArtifactFileWriteRequest(), "Editable artifact file body.", true));
            return metadata;
        }

        private static OpenApiRouteMetadata WorkerDownloadOpenApi()
        {
            return OpenApiRouteMetadata.Create("Download artifact version for a worker assignment", "Workers")
                .WithParameter(OpenApiParameterMetadata.Query("runAssignmentId", "Run-assignment identifier for the active worker lease.", true, OpenApiSchemaMetadata.String(null)))
                .WithParameter(OpenApiParameterMetadata.Query("leaseToken", "Lease token for the active worker assignment.", true, OpenApiSchemaMetadata.String(null)))
                .WithResponse(200, OpenApiResponseMetadata.Binary("Artifact package bytes.", "application/octet-stream"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound());
        }

        private async Task<(RequestContext?, string?)> TenantAuthAsync(HttpContextBase ctx, OperationTypeEnum operation)
        {
            RequestContext? rc = RouteHelpers.Context(ctx);
            if (rc == null || !rc.IsAuthenticated) { await RouteHelpers.UnauthorizedAsync(ctx); return (null, null); }
            string? tenantId = RouteHelpers.Path(ctx, "tenantId");
            if (string.IsNullOrEmpty(tenantId)) { await RouteHelpers.BadRequestAsync(ctx, "tenantId required."); return (null, null); }
            if (!_Host.Authorization.CanActOnTenant(rc, tenantId)) { await RouteHelpers.ForbiddenAsync(ctx); return (null, null); }
            bool authorized = await _Host.Authorization.AuthorizeAsync(rc, ResourceTypeEnum.Artifact, operation).ConfigureAwait(false);
            if (!authorized) { await RouteHelpers.ForbiddenAsync(ctx); return (null, null); }
            return (rc, tenantId);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Create).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactCreateRequest? body = RouteHelpers.Body<ArtifactCreateRequest>(ctx);
            if (body == null || string.IsNullOrWhiteSpace(body.Name)) { await RouteHelpers.BadRequestAsync(ctx, "name required."); return; }
            ArtifactRecord created = await _Host.Database.Artifacts.CreateAsync(new ArtifactRecord
            {
                TenantId = tenantId,
                Name = body.Name,
                Description = body.Description
            }).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 201, created).ConfigureAwait(false);
        }

        private async Task EnumerateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Read).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            EnumerationFilter filter = new EnumerationFilter
            {
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                IncludeInactive = RouteHelpers.QueryBool(ctx, "includeInactive")
            };
            var result = await _Host.Database.Artifacts.EnumerateAsync(tenantId, filter).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Read).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            ArtifactRecord? record = await _Host.Database.Artifacts.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (record == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return; }
            await RouteHelpers.JsonAsync(ctx, 200, record).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Update).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            ArtifactRecord? existing = await _Host.Database.Artifacts.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return; }
            ArtifactUpdateRequest? body = RouteHelpers.Body<ArtifactUpdateRequest>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required."); return; }
            if (!string.IsNullOrWhiteSpace(body.Name)) existing.Name = body.Name;
            existing.Description = body.Description;
            if (body.Active.HasValue) existing.Active = body.Active.Value;
            if (body.IsProtected.HasValue) existing.IsProtected = body.IsProtected.Value;
            ArtifactRecord updated = await _Host.Database.Artifacts.UpdateAsync(existing).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, updated).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Delete).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            string? id = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(id)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            ArtifactRecord? existing = await _Host.Database.Artifacts.ReadAsync(tenantId, id).ConfigureAwait(false);
            if (existing == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return; }
            if (existing.IsProtected) { await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false); return; }
            DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindArtifactReferencesAsync(tenantId, id).ConfigureAwait(false);
            if (dependencies.IsBlocked) { await RouteHelpers.ErrorAsync(ctx, 409, "InUse", dependencies.ToMessage("Artifact")).ConfigureAwait(false); return; }
            await _Host.ArtifactRetention.MarkArtifactDeletedAsync(tenantId, id).ConfigureAwait(false);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send().ConfigureAwait(false);
        }

        private async Task EnumerateFilesAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Read).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactRecord? artifact = await ReadArtifactFromPathAsync(ctx, tenantId).ConfigureAwait(false);
            if (artifact == null) return;
            List<ArtifactFileRecord> files = await _Host.Database.ArtifactFiles.AllAsync(tenantId, artifact.Id).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, files).ConfigureAwait(false);
        }

        private async Task ReadFileAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Read).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactRecord? artifact = await ReadArtifactFromPathAsync(ctx, tenantId).ConfigureAwait(false);
            if (artifact == null) return;
            string? path = ArtifactFilePathQuery(ctx);
            if (string.IsNullOrWhiteSpace(path)) { await RouteHelpers.BadRequestAsync(ctx, "path required.").ConfigureAwait(false); return; }
            ArtifactFileRecord? file;
            try
            {
                file = await _Host.Database.ArtifactFiles.ReadAsync(tenantId, artifact.Id, path).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
                return;
            }
            if (file == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return; }
            await RouteHelpers.JsonAsync(ctx, 200, file).ConfigureAwait(false);
        }

        private async Task SaveFileAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Update).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactRecord? artifact = await ReadArtifactFromPathAsync(ctx, tenantId).ConfigureAwait(false);
            if (artifact == null) return;
            ArtifactFileWriteRequest? body = RouteHelpers.Body<ArtifactFileWriteRequest>(ctx);
            if (body == null) { await RouteHelpers.BadRequestAsync(ctx, "body required.").ConfigureAwait(false); return; }
            string? path = ArtifactFilePathQuery(ctx) ?? body.Path;
            if (string.IsNullOrWhiteSpace(path)) { await RouteHelpers.BadRequestAsync(ctx, "path required.").ConfigureAwait(false); return; }

            try
            {
                ArtifactFileRecord file = ArtifactFileSnapshotService.CreateFileRecord(tenantId, artifact.Id, path, body.Content, body.IsBinary, body.ContentType);
                ArtifactFileRecord saved = await _Host.Database.ArtifactFiles.UpsertAsync(file).ConfigureAwait(false);
                ArtifactFileWriteResponse response = await TrySnapshotAsync(tenantId, artifact.Id).ConfigureAwait(false);
                response.File = saved;
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
        }

        private async Task DeleteFileAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Update).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactRecord? artifact = await ReadArtifactFromPathAsync(ctx, tenantId).ConfigureAwait(false);
            if (artifact == null) return;
            string? path = ArtifactFilePathQuery(ctx);
            if (string.IsNullOrWhiteSpace(path)) { await RouteHelpers.BadRequestAsync(ctx, "path required.").ConfigureAwait(false); return; }
            try
            {
                ArtifactFileRecord? existing = await _Host.Database.ArtifactFiles.ReadAsync(tenantId, artifact.Id, path).ConfigureAwait(false);
                if (existing == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return; }
                await _Host.Database.ArtifactFiles.DeleteAsync(tenantId, artifact.Id, path).ConfigureAwait(false);
                ArtifactFileWriteResponse response = await TrySnapshotAsync(tenantId, artifact.Id).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(ctx, 200, response).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                await RouteHelpers.BadRequestAsync(ctx, ex.Message).ConfigureAwait(false);
            }
        }

        private async Task UploadVersionAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Create).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            string? artifactId = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(artifactId)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            ArtifactRecord? artifact = await _Host.Database.Artifacts.ReadAsync(tenantId, artifactId).ConfigureAwait(false);
            if (artifact == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return; }

            ArtifactVersionUploadMetadata metadata = MetadataFromRequest(ctx);
            if (string.IsNullOrWhiteSpace(metadata.Version)) { await RouteHelpers.BadRequestAsync(ctx, "version required."); return; }

            byte[] body = RouteHelpers.BodyBytes(ctx);
            if (body.Length == 0) { await RouteHelpers.BadRequestAsync(ctx, "artifact body required."); return; }
            if (body.LongLength > _Host.Settings.Artifacts.MaxUploadBytes) { await RouteHelpers.BadRequestAsync(ctx, "artifact body exceeds maximum upload size."); return; }
            string computedSha = ComputeSha256(body);
            string sha = string.IsNullOrWhiteSpace(metadata.Sha256) ? computedSha : metadata.Sha256.Trim().ToLowerInvariant();
            if (!string.Equals(computedSha, sha, StringComparison.Ordinal))
            {
                await RouteHelpers.BadRequestAsync(ctx, "sha256 did not match artifact body.").ConfigureAwait(false);
                return;
            }

            ArtifactManifest? manifest = null;
            try
            {
                manifest = ArtifactManifestService.Parse(metadata.ManifestJson) ?? ArtifactManifestService.ReadFromZip(body);
                if (manifest != null)
                {
                    IReadOnlyList<string> manifestErrors = ArtifactManifestService.Validate(manifest);
                    if (manifestErrors.Count > 0)
                    {
                        await RouteHelpers.BadRequestAsync(ctx, "manifest invalid: " + string.Join("; ", manifestErrors)).ConfigureAwait(false);
                        return;
                    }
                    metadata.ManifestJson = ArtifactManifestService.Serialize(manifest);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.Text.Json.JsonException || ex is InvalidDataException)
            {
                await RouteHelpers.BadRequestAsync(ctx, "manifest invalid: " + ex.Message).ConfigureAwait(false);
                return;
            }

            try
            {
                ArtifactVersionRecord? mutableVersion = null;
                if (manifest != null)
                {
                    try
                    {
                        ArtifactFileSnapshotService fileService = new ArtifactFileSnapshotService(_Host.Database, _Host.ArtifactBlobStore, _Host.Settings.Runtimes.ExternalExecution);
                        mutableVersion = await fileService.ImportZipAndSnapshotAsync(tenantId, artifactId, body).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is InvalidOperationException || ex is System.Text.Json.JsonException)
                    {
                        await RouteHelpers.BadRequestAsync(ctx, "artifact files invalid: " + ex.Message).ConfigureAwait(false);
                        return;
                    }
                }

                if (string.Equals(metadata.Version, Constants.MutableArtifactVersion, StringComparison.OrdinalIgnoreCase) && mutableVersion != null)
                {
                    await RouteHelpers.JsonAsync(ctx, 201, mutableVersion).ConfigureAwait(false);
                    return;
                }

                using MemoryStream input = new MemoryStream(body, writable: false);
                ArtifactBlobWriteResult write = await _Host.ArtifactBlobStore.PutAsync(tenantId, sha, input, body.LongLength).ConfigureAwait(false);
                ArtifactVersionRecord? existingVersion = await _Host.Database.ArtifactVersions.ReadByVersionAsync(tenantId, artifactId, metadata.Version).ConfigureAwait(false);
                ArtifactVersionRecord version = existingVersion ?? new ArtifactVersionRecord
                {
                    TenantId = tenantId,
                    ArtifactId = artifactId,
                    Version = metadata.Version
                };
                version.Sha256 = write.Sha256;
                version.ByteLength = write.ByteLength;
                version.ContentType = metadata.ContentType;
                version.OriginalFileName = metadata.OriginalFileName;
                version.ManifestJson = metadata.ManifestJson;
                version.StorageKey = write.StorageKey;
                version.Active = true;
                version.DeletedUtc = null;
                version.GcEligibleUtc = null;

                ArtifactVersionRecord saved = existingVersion == null
                    ? await _Host.Database.ArtifactVersions.CreateAsync(version).ConfigureAwait(false)
                    : await _Host.Database.ArtifactVersions.UpdateAsync(version).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(ctx, 201, saved).ConfigureAwait(false);
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

        private async Task EnumerateVersionsAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Read).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            string? artifactId = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(artifactId)) { await RouteHelpers.BadRequestAsync(ctx, "id required."); return; }
            EnumerationFilter filter = new EnumerationFilter
            {
                PageNumber = RouteHelpers.QueryInt(ctx, "pageNumber", 1),
                PageSize = RouteHelpers.QueryInt(ctx, "pageSize", 25),
                IncludeInactive = RouteHelpers.QueryBool(ctx, "includeInactive")
            };
            var result = await _Host.Database.ArtifactVersions.EnumerateAsync(tenantId, artifactId, filter).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private async Task ReadVersionAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Read).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactVersionRecord? version = await ReadVersionFromPathAsync(ctx, tenantId).ConfigureAwait(false);
            if (version == null) return;
            await RouteHelpers.JsonAsync(ctx, 200, version).ConfigureAwait(false);
        }

        private async Task DownloadVersionAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Read).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactVersionRecord? version = await ReadVersionFromPathAsync(ctx, tenantId).ConfigureAwait(false);
            if (version == null) return;
            try
            {
                using Stream stream = await _Host.ArtifactBlobStore.OpenReadAsync(tenantId, version.Sha256).ConfigureAwait(false);
                using MemoryStream ms = new MemoryStream();
                await stream.CopyToAsync(ms).ConfigureAwait(false);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = string.IsNullOrWhiteSpace(version.ContentType) ? "application/octet-stream" : version.ContentType;
                if (!string.IsNullOrWhiteSpace(version.OriginalFileName))
                    ctx.Response.Headers.Add("Content-Disposition", "attachment; filename=\"" + SafeFileName(version.OriginalFileName) + "\"");
                await ctx.Response.Send(ms.ToArray()).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await RouteHelpers.NotFoundAsync(ctx, "Artifact blob not found.").ConfigureAwait(false);
            }
        }

        private async Task WorkerDownloadVersionAsync(HttpContextBase ctx)
        {
            string? workerId = ctx.Request.Headers[Constants.HeaderWorkerId];
            string? workerToken = ctx.Request.Headers[Constants.HeaderWorkerToken];
            string? tenantId = RouteHelpers.Path(ctx, "tenantId");
            string? sha256 = RouteHelpers.Path(ctx, "sha256");
            string? runAssignmentId = RouteHelpers.Query(ctx, "runAssignmentId");
            string? leaseToken = RouteHelpers.Query(ctx, "leaseToken");

            if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(workerToken))
            {
                await RouteHelpers.UnauthorizedAsync(ctx).ConfigureAwait(false);
                return;
            }
            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(sha256))
            {
                await RouteHelpers.BadRequestAsync(ctx, "tenantId and sha256 are required.").ConfigureAwait(false);
                return;
            }
            if (string.IsNullOrWhiteSpace(runAssignmentId) || string.IsNullOrWhiteSpace(leaseToken))
            {
                await RouteHelpers.BadRequestAsync(ctx, "runAssignmentId and leaseToken are required.").ConfigureAwait(false);
                return;
            }

            WorkerRecord? worker = await _Host.DispatchCoordinator.AuthenticateWorkerAsync(workerId, workerToken).ConfigureAwait(false);
            if (worker == null)
            {
                await RouteHelpers.UnauthorizedAsync(ctx).ConfigureAwait(false);
                return;
            }

            bool authorized = await _Host.DispatchCoordinator.ValidateWorkerArtifactAccessAsync(
                workerId,
                runAssignmentId,
                leaseToken,
                tenantId,
                sha256).ConfigureAwait(false);

            if (!authorized)
            {
                await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false);
                return;
            }

            try
            {
                using Stream stream = await _Host.ArtifactBlobStore.OpenReadAsync(tenantId, sha256).ConfigureAwait(false);
                using MemoryStream ms = new MemoryStream();
                await stream.CopyToAsync(ms).ConfigureAwait(false);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/octet-stream";
                await ctx.Response.Send(ms.ToArray()).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                await RouteHelpers.NotFoundAsync(ctx, "Artifact blob not found.").ConfigureAwait(false);
            }
        }

        private async Task DeleteVersionAsync(HttpContextBase ctx)
        {
            (RequestContext? rc, string? tenantId) = await TenantAuthAsync(ctx, OperationTypeEnum.Delete).ConfigureAwait(false);
            if (rc == null || tenantId == null) return;
            ArtifactVersionRecord? version = await ReadVersionFromPathAsync(ctx, tenantId).ConfigureAwait(false);
            if (version == null) return;
            if (version.IsProtected) { await RouteHelpers.ForbiddenAsync(ctx).ConfigureAwait(false); return; }
            DeletionDependencyResult dependencies = await _Host.DeleteGuard.FindArtifactVersionReferencesAsync(tenantId, version).ConfigureAwait(false);
            if (dependencies.IsBlocked) { await RouteHelpers.ErrorAsync(ctx, 409, "InUse", dependencies.ToMessage("Artifact version")).ConfigureAwait(false); return; }
            await _Host.ArtifactRetention.MarkVersionDeletedAsync(version).ConfigureAwait(false);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send().ConfigureAwait(false);
        }

        private async Task<ArtifactVersionRecord?> ReadVersionFromPathAsync(HttpContextBase ctx, string tenantId)
        {
            string? artifactId = RouteHelpers.Path(ctx, "id");
            string? versionLabel = RouteHelpers.Path(ctx, "version");
            if (string.IsNullOrEmpty(artifactId)) { await RouteHelpers.BadRequestAsync(ctx, "id required.").ConfigureAwait(false); return null; }
            if (string.IsNullOrEmpty(versionLabel)) { await RouteHelpers.BadRequestAsync(ctx, "version required.").ConfigureAwait(false); return null; }
            ArtifactVersionRecord? version = await _Host.Database.ArtifactVersions.ReadByVersionAsync(tenantId, artifactId, versionLabel).ConfigureAwait(false);
            if (version == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return null; }
            return version;
        }

        private async Task<ArtifactRecord?> ReadArtifactFromPathAsync(HttpContextBase ctx, string tenantId)
        {
            string? artifactId = RouteHelpers.Path(ctx, "id");
            if (string.IsNullOrEmpty(artifactId)) { await RouteHelpers.BadRequestAsync(ctx, "id required.").ConfigureAwait(false); return null; }
            ArtifactRecord? artifact = await _Host.Database.Artifacts.ReadAsync(tenantId, artifactId).ConfigureAwait(false);
            if (artifact == null) { await RouteHelpers.NotFoundAsync(ctx).ConfigureAwait(false); return null; }
            return artifact;
        }

        private async Task<ArtifactFileWriteResponse> TrySnapshotAsync(string tenantId, string artifactId)
        {
            ArtifactFileWriteResponse response = new ArtifactFileWriteResponse();
            try
            {
                ArtifactFileSnapshotService service = new ArtifactFileSnapshotService(_Host.Database, _Host.ArtifactBlobStore, _Host.Settings.Runtimes.ExternalExecution);
                response.ArtifactVersion = await service.SnapshotCurrentAsync(tenantId, artifactId).ConfigureAwait(false);
                response.SnapshotUpdated = true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is InvalidOperationException || ex is System.Text.Json.JsonException || ex is FormatException)
            {
                response.SnapshotError = ex.Message;
            }
            return response;
        }

        private static string? ArtifactFilePathQuery(HttpContextBase ctx)
        {
            string? value = RouteHelpers.Query(ctx, "path");
            if (string.IsNullOrWhiteSpace(value)) return value;
            if (value.Contains('%', StringComparison.Ordinal) || value.Contains('+', StringComparison.Ordinal))
                return Uri.UnescapeDataString(value.Replace("+", "%20"));
            return value;
        }

        private static ArtifactVersionUploadMetadata MetadataFromRequest(HttpContextBase ctx)
        {
            return new ArtifactVersionUploadMetadata
            {
                Version = RouteHelpers.Query(ctx, "version") ?? string.Empty,
                Sha256 = RouteHelpers.Query(ctx, "sha256"),
                ContentType = RouteHelpers.Query(ctx, "contentType") ?? ctx.Request.Headers["Content-Type"],
                OriginalFileName = RouteHelpers.Query(ctx, "originalFileName") ?? ctx.Request.Headers["x-artifact-file-name"],
                ManifestJson = RouteHelpers.Query(ctx, "manifestJson") ?? ctx.Request.Headers["x-artifact-manifest"]
            };
        }

        private static string ComputeSha256(byte[] body)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(body);
            char[] chars = new char[hash.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < hash.Length; i++)
            {
                chars[i * 2] = hex[hash[i] >> 4];
                chars[i * 2 + 1] = hex[hash[i] & 0xF];
            }
            return new string(chars);
        }

        private static string SafeFileName(string fileName)
        {
            string justName = Path.GetFileName(fileName);
            char[] chars = justName.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!ok) chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
