namespace Tempo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Tempo.Enums;
    using Tempo.Protocol;

    /// <summary>
    /// Step request.
    /// </summary>
    public class StepRequest
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Tempo step protocol version.
        /// </summary>
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion
        {
            get => _ProtocolVersion;
            set => _ProtocolVersion = ProtocolVersions.Normalize(value);
        }

        /// <summary>
        /// Tenant identifier, when the host is running in a tenant-scoped context.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Data flow identifier.
        /// </summary>
        [JsonPropertyName("dataFlowId")]
        public string DataFlowId
        {
            get => _DataFlowId;
            set => _DataFlowId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(DataFlowId)));
        }

        /// <summary>
        /// Flow run identifier, when the request is tied to a persisted flow run.
        /// </summary>
        [JsonPropertyName("flowRunId")]
        public string? FlowRunId { get; set; } = null;

        /// <summary>
        /// Step run identifier, when the host pre-allocates a persisted step run.
        /// </summary>
        [JsonPropertyName("stepRunId")]
        public string? StepRunId { get; set; } = null;

        /// <summary>
        /// Request identifier.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId
        {
            get => _RequestId;
            set => _RequestId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(RequestId)));
        }

        /// <summary>
        /// Request data.
        /// </summary>
        [JsonPropertyName("data")]
        public object Data { get; set; } = null;

        /// <summary>
        /// Metadata.
        /// </summary>
        [JsonPropertyName("metadata")]
        public object Metadata { get; set; } = null;

        /// <summary>
        /// Result type from the previous step in the data flow.
        /// Null if this is the first step.
        /// </summary>
        [JsonPropertyName("previousResult")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StepResultTypeEnum? PreviousResult { get; set; } = null;

        private string _ProtocolVersion = ProtocolVersions.Current;
        private string _DataFlowId = TempoIds.GenerateDataFlowId();
        private string _RequestId = TempoIds.GenerateRequestId();

        /// <summary>
        /// Step request.
        /// </summary>
        public StepRequest()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
