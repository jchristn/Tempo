namespace Test.Automated.Steps
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Example static methods decorated with [StepMethod] attribute.
    /// These demonstrate method-based steps as an alternative to class-based steps.
    /// </summary>
    public static class MethodBasedSteps
    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8601 // Possible null reference assignment.

        /// <summary>
        /// Simple validation step implemented as a static method.
        /// </summary>
        [StepMethod("method_validate")]
        public static async Task<StepResult> ValidateData(StepRequest req)
        {
            await Task.Delay(10); // Simulate work

            // Simple validation logic
            bool isValid = req.Data != null;

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = isValid ? StepResultTypeEnum.Success : StepResultTypeEnum.Error,
                Data = isValid ? req.Data : "Validation failed",
                Metadata = req.Metadata
            };
        }

        /// <summary>
        /// Transformation step that multiplies numbers.
        /// </summary>
        [StepMethod("method_multiply", MaxRuntimeMs = 5000)]
        public static async Task<StepResult> MultiplyNumbers(StepRequest req)
        {
            await Task.Delay(10);

            int result = 0;
            if (req.Data is int number)
            {
                result = number * 2;
            }
            else if (int.TryParse(req.Data?.ToString(), out int parsed))
            {
                result = parsed * 2;
            }

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = result,
                Metadata = req.Metadata
            };
        }

        /// <summary>
        /// Final step that formats output.
        /// </summary>
        [StepMethod("method_format")]
        public static async Task<StepResult> FormatOutput(StepRequest req)
        {
            await Task.Delay(10);

            string formatted = $"Processed: {req.Data}";

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = formatted,
                Metadata = req.Metadata
            };
        }

        /// <summary>
        /// Example of a tenant-specific step.
        /// </summary>
        [StepMethod("tenant_method_step", TenantId = "tenant_abc123")]
        public static async Task<StepResult> TenantSpecificStep(StepRequest req)
        {
            await Task.Delay(10);

            return new StepResult
            {
                DataFlowId = req.DataFlowId,
                RequestId = req.RequestId,
                Result = StepResultTypeEnum.Success,
                Data = "Tenant-specific processing complete",
                Metadata = req.Metadata
            };
        }

        /// <summary>
        /// Parse first random number from REST API response.
        /// </summary>
        [StepMethod("parse_first_number")]
        public static async Task<StepResult> ParseFirstNumber(StepRequest req)
        {
            await Task.Delay(10);

            try
            {
                if (req.Data is Dictionary<string, object> resultDict &&
                    resultDict.TryGetValue("ResponseBody", out object responseBodyObj) &&
                    responseBodyObj is byte[] responseBytes)
                {
                    string responseJson = System.Text.Encoding.UTF8.GetString(responseBytes);
                    int[] numbers = System.Text.Json.JsonSerializer.Deserialize<int[]>(responseJson);

                    if (numbers != null && numbers.Length > 0)
                    {
                        // Store first number in metadata for later use
                        Dictionary<string, object> metadata = req.Metadata != null && req.Metadata is Dictionary<string, object> existingMetadata
                            ? new Dictionary<string, object>(existingMetadata)
                            : new Dictionary<string, object>();
                        metadata["FirstNumber"] = numbers[0];

                        return new StepResult
                        {
                            DataFlowId = req.DataFlowId,
                            RequestId = req.RequestId,
                            Result = StepResultTypeEnum.Success,
                            Data = numbers[0],
                            Metadata = metadata
                        };
                    }
                }

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Error,
                    Data = "Failed to parse first random number",
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

        /// <summary>
        /// Parse second random number from REST API response and combine with first.
        /// </summary>
        [StepMethod("parse_second_number")]
        public static async Task<StepResult> ParseSecondNumber(StepRequest req)
        {
            await Task.Delay(10);

            try
            {
                if (req.Data is Dictionary<string, object> resultDict &&
                    resultDict.TryGetValue("ResponseBody", out object responseBodyObj) &&
                    responseBodyObj is byte[] responseBytes)
                {
                    string responseJson = System.Text.Encoding.UTF8.GetString(responseBytes);
                    int[] numbers = System.Text.Json.JsonSerializer.Deserialize<int[]>(responseJson);

                    if (numbers != null && numbers.Length > 0)
                    {
                        int secondNumber = numbers[0];

                        // Retrieve first number from metadata
                        int firstNumber = 0;
                        if (req.Metadata != null && req.Metadata is Dictionary<string, object> metadata && metadata.ContainsKey("FirstNumber"))
                        {
                            firstNumber = Convert.ToInt32(metadata["FirstNumber"]);
                        }

                        return new StepResult
                        {
                            DataFlowId = req.DataFlowId,
                            RequestId = req.RequestId,
                            Result = StepResultTypeEnum.Success,
                            Data = new Dictionary<string, object>
                            {
                                ["FirstNumber"] = firstNumber,
                                ["SecondNumber"] = secondNumber
                            },
                            Metadata = req.Metadata
                        };
                    }
                }

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Error,
                    Data = "Failed to parse second random number",
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

        /// <summary>
        /// Add two numbers together.
        /// </summary>
        [StepMethod("add_two_numbers")]
        public static async Task<StepResult> AddTwoNumbers(StepRequest req)
        {
            await Task.Delay(10);

            try
            {
                if (req.Data is Dictionary<string, object> data)
                {
                    int num1 = Convert.ToInt32(data["FirstNumber"]);
                    int num2 = Convert.ToInt32(data["SecondNumber"]);
                    int sum = num1 + num2;

                    return new StepResult
                    {
                        DataFlowId = req.DataFlowId,
                        RequestId = req.RequestId,
                        Result = StepResultTypeEnum.Success,
                        Data = new Dictionary<string, object>
                        {
                            ["FirstNumber"] = num1,
                            ["SecondNumber"] = num2,
                            ["Sum"] = sum
                        },
                        Metadata = req.Metadata
                    };
                }

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Error,
                    Data = "Invalid input format",
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

        /// <summary>
        /// Format final output message.
        /// </summary>
        [StepMethod("format_final_output")]
        public static async Task<StepResult> FormatFinalOutput(StepRequest req)
        {
            await Task.Delay(10);

            try
            {
                if (req.Data is Dictionary<string, object> data)
                {
                    int firstNum = Convert.ToInt32(data["FirstNumber"]);
                    int secondNum = Convert.ToInt32(data["SecondNumber"]);
                    int sum = Convert.ToInt32(data["Sum"]);

                    string output = $"REST API returned {firstNum} and {secondNum}. Total sum: {sum}";

                    return new StepResult
                    {
                        DataFlowId = req.DataFlowId,
                        RequestId = req.RequestId,
                        Result = StepResultTypeEnum.Success,
                        Data = new Dictionary<string, object>
                        {
                            ["FirstNumber"] = firstNum,
                            ["SecondNumber"] = secondNum,
                            ["Sum"] = sum,
                            ["Message"] = output
                        },
                        Metadata = req.Metadata
                    };
                }

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Error,
                    Data = "Invalid data format",
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

        /// <summary>
        /// Extract number from REST API response (for mixed step type tests).
        /// </summary>
        [StepMethod("extract_number_from_api")]
        public static async Task<StepResult> ExtractNumberFromApi(StepRequest req)
        {
            await Task.Delay(10);

            try
            {
                if (req.Data is Dictionary<string, object> resultDict &&
                    resultDict.TryGetValue("ResponseBody", out object responseBodyObj) &&
                    responseBodyObj is byte[] responseBytes)
                {
                    string responseJson = System.Text.Encoding.UTF8.GetString(responseBytes);
                    int[] numbers = System.Text.Json.JsonSerializer.Deserialize<int[]>(responseJson);

                    if (numbers != null && numbers.Length > 0)
                    {
                        return new StepResult
                        {
                            DataFlowId = req.DataFlowId,
                            RequestId = req.RequestId,
                            Result = StepResultTypeEnum.Success,
                            Data = numbers[0],
                            Metadata = req.Metadata
                        };
                    }
                }

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Error,
                    Data = "Failed to extract number from API response",
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

        /// <summary>
        /// Add 10 to a number (for mixed step type tests).
        /// </summary>
        [StepMethod("add_ten")]
        public static async Task<StepResult> AddTen(StepRequest req)
        {
            await Task.Delay(10);

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

                int result = value + 10;

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

        /// <summary>
        /// Create summary message (for mixed step type tests).
        /// </summary>
        [StepMethod("create_summary")]
        public static async Task<StepResult> CreateSummary(StepRequest req)
        {
            await Task.Delay(10);

            try
            {
                string summary = $"Final result: {req.Data}";

                return new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Success,
                    Data = summary,
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

#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    }
}
