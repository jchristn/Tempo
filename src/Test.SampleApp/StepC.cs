namespace Test.SampleApp
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step C - Multiply the number by two.
    /// Always returns success.
    /// </summary>
    public class StepC : Step
    {
        public StepC()
        {
            Name = "Step C - Multiply by Two";
        }

        public override async Task<StepResult> Run(StepRequest req)
        {
            int inputValue = Convert.ToInt32(req.Data);
            int result = inputValue * 2;

            Console.WriteLine($"[Step C] Input: {inputValue}, Output: {result}");

            StepResult stepResult = new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Data = result,
                Metadata = req.Metadata,
                Result = StepResultTypeEnum.Success
            };

            return await Task.FromResult(stepResult);
        }
    }
}
