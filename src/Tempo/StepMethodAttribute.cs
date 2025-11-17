namespace Tempo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Attribute to mark a static method as a step.
    /// The method must have signature: static Task&lt;StepResult&gt; MethodName(StepRequest req)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class StepMethodAttribute : Attribute
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Step identifier used to reference this step in DataFlows.
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Identifier)));
        }

        /// <summary>
        /// Tenant identifier for multi-tenant isolation.
        /// If null, the step is available to all tenants.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Maximum runtime in milliseconds (0 for no timeout).
        /// </summary>
        public int MaxRuntimeMs { get; set; } = 0;

        private string _Identifier = null;

        /// <summary>
        /// Step method attribute.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        public StepMethodAttribute(string identifier)
        {
            _Identifier = (!String.IsNullOrEmpty(identifier) ? identifier : throw new ArgumentNullException(nameof(identifier)));
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
