namespace Tempo.Core.Requests
{
    using Tempo.Core.Runtime;

    /// <summary>Request body for validating runtime config.</summary>
    public class RuntimeValidationRequest
    {
        public RuntimeKey RuntimeKey { get; set; }
        public StepRuntimeConfig? Config { get; set; }
    }
}
