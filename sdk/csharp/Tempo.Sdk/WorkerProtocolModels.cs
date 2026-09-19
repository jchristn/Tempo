namespace Tempo.Sdk.Workers
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>Shared JSON options for worker protocol messages.</summary>
    public static class WorkerProtocolJson
    {
        /// <summary>Serialization options that match Tempo.Worker websocket frames.</summary>
        public static readonly JsonSerializerOptions Options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }

    /// <summary>Frame type constants used by the Tempo worker protocol.</summary>
    public static class WorkerFrameTypes
    {
        /// <summary>Frame type for the initial worker hello message.</summary>
        public const string Hello = "hello";

        /// <summary>Frame type for the server hello acknowledgement.</summary>
        public const string HelloAck = "hello-ack";

        /// <summary>Frame type for a worker heartbeat.</summary>
        public const string Heartbeat = "heartbeat";

        /// <summary>Frame type for a server-to-worker run assignment.</summary>
        public const string Assign = "assign";

        /// <summary>Frame type for a worker acknowledgement of an assignment.</summary>
        public const string AssignAck = "assign-ack";

        /// <summary>Frame type for a worker run completion report.</summary>
        public const string RunCompleted = "run-completed";

        /// <summary>Frame type for a server drain command.</summary>
        public const string Drain = "drain";

        /// <summary>Frame type for a server resume command.</summary>
        public const string Resume = "resume";
    }

    /// <summary>Advertised worker capability descriptor.</summary>
    public class WorkerCapabilityDescriptor
    {
        /// <summary>Execution key identifying the capability. Default: empty string.</summary>
        [JsonPropertyName("executionKey")]
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>Tenant scope this capability applies to. Default: "*" (all tenants).</summary>
        [JsonPropertyName("tenantScope")]
        public string TenantScope { get; set; } = "*";

        /// <summary>Source kind associated with the capability. Default: empty string.</summary>
        [JsonPropertyName("sourceKind")]
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>Runtime key associated with the capability. Default: empty string.</summary>
        [JsonPropertyName("runtimeKey")]
        public string RuntimeKey { get; set; } = string.Empty;

        /// <summary>Signature hash for the capability. Default: "*" (any signature).</summary>
        [JsonPropertyName("signatureHash")]
        public string SignatureHash { get; set; } = "*";
    }

    /// <summary>Initial worker hello frame.</summary>
    public class WorkerHelloMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Hello"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Hello;

        /// <summary>Worker protocol version. Default: "1.0".</summary>
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "1.0";

        /// <summary>Unique identifier of the worker. Default: empty string.</summary>
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Human-readable worker name. Default: empty string.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Worker kind. Default: "Worker".</summary>
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "Worker";

        /// <summary>Worker software version. Default: empty string.</summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>Host name where the worker is running. Default: empty string.</summary>
        [JsonPropertyName("hostName")]
        public string HostName { get; set; } = string.Empty;

        /// <summary>Maximum number of concurrent runs the worker can execute. Default: 1.</summary>
        [JsonPropertyName("maxConcurrentRuns")]
        public int MaxConcurrentRuns { get; set; } = 1;

        /// <summary>Labels advertised by the worker. Default: empty list.</summary>
        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>Capabilities advertised by the worker. Default: empty list.</summary>
        [JsonPropertyName("capabilities")]
        public List<WorkerCapabilityDescriptor> Capabilities { get; set; } = new List<WorkerCapabilityDescriptor>();
    }

    /// <summary>Server hello acknowledgement.</summary>
    public class WorkerHelloAckMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.HelloAck"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.HelloAck;

        /// <summary>Worker protocol version. Default: "1.0".</summary>
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "1.0";

        /// <summary>Unique identifier of the worker. Default: empty string.</summary>
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Server-assigned worker session identifier. Default: empty string.</summary>
        [JsonPropertyName("workerSessionId")]
        public string WorkerSessionId { get; set; } = string.Empty;

        /// <summary>Interval in milliseconds at which the worker should send heartbeats. Default: 10000.</summary>
        [JsonPropertyName("heartbeatIntervalMs")]
        public int HeartbeatIntervalMs { get; set; } = 10000;

        /// <summary>Timeout in milliseconds after which a missing heartbeat is considered a failure. Default: 30000.</summary>
        [JsonPropertyName("heartbeatTimeoutMs")]
        public int HeartbeatTimeoutMs { get; set; } = 30000;

        /// <summary>Lease duration in milliseconds granted to the worker. Default: 300000.</summary>
        [JsonPropertyName("leaseDurationMs")]
        public int LeaseDurationMs { get; set; } = 300000;

        /// <summary>Indicates whether the worker should start in drain mode. Default: false.</summary>
        [JsonPropertyName("drainMode")]
        public bool DrainMode { get; set; } = false;
    }

    /// <summary>Worker heartbeat frame.</summary>
    public class WorkerHeartbeatMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Heartbeat"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Heartbeat;

        /// <summary>Unique identifier of the worker. Default: empty string.</summary>
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Server-assigned worker session identifier. Default: empty string.</summary>
        [JsonPropertyName("workerSessionId")]
        public string WorkerSessionId { get; set; } = string.Empty;

        /// <summary>Number of runs currently active on the worker. Default: 0.</summary>
        [JsonPropertyName("activeRuns")]
        public int ActiveRuns { get; set; } = 0;

        /// <summary>UTC timestamp when the heartbeat was sent. Default: current UTC time.</summary>
        [JsonPropertyName("sentUtc")]
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Server-to-worker assignment frame.</summary>
    public class WorkerAssignMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Assign"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Assign;

        /// <summary>Opaque JSON payload for the server-issued run assignment.</summary>
        [JsonPropertyName("assignment")]
        public JsonElement Assignment { get; set; }

        /// <summary>Opaque JSON payload for the serialized execution plan.</summary>
        [JsonPropertyName("plan")]
        public JsonElement Plan { get; set; }
    }

    /// <summary>Worker acknowledgement of an assignment delivery.</summary>
    public class WorkerAssignAckMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.AssignAck"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.AssignAck;

        /// <summary>Unique identifier of the worker. Default: empty string.</summary>
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Server-assigned worker session identifier. Default: empty string.</summary>
        [JsonPropertyName("workerSessionId")]
        public string WorkerSessionId { get; set; } = string.Empty;

        /// <summary>Identifier of the run assignment being acknowledged. Default: empty string.</summary>
        [JsonPropertyName("runAssignmentId")]
        public string RunAssignmentId { get; set; } = string.Empty;

        /// <summary>Lease token associated with the accepted assignment. Default: empty string.</summary>
        [JsonPropertyName("leaseToken")]
        public string LeaseToken { get; set; } = string.Empty;

        /// <summary>Indicates whether the worker accepted the assignment. Default: true.</summary>
        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; } = true;

        /// <summary>Optional message describing the acknowledgement outcome. Default: null.</summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>Worker terminal completion frame.</summary>
    public class WorkerRunCompletedMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.RunCompleted"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.RunCompleted;

        /// <summary>Opaque JSON payload for the completion report.</summary>
        [JsonPropertyName("completion")]
        public JsonElement Completion { get; set; }
    }

    /// <summary>Server drain command.</summary>
    public class WorkerDrainMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Drain"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Drain;

        /// <summary>Unique identifier of the worker. Default: empty string.</summary>
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Optional message describing the drain command. Default: null.</summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>Server resume command.</summary>
    public class WorkerResumeMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Resume"/>.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Resume;

        /// <summary>Unique identifier of the worker. Default: empty string.</summary>
        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Optional message describing the resume command. Default: null.</summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
