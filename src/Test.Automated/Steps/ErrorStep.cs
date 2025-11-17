namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that always returns an error.
    /// </summary>
    public class ErrorStep : Step
    {
        /// <summary>
        /// Error step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        public ErrorStep(string identifier, string tenantId) : base()
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
            await Task.Delay(10); // Simulate some work

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Error,
                Data = "Error occurred",
                Metadata = req.Metadata
            };
        }
    }
}
