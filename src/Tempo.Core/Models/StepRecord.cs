namespace Tempo.Core.Models
{
    using System;
    using System.Linq;
    using System.Text.Json.Serialization;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;
    using Tempo.Core.Runtime;

    /// <summary>
    /// Persisted step definition. Code-based steps are registered at server startup by scanning
    /// configured assemblies and backfilled into this table; REST steps are fully owned by the DB.
    /// </summary>
    public class StepRecord
    {
        /// <summary>Maximum length for tenant-scoped execution keys.</summary>
        public const int ExecutionKeyMaxLength = 255;

        /// <summary>Step identifier (prefix "step_").</summary>
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

        /// <summary>Tenant identifier (or the string "global" for cross-tenant steps).</summary>
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

        /// <summary>Stable tenant-scoped key used by flows to execute this step.</summary>
        public string ExecutionKey
        {
            get
            {
                return _ExecutionKey;
            }
            set
            {
                _ExecutionKey = ValidateExecutionKey(value);
            }
        }

        /// <summary>Display name.</summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>Optional description.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Runtime provider key used to execute this step.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>Typed provider-specific runtime configuration.</summary>
        public StepRuntimeConfig? RuntimeConfig { get; set; } = null;

        /// <summary>Serialized runtime config used only by storage.</summary>
        [JsonIgnore]
        public string? RuntimeConfigJson { get; set; } = null;

        /// <summary>Core-owned input/output contract kind.</summary>
        public StepContractTypeEnum ContractType { get; set; } = StepContractTypeEnum.Loose;

        /// <summary>Optional JSON schema for step input.</summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>Optional JSON schema for step output.</summary>
        public string? OutputSchema { get; set; } = null;

        /// <summary>Whether core validates input before provider invocation.</summary>
        public bool ValidateInput { get; set; } = false;

        /// <summary>Whether core validates output after provider invocation.</summary>
        public bool ValidateOutput { get; set; } = false;

        /// <summary>Referenced artifact identifier for artifact-backed runtimes.</summary>
        public string? ArtifactId { get; set; } = null;

        /// <summary>Referenced artifact version for artifact-backed runtimes.</summary>
        public string? ArtifactVersion { get; set; } = null;

        /// <summary>Current binding state for runtime reconciliation.</summary>
        public StepRuntimeBindingStateEnum RuntimeBindingState { get; set; } = StepRuntimeBindingStateEnum.Unresolved;

        /// <summary>Optional binding diagnostic message.</summary>
        public string? RuntimeBindingMessage { get; set; } = null;

        /// <summary>How this step is executed.</summary>
        public PersistedStepTypeEnum StepType { get; set; } = PersistedStepTypeEnum.Code;

        /// <summary>Maximum step runtime in milliseconds. 0 means no timeout.</summary>
        public int MaxRuntimeMs
        {
            get
            {
                return _MaxRuntimeMs;
            }
            set
            {
                _MaxRuntimeMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(MaxRuntimeMs));
            }
        }

        /// <summary>
        /// REST step configuration when <see cref="StepType"/> is <c>Rest</c>.
        /// </summary>
        public Tempo.RestStepConfiguration? Rest { get; set; } = null;

        /// <summary>Whether the step is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the step is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateStepId();
        private string _TenantId = String.Empty;
        private string _ExecutionKey = String.Empty;
        private string _Name = "My step";
        private int _MaxRuntimeMs = 0;

        /// <summary>Set a default execution key when loading legacy records or accepting old clients.</summary>
        public void EnsureExecutionKey()
        {
            if (String.IsNullOrWhiteSpace(_ExecutionKey)) ExecutionKey = Name;
        }

        private static string ValidateExecutionKey(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ExecutionKey));

            string trimmed = value.Trim();
            if (trimmed.Length > ExecutionKeyMaxLength) throw new ArgumentOutOfRangeException(nameof(ExecutionKey), "ExecutionKey must be 255 characters or fewer.");
            if (trimmed.Any(char.IsControl)) throw new ArgumentException("ExecutionKey cannot contain control characters.", nameof(ExecutionKey));
            return trimmed;
        }
    }
}
