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
    /// Step result.
    /// </summary>
    public class StepResult
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
        /// Flow run identifier, when the result is tied to a persisted flow run.
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
        /// Step result.
        /// </summary>
        [JsonPropertyName("result")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StepResultTypeEnum Result { get; set; } = StepResultTypeEnum.Success;

        /// <summary>
        /// Request data.
        /// </summary>
        [JsonPropertyName("data")]
        public object Data { get; set; } = null;

        /// <summary>
        /// Exception data.
        /// </summary>
        [JsonIgnore]
        public Exception Exception { get; set; } = null;

        /// <summary>
        /// Protocol-safe exception text.
        /// </summary>
        [JsonPropertyName("exception")]
        public string? ExceptionMessage
        {
            get => _ExceptionMessage ?? Exception?.Message;
            set => _ExceptionMessage = value;
        }

        /// <summary>
        /// Metadata.
        /// </summary>
        [JsonPropertyName("metadata")]
        public object Metadata { get; set; } = null;

        private string _ProtocolVersion = ProtocolVersions.Current;
        private string _DataFlowId = TempoIds.GenerateDataFlowId();
        private string _RequestId = TempoIds.GenerateRequestId();
        private string? _ExceptionMessage = null;

        /// <summary>
        /// Step result.
        /// </summary>
        public StepResult()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
