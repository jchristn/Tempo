namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that always throws an exception.
    /// </summary>
    public class ExceptionStep : Step
    {
        /// <summary>
        /// Exception step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        public ExceptionStep(string identifier, string tenantId) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
        }

        /// <summary>
        /// Run the step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            await Task.Delay(10); // Simulate some work before throwing
            throw new InvalidOperationException("This step always throws an exception");
        }
    }
}
