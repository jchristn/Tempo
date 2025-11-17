namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that recovers from errors/exceptions.
    /// </summary>
    public class RecoveryStep : Step
    {
        /// <summary>
        /// Recovery step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        public RecoveryStep(string identifier, string tenantId) : base()
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
            await Task.Delay(10); // Simulate recovery work

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = "Recovered from previous error",
                Metadata = req.Metadata
            };
        }
    }
}
