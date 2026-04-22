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
        public const string Hello = "hello";
        public const string HelloAck = "hello-ack";
        public const string Heartbeat = "heartbeat";
        public const string Assign = "assign";
        public const string AssignAck = "assign-ack";
        public const string RunCompleted = "run-completed";
        public const string Drain = "drain";
        public const string Resume = "resume";
    }

    /// <summary>Advertised worker capability descriptor.</summary>
    public class WorkerCapabilityDescriptor
    {
        [JsonPropertyName("executionKey")]
        public string ExecutionKey { get; set; } = string.Empty;

        [JsonPropertyName("tenantScope")]
        public string TenantScope { get; set; } = "*";

        [JsonPropertyName("sourceKind")]
        public string SourceKind { get; set; } = string.Empty;

        [JsonPropertyName("runtimeKey")]
        public string RuntimeKey { get; set; } = string.Empty;

        [JsonPropertyName("signatureHash")]
        public string SignatureHash { get; set; } = "*";
    }

    /// <summary>Initial worker hello frame.</summary>
    public class WorkerHelloMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Hello;

        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "1.0";

        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "Worker";

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("hostName")]
        public string HostName { get; set; } = string.Empty;

        [JsonPropertyName("maxConcurrentRuns")]
        public int MaxConcurrentRuns { get; set; } = 1;

        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new List<string>();

        [JsonPropertyName("capabilities")]
        public List<WorkerCapabilityDescriptor> Capabilities { get; set; } = new List<WorkerCapabilityDescriptor>();
    }

    /// <summary>Server hello acknowledgement.</summary>
    public class WorkerHelloAckMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.HelloAck;

        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "1.0";

        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("workerSessionId")]
        public string WorkerSessionId { get; set; } = string.Empty;

        [JsonPropertyName("heartbeatIntervalMs")]
        public int HeartbeatIntervalMs { get; set; } = 10000;

        [JsonPropertyName("heartbeatTimeoutMs")]
        public int HeartbeatTimeoutMs { get; set; } = 30000;

        [JsonPropertyName("leaseDurationMs")]
        public int LeaseDurationMs { get; set; } = 300000;

        [JsonPropertyName("drainMode")]
        public bool DrainMode { get; set; } = false;
    }

    /// <summary>Worker heartbeat frame.</summary>
    public class WorkerHeartbeatMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Heartbeat;

        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("workerSessionId")]
        public string WorkerSessionId { get; set; } = string.Empty;

        [JsonPropertyName("activeRuns")]
        public int ActiveRuns { get; set; } = 0;

        [JsonPropertyName("sentUtc")]
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Server-to-worker assignment frame.</summary>
    public class WorkerAssignMessage
    {
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
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.AssignAck;

        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("workerSessionId")]
        public string WorkerSessionId { get; set; } = string.Empty;

        [JsonPropertyName("runAssignmentId")]
        public string RunAssignmentId { get; set; } = string.Empty;

        [JsonPropertyName("leaseToken")]
        public string LeaseToken { get; set; } = string.Empty;

        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; } = true;

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>Worker terminal completion frame.</summary>
    public class WorkerRunCompletedMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.RunCompleted;

        /// <summary>Opaque JSON payload for the completion report.</summary>
        [JsonPropertyName("completion")]
        public JsonElement Completion { get; set; }
    }

    /// <summary>Server drain command.</summary>
    public class WorkerDrainMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Drain;

        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>Server resume command.</summary>
    public class WorkerResumeMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = WorkerFrameTypes.Resume;

        [JsonPropertyName("workerId")]
        public string WorkerId { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
