namespace Tempo.Triggers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Tempo.Enums;

    /// <summary>
    /// Trigger.
    /// </summary>
    public class Trigger
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Identifier));
        }

        /// <summary>
        /// Name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Name));
        }

        /// <summary>
        /// Trigger type.
        /// </summary>
        public TriggerTypeEnum Type { get; set; } = TriggerTypeEnum.Http;

        /// <summary>
        /// Creation timestamp, in UTC.
        /// </summary>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value.ToUniversalTime();
        }

        private string _Identifier = Tempo.TempoIds.GenerateTriggerId();
        private string _Name = "My trigger";
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// Trigger.
        /// </summary>
        public Trigger()
        {

        }
    }
}
