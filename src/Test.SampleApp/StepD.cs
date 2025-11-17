namespace Test.SampleApp
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step D - Test if the input number is a prime number.
    /// Returns success if prime, failure if not prime.
    /// </summary>
    public class StepD : Step
    {
        public StepD()
        {
            Name = "Step D - Prime Number Test";
        }

        public override async Task<StepResult> Run(StepRequest req)
        {
            int inputValue = Convert.ToInt32(req.Data);
            bool isPrime = IsPrime(inputValue);

            Console.WriteLine($"[Step D] Testing {inputValue}: {(isPrime ? "IS PRIME" : "NOT PRIME")}");

            StepResult stepResult = new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Data = inputValue,
                Metadata = req.Metadata,
                Result = StepResultTypeEnum.Success
            };

            return await Task.FromResult(stepResult);
        }

        private bool IsPrime(int number)
        {
            if (number <= 1) return false;
            if (number == 2) return true;
            if (number % 2 == 0) return false;

            int boundary = (int)Math.Floor(Math.Sqrt(number));

            for (int i = 3; i <= boundary; i += 2)
            {
                if (number % i == 0)
                    return false;
            }

            return true;
        }
    }
}
