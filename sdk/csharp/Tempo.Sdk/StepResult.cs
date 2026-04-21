namespace Tempo.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>Tempo protocol result envelope emitted by a step handler.</summary>
    public class StepResult
    {
        private string _ProtocolVersion = ProtocolVersions.Current;
        private string _DataFlowId = Ids.DataFlowId();
        private string _RequestId = Ids.RequestId();
        private string? _ExceptionMessage;

        /// <summary>Tempo step protocol version.</summary>
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion
        {
            get => _ProtocolVersion;
            set => _ProtocolVersion = ProtocolVersions.Normalize(value);
        }

        /// <summary>Tenant identifier when running in tenant scope.</summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; }

        /// <summary>Data flow identifier.</summary>
        [JsonPropertyName("dataFlowId")]
        public string DataFlowId
        {
            get => _DataFlowId;
            set => _DataFlowId = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(nameof(DataFlowId));
        }

        /// <summary>Flow run identifier when tied to a persisted run.</summary>
        [JsonPropertyName("flowRunId")]
        public string? FlowRunId { get; set; }

        /// <summary>Step run identifier when tied to a persisted step run.</summary>
        [JsonPropertyName("stepRunId")]
        public string? StepRunId { get; set; }

        /// <summary>Request correlation identifier.</summary>
        [JsonPropertyName("requestId")]
        public string RequestId
        {
            get => _RequestId;
            set => _RequestId = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(nameof(RequestId));
        }

        /// <summary>Step result state.</summary>
        [JsonPropertyName("result")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StepResultType Result { get; set; } = StepResultType.Success;

        /// <summary>User data emitted by the step.</summary>
        [JsonPropertyName("data")]
        public object? Data { get; set; }

        /// <summary>Exception object used locally by handlers.</summary>
        [JsonIgnore]
        public Exception? Exception { get; set; }

        /// <summary>Protocol-safe exception text.</summary>
        [JsonPropertyName("exception")]
        public string? ExceptionMessage
        {
            get => _ExceptionMessage ?? Exception?.Message;
            set => _ExceptionMessage = value;
        }

        /// <summary>Metadata emitted by the step.</summary>
        [JsonPropertyName("metadata")]
        public object? Metadata { get; set; }
    }
}
