namespace Test.Automated.Steps
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using RestWrapper;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Step that receives a random number, fetches another from randomnumberapi.com, and adds them.
    /// </summary>
    public class AddRandomNumbersStep : Step
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

        /// <summary>
        /// Add random numbers step.
        /// </summary>
        /// <param name="identifier">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        public AddRandomNumbersStep(string identifier, string tenantId) : base()
        {
            Identifier = identifier;
            TenantId = tenantId;
            Name = "Add Random Numbers Step";
        }

        /// <summary>
        /// Run the step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <returns>Step result.</returns>
        public override async Task<StepResult> Run(StepRequest req)
        {
            try
            {
                Console.WriteLine($"  [AddRandomNumbersStep] Executing...");

                // Extract the first random number from the previous REST step
                int firstNumber = ExtractNumberFromPreviousStep(req.Data);
                Console.WriteLine($"  [AddRandomNumbersStep] Received first number from previous step: {firstNumber}");

                // Make another REST call to get a second random number
                Console.WriteLine($"  [AddRandomNumbersStep] Fetching second random number from API...");
                int secondNumber = await FetchRandomNumber();
                Console.WriteLine($"  [AddRandomNumbersStep] Received second number from API: {secondNumber}");

                // Add the numbers
                int sum = firstNumber + secondNumber;
                Console.WriteLine($"  [AddRandomNumbersStep] Sum: {firstNumber} + {secondNumber} = {sum}");

                // Return result with the sum
                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Success,
                    Data = new Dictionary<string, object>
                    {
                        ["FirstNumber"] = firstNumber,
                        ["SecondNumber"] = secondNumber,
                        ["Sum"] = sum
                    },
                    Metadata = req.Metadata
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [AddRandomNumbersStep] ERROR: {ex.Message}");
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

        /// <summary>
        /// Extract the random number from the previous REST step result.
        /// </summary>
        /// <param name="data">Data from previous step.</param>
        /// <returns>Random number.</returns>
        private int ExtractNumberFromPreviousStep(object data)
        {
            if (data == null)
                throw new InvalidOperationException("No data received from previous step");

            // The data should be a Dictionary from the REST step
            if (data is Dictionary<string, object> resultDict)
            {
                // Extract the response body
                if (!resultDict.TryGetValue("ResponseBody", out object responseBodyObj) || responseBodyObj == null)
                    throw new InvalidOperationException("No ResponseBody in previous step data");

                byte[] responseBytes = responseBodyObj as byte[];
                if (responseBytes == null || responseBytes.Length == 0)
                    throw new InvalidOperationException("ResponseBody is empty");

                string responseJson = Encoding.UTF8.GetString(responseBytes);
                Console.WriteLine($"  [AddRandomNumbersStep] Parsing response: {responseJson}");

                // Parse the JSON array - randomnumberapi returns an array like [42]
                int[] numbers = JsonSerializer.Deserialize<int[]>(responseJson);
                if (numbers == null || numbers.Length == 0)
                    throw new InvalidOperationException("Failed to parse random number from API response");

                return numbers[0];
            }

            throw new InvalidOperationException($"Unexpected data type from previous step: {data.GetType()}");
        }

        /// <summary>
        /// Fetch a random number from randomnumberapi.com.
        /// </summary>
        /// <returns>Random number.</returns>
        private async Task<int> FetchRandomNumber()
        {
            // Call randomnumberapi.com to get a random number between 1 and 100
            string url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1";

            RestRequest request = new RestRequest(url, System.Net.Http.HttpMethod.Get);
            RestResponse response = await request.SendAsync();

            if (response.StatusCode < 200 || response.StatusCode >= 300)
            {
                throw new Exception($"API request failed with status {response.StatusCode}: {response.DataAsString}");
            }

            // Parse the response - API returns array like [42]
            string responseJson = response.DataAsString ?? "[]";
            int[] numbers = JsonSerializer.Deserialize<int[]>(responseJson);

            if (numbers == null || numbers.Length == 0)
                throw new Exception("Failed to parse random number from API response");

            return numbers[0];
        }

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
