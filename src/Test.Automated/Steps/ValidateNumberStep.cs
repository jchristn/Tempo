namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Native code step that validates if a number is within range.
    /// </summary>
    public class ValidateNumberStep : Step
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        private readonly int _minValue;
        private readonly int _maxValue;

        /// <summary>
        /// Validate number step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="minValue">Minimum valid value.</param>
        /// <param name="maxValue">Maximum valid value.</param>
        public ValidateNumberStep(string identifier, string tenantId, int minValue = 0, int maxValue = 1000) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
            Name = "Validate Number Step";
            _minValue = minValue;
            _maxValue = maxValue;
        }

        /// <summary>
        /// Run the step - validates number is in range.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            await Task.Delay(10); // Simulate work

            try
            {
                int value = 0;

                if (req.Data is int intVal)
                {
                    value = intVal;
                }
                else if (int.TryParse(req.Data?.ToString(), out int parsedVal))
                {
                    value = parsedVal;
                }

                bool isValid = value >= _minValue && value <= _maxValue;

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = isValid ? StepResultTypeEnum.Success : StepResultTypeEnum.Error,
                    Data = value,
                    Metadata = req.Metadata
                };
            }
            catch (Exception ex)
            {
                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Exception,
                    Exception = ex,
                    Data = null,
                    Metadata = req.Metadata
                };
            }
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
