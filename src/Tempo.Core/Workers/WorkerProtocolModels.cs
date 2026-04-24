namespace Tempo.Core.Workers
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;

    /// <summary>
    /// Shared JSON options for worker protocol messages.
    /// </summary>
    public static class WorkerProtocolSerialization
    {
        /// <summary>JSON options for websocket worker frames.</summary>
        public static readonly JsonSerializerOptions Options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions(StepRuntimeSerialization.Options)
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }

    /// <summary>
    /// Frame type constants used by the worker websocket.
    /// </summary>
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

    /// <summary>
    /// Advertised worker capability descriptor.
    /// </summary>
    public class WorkerCapabilityDescriptor
    {
        public string ExecutionKey { get; set; } = string.Empty;
        public string TenantScope { get; set; } = "*";
        public string SourceKind { get; set; } = string.Empty;
        public string RuntimeKey { get; set; } = string.Empty;
        public string SignatureHash { get; set; } = "*";
    }

    /// <summary>
    /// Initial worker hello frame.
    /// </summary>
    public class WorkerHelloMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.Hello;
        public string ProtocolVersion { get; set; } = "1.0";
        public string WorkerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = "Worker";
        public string Version { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public int MaxConcurrentRuns { get; set; } = 1;
        public int MaxTaskTimeoutMs { get; set; } = 0;
        public List<string> Labels { get; set; } = new List<string>();
        public List<WorkerCapabilityDescriptor> Capabilities { get; set; } = new List<WorkerCapabilityDescriptor>();
    }

    /// <summary>
    /// Server hello acknowledgement.
    /// </summary>
    public class WorkerHelloAckMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.HelloAck;
        public string ProtocolVersion { get; set; } = "1.0";
        public string WorkerId { get; set; } = string.Empty;
        public string WorkerSessionId { get; set; } = string.Empty;
        public int HeartbeatIntervalMs { get; set; } = 10000;
        public int HeartbeatTimeoutMs { get; set; } = 30000;
        public int LeaseDurationMs { get; set; } = 300000;
        public bool DrainMode { get; set; } = false;
    }

    /// <summary>
    /// Worker heartbeat frame.
    /// </summary>
    public class WorkerHeartbeatMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.Heartbeat;
        public string WorkerId { get; set; } = string.Empty;
        public string WorkerSessionId { get; set; } = string.Empty;
        public int ActiveRuns { get; set; } = 0;
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Server-to-worker assignment frame.
    /// </summary>
    public class WorkerAssignMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.Assign;
        public RunAssignmentRecord Assignment { get; set; } = new RunAssignmentRecord();
        public FlowRunExecutionPlan Plan { get; set; } = new FlowRunExecutionPlan();
    }

    /// <summary>
    /// Worker acknowledgement of an assignment delivery.
    /// </summary>
    public class WorkerAssignAckMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.AssignAck;
        public string WorkerId { get; set; } = string.Empty;
        public string WorkerSessionId { get; set; } = string.Empty;
        public string RunAssignmentId { get; set; } = string.Empty;
        public string LeaseToken { get; set; } = string.Empty;
        public bool Accepted { get; set; } = true;
        public string? Message { get; set; } = null;
    }

    /// <summary>
    /// Worker terminal completion frame.
    /// </summary>
    public class WorkerRunCompletedMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.RunCompleted;
        public RunCompletionReport Completion { get; set; } = new RunCompletionReport();
    }

    /// <summary>
    /// Server drain command.
    /// </summary>
    public class WorkerDrainMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.Drain;
        public string WorkerId { get; set; } = string.Empty;
        public string? Message { get; set; } = null;
    }

    /// <summary>
    /// Server resume command.
    /// </summary>
    public class WorkerResumeMessage
    {
        public string Type { get; set; } = WorkerFrameTypes.Resume;
        public string WorkerId { get; set; } = string.Empty;
        public string? Message { get; set; } = null;
    }
}
