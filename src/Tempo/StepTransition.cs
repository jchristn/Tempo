namespace Tempo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Enums;

    /// <summary>
    /// Step transition.
    /// </summary>
    public class StepTransition
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Name of the step transition.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Step to invoke on success.  
        /// Use null to terminate the data flow.
        /// </summary>
        public string OnSuccess { get; set; } = null;

        /// <summary>
        /// Step to invoke on failure.
        /// Use null to terminate the data flow.
        /// </summary>
        public string OnFailure { get; set; } = null;

        /// <summary>
        /// Step to invoke on exception.
        /// Use null to terminate the data flow.
        /// </summary>
        public string OnException { get; set; } = null;

        /// <summary>
        /// Maximum number of times this transition can be used.
        /// Set to 0 to allow unlimited transitions.
        /// </summary>
        public int MaxTransitions
        {
            get => _MaxTransitions;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MaxTransitions), "MaxTransitions must be greater than or equal to 0.");
                _MaxTransitions = value;
            }
        }

        /// <summary>
        /// Step type for inline steps.
        /// When null, the step is looked up from StepManager by Name (code-based step).
        /// When set, the step is created dynamically using the corresponding configuration property.
        /// </summary>
        public StepTypeEnum? StepType { get; set; } = null;

        /// <summary>
        /// REST API step configuration (when StepType is "rest").
        /// </summary>
        public RestStepConfiguration Rest { get; set; } = null;

        private int _MaxTransitions = 0;

        /// <summary>
        /// Step transition.
        /// </summary>
        public StepTransition()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
