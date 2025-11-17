namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that runs longer than its timeout.
    /// </summary>
    public class TimeoutStep : Step
    {
        /// <summary>
        /// Timeout step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="maxRuntimeMs">Maximum runtime in milliseconds.</param>
        public TimeoutStep(string identifier, string tenantId, int maxRuntimeMs = 100) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
            MaxRuntimeMs = maxRuntimeMs;
        }

        /// <summary>
        /// Run the step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            // Sleep for longer than the timeout
            await Task.Delay(MaxRuntimeMs * 2);

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = req.Data,
                Metadata = req.Metadata
            };
        }
    }
}
