namespace Tempo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Information about an attribute-based code step (static method decorated with [StepMethod]).
    /// Used by StepManager to track and instantiate CodeAttributeStepRunner instances.
    /// </summary>
    public class CodeAttributeStepInfo
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        /// <summary>
        /// Gets or sets the step identifier.
        /// This is the unique identifier used to reference the step in DataFlow transitions.
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Gets or sets the method info for the static method to invoke.
        /// The method must have signature: static Task&lt;StepResult&gt; MethodName(StepRequest req).
        /// </summary>
        public MethodInfo Method { get; set; }

        /// <summary>
        /// Gets or sets the tenant identifier for multi-tenant isolation.
        /// Null indicates the step is available to all tenants.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Gets or sets the maximum runtime in milliseconds.
        /// Zero indicates no timeout.
        /// </summary>
        public int MaxRuntimeMs { get; set; }

        /// <summary>
        /// Initializes a new instance of the CodeAttributeStepInfo class.
        /// </summary>
        public CodeAttributeStepInfo()
        {
        }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
