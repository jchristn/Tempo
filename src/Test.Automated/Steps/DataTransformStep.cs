namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that transforms data by appending a suffix.
    /// </summary>
    public class DataTransformStep : Step
    {
        private readonly string _suffix;

        /// <summary>
        /// Data transform step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="suffix">Suffix to append to data.</param>
        public DataTransformStep(string identifier, string tenantId, string suffix) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
            _suffix = suffix;
        }

        /// <summary>
        /// Run the step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            await Task.Delay(10);

            string currentData = req.Data?.ToString() ?? string.Empty;
            string transformedData = currentData + _suffix;

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = transformedData,
                Metadata = req.Metadata
            };
        }
    }
}
