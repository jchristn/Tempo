namespace Test.SampleApp
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step A - Generate a random number between -2 and 4.
    /// Returns success if > 0, failure if < 0, exception if == 0.
    /// </summary>
    public class StepA : Step
    {
        public StepA()
        {
            Name = "Step A - Random Number Generator";
        }

        public override async Task<StepResult> Run(StepRequest req)
        {
            Random random = new Random();
            int randomNumber = random.Next(-2, 5); // Upper bound is exclusive, so 5 gives us -2 to 4

            Console.WriteLine($"[Step A] Generated random number: {randomNumber}");

            StepResult result = new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Data = randomNumber,
                Metadata = req.Metadata
            };

            if (randomNumber > 0)
            {
                result.Result = StepResultTypeEnum.Success;
            }
            else if (randomNumber < 0)
            {
                result.Result = StepResultTypeEnum.Error;
            }
            else // randomNumber == 0
            {
                Exception ex = new Exception("Random number was zero");
                result.Result = StepResultTypeEnum.Exception;
                result.Exception = ex;
                result.Metadata = ex; // Pass exception via metadata so next step can access it
            }

            return await Task.FromResult(result);
        }
    }
}
