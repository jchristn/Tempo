namespace Test.SampleApp
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step E - Log the input exception to the console.
    /// This is a terminal step that ends the flow.
    /// </summary>
    public class StepE : Step
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8601 // Possible null reference assignment.

        public StepE()
        {
            Name = "Step E - Exception Logger";
        }

        public override async Task<StepResult> Run(StepRequest req)
        {
            // The exception is passed via the Metadata field
            if (req.Metadata != null && req.Metadata is Exception ex)
            {
                Console.WriteLine($"[Step E] Exception occurred: {ex.ToString()}");
            }
            else
            {
                Console.WriteLine($"[Step E] Exception handling step triggered. Metadata: {req.Metadata}");
            }

            // Honor and carry forward the previous result type
            StepResultTypeEnum resultType = req.PreviousResult ?? StepResultTypeEnum.Success;

            StepResult result = new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Data = null,
                Metadata = req.Metadata,
                Result = resultType
            };

            return await Task.FromResult(result);
        }

#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
