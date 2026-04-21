namespace Tempo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// A step is an individual unit of work within a data flow.  A step can be re-used across data flows.
    /// </summary>
    public abstract class Step
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Identifier.
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Identifier)));
        }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(TenantId)));
        }

        /// <summary>
        /// Tenant object.  This property is only used in metadata expansion.  Do not use directly.
        /// </summary>
        public Tenant Tenant { get; set; } = null;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Name)));
        }

        /// <summary>
        /// Maximum runtime in milliseconds.
        /// </summary>
        public int MaxRuntimeMs
        {
            get => _MaxRuntimeMs;
            set => _MaxRuntimeMs = (value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(MaxRuntimeMs)));
        }

        /// <summary>
        /// Creation timestamp, in UTC.
        /// </summary>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value.ToUniversalTime();
        }

        private string _Identifier = TempoIds.GenerateStepId();
        private string _TenantId = null;
        private string _Name = "My step";
        private int _MaxRuntimeMs = 0;
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// A step is an individual unit of work within a data flow.  A step can be re-used across data flows.
        /// </summary>
        public Step()
        {

        }

        /// <summary>
        /// Run this step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public abstract Task<StepResult> Run(StepRequest req);

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type. 
    }
}
