namespace Tempo.Core.Requests
{
    using Tempo.Core.Runtime;

    /// <summary>Request body for validating runtime config.</summary>
    public class RuntimeValidationRequest
    {
        /// <summary>Key identifying the runtime whose config is being validated.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>The runtime configuration to validate. Default: null.</summary>
        public StepRuntimeConfig? Config { get; set; }
    }
}
