namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that counts how many times it's been called and eventually succeeds.
    /// </summary>
    public class CountingStep : Step
    {
        private int _callCount = 0;
        private readonly int _successAfterCalls;

        /// <summary>
        /// Counting step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="successAfterCalls">Number of calls before returning success.</param>
        public CountingStep(string identifier, string tenantId, int successAfterCalls = 5) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
            _successAfterCalls = successAfterCalls;
        }

        /// <summary>
        /// Run the step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            await Task.Delay(10);
            _callCount++;

            if (_callCount >= _successAfterCalls)
            {
                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Success,
                    Data = $"Succeeded after {_callCount} calls",
                    Metadata = req.Metadata
                };
            }
            else
            {
                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Error,
                    Data = $"Call {_callCount} of {_successAfterCalls}",
                    Metadata = req.Metadata
                };
            }
        }
    }
}
