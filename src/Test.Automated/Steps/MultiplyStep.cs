namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Native code step that multiplies a number by 3.
    /// </summary>
    public class MultiplyStep : Step
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Multiply step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        public MultiplyStep(string identifier, string tenantId) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
            Name = "Multiply Step";
        }

        /// <summary>
        /// Run the step - multiplies input by 3.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            await Task.Delay(10); // Simulate work

            try
            {
                int inputValue = 0;

                if (req.Data is int intVal)
                {
                    inputValue = intVal;
                }
                else if (int.TryParse(req.Data?.ToString(), out int parsedVal))
                {
                    inputValue = parsedVal;
                }

                int result = inputValue * 3;

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Success,
                    Data = result,
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
