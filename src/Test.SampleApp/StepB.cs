namespace Test.SampleApp
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step B - Write input data to console.
    /// This is a terminal step that ends the flow.
    /// </summary>
    public class StepB : Step
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        public StepB()
        {
            Name = "Step B - Console Writer";
        }

        public override async Task<StepResult> Run(StepRequest req)
        {
            Console.WriteLine($"[Step B] Input value: {req.Data}");

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

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
