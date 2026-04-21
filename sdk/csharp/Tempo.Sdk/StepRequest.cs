namespace Tempo.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>Tempo protocol request envelope sent to a step handler.</summary>
    public class StepRequest
    {
        private string _ProtocolVersion = ProtocolVersions.Current;
        private string _DataFlowId = Ids.DataFlowId();
        private string _RequestId = Ids.RequestId();

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

        /// <summary>User data for the step.</summary>
        [JsonPropertyName("data")]
        public object? Data { get; set; }

        /// <summary>Metadata for the step.</summary>
        [JsonPropertyName("metadata")]
        public object? Metadata { get; set; }

        /// <summary>Previous step result, or null when this is the first step.</summary>
        [JsonPropertyName("previousResult")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StepResultType? PreviousResult { get; set; }
    }
}
