namespace Tempo.McpServer.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.McpServer.Services;
    using Voltaic;

    /// <summary>
    /// Registers Tempo tools on Voltaic MCP transports.
    /// </summary>
    public static class TempoToolRegistrar
    {
        /// <summary>Register tools on an HTTP MCP server.</summary>
        /// <param name="server">HTTP MCP server.</param>
        /// <param name="client">Tempo API client.</param>
        public static void Register(McpHttpServer server, TempoApiClient client)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            foreach (TempoToolDefinition tool in CreateDefinitions(client))
            {
                server.RegisterTool(tool.Name, tool.Description, tool.InputSchema, args => Invoke(tool, args));
            }
        }

        /// <summary>Register methods on a TCP MCP server.</summary>
        /// <param name="server">TCP MCP server.</param>
        /// <param name="client">Tempo API client.</param>
        public static void Register(McpTcpServer server, TempoApiClient client)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            foreach (TempoToolDefinition tool in CreateDefinitions(client))
            {
                server.RegisterMethod(tool.Name, args => Invoke(tool, args));
            }
        }

        /// <summary>Register methods on a WebSocket MCP server.</summary>
        /// <param name="server">WebSocket MCP server.</param>
        /// <param name="client">Tempo API client.</param>
        public static void Register(McpWebsocketsServer server, TempoApiClient client)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            foreach (TempoToolDefinition tool in CreateDefinitions(client))
            {
                server.RegisterMethod(tool.Name, args => Invoke(tool, args));
            }
        }

        private static object Invoke(TempoToolDefinition tool, JsonElement? args)
        {
            return tool.Handler(args, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static IReadOnlyList<TempoToolDefinition> CreateDefinitions(TempoApiClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            List<TempoToolDefinition> tools = new List<TempoToolDefinition>();

            tools.Add(new TempoToolDefinition(
                "tempo_health",
                "Check Tempo.Server health",
                ToolSchemas.Empty(),
                async (args, token) => await client.GetAsync("/v1.0/api/health", token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "tempo_me",
                "Read the authenticated Tempo principal",
                ToolSchemas.Empty(),
                async (args, token) => await client.GetAsync("/v1.0/me", token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "tempo_settings_meta",
                "Read editable Tempo settings metadata",
                ToolSchemas.Empty(),
                async (args, token) => await client.GetAsync("/v1.0/settings/meta", token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "tenant_list",
                "List Tempo tenants",
                PagedListWithoutTenantSchema(),
                async (args, token) => await client.GetAsync(PagedPath("/v1.0/tenants", args), token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "tenant_get",
                "Read a Tempo tenant by identifier",
                IdOnlySchema(),
                async (args, token) => await client.GetAsync("/v1.0/tenants/" + TempoApiClient.EscapeSegment(JsonArgumentReader.RequiredString(args, "id")), token).ConfigureAwait(false)));

            AddTenantCollectionTools(tools, client, "step", "steps", "Tempo steps");
            AddTenantCollectionTools(tools, client, "flow", "flows", "Tempo data flows");
            AddTenantCollectionTools(tools, client, "trigger", "triggers", "Tempo triggers");
            AddTenantCollectionTools(tools, client, "run", "runs", "Tempo flow runs");
            AddTenantCollectionTools(tools, client, "artifact", "artifacts", "Tempo artifacts");

            tools.Add(new TempoToolDefinition(
                "step_registered",
                "List built-in step registrations in the running Tempo.Server process",
                ToolSchemas.Empty(),
                async (args, token) => await client.GetAsync("/v1.0/steps/registered", token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "step_create_from_source",
                "Create a runnable Python, JavaScript, or C# source step",
                ToolSchemas.SourceStepCreate(),
                async (args, token) => await CreateSourceStepAsync(client, args, token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "flow_enqueue_run",
                "Enqueue a run for a data flow",
                FlowEnqueueSchema(),
                async (args, token) => await EnqueueFlowRunAsync(client, args, token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "run_steps",
                "List step runs for a flow run",
                ToolSchemas.TenantRead(),
                async (args, token) => await client.GetAsync(TenantPath(client, args, "/runs/" + TempoApiClient.EscapeSegment(JsonArgumentReader.RequiredString(args, "id")) + "/steps"), token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "runtime_list",
                "List Tempo runtime providers",
                ToolSchemas.Empty(),
                async (args, token) => await client.GetAsync("/v1.0/runtimes", token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "runtime_get",
                "Read a Tempo runtime provider by runtime key",
                RuntimeGetSchema(),
                async (args, token) => await client.GetAsync("/v1.0/runtimes/" + TempoApiClient.EscapeSegment(JsonArgumentReader.RequiredString(args, "runtimeKey")), token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "runtime_external_execution",
                "Read process-backed runtime capacity and availability",
                ToolSchemas.Empty(),
                async (args, token) => await client.GetAsync("/v1.0/runtimes/external-execution", token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "artifact_files",
                "List mutable files in an artifact",
                ArtifactFilesSchema(),
                async (args, token) => await client.GetAsync(TenantPath(client, args, "/artifacts/" + TempoApiClient.EscapeSegment(JsonArgumentReader.RequiredString(args, "artifactId")) + "/files"), token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "artifact_file_read",
                "Read a mutable artifact file",
                ToolSchemas.ArtifactFileRead(),
                async (args, token) => await ReadArtifactFileAsync(client, args, token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "artifact_file_save",
                "Save a mutable artifact file",
                ToolSchemas.ArtifactFileSave(),
                async (args, token) => await SaveArtifactFileAsync(client, args, token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "trigger_fire",
                "Fire an HTTP trigger and return the response body plus run metadata headers",
                ToolSchemas.TriggerFire(),
                async (args, token) => await FireTriggerAsync(client, args, token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                "tempo_request",
                "Call a Tempo REST endpoint by relative path for API surface not covered by a typed tool",
                ToolSchemas.RestRequest(),
                async (args, token) => await SendGenericRequestAsync(client, args, token).ConfigureAwait(false)));

            return tools;
        }

        private static void AddTenantCollectionTools(List<TempoToolDefinition> tools, TempoApiClient client, string singular, string routeSegment, string description)
        {
            tools.Add(new TempoToolDefinition(
                singular + "_list",
                "List " + description,
                ToolSchemas.TenantPagedList(),
                async (args, token) => await client.GetAsync(PagedTenantPath(client, args, "/" + routeSegment), token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                singular + "_get",
                "Read one " + description + " record",
                ToolSchemas.TenantRead(),
                async (args, token) => await client.GetAsync(TenantPath(client, args, "/" + routeSegment + "/" + TempoApiClient.EscapeSegment(JsonArgumentReader.RequiredString(args, "id"))), token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                singular + "_create",
                "Create one " + description + " record using a JSON body",
                ToolSchemas.TenantBody(),
                async (args, token) => await client.PostAsync(TenantPath(client, args, "/" + routeSegment), JsonArgumentReader.OptionalNode(args, "body"), token).ConfigureAwait(false)));

            tools.Add(new TempoToolDefinition(
                singular + "_update",
                "Update one " + description + " record using a JSON body",
                ToolSchemas.TenantBodyWithId(),
                async (args, token) => await client.PutAsync(TenantPath(client, args, "/" + routeSegment + "/" + TempoApiClient.EscapeSegment(JsonArgumentReader.RequiredString(args, "id"))), JsonArgumentReader.OptionalNode(args, "body"), token).ConfigureAwait(false)));
        }

        private static async Task<object> CreateSourceStepAsync(TempoApiClient client, JsonElement? args, CancellationToken token)
        {
            JsonObject body = new JsonObject
            {
                ["name"] = JsonArgumentReader.RequiredString(args, "name"),
                ["language"] = JsonArgumentReader.RequiredString(args, "language"),
                ["code"] = JsonArgumentReader.RequiredString(args, "code")
            };

            AddOptionalString(body, args, "executionKey");
            AddOptionalString(body, args, "description");
            AddOptionalString(body, args, "function");
            AddOptionalString(body, args, "handlerType");
            AddOptionalString(body, args, "entrypoint");
            AddOptionalString(body, args, "fileName");
            AddOptionalString(body, args, "artifactName");
            AddOptionalString(body, args, "module");

            return await client.PostAsync(TenantPath(client, args, "/steps/source"), body, token).ConfigureAwait(false);
        }

        private static async Task<object> EnqueueFlowRunAsync(TempoApiClient client, JsonElement? args, CancellationToken token)
        {
            string flowId = JsonArgumentReader.RequiredString(args, "flowId");
            JsonNode? body = JsonArgumentReader.OptionalNode(args, "body");
            return await client.PostAsync(TenantPath(client, args, "/flows/" + TempoApiClient.EscapeSegment(flowId) + "/runs"), body, token).ConfigureAwait(false);
        }

        private static async Task<object> ReadArtifactFileAsync(TempoApiClient client, JsonElement? args, CancellationToken token)
        {
            string artifactId = JsonArgumentReader.RequiredString(args, "artifactId");
            string path = JsonArgumentReader.RequiredString(args, "path");
            Dictionary<string, string?> query = new Dictionary<string, string?> { ["path"] = path };
            string requestPath = TempoApiClient.AddQuery(TenantPath(client, args, "/artifacts/" + TempoApiClient.EscapeSegment(artifactId) + "/files/content"), query);
            return await client.GetAsync(requestPath, token).ConfigureAwait(false);
        }

        private static async Task<object> SaveArtifactFileAsync(TempoApiClient client, JsonElement? args, CancellationToken token)
        {
            string artifactId = JsonArgumentReader.RequiredString(args, "artifactId");
            string path = JsonArgumentReader.RequiredString(args, "path");
            Dictionary<string, string?> query = new Dictionary<string, string?> { ["path"] = path };
            JsonObject body = new JsonObject
            {
                ["content"] = JsonArgumentReader.RequiredString(args, "content")
            };
            AddOptionalString(body, args, "contentType");
            string requestPath = TempoApiClient.AddQuery(TenantPath(client, args, "/artifacts/" + TempoApiClient.EscapeSegment(artifactId) + "/files/content"), query);
            return await client.PutAsync(requestPath, body, token).ConfigureAwait(false);
        }

        private static async Task<object> FireTriggerAsync(TempoApiClient client, JsonElement? args, CancellationToken token)
        {
            string triggerId = JsonArgumentReader.RequiredString(args, "triggerId");
            JsonNode? body = JsonArgumentReader.OptionalNode(args, "body");
            return await client.PostAsync("/v1.0/triggers/http/" + TempoApiClient.EscapeSegment(triggerId), body, token).ConfigureAwait(false);
        }

        private static async Task<object> SendGenericRequestAsync(TempoApiClient client, JsonElement? args, CancellationToken token)
        {
            string method = JsonArgumentReader.RequiredString(args, "method").Trim().ToUpperInvariant();
            string path = JsonArgumentReader.RequiredString(args, "path");
            JsonNode? body = JsonArgumentReader.OptionalNode(args, "body");

            if (method == "GET") return await client.GetAsync(path, token).ConfigureAwait(false);
            if (method == "POST") return await client.PostAsync(path, body, token).ConfigureAwait(false);
            if (method == "PUT") return await client.PutAsync(path, body, token).ConfigureAwait(false);
            if (method == "DELETE") return await client.DeleteAsync(path, token).ConfigureAwait(false);

            throw new ArgumentException("method must be GET, POST, PUT, or DELETE");
        }

        private static string PagedPath(string path, JsonElement? args)
        {
            Dictionary<string, string?> query = new Dictionary<string, string?>();
            AddPagingQuery(query, args);
            return TempoApiClient.AddQuery(path, query);
        }

        private static string PagedTenantPath(TempoApiClient client, JsonElement? args, string tenantRelativePath)
        {
            return PagedPath(TenantPath(client, args, tenantRelativePath), args);
        }

        private static string TenantPath(TempoApiClient client, JsonElement? args, string tenantRelativePath)
        {
            string? tenantId = JsonArgumentReader.OptionalString(args, "tenantId");
            if (string.IsNullOrWhiteSpace(tenantId)) tenantId = client.DefaultTenantId;
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("tenantId is required when settings.tempo.defaultTenantId is not configured");
            if (!tenantRelativePath.StartsWith("/", StringComparison.Ordinal)) tenantRelativePath = "/" + tenantRelativePath;
            return "/v1.0/tenants/" + TempoApiClient.EscapeSegment(tenantId) + tenantRelativePath;
        }

        private static void AddPagingQuery(Dictionary<string, string?> query, JsonElement? args)
        {
            query["pageNumber"] = Math.Max(1, JsonArgumentReader.OptionalInt(args, "pageNumber", 1)).ToString();
            query["pageSize"] = Math.Max(1, JsonArgumentReader.OptionalInt(args, "pageSize", 25)).ToString();
            if (JsonArgumentReader.HasProperty(args, "includeInactive"))
                query["includeInactive"] = JsonArgumentReader.OptionalBool(args, "includeInactive", false).ToString().ToLowerInvariant();
        }

        private static void AddOptionalString(JsonObject body, JsonElement? args, string propertyName)
        {
            string? value = JsonArgumentReader.OptionalString(args, propertyName);
            if (!string.IsNullOrWhiteSpace(value)) body[propertyName] = value;
        }

        private static object IdOnlySchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    id = new { type = "string", description = "Record identifier." }
                },
                required = new[] { "id" }
            };
        }

        private static object PagedListWithoutTenantSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    pageNumber = new { type = "integer", description = "Page number, starting at 1." },
                    pageSize = new { type = "integer", description = "Page size." },
                    includeInactive = new { type = "boolean", description = "Include inactive records." }
                },
                required = new string[] { }
            };
        }

        private static object FlowEnqueueSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    flowId = new { type = "string", description = "Flow identifier." },
                    body = new { description = "Optional JSON input body for the flow run." }
                },
                required = new[] { "flowId" }
            };
        }

        private static object RuntimeGetSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    runtimeKey = new { type = "string", description = "Runtime key, for example Builtin.Method or Artifact.PythonProcess." }
                },
                required = new[] { "runtimeKey" }
            };
        }

        private static object ArtifactFilesSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    artifactId = new { type = "string", description = "Artifact identifier." }
                },
                required = new[] { "artifactId" }
            };
        }
    }
}
