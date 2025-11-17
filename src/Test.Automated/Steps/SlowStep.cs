namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that takes a specified amount of time to complete.
    /// </summary>
    public class SlowStep : Step
    {
        private readonly int _sleepMs;

        /// <summary>
        /// Slow step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="sleepMs">Milliseconds to sleep.</param>
        public SlowStep(string identifier, string tenantId, int sleepMs = 100) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
            _sleepMs = sleepMs;
            MaxRuntimeMs = 0; // No step-level timeout
        }

        /// <summary>
        /// Run the step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            // Sleep for the specified time
            await Task.Delay(_sleepMs);

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
