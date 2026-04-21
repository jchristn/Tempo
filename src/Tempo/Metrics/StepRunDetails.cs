namespace Tempo.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Enums;
    using Tempo.Protocol;

    /// <summary>
    /// Step run details.
    /// </summary>
    public class StepRunDetails
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Row identifier.
        /// </summary>
        public string RowId { get; set; } = null;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(TenantId));
        }

        /// <summary>
        /// Data flow identifier.
        /// </summary>
        public string DataFlowId
        {
            get => _DataFlowId;
            set => _DataFlowId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(DataFlowId));
        }

        /// <summary>
        /// Step identifier.
        /// </summary>
        public string StepId
        {
            get => _StepId;
            set => _StepId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(StepId));
        }

        /// <summary>
        /// Request identifier.
        /// </summary>
        public string RequestId
        {
            get => _RequestId;
            set => _RequestId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(RequestId));
        }

        /// <summary>
        /// Next step identifier.
        /// </summary>
        public string NextStepId { get; set; } = null;

        /// <summary>
        /// Start time, in UTC time.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End time, in UTC time.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total runtime in milliseconds.
        /// </summary>
        public double TotalMs
        {
            get => (EndUtc - StartUtc).TotalMilliseconds;
        }

        /// <summary>
        /// Result.
        /// </summary>
        public StepResultTypeEnum Result { get; set; } = StepResultTypeEnum.Success;

        /// <summary>Exception or diagnostic text.</summary>
        public string ExceptionMessage { get; set; } = null;

        /// <summary>Execution lifecycle state.</summary>
        public string ExecutionState { get; set; } = "Complete";

        /// <summary>Artifact identifier executed by this step run.</summary>
        public string ArtifactId { get; set; } = null;

        /// <summary>Resolved artifact version row identifier.</summary>
        public string ArtifactVersionId { get; set; } = null;

        /// <summary>Resolved artifact version label.</summary>
        public string ArtifactVersion { get; set; } = null;

        /// <summary>Resolved artifact SHA-256.</summary>
        public string ArtifactSha256 { get; set; } = null;

        /// <summary>Manifest entrypoint name.</summary>
        public string ManifestEntrypoint { get; set; } = null;

        /// <summary>UTC time the step began waiting for external runtime capacity.</summary>
        public DateTime? CapacityQueuedUtc { get; set; } = null;

        /// <summary>UTC time the step acquired external runtime capacity.</summary>
        public DateTime? CapacityAcquiredUtc { get; set; } = null;

        /// <summary>External runtime capacity wait duration in milliseconds.</summary>
        public long? CapacityWaitMs { get; set; } = null;

        /// <summary>
        /// Negotiated step protocol version used for this step run.
        /// </summary>
        public string ProtocolVersion
        {
            get => _ProtocolVersion;
            set => _ProtocolVersion = ProtocolVersions.Normalize(value);
        }

        private string _TenantId = Tempo.TempoIds.GenerateTenantId();
        private string _DataFlowId = Tempo.TempoIds.GenerateDataFlowId();
        private string _StepId = Tempo.TempoIds.GenerateStepId();
        private string _RequestId = Tempo.TempoIds.GenerateRequestId();
        private string _ProtocolVersion = ProtocolVersions.Current;

        /// <summary>
        /// Step run details.
        /// </summary>
        public StepRunDetails()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
