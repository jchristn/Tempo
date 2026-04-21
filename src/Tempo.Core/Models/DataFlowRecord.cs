namespace Tempo.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Persisted data flow definition. Transitions are stored as a keyed dictionary mirroring
    /// the in-memory <see cref="Tempo.DataFlow.Steps"/> structure.
    /// </summary>
    public class DataFlowRecord
    {
        /// <summary>Data flow identifier (prefix "flow_").</summary>
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

        /// <summary>Trigger identifier associated with this flow (optional).</summary>
        public string? TriggerId { get; set; } = null;

        /// <summary>Execution key of the starting step (must appear as a key in <see cref="Transitions"/>).</summary>
        public string StartStepId
        {
            get
            {
                return _StartStepId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(StartStepId));
                _StartStepId = value;
            }
        }

        /// <summary>Maximum flow runtime in milliseconds. 0 means no timeout.</summary>
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
        /// Step transitions keyed by stable execution key. Values mirror the shape of <see cref="Tempo.StepTransition"/>.
        /// </summary>
        public Dictionary<string, Tempo.StepTransition> Transitions { get; set; } = new Dictionary<string, Tempo.StepTransition>();

        /// <summary>Whether the flow is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the flow is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateDataFlowId();
        private string _TenantId = String.Empty;
        private string _Name = "My flow";
        private string _StartStepId = "start";
        private int _MaxRuntimeMs = 0;
    }
}
