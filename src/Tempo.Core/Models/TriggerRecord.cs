namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;
    using Tempo.Enums;

    /// <summary>
    /// Persisted trigger definition.
    /// </summary>
    public class TriggerRecord
    {
        /// <summary>Trigger identifier (prefix "trg_").</summary>
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

        /// <summary>Trigger type.</summary>
        public TriggerTypeEnum TriggerType { get; set; } = TriggerTypeEnum.Http;

        /// <summary>Optional flow identifier fired when this trigger hits.</summary>
        public string? DataFlowId { get; set; } = null;

        /// <summary>
        /// Opaque trigger configuration serialized as JSON, interpreted based on <see cref="TriggerType"/>.
        /// </summary>
        public string? Configuration { get; set; } = null;

        /// <summary>Whether the trigger is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the trigger is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateTriggerId();
        private string _TenantId = String.Empty;
        private string _Name = "My trigger";
    }
}
