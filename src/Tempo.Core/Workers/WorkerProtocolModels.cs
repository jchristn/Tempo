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
        /// <summary>Frame type for the initial worker hello message.</summary>
        public const string Hello = "hello";

        /// <summary>Frame type for the server hello acknowledgement message.</summary>
        public const string HelloAck = "hello-ack";

        /// <summary>Frame type for the worker heartbeat message.</summary>
        public const string Heartbeat = "heartbeat";

        /// <summary>Frame type for the server-to-worker assignment message.</summary>
        public const string Assign = "assign";

        /// <summary>Frame type for the worker acknowledgement of an assignment delivery.</summary>
        public const string AssignAck = "assign-ack";

        /// <summary>Frame type for the worker terminal completion message.</summary>
        public const string RunCompleted = "run-completed";

        /// <summary>Frame type for the server drain command.</summary>
        public const string Drain = "drain";

        /// <summary>Frame type for the server resume command.</summary>
        public const string Resume = "resume";
    }

    /// <summary>
    /// Advertised worker capability descriptor.
    /// </summary>
    public class WorkerCapabilityDescriptor
    {
        /// <summary>Execution key identifying the work the worker can perform.</summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>Tenant scope the capability applies to. Default: "*" (all tenants).</summary>
        public string TenantScope { get; set; } = "*";

        /// <summary>Kind of source the capability handles.</summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>Runtime key identifying the execution runtime.</summary>
        public string RuntimeKey { get; set; } = string.Empty;

        /// <summary>Signature hash constraint for the capability. Default: "*" (any signature).</summary>
        public string SignatureHash { get; set; } = "*";
    }

    /// <summary>
    /// Initial worker hello frame.
    /// </summary>
    public class WorkerHelloMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Hello"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.Hello;

        /// <summary>Worker protocol version. Default: "1.0".</summary>
        public string ProtocolVersion { get; set; } = "1.0";

        /// <summary>Unique identifier of the worker.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Human-readable name of the worker.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Kind of worker. Default: "Worker".</summary>
        public string Kind { get; set; } = "Worker";

        /// <summary>Version of the worker software.</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>Host name on which the worker is running.</summary>
        public string HostName { get; set; } = string.Empty;

        /// <summary>Maximum number of concurrent runs the worker can execute. Default: 1.</summary>
        public int MaxConcurrentRuns { get; set; } = 1;

        /// <summary>Maximum task timeout in milliseconds. Default: 0 (no timeout).</summary>
        public int MaxTaskTimeoutMs { get; set; } = 0;

        /// <summary>Labels advertised by the worker.</summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>Capabilities advertised by the worker.</summary>
        public List<WorkerCapabilityDescriptor> Capabilities { get; set; } = new List<WorkerCapabilityDescriptor>();
    }

    /// <summary>
    /// Server hello acknowledgement.
    /// </summary>
    public class WorkerHelloAckMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.HelloAck"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.HelloAck;

        /// <summary>Worker protocol version. Default: "1.0".</summary>
        public string ProtocolVersion { get; set; } = "1.0";

        /// <summary>Unique identifier of the worker.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Identifier of the established worker session.</summary>
        public string WorkerSessionId { get; set; } = string.Empty;

        /// <summary>Interval in milliseconds between worker heartbeats. Default: 10000 (10 seconds).</summary>
        public int HeartbeatIntervalMs { get; set; } = 10000;

        /// <summary>Timeout in milliseconds after which a missing heartbeat is considered a failure. Default: 30000 (30 seconds).</summary>
        public int HeartbeatTimeoutMs { get; set; } = 30000;

        /// <summary>Duration in milliseconds of an assignment lease. Default: 300000 (5 minutes).</summary>
        public int LeaseDurationMs { get; set; } = 300000;

        /// <summary>Indicates whether the worker should start in drain mode. Default: false.</summary>
        public bool DrainMode { get; set; } = false;
    }

    /// <summary>
    /// Worker heartbeat frame.
    /// </summary>
    public class WorkerHeartbeatMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Heartbeat"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.Heartbeat;

        /// <summary>Unique identifier of the worker.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Identifier of the worker session.</summary>
        public string WorkerSessionId { get; set; } = string.Empty;

        /// <summary>Number of runs currently active on the worker. Default: 0.</summary>
        public int ActiveRuns { get; set; } = 0;

        /// <summary>UTC timestamp at which the heartbeat was sent. Default: current UTC time.</summary>
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Server-to-worker assignment frame.
    /// </summary>
    public class WorkerAssignMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Assign"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.Assign;

        /// <summary>The run assignment record being delivered to the worker.</summary>
        public RunAssignmentRecord Assignment { get; set; } = new RunAssignmentRecord();

        /// <summary>The execution plan for the assigned flow run.</summary>
        public FlowRunExecutionPlan Plan { get; set; } = new FlowRunExecutionPlan();
    }

    /// <summary>
    /// Worker acknowledgement of an assignment delivery.
    /// </summary>
    public class WorkerAssignAckMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.AssignAck"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.AssignAck;

        /// <summary>Unique identifier of the worker.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Identifier of the worker session.</summary>
        public string WorkerSessionId { get; set; } = string.Empty;

        /// <summary>Identifier of the run assignment being acknowledged.</summary>
        public string RunAssignmentId { get; set; } = string.Empty;

        /// <summary>Lease token granted for the assignment.</summary>
        public string LeaseToken { get; set; } = string.Empty;

        /// <summary>Indicates whether the worker accepted the assignment. Default: true.</summary>
        public bool Accepted { get; set; } = true;

        /// <summary>Optional message describing the acknowledgement outcome. Nullable; default: null.</summary>
        public string? Message { get; set; } = null;
    }

    /// <summary>
    /// Worker terminal completion frame.
    /// </summary>
    public class WorkerRunCompletedMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.RunCompleted"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.RunCompleted;

        /// <summary>The completion report for the finished run.</summary>
        public RunCompletionReport Completion { get; set; } = new RunCompletionReport();
    }

    /// <summary>
    /// Server drain command.
    /// </summary>
    public class WorkerDrainMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Drain"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.Drain;

        /// <summary>Unique identifier of the worker to drain.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Optional message describing the drain reason. Nullable; default: null.</summary>
        public string? Message { get; set; } = null;
    }

    /// <summary>
    /// Server resume command.
    /// </summary>
    public class WorkerResumeMessage
    {
        /// <summary>Frame type discriminator. Default: <see cref="WorkerFrameTypes.Resume"/>.</summary>
        public string Type { get; set; } = WorkerFrameTypes.Resume;

        /// <summary>Unique identifier of the worker to resume.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Optional message describing the resume reason. Nullable; default: null.</summary>
        public string? Message { get; set; } = null;
    }
}
