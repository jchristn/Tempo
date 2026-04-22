namespace Tempo.McpServer.Tools
{
    /// <summary>
    /// JSON schema factories for Tempo MCP tools.
    /// </summary>
    public static class ToolSchemas
    {
        /// <summary>Empty object schema.</summary>
        /// <returns>Schema.</returns>
        public static object Empty()
        {
            return new
            {
                type = "object",
                properties = new { },
                required = new string[] { }
            };
        }

        /// <summary>Tenant-scoped paged list schema.</summary>
        /// <returns>Schema.</returns>
        public static object TenantPagedList()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    pageNumber = new { type = "integer", description = "Page number, starting at 1." },
                    pageSize = new { type = "integer", description = "Page size." },
                    includeInactive = new { type = "boolean", description = "Include inactive records." }
                },
                required = new string[] { }
            };
        }

        /// <summary>Tenant-scoped read schema.</summary>
        /// <returns>Schema.</returns>
        public static object TenantRead()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    id = new { type = "string", description = "Record identifier." }
                },
                required = new[] { "id" }
            };
        }

        /// <summary>Tenant-scoped update schema.</summary>
        /// <returns>Schema.</returns>
        public static object TenantBody()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    body = new { type = "object", description = "JSON request body." }
                },
                required = new[] { "body" }
            };
        }

        /// <summary>Tenant-scoped body with identifier schema.</summary>
        /// <returns>Schema.</returns>
        public static object TenantBodyWithId()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    id = new { type = "string", description = "Record identifier." },
                    body = new { type = "object", description = "JSON request body." }
                },
                required = new[] { "id", "body" }
            };
        }

        /// <summary>Artifact file read schema.</summary>
        /// <returns>Schema.</returns>
        public static object ArtifactFileRead()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    artifactId = new { type = "string", description = "Artifact identifier." },
                    path = new { type = "string", description = "Artifact-relative file path." }
                },
                required = new[] { "artifactId", "path" }
            };
        }

        /// <summary>Artifact file save schema.</summary>
        /// <returns>Schema.</returns>
        public static object ArtifactFileSave()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    artifactId = new { type = "string", description = "Artifact identifier." },
                    path = new { type = "string", description = "Artifact-relative file path." },
                    content = new { type = "string", description = "File content." },
                    contentType = new { type = "string", description = "Optional content type." }
                },
                required = new[] { "artifactId", "path", "content" }
            };
        }

        /// <summary>HTTP trigger fire schema.</summary>
        /// <returns>Schema.</returns>
        public static object TriggerFire()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    triggerId = new { type = "string", description = "HTTP trigger identifier." },
                    body = new { description = "JSON request body to send to the trigger." }
                },
                required = new[] { "triggerId" }
            };
        }

        /// <summary>Source step creation schema.</summary>
        /// <returns>Schema.</returns>
        public static object SourceStepCreate()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    tenantId = new { type = "string", description = "Tenant identifier. Uses settings.tempo.defaultTenantId when omitted." },
                    name = new { type = "string", description = "Step name." },
                    language = new { type = "string", description = "Python, JavaScript, or CSharp." },
                    code = new { type = "string", description = "Source code." },
                    function = new { type = "string", description = "Python or JavaScript function name." },
                    handlerType = new { type = "string", description = "C# handler type." },
                    entrypoint = new { type = "string", description = "Entrypoint file or assembly name." },
                    fileName = new { type = "string", description = "Optional source file name." }
                },
                required = new[] { "name", "language", "code" }
            };
        }

        /// <summary>Generic REST request schema.</summary>
        /// <returns>Schema.</returns>
        public static object RestRequest()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    method = new { type = "string", description = "GET, POST, PUT, or DELETE." },
                    path = new { type = "string", description = "Relative Tempo API path. Must be / or start with /v1.0/." },
                    body = new { description = "Optional JSON request body for POST and PUT." }
                },
                required = new[] { "method", "path" }
            };
        }

        /// <summary>Log source list schema.</summary>
        /// <returns>Schema.</returns>
        public static object LogFiles()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    sourceKind = new { type = "string", description = "Log source kind: server or worker." },
                    sourceId = new { type = "string", description = "Log source identifier." }
                },
                required = new[] { "sourceKind", "sourceId" }
            };
        }

        /// <summary>Bounded log file read schema.</summary>
        /// <returns>Schema.</returns>
        public static object LogFileRead()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    sourceKind = new { type = "string", description = "Log source kind: server or worker." },
                    sourceId = new { type = "string", description = "Log source identifier." },
                    path = new { type = "string", description = "Log file path relative to the source root." },
                    tailLines = new { type = "integer", description = "Optional tail line count for bounded reads." },
                    maxBytes = new { type = "integer", description = "Optional maximum UTF-8 bytes returned for bounded reads." }
                },
                required = new[] { "sourceKind", "sourceId", "path" }
            };
        }
    }
}
