namespace Tempo.Server.Routes
{
    using System.Collections.Generic;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
    using Tempo.Core.Workers;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Reusable OpenAPI schema metadata for route bodies that Watson cannot infer from raw context handlers.</summary>
    internal static class OpenApiSchemaCatalog
    {
        private const string BuiltinClassRuntimeConfigSchemaName = "BuiltinClassRuntimeConfig";
        private const string BuiltinMethodRuntimeConfigSchemaName = "BuiltinMethodRuntimeConfig";
        private const string BuiltinUnknownRuntimeConfigSchemaName = "BuiltinUnknownRuntimeConfig";
        private const string ExternalRestRuntimeConfigSchemaName = "ExternalRestRuntimeConfig";
        private const string LegacyInlineRestRuntimeConfigSchemaName = "LegacyInlineRestRuntimeConfig";
        private const string ArtifactProcessRuntimeConfigSchemaName = "ArtifactProcessRuntimeConfig";
        private const string ArtifactPythonRuntimeConfigSchemaName = "ArtifactPythonRuntimeConfig";
        private const string ArtifactJavaScriptRuntimeConfigSchemaName = "ArtifactJavaScriptRuntimeConfig";
        private const string ArtifactDotnetProcessRuntimeConfigSchemaName = "ArtifactDotnetProcessRuntimeConfig";
        private const string HostExecutableRuntimeConfigSchemaName = "HostExecutableRuntimeConfig";

        public static void RegisterSchemas(OpenApiSettings settings)
        {
            if (settings == null) return;
            settings.Schemas[BuiltinClassRuntimeConfigSchemaName] = BuiltinClassRuntimeConfigSchema();
            settings.Schemas[BuiltinMethodRuntimeConfigSchemaName] = BuiltinMethodRuntimeConfigSchema();
            settings.Schemas[BuiltinUnknownRuntimeConfigSchemaName] = BuiltinUnknownRuntimeConfigSchema();
            settings.Schemas[ExternalRestRuntimeConfigSchemaName] = ExternalRestRuntimeConfigSchema();
            settings.Schemas[LegacyInlineRestRuntimeConfigSchemaName] = LegacyInlineRestRuntimeConfigSchema();
            settings.Schemas[ArtifactProcessRuntimeConfigSchemaName] = ArtifactProcessRuntimeConfigSchema();
            settings.Schemas[ArtifactPythonRuntimeConfigSchemaName] = ArtifactPythonRuntimeConfigSchema();
            settings.Schemas[ArtifactJavaScriptRuntimeConfigSchemaName] = ArtifactJavaScriptRuntimeConfigSchema();
            settings.Schemas[ArtifactDotnetProcessRuntimeConfigSchemaName] = ArtifactDotnetProcessRuntimeConfigSchema();
            settings.Schemas[HostExecutableRuntimeConfigSchemaName] = HostExecutableRuntimeConfigSchema();
        }

        public static OpenApiSchemaMetadata ErrorResponse()
        {
            OpenApiSchemaMetadata schema = Object("Standard error response.");
            schema.Properties["code"] = String("Stable error code.", false);
            schema.Properties["message"] = String("Human-readable error message.", false);
            schema.Properties["details"] = String("Optional error details.", true);
            schema.Required.Add("code");
            schema.Required.Add("message");
            return schema;
        }

        public static OpenApiSchemaMetadata DataFlowRecord()
        {
            OpenApiSchemaMetadata schema = DataFlowShape("Persisted data flow definition.");
            schema.Properties["id"] = String("Flow identifier.", false);
            schema.Properties["tenantId"] = String("Tenant identifier.", false);
            schema.Properties["createdUtc"] = String("Creation timestamp.", false, "date-time");
            schema.Properties["lastUpdateUtc"] = String("Last update timestamp.", false, "date-time");
            schema.Required.Add("id");
            schema.Required.Add("tenantId");
            schema.Required.Add("createdUtc");
            schema.Required.Add("lastUpdateUtc");
            return schema;
        }

        public static OpenApiSchemaMetadata DataFlowWriteRequest()
        {
            OpenApiSchemaMetadata schema = DataFlowShape("Create or update a data flow.");
            schema.Example = new
            {
                name = "Echo flow",
                description = "Returns the output from the echo step",
                startStepId = "example.echo",
                invocationAuthMode = "Public",
                maxRuntimeMs = 30000,
                transitions = new Dictionary<string, object>
                {
                    ["example.echo"] = new
                    {
                        name = "Echo",
                        onSuccess = (string?)null,
                        onFailure = (string?)null,
                        onException = (string?)null,
                        maxTransitions = 1
                    }
                },
                active = true
            };
            return schema;
        }

