namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Enums;
    using Tempo.Protocol;

    /// <summary>
    /// Execution record for a single step within a <see cref="FlowRun"/>.
    /// </summary>
    public class StepRun
    {
        /// <summary>Step run identifier (prefix "sru_").</summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>Tenant identifier.</summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>Parent flow run identifier.</summary>
        public string FlowRunId
        {
            get
            {
                return _FlowRunId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FlowRunId));
                _FlowRunId = value;
            }
        }

        /// <summary>Data flow identifier.</summary>
        public string DataFlowId
        {
            get
            {
                return _DataFlowId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(DataFlowId));
                _DataFlowId = value;
            }
        }

        /// <summary>Logical step identifier as referenced in the flow's transitions map.</summary>
        public string StepId
        {
            get
            {
                return _StepId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(StepId));
                _StepId = value;
            }
        }

        /// <summary>Step result type.</summary>
        public StepResultTypeEnum Result { get; set; } = StepResultTypeEnum.Success;

        /// <summary>Next step identifier chosen after this step, or null for terminal.</summary>
        public string? NextStepId { get; set; } = null;

        /// <summary>Input data serialized as JSON.</summary>
        public string? InputData { get; set; } = null;

        /// <summary>Output data serialized as JSON.</summary>
        public string? OutputData { get; set; } = null;

        /// <summary>Exception text if the step raised one.</summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>Artifact identifier executed by this step run, when artifact-backed.</summary>
        public string? ArtifactId { get; set; } = null;

        /// <summary>Resolved artifact version row identifier executed by this step run.</summary>
        public string? ArtifactVersionId { get; set; } = null;

        /// <summary>Resolved artifact version label executed by this step run.</summary>
        public string? ArtifactVersion { get; set; } = null;

        /// <summary>Resolved artifact content SHA-256 executed by this step run.</summary>
        public string? ArtifactSha256 { get; set; } = null;

        /// <summary>Manifest entrypoint executed by this step run.</summary>
        public string? ManifestEntrypoint { get; set; } = null;

        /// <summary>Execution lifecycle state for this step run row.</summary>
        public StepRunExecutionStateEnum ExecutionState { get; set; } = StepRunExecutionStateEnum.Complete;

        /// <summary>Negotiated step protocol version used for this step run.</summary>
        public string ProtocolVersion
        {
            get
            {
                return _ProtocolVersion;
            }
            set
            {
                _ProtocolVersion = ProtocolVersions.Normalize(value);
            }
        }

        /// <summary>Zero-based order in which the step ran within its parent flow run.</summary>
        public int Sequence { get; set; } = 0;

        /// <summary>UTC time the step started.</summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC time the step began waiting for external runtime capacity.</summary>
        public DateTime? CapacityQueuedUtc { get; set; } = null;

        /// <summary>UTC time the step acquired external runtime capacity.</summary>
        public DateTime? CapacityAcquiredUtc { get; set; } = null;

        /// <summary>External runtime capacity wait duration in milliseconds.</summary>
        public long? CapacityWaitMs { get; set; } = null;

        /// <summary>UTC time the step completed.</summary>
        public DateTime? CompletedUtc { get; set; } = null;

        private string _Id = IdGenerator.GenerateStepRunId();
        private string _TenantId = String.Empty;
        private string _FlowRunId = String.Empty;
        private string _DataFlowId = String.Empty;
        private string _StepId = String.Empty;
        private string _ProtocolVersion = ProtocolVersions.Current;
    }
}