        public static OpenApiSchemaMetadata StepResponse()
        {
            OpenApiSchemaMetadata schema = Object("Persisted step definition response.");
            schema.Properties["id"] = String("Step identifier.", false);
            schema.Properties["tenantId"] = String("Tenant identifier.", false);
            schema.Properties["executionKey"] = String("Tenant-scoped execution key used by flow transitions.", false);
            schema.Properties["name"] = String("Display name.", false);
            schema.Properties["description"] = String("Optional description.", true);
            schema.Properties["runtimeKey"] = RuntimeKeySchema();
            schema.Properties["runtimeConfig"] = RuntimeConfigSchema();
            schema.Properties["contractType"] = ContractTypeSchema();
            schema.Properties["inputSchema"] = String("Optional JSON schema for step input.", true);
            schema.Properties["outputSchema"] = String("Optional JSON schema for step output.", true);
            schema.Properties["validateInput"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["validateOutput"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["artifactId"] = String("Referenced artifact identifier for artifact-backed runtimes.", true);
            schema.Properties["artifactVersion"] = String("Referenced artifact version for artifact-backed runtimes.", true);
            schema.Properties["runtimeBindingState"] = EnumString("Current runtime binding state.", "Unresolved", "Resolved", "Ambiguous", "Orphaned");
            schema.Properties["runtimeBindingMessage"] = String("Optional runtime binding diagnostic.", true);
            schema.Properties["maxRuntimeMs"] = NonNegativeInteger("Maximum runtime in milliseconds. Zero means no step-level override.");
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["isProtected"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["createdUtc"] = String("Creation timestamp.", false, "date-time");
            schema.Properties["lastUpdateUtc"] = String("Last update timestamp.", false, "date-time");
            schema.Required.Add("id");
            schema.Required.Add("tenantId");
            schema.Required.Add("executionKey");
            schema.Required.Add("name");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("runtimeConfig");
            return schema;
        }

        public static OpenApiSchemaMetadata StepCreateRequest()
        {
            OpenApiSchemaMetadata schema = StepWriteRequest("Create a persisted step.");
            schema.Required.Add("name");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("runtimeConfig");
            return schema;
        }

        public static OpenApiSchemaMetadata SourceStepCreateRequest()
        {
            OpenApiSchemaMetadata schema = Object("Create an artifact-backed step from pasted source code.");
            schema.Properties["executionKey"] = String("Tenant-scoped execution key. Defaults to name when omitted.", true);
            schema.Properties["name"] = String("Display name.", false);
            schema.Properties["description"] = String("Optional description.", true);
            schema.Properties["language"] = EnumString("Source language.", "Python", "JavaScript", "CSharp");
            schema.Properties["code"] = String("Complete source file contents.", false);
            schema.Properties["fileName"] = String("Simple file name to store in the generated artifact.", true);
            schema.Properties["artifactName"] = String("Optional artifact display name. Defaults to the step name.", true);
            schema.Properties["version"] = String("Deprecated. Source steps now use the mutable current artifact snapshot.", true);
            schema.Properties["entrypoint"] = String("Manifest entrypoint name.", true);
            schema.Properties["module"] = String("Python module name or JavaScript module path. Defaults from fileName.", true);
            schema.Properties["function"] = String("Python function or JavaScript export to call.", true);
            schema.Properties["handlerType"] = String("C# handler type implementing Tempo.Protocol.ITempoStepHandler or inheriting Tempo.Protocol.TempoStepHandlerBase.", true);
            schema.Properties["contractType"] = ContractTypeSchema();
            schema.Properties["inputSchema"] = String("Optional JSON schema for step input.", true);
            schema.Properties["outputSchema"] = String("Optional JSON schema for step output.", true);
            schema.Properties["validateInput"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["validateOutput"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["maxRuntimeMs"] = NonNegativeInteger("Maximum runtime in milliseconds. Zero means no step-level override.");
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Required.Add("name");
            schema.Required.Add("language");
            schema.Required.Add("code");
            schema.Example = new
            {
                executionKey = "transform_order",
                name = "Transform order",
                language = "JavaScript",
                fileName = "handler.js",
                function = "run",
                code = "exports.run = async (req) => ({ ok: true, input: req.data });",
                contractType = "Loose",
                active = true
            };
            return schema;
        }

        public static OpenApiSchemaMetadata SourceStepCreateResponse()
        {
            OpenApiSchemaMetadata schema = Object("Created source step plus generated artifact metadata.");
            schema.Properties["step"] = StepResponse();
            schema.Properties["artifact"] = ArtifactRecord();
            schema.Properties["artifactVersion"] = ArtifactVersionRecord();
            schema.Required.Add("step");
            schema.Required.Add("artifact");
            schema.Required.Add("artifactVersion");
            return schema;
        }

        public static OpenApiSchemaMetadata StepUpdateRequest()
        {
            return StepWriteRequest("Update a persisted step. Omitted values preserve the existing record where supported by the route.");
        }

        public static OpenApiSchemaMetadata RuntimeValidationRequest()
        {
            OpenApiSchemaMetadata schema = Object("Validate a runtime configuration without saving a step.");
            schema.Properties["runtimeKey"] = RuntimeKeySchema();
            schema.Properties["config"] = RuntimeConfigSchema();
            schema.Required.Add("runtimeKey");
            schema.Required.Add("config");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.ExternalRest.ToString(),
                config = new
                {
                    runtimeKey = StepRuntimeKeys.ExternalRest.ToString(),
                    method = "GET",
                    url = "https://example.com/data",
                    headers = new Dictionary<string, string>(),
                    timeoutMs = 30000
                }
            };
            return schema;
        }

        public static OpenApiSchemaMetadata RuntimeValidationResponse()
        {
            OpenApiSchemaMetadata schema = Object("Runtime configuration validation result.");
            schema.Properties["valid"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["errors"] = OpenApiSchemaMetadata.CreateArray(String("Validation error message.", false));
            schema.Required.Add("valid");
            schema.Required.Add("errors");
            return schema;
        }

        public static OpenApiSchemaMetadata InlineRestMigrationRequest()
        {
            OpenApiSchemaMetadata schema = Object("Optional inline REST migration scope.");
            schema.Properties["tenantId"] = String("Tenant to scan. Omit with flowId omitted to scan all tenants.", true);
            schema.Properties["flowId"] = String("Single flow to scan. Requires tenantId.", true);
            schema.Example = new
            {
                tenantId = "ten_...",
                flowId = "flow_..."
            };
            return schema;
        }

        public static OpenApiSchemaMetadata StepCompatibilityMigrationResult()
        {
            OpenApiSchemaMetadata schema = Object("Inline REST migration result.");
            schema.Properties["flowsScanned"] = NonNegativeInteger("Number of flows scanned.");
            schema.Properties["flowsUpdated"] = NonNegativeInteger("Number of flows updated.");
            schema.Properties["inlineRestStepsFound"] = NonNegativeInteger("Inline REST transitions found.");
            schema.Properties["stepsCreated"] = NonNegativeInteger("Persisted External.Rest steps created.");
            schema.Properties["stepsReused"] = NonNegativeInteger("Existing matching External.Rest steps reused.");
            schema.Properties["entries"] = OpenApiSchemaMetadata.CreateArray(StepCompatibilityMigrationEntry());
            schema.Required.Add("flowsScanned");
            schema.Required.Add("flowsUpdated");
            schema.Required.Add("inlineRestStepsFound");
            schema.Required.Add("stepsCreated");
            schema.Required.Add("stepsReused");
            schema.Required.Add("entries");
            return schema;
        }

        public static OpenApiSchemaMetadata RuntimeDescriptor()
        {
            OpenApiSchemaMetadata schema = Object("Runtime provider descriptor.");
            schema.Properties["runtimeKey"] = RuntimeKeySchema();
            schema.Properties["displayName"] = String("Display name.", false);
            schema.Properties["description"] = String("Description.", false);
            schema.Properties["packagingType"] = EnumString("Runtime packaging model.", "Builtin", "External", "Host", "Container");
            schema.Properties["supportedContractTypes"] = OpenApiSchemaMetadata.CreateArray(ContractTypeSchema());
            schema.Properties["configTypeName"] = String("Concrete runtime config DTO type name.", false);
            schema.Properties["configProperties"] = OpenApiSchemaMetadata.CreateArray(RuntimeConfigPropertyDescriptor());
            schema.Properties["supportsArtifacts"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["supportsVersioning"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["availability"] = EnumString("Runtime availability state.", "Available", "DisabledBySettings", "MissingDependency", "UnsupportedPlatform", "Preview");
            schema.Properties["securityNotes"] = String("Runtime security notes.", true);
            return schema;
        }

        public static OpenApiSchemaMetadata RuntimeDescriptorArray()
        {
            return OpenApiSchemaMetadata.CreateArray(RuntimeDescriptor());
        }

        public static OpenApiSchemaMetadata ArtifactCreateRequest()
        {
            OpenApiSchemaMetadata schema = Object("Create artifact metadata.");
            schema.Properties["name"] = String("Tenant-scoped artifact name.", false);
            schema.Properties["description"] = String("Optional description.", true);
            schema.Required.Add("name");
            return schema;
        }

        public static OpenApiSchemaMetadata ArtifactUpdateRequest()
        {
            OpenApiSchemaMetadata schema = Object("Update artifact metadata.");
            schema.Properties["name"] = String("Tenant-scoped artifact name.", true);
            schema.Properties["description"] = String("Optional description.", true);
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["isProtected"] = OpenApiSchemaMetadata.Boolean();
            return schema;
        }

        public static OpenApiSchemaMetadata ArtifactRecord()
        {
            OpenApiSchemaMetadata schema = Object("Tenant-owned artifact metadata.");
            schema.Properties["id"] = String("Artifact identifier.", false);
            schema.Properties["tenantId"] = String("Tenant identifier.", false);
            schema.Properties["name"] = String("Tenant-scoped artifact name.", false);
            schema.Properties["description"] = String("Optional description.", true);
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["isProtected"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["createdUtc"] = String("Creation timestamp.", false, "date-time");
            schema.Properties["lastUpdateUtc"] = String("Last update timestamp.", false, "date-time");
            schema.Required.Add("id");
            schema.Required.Add("tenantId");
            schema.Required.Add("name");
            return schema;
        }

        public static OpenApiSchemaMetadata ArtifactVersionRecord()
        {
            OpenApiSchemaMetadata schema = Object("Uploaded artifact package version metadata.");
            schema.Properties["id"] = String("Artifact version identifier.", false);
            schema.Properties["tenantId"] = String("Tenant identifier.", false);
            schema.Properties["artifactId"] = String("Parent artifact identifier.", false);
            schema.Properties["version"] = String("Artifact version label.", false);
            schema.Properties["sha256"] = String("Content SHA-256 digest.", false);
            schema.Properties["byteLength"] = NonNegativeLong("Stored byte length.");
            schema.Properties["contentType"] = String("Content type.", true);
            schema.Properties["originalFileName"] = String("Original file name.", true);
            schema.Properties["manifestJson"] = String("Artifact manifest JSON.", true);
            schema.Properties["storageKey"] = String("Blob-store storage key.", true);
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["isProtected"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["createdUtc"] = String("Creation timestamp.", false, "date-time");
            schema.Properties["lastUpdateUtc"] = String("Last update timestamp.", false, "date-time");
            schema.Properties["deletedUtc"] = String("Soft-delete timestamp.", true, "date-time");
            schema.Properties["gcEligibleUtc"] = String("Garbage-collection eligibility timestamp.", true, "date-time");
            schema.Required.Add("id");
            schema.Required.Add("tenantId");
            schema.Required.Add("artifactId");
            schema.Required.Add("version");
            schema.Required.Add("sha256");
            schema.Required.Add("byteLength");
            return schema;
        }

        public static OpenApiSchemaMetadata ArtifactFileRecord()
        {
            OpenApiSchemaMetadata schema = Object("Editable artifact file.");
            schema.Properties["tenantId"] = String("Tenant identifier.", false);
            schema.Properties["artifactId"] = String("Parent artifact identifier.", false);
            schema.Properties["path"] = String("Artifact-relative path using forward slashes.", false);
            schema.Properties["content"] = String("UTF-8 text content, or base64 content when isBinary is true.", false);
            schema.Properties["contentType"] = String("Best-effort content type.", true);
            schema.Properties["isBinary"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["sha256"] = String("Decoded file SHA-256 digest.", false);
            schema.Properties["byteLength"] = NonNegativeLong("Decoded file byte length.");
            schema.Properties["createdUtc"] = String("Creation timestamp.", false, "date-time");
            schema.Properties["lastUpdateUtc"] = String("Last update timestamp.", false, "date-time");
            schema.Required.Add("tenantId");
            schema.Required.Add("artifactId");
            schema.Required.Add("path");
            schema.Required.Add("content");
            schema.Required.Add("isBinary");
            schema.Required.Add("sha256");
            schema.Required.Add("byteLength");
            return schema;
        }

        public static OpenApiSchemaMetadata ArtifactFileWriteRequest()
        {
            OpenApiSchemaMetadata schema = Object("Create or replace one editable artifact file.");
            schema.Properties["path"] = String("Artifact-relative path. Query-string path wins when both are supplied.", true);
            schema.Properties["content"] = String("UTF-8 text content, or base64 content when isBinary is true.", true);
            schema.Properties["contentType"] = String("Best-effort content type.", true);
            schema.Properties["isBinary"] = OpenApiSchemaMetadata.Boolean();
            return schema;
        }

        public static OpenApiSchemaMetadata ArtifactFileWriteResponse()
        {
            OpenApiSchemaMetadata schema = Object("Editable artifact file write result.");
            schema.Properties["file"] = ArtifactFileRecord();
            schema.Properties["file"].Nullable = true;
            schema.Properties["artifactVersion"] = ArtifactVersionRecord();
            schema.Properties["artifactVersion"].Nullable = true;
            schema.Properties["snapshotUpdated"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["snapshotError"] = String("Snapshot packaging or manifest validation error.", true);
            schema.Required.Add("snapshotUpdated");
            return schema;
        }

        public static OpenApiSchemaMetadata Enumeration(OpenApiSchemaMetadata itemSchema)
        {
            OpenApiSchemaMetadata schema = Object("Paged enumeration result.");
            schema.Properties["pageNumber"] = PositiveInteger("1-based page number.");
            schema.Properties["pageSize"] = PositiveInteger("Page size.");
            schema.Properties["totalCount"] = NonNegativeInteger("Total matching rows.");
            schema.Properties["items"] = OpenApiSchemaMetadata.CreateArray(itemSchema);
            schema.Required.Add("pageNumber");
            schema.Required.Add("pageSize");
            schema.Required.Add("totalCount");
            schema.Required.Add("items");
            return schema;
        }

        public static OpenApiSchemaMetadata WorkerCapability()
        {
            OpenApiSchemaMetadata schema = Object("Advertised worker capability.");
            schema.Properties["executionKey"] = String("Execution key or wildcard capability marker.", false);
            schema.Properties["tenantScope"] = String("Tenant scope or wildcard.", false);
            schema.Properties["sourceKind"] = String("Source kind used for placement matching.", false);
            schema.Properties["runtimeKey"] = String("Runtime provider key or wildcard.", false);
            schema.Properties["signatureHash"] = String("Capability signature hash or wildcard.", false);
            schema.Required.Add("executionKey");
            schema.Required.Add("tenantScope");
            schema.Required.Add("sourceKind");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("signatureHash");
            return schema;
        }

        public static OpenApiSchemaMetadata WorkerSession()
        {
            OpenApiSchemaMetadata schema = Object("Latest worker session.");
            schema.Properties["id"] = String("Worker session identifier.", false);
            schema.Properties["workerId"] = String("Worker identifier.", false);
            schema.Properties["connectedUtc"] = String("Session start timestamp.", false, "date-time");
            schema.Properties["disconnectedUtc"] = String("Session end timestamp, when disconnected.", true, "date-time");
            schema.Properties["disconnectReason"] = String("Disconnect reason, when known.", true);
            schema.Properties["protocolVersion"] = String("Worker protocol version.", true);
            schema.Required.Add("id");
            schema.Required.Add("workerId");
            schema.Required.Add("connectedUtc");
            return schema;
        }

        public static OpenApiSchemaMetadata WorkerSummary()
        {
            OpenApiSchemaMetadata schema = Object("Worker summary.");
            schema.Properties["id"] = String("Worker identifier.", false);
            schema.Properties["name"] = String("Worker display name.", false);
            schema.Properties["kind"] = String("Worker kind.", false);
            schema.Properties["state"] = String("Worker state.", false);
            schema.Properties["enabled"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["drainMode"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["version"] = String("Worker version.", true);
            schema.Properties["hostName"] = String("Worker host name.", true);
            schema.Properties["labels"] = OpenApiSchemaMetadata.CreateArray(String("Worker label.", false));
            schema.Properties["capabilities"] = OpenApiSchemaMetadata.CreateArray(WorkerCapability());
            schema.Properties["maxConcurrentRuns"] = PositiveInteger("Maximum concurrent runs.");
            schema.Properties["maxTaskTimeoutMs"] = NonNegativeInteger("Maximum worker-enforced task timeout in milliseconds. Zero means no explicit worker timeout.");
            schema.Properties["activeAssignmentCount"] = NonNegativeInteger("Current active assignments.");
            schema.Properties["tokenLastRotatedUtc"] = String("Worker token last-rotated timestamp.", true, "date-time");
            schema.Properties["lastHeartbeatUtc"] = String("Last observed heartbeat timestamp.", true, "date-time");
            schema.Properties["createdUtc"] = String("Worker creation timestamp.", false, "date-time");
            schema.Properties["latestSession"] = WorkerSession();
            schema.Properties["latestSession"].Nullable = true;
            schema.Required.Add("id");
            schema.Required.Add("name");
            schema.Required.Add("kind");
            schema.Required.Add("state");
            schema.Required.Add("enabled");
            schema.Required.Add("drainMode");
            schema.Required.Add("labels");
            schema.Required.Add("capabilities");
            schema.Required.Add("maxConcurrentRuns");
            schema.Required.Add("maxTaskTimeoutMs");
            schema.Required.Add("activeAssignmentCount");
            schema.Required.Add("createdUtc");
            return schema;
        }

        public static OpenApiSchemaMetadata LogSourceSummary()
        {
            OpenApiSchemaMetadata schema = Object("Log source summary.");
            schema.Properties["sourceKind"] = EnumString("Log source kind.", "server", "worker");
            schema.Properties["sourceId"] = String("Log source identifier.", false);
            schema.Properties["displayName"] = String("Human-readable source label.", false);
            schema.Properties["available"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["hasFiles"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["fileCount"] = NonNegativeInteger("Number of files currently visible.");
            schema.Properties["enabled"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["state"] = String("Optional source state, primarily for workers.", true);
            schema.Properties["hostName"] = String("Optional host name.", true);
            schema.Properties["lastModifiedUtc"] = String("Latest file-modified timestamp within this source.", true, "date-time");
            schema.Required.Add("sourceKind");
            schema.Required.Add("sourceId");
            schema.Required.Add("displayName");
            schema.Required.Add("available");
            schema.Required.Add("hasFiles");
            schema.Required.Add("fileCount");
            schema.Required.Add("enabled");
            schema.Required.Add("active");
            return schema;
        }

        public static OpenApiSchemaMetadata FlowRunSummary()
        {
            OpenApiSchemaMetadata schema = Object("Flow run record.");
            schema.Properties["id"] = String("Run identifier.", false);
            schema.Properties["tenantId"] = String("Tenant identifier.", false);
            schema.Properties["dataFlowId"] = String("Flow identifier.", false);
            schema.Properties["triggeredByUserId"] = String("User identifier that enqueued the run.", true);
            schema.Properties["triggerId"] = String("Trigger identifier that enqueued the run.", true);
            schema.Properties["sourceIp"] = String("Client source IP observed at enqueue time.", true);
            schema.Properties["state"] = EnumString("Run lifecycle state.", "Queued", "Running", "Succeeded", "Failed", "Exception", "Cancelled");
            schema.Properties["inputData"] = String("Serialized input payload.", true);
            schema.Properties["outputData"] = String("Serialized output payload.", true);
            schema.Properties["errorMessage"] = String("Terminal error message when present.", true);
            schema.Properties["executionSnapshotJson"] = String("Serialized execution snapshot.", true);
            schema.Properties["dispatchState"] = String("Fine-grained dispatch state.", true);
            schema.Properties["dispatchAttempt"] = NonNegativeInteger("Dispatch attempt count.");
            schema.Properties["assignedWorkerId"] = String("Assigned worker identifier.", true);
            schema.Properties["runAssignmentId"] = String("Current assignment identifier.", true);
            schema.Properties["queueWaitMs"] = NonNegativeLong("Queue wait in milliseconds.");
            schema.Properties["assignedUtc"] = String("Assignment timestamp.", true, "date-time");
            schema.Properties["leaseExpiresUtc"] = String("Lease-expiry timestamp.", true, "date-time");
            schema.Properties["executionNodeKind"] = EnumString("Execution node kind.", "Server", "Worker");
            schema.Properties["createdUtc"] = String("Creation timestamp.", false, "date-time");
            schema.Properties["startedUtc"] = String("Start timestamp.", true, "date-time");
            schema.Properties["completedUtc"] = String("Completion timestamp.", true, "date-time");
            schema.Properties["lastUpdateUtc"] = String("Last update timestamp.", false, "date-time");
            schema.Required.Add("id");
            schema.Required.Add("tenantId");
            schema.Required.Add("dataFlowId");
            schema.Required.Add("state");
            schema.Required.Add("createdUtc");
            schema.Required.Add("lastUpdateUtc");
            return schema;
        }

        public static OpenApiSchemaMetadata RunAssignmentRecord()
        {
            OpenApiSchemaMetadata schema = Object("Run assignment attempt.");
            schema.Properties["id"] = String("Assignment identifier.", false);
            schema.Properties["flowRunId"] = String("Flow-run identifier.", false);
            schema.Properties["workerId"] = String("Worker identifier.", false);
            schema.Properties["workerSessionId"] = String("Worker-session identifier.", true);
            schema.Properties["attemptNumber"] = NonNegativeInteger("Assignment attempt number.");
            schema.Properties["state"] = String("Assignment state.", false);
            schema.Properties["leaseToken"] = String("Assignment lease token.", false);
            schema.Properties["leaseExpiresUtc"] = String("Lease-expiry timestamp.", false, "date-time");
            schema.Properties["assignedUtc"] = String("Assignment timestamp.", false, "date-time");
            schema.Properties["completedUtc"] = String("Completion timestamp.", true, "date-time");
            schema.Required.Add("id");
            schema.Required.Add("flowRunId");
            schema.Required.Add("workerId");
            schema.Required.Add("attemptNumber");
            schema.Required.Add("state");
            schema.Required.Add("leaseToken");
            schema.Required.Add("leaseExpiresUtc");
            schema.Required.Add("assignedUtc");
            return schema;
        }

        public static OpenApiSchemaMetadata WorkerActivityRecord()
        {
            OpenApiSchemaMetadata schema = Object("Worker activity event.");
            schema.Properties["id"] = String("Activity identifier.", false);
            schema.Properties["workerId"] = String("Worker identifier.", false);
            schema.Properties["workerSessionId"] = String("Worker-session identifier.", true);
            schema.Properties["flowRunId"] = String("Flow-run identifier when present.", true);
            schema.Properties["runAssignmentId"] = String("Run-assignment identifier when present.", true);
            schema.Properties["eventType"] = String("Activity event type.", false);
            schema.Properties["severity"] = String("Severity label.", true);
            schema.Properties["message"] = String("Human-readable message.", true);
            schema.Properties["payloadJson"] = String("Optional structured payload JSON.", true);
            schema.Properties["createdUtc"] = String("Creation timestamp.", false, "date-time");
            schema.Required.Add("id");
            schema.Required.Add("workerId");
            schema.Required.Add("eventType");
            schema.Required.Add("createdUtc");
            return schema;
        }

        public static OpenApiSchemaMetadata RunActivityResponse()
        {
            OpenApiSchemaMetadata schema = Object("Flow run plus assignment and worker activity history.");
            schema.Properties["run"] = FlowRunSummary();
            schema.Properties["assignments"] = OpenApiSchemaMetadata.CreateArray(RunAssignmentRecord());
            schema.Properties["activity"] = OpenApiSchemaMetadata.CreateArray(WorkerActivityRecord());
            schema.Required.Add("run");
            schema.Required.Add("assignments");
            schema.Required.Add("activity");
            return schema;
        }

        public static OpenApiSchemaMetadata RunLogFileSummary()
        {
            OpenApiSchemaMetadata schema = Object("Run-log file summary.");
            schema.Properties["flowRunId"] = String("Flow-run identifier.", false);
            schema.Properties["path"] = String("Path relative to the run directory.", false);
            schema.Properties["fileName"] = String("Simple file name.", false);
            schema.Properties["kind"] = EnumString("Run-log file kind.", "Run", "Worker", "Host", "Step", "StepStderr");
            schema.Properties["attemptNumber"] = NonNegativeInteger("Assignment attempt number.");
            schema.Properties["runAssignmentId"] = String("Run-assignment identifier.", true);
            schema.Properties["workerId"] = String("Worker identifier.", true);
            schema.Properties["stepId"] = String("Step identifier.", true);
            schema.Properties["stepRunId"] = String("Step-run identifier.", true);
            schema.Properties["byteLength"] = NonNegativeLong("Total file size in bytes.");
            schema.Properties["lastModifiedUtc"] = String("Last file-modified timestamp.", false, "date-time");
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["deleteAllowed"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["downloadAllowed"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["deleteMode"] = EnumString("Delete behavior for this file.", "Delete", "Truncate");
            schema.Required.Add("flowRunId");
            schema.Required.Add("path");
            schema.Required.Add("fileName");
            schema.Required.Add("kind");
            schema.Required.Add("byteLength");
            schema.Required.Add("lastModifiedUtc");
            schema.Required.Add("active");
            schema.Required.Add("deleteAllowed");
            schema.Required.Add("downloadAllowed");
            schema.Required.Add("deleteMode");
            return schema;
        }

        public static OpenApiSchemaMetadata RunLogFileRead()
        {
            OpenApiSchemaMetadata schema = RunLogFileSummary();
            schema.Description = "Bounded run-log file read response.";
            schema.Properties["contentType"] = String("Returned content type.", false);
            schema.Properties["content"] = String("Returned text content.", false);
            schema.Properties["truncated"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["tailLines"] = PositiveInteger("Tail line count applied to this read.");
            schema.Properties["maxBytes"] = NonNegativeLong("Maximum bytes allowed for this read.");
            schema.Properties["returnedByteLength"] = NonNegativeLong("UTF-8 byte count returned in content.");
            schema.Required.Add("contentType");
            schema.Required.Add("content");
            schema.Required.Add("truncated");
            schema.Required.Add("tailLines");
            schema.Required.Add("maxBytes");
            schema.Required.Add("returnedByteLength");
            return schema;
        }

        public static OpenApiSchemaMetadata RunLogDelete()
        {
            OpenApiSchemaMetadata schema = Object("Run-log file delete or truncate result.");
            schema.Properties["flowRunId"] = String("Flow-run identifier.", false);
            schema.Properties["path"] = String("Path relative to the run directory.", false);
            schema.Properties["action"] = EnumString("Mutation applied to the file.", "Deleted", "Truncated");
            schema.Properties["success"] = OpenApiSchemaMetadata.Boolean();
            schema.Required.Add("flowRunId");
            schema.Required.Add("path");
            schema.Required.Add("action");
            schema.Required.Add("success");
            return schema;
        }

        public static OpenApiSchemaMetadata LogFileSummary()
        {
            OpenApiSchemaMetadata schema = Object("Log file summary.");
            schema.Properties["sourceKind"] = EnumString("Log source kind.", "server", "worker");
            schema.Properties["sourceId"] = String("Log source identifier.", false);
            schema.Properties["path"] = String("Path relative to the source root.", false);
            schema.Properties["fileName"] = String("Simple file name.", false);
            schema.Properties["byteLength"] = NonNegativeLong("Total file byte length.");
            schema.Properties["lastModifiedUtc"] = String("Last file-modified timestamp.", false, "date-time");
            schema.Properties["isCurrent"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["sourceActive"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["deleteAllowed"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["downloadAllowed"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["deleteMode"] = EnumString("Delete behavior for this file.", "Delete", "Truncate");
            schema.Required.Add("sourceKind");
            schema.Required.Add("sourceId");
            schema.Required.Add("path");
            schema.Required.Add("fileName");
            schema.Required.Add("byteLength");
            schema.Required.Add("lastModifiedUtc");
            schema.Required.Add("isCurrent");
            schema.Required.Add("sourceActive");
            schema.Required.Add("deleteAllowed");
            schema.Required.Add("downloadAllowed");
            schema.Required.Add("deleteMode");
            return schema;
        }

        public static OpenApiSchemaMetadata LogFileRead()
        {
            OpenApiSchemaMetadata schema = LogFileSummary();
            schema.Description = "Bounded log file read response.";
            schema.Properties["contentType"] = String("Returned content type.", false);
            schema.Properties["content"] = String("Returned text content.", false);
            schema.Properties["truncated"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["tailLines"] = PositiveInteger("Tail line count applied to this read.");
            schema.Properties["maxBytes"] = NonNegativeLong("Maximum bytes allowed for this read.");
            schema.Properties["returnedByteLength"] = NonNegativeLong("UTF-8 byte count returned in content.");
            schema.Required.Add("contentType");
            schema.Required.Add("content");
            schema.Required.Add("truncated");
            schema.Required.Add("tailLines");
            schema.Required.Add("maxBytes");
            schema.Required.Add("returnedByteLength");
            return schema;
        }

        public static OpenApiSchemaMetadata LogFileDelete()
        {
            OpenApiSchemaMetadata schema = Object("Log file delete or truncate result.");
            schema.Properties["sourceKind"] = EnumString("Log source kind.", "server", "worker");
            schema.Properties["sourceId"] = String("Log source identifier.", false);
            schema.Properties["path"] = String("Path relative to the source root.", false);
            schema.Properties["action"] = EnumString("Mutation applied to the file.", "Deleted", "Truncated");
            schema.Properties["success"] = OpenApiSchemaMetadata.Boolean();
            schema.Required.Add("sourceKind");
            schema.Required.Add("sourceId");
            schema.Required.Add("path");
            schema.Required.Add("action");
            schema.Required.Add("success");
            return schema;
        }

        public static OpenApiSchemaMetadata WorkerTokenIssueResult()
        {
            OpenApiSchemaMetadata schema = Object("Issued worker token.");
            schema.Properties["workerId"] = String("Worker identifier.", false);
            schema.Properties["token"] = String("Plaintext worker token. Store it immediately; only the hash is persisted.", false);
            schema.Properties["issuedUtc"] = String("Issue timestamp.", false, "date-time");
            schema.Required.Add("workerId");
            schema.Required.Add("token");
            schema.Required.Add("issuedUtc");
            return schema;
        }

        public static OpenApiSchemaMetadata BinaryBody()
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.Create("string", "binary");
            schema.Description = "Raw artifact package bytes.";
            return schema;
        }

        private static OpenApiSchemaMetadata StepWriteRequest(string description)
        {
            OpenApiSchemaMetadata schema = Object(description);
            schema.Properties["executionKey"] = String("Tenant-scoped execution key. Defaults to name when omitted on create.", true);
            schema.Properties["name"] = String("Display name.", true);
            schema.Properties["description"] = String("Optional description.", true);
            schema.Properties["runtimeKey"] = RuntimeKeySchema();
            schema.Properties["runtimeConfig"] = RuntimeConfigSchema();
            schema.Properties["contractType"] = ContractTypeSchema();
            schema.Properties["inputSchema"] = String("Optional JSON schema for step input.", true);
            schema.Properties["outputSchema"] = String("Optional JSON schema for step output.", true);
            schema.Properties["validateInput"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["validateOutput"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["maxRuntimeMs"] = NonNegativeInteger("Maximum runtime in milliseconds. Zero means no step-level override.");
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Example = new
            {
                executionKey = "call_api",
                name = "Call API",
                runtimeKey = StepRuntimeKeys.ExternalRest.ToString(),
                runtimeConfig = new
                {
                    runtimeKey = StepRuntimeKeys.ExternalRest.ToString(),
                    method = "POST",
                    url = "https://example.com/orders",
                    headers = new Dictionary<string, string>(),
                    timeoutMs = 30000
                },
                contractType = "Loose",
                maxRuntimeMs = 0,
                active = true
            };
            return schema;
        }

        private static OpenApiSchemaMetadata RuntimeConfigSchema()
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>
            {
                [StepRuntimeKeys.BuiltinClass.ToString()] = ComponentRef(BuiltinClassRuntimeConfigSchemaName),
                [StepRuntimeKeys.BuiltinMethod.ToString()] = ComponentRef(BuiltinMethodRuntimeConfigSchemaName),
                [StepRuntimeKeys.BuiltinUnknown.ToString()] = ComponentRef(BuiltinUnknownRuntimeConfigSchemaName),
                [StepRuntimeKeys.ExternalRest.ToString()] = ComponentRef(ExternalRestRuntimeConfigSchemaName),
                [StepRuntimeKeys.LegacyInlineRest.ToString()] = ComponentRef(LegacyInlineRestRuntimeConfigSchemaName),
                [StepRuntimeKeys.ArtifactProcess.ToString()] = ComponentRef(ArtifactProcessRuntimeConfigSchemaName),
                [StepRuntimeKeys.ArtifactPython.ToString()] = ComponentRef(ArtifactPythonRuntimeConfigSchemaName),
                [StepRuntimeKeys.ArtifactJavaScript.ToString()] = ComponentRef(ArtifactJavaScriptRuntimeConfigSchemaName),
                [StepRuntimeKeys.ArtifactDotnetProcess.ToString()] = ComponentRef(ArtifactDotnetProcessRuntimeConfigSchemaName),
                [StepRuntimeKeys.HostExecutable.ToString()] = ComponentRef(HostExecutableRuntimeConfigSchemaName)
            };

            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.CreateOneOf(
                OpenApiSchemaMetadata.CreateRef(BuiltinClassRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(BuiltinMethodRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(BuiltinUnknownRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(ExternalRestRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(LegacyInlineRestRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(ArtifactProcessRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(ArtifactPythonRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(ArtifactJavaScriptRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(ArtifactDotnetProcessRuntimeConfigSchemaName),
                OpenApiSchemaMetadata.CreateRef(HostExecutableRuntimeConfigSchemaName))
                .WithDiscriminator("runtimeKey", mapping);
            schema.Description = "Typed runtime configuration. The runtimeKey field is the discriminator; supported values are concrete provider keys.";
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.ExternalRest.ToString(),
                method = "GET",
                url = "https://example.com/data",
                headers = new Dictionary<string, string>(),
                timeoutMs = 30000
            };
            return schema;
        }

        private static OpenApiSchemaMetadata BuiltinClassRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Configuration for a registered class-based built-in step.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.BuiltinClass);
            schema.Properties["identifier"] = String("Built-in runtime identifier.", true);
            schema.Properties["typeName"] = String("Built-in class type name.", true);
            schema.Properties["assemblyName"] = String("Built-in assembly name.", true);
            schema.Properties["assemblyVersion"] = String("Built-in assembly version.", true);
            schema.Properties["signatureHash"] = String("Built-in signature hash.", true);
            schema.Required.Add("runtimeKey");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.BuiltinClass.ToString(),
                identifier = "class_step",
                typeName = "Example.Steps.ValidateOrder"
            };
            return schema;
        }

        private static OpenApiSchemaMetadata BuiltinMethodRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Configuration for a registered method-based built-in step.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.BuiltinMethod);
            schema.Properties["identifier"] = String("Built-in runtime identifier.", true);
            schema.Properties["declaringType"] = String("Built-in method declaring type.", true);
            schema.Properties["methodName"] = String("Built-in method name.", true);
            schema.Properties["assemblyName"] = String("Built-in assembly name.", true);
            schema.Properties["assemblyVersion"] = String("Built-in assembly version.", true);
            schema.Properties["signatureHash"] = String("Built-in signature hash.", true);
            schema.Required.Add("runtimeKey");
            schema.Required.Add("methodName");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.BuiltinMethod.ToString(),
                identifier = "method_step",
                methodName = "Run"
            };
            return schema;
        }

        private static OpenApiSchemaMetadata BuiltinUnknownRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Compatibility marker for legacy code steps before reconciliation.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.BuiltinUnknown);
            schema.Properties["identifier"] = String("Legacy built-in runtime identifier.", true);
            schema.Required.Add("runtimeKey");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.BuiltinUnknown.ToString(),
                identifier = "legacy_step"
            };
            return schema;
        }

        private static OpenApiSchemaMetadata ExternalRestRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = RestRuntimeConfigSchema(StepRuntimeKeys.ExternalRest, "Configuration for persisted REST steps.");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.ExternalRest.ToString(),
                method = "GET",
                url = "https://example.com/data",
                headers = new Dictionary<string, string>(),
                timeoutMs = 30000
            };
            return schema;
        }

        private static OpenApiSchemaMetadata LegacyInlineRestRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = RestRuntimeConfigSchema(StepRuntimeKeys.LegacyInlineRest, "Read-path compatibility configuration for inline REST flow transitions.");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.LegacyInlineRest.ToString(),
                method = "GET",
                url = "https://example.com/data",
                headers = new Dictionary<string, string>(),
                timeoutMs = 30000
            };
            return schema;
        }

        private static OpenApiSchemaMetadata RestRuntimeConfigSchema(RuntimeKey key, string description)
        {
            OpenApiSchemaMetadata schema = Object(description);
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(key);
            schema.Properties["method"] = String("HTTP method.", false);
            schema.Properties["url"] = String("HTTP URL.", false, "uri");
            schema.Properties["headers"] = Object("HTTP headers.");
            schema.Properties["timeoutMs"] = PositiveInteger("REST timeout in milliseconds.");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("method");
            schema.Required.Add("url");
            schema.Required.Add("timeoutMs");
            return schema;
        }

        private static OpenApiSchemaMetadata ArtifactProcessRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Configuration for uploaded process artifacts.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.ArtifactProcess);
            schema.Properties["artifactId"] = String("Artifact id for artifact-backed runtimes.", false);
            schema.Properties["artifactVersion"] = String("Artifact version label for artifact-backed runtimes.", true);
            schema.Properties["entrypoint"] = String("Artifact entrypoint override.", true);
            schema.Properties["arguments"] = StringArray("Argument value.");
            schema.Properties["environmentReferences"] = StringArray("Environment variable name to pass through without embedding secrets.");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("artifactId");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.ArtifactProcess.ToString(),
                artifactId = "art_...",
                artifactVersion = "current",
                entrypoint = "main",
                arguments = new string[0],
                environmentReferences = new string[0]
            };
            return schema;
        }

        private static OpenApiSchemaMetadata ArtifactPythonRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Configuration for Python artifacts.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.ArtifactPython);
            schema.Properties["artifactId"] = String("Artifact id for artifact-backed runtimes.", false);
            schema.Properties["artifactVersion"] = String("Artifact version label for artifact-backed runtimes.", true);
            schema.Properties["entrypoint"] = String("Artifact entrypoint override.", true);
            schema.Properties["module"] = String("Python module for Artifact.Python.", true);
            schema.Properties["function"] = String("Python function for Artifact.Python.", false);
            schema.Properties["pythonVersion"] = String("Python version hint for Artifact.Python.", true);
            schema.Properties["arguments"] = StringArray("Argument value.");
            schema.Properties["environmentReferences"] = StringArray("Environment variable name to pass through without embedding secrets.");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("artifactId");
            schema.Required.Add("function");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.ArtifactPython.ToString(),
                artifactId = "art_...",
                artifactVersion = "current",
                entrypoint = "main",
                module = "handler",
                function = "run",
                arguments = new string[0],
                environmentReferences = new string[0]
            };
            return schema;
        }

        private static OpenApiSchemaMetadata ArtifactDotnetProcessRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Configuration for .NET process artifacts.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.ArtifactDotnetProcess);
            schema.Properties["artifactId"] = String("Artifact id for artifact-backed runtimes.", false);
            schema.Properties["artifactVersion"] = String("Artifact version label for artifact-backed runtimes.", true);
            schema.Properties["entrypoint"] = String("Artifact entrypoint override.", true);
            schema.Properties["arguments"] = StringArray("Argument value.");
            schema.Properties["environmentReferences"] = StringArray("Environment variable name to pass through without embedding secrets.");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("artifactId");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.ArtifactDotnetProcess.ToString(),
                artifactId = "art_...",
                artifactVersion = "current",
                entrypoint = "main",
                arguments = new string[0],
                environmentReferences = new string[0]
            };
            return schema;
        }

        private static OpenApiSchemaMetadata ArtifactJavaScriptRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Configuration for JavaScript artifacts.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.ArtifactJavaScript);
            schema.Properties["artifactId"] = String("Artifact id for artifact-backed runtimes.", false);
            schema.Properties["artifactVersion"] = String("Artifact version label for artifact-backed runtimes.", true);
            schema.Properties["entrypoint"] = String("Artifact entrypoint override.", true);
            schema.Properties["module"] = String("JavaScript module path for Artifact.JavaScript.", true);
            schema.Properties["function"] = String("Exported function for Artifact.JavaScript.", false);
            schema.Properties["arguments"] = StringArray("Argument value.");
            schema.Properties["environmentReferences"] = StringArray("Environment variable name to pass through without embedding secrets.");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("artifactId");
            schema.Required.Add("function");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.ArtifactJavaScript.ToString(),
                artifactId = "art_...",
                artifactVersion = "current",
                entrypoint = "main",
                module = "handler.js",
                function = "run",
                arguments = new string[0],
                environmentReferences = new string[0]
            };
            return schema;
        }

        private static OpenApiSchemaMetadata HostExecutableRuntimeConfigSchema()
        {
            OpenApiSchemaMetadata schema = Object("Configuration for operator allowlisted host executables.");
            schema.Properties["runtimeKey"] = RuntimeKeyLiteral(StepRuntimeKeys.HostExecutable);
            schema.Properties["allowListKey"] = String("Operator allowlist key for Host.Executable.", false);
            schema.Properties["arguments"] = StringArray("Argument value.");
            schema.Required.Add("runtimeKey");
            schema.Required.Add("allowListKey");
            schema.Example = new
            {
                runtimeKey = StepRuntimeKeys.HostExecutable.ToString(),
                allowListKey = "fixture",
                arguments = new string[0]
            };
            return schema;
        }

        private static OpenApiSchemaMetadata RuntimeConfigPropertyDescriptor()
        {
            OpenApiSchemaMetadata schema = Object("Runtime config property descriptor.");
            schema.Properties["name"] = String("Property name.", false);
            schema.Properties["type"] = String("Property type.", false);
            schema.Properties["required"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["description"] = String("Property description.", true);
            schema.Required.Add("name");
            schema.Required.Add("type");
            schema.Required.Add("required");
            return schema;
        }

        private static OpenApiSchemaMetadata StepCompatibilityMigrationEntry()
        {
            OpenApiSchemaMetadata schema = Object("Single inline REST migration entry.");
            schema.Properties["tenantId"] = String("Tenant identifier.", false);
            schema.Properties["flowId"] = String("Flow identifier.", false);
            schema.Properties["originalExecutionKey"] = String("Original inline transition key.", false);
            schema.Properties["executionKey"] = String("Persisted step execution key.", false);
            schema.Properties["stepId"] = String("Persisted step identifier.", false);
            schema.Properties["stepCreated"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["flowUpdated"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["message"] = String("Migration detail.", false);
            schema.Required.Add("tenantId");
            schema.Required.Add("flowId");
            schema.Required.Add("originalExecutionKey");
            schema.Required.Add("executionKey");
            schema.Required.Add("stepId");
            schema.Required.Add("stepCreated");
            schema.Required.Add("flowUpdated");
            schema.Required.Add("message");
            return schema;
        }

        private static OpenApiSchemaMetadata RuntimeKeySchema()
        {
            return EnumString(
                "Runtime provider key.",
                StepRuntimeKeys.BuiltinClass.ToString(),
                StepRuntimeKeys.BuiltinMethod.ToString(),
                StepRuntimeKeys.BuiltinUnknown.ToString(),
                StepRuntimeKeys.ExternalRest.ToString(),
                StepRuntimeKeys.LegacyInlineRest.ToString(),
                StepRuntimeKeys.ArtifactProcess.ToString(),
                StepRuntimeKeys.ArtifactPython.ToString(),
                StepRuntimeKeys.ArtifactJavaScript.ToString(),
                StepRuntimeKeys.ArtifactDotnetProcess.ToString(),
                StepRuntimeKeys.HostExecutable.ToString());
        }

        private static OpenApiSchemaMetadata ContractTypeSchema()
        {
            return EnumString("Step input/output contract behavior.", "Loose", "Schema");
        }

        private static OpenApiSchemaMetadata EnumString(string description, params string[] values)
        {
            OpenApiSchemaMetadata schema = String(description, false);
            schema.Enum = new List<object>();
            foreach (string value in values) schema.Enum.Add(value);
            return schema;
        }

        private static OpenApiSchemaMetadata RuntimeKeyLiteral(RuntimeKey key)
        {
            return EnumString("Runtime discriminator value.", key.ToString());
        }

        private static OpenApiSchemaMetadata DataFlowShape(string description)
        {
            OpenApiSchemaMetadata schema = Object(description);
            schema.Properties["name"] = String("Display name.", false);
            schema.Properties["description"] = String("Optional description.", true);
            schema.Properties["triggerId"] = String("Optional associated trigger identifier.", true);
            schema.Properties["startStepId"] = String("First execution key in the transition graph.", false);
            schema.Properties["routingHintLabel"] = String("Optional worker-placement label for label-pinned scheduling.", true);
            schema.Properties["invocationAuthMode"] = EnumString("HTTP trigger invocation authentication policy.", "Public", "ApiAuthenticated");
            schema.Properties["maxRuntimeMs"] = NonNegativeInteger("Maximum flow runtime in milliseconds. Zero means no timeout.");
            schema.Properties["transitions"] = Object("Transition map keyed by step execution key.");
            schema.Properties["active"] = OpenApiSchemaMetadata.Boolean();
            schema.Properties["isProtected"] = OpenApiSchemaMetadata.Boolean();
            schema.Required.Add("name");
            schema.Required.Add("startStepId");
            schema.Required.Add("transitions");
            return schema;
        }

        private static OpenApiSchemaMetadata StringArray(string itemDescription)
        {
            return OpenApiSchemaMetadata.CreateArray(String(itemDescription, false));
        }

        private static string ComponentRef(string schemaName)
        {
            return "#/components/schemas/" + schemaName;
        }

        private static OpenApiSchemaMetadata NonNegativeInteger(string description)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.Integer("int32");
            schema.Description = description;
            schema.Minimum = 0;
            return schema;
        }

        private static OpenApiSchemaMetadata NonNegativeLong(string description)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.Integer("int64");
            schema.Description = description;
            schema.Minimum = 0;
            return schema;
        }

        private static OpenApiSchemaMetadata PositiveInteger(string description)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.Integer("int32");
            schema.Description = description;
            schema.Minimum = 1;
            return schema;
        }

        private static OpenApiSchemaMetadata String(string description, bool nullable, string? format = null)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.String(format);
            schema.Description = description;
            schema.Nullable = nullable;
            return schema;
        }

        private static OpenApiSchemaMetadata Object(string description)
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.Create("object", null);
            schema.Description = description;
            schema.Properties = new Dictionary<string, OpenApiSchemaMetadata>();
            schema.Required = new List<string>();
            return schema;
        }
    }
}
