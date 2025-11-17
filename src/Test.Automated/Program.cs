namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using PrettyId;
    using Tempo;
    using Tempo.Enumeration;
    using Tempo.Enums;
    using Tempo.Metrics;
    using Tempo.Runners;
    using Test.Automated;
    using Test.Automated.Steps;

    class Program
    {
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=== Tempo Automated Test Suite ===");
            Console.WriteLine();

            // Prompt for cleanup
            Console.Write("Clean up old test database and log files before running? (y/n): ");
            string cleanupResponse = Console.ReadLine()?.Trim().ToLower() ?? "n";
            Console.WriteLine();

            if (cleanupResponse == "y" || cleanupResponse == "yes")
            {
                CleanupTestDatabases();
                CleanupTestLogFiles();
                Console.WriteLine();
            }

            List<TestResult> allTests = new List<TestResult>();

            try
            {
                // Run all test suites
                allTests.AddRange(await RunWithoutMetricsStoreTests());
                allTests.AddRange(await RunWithMetricsStoreTests());
                allTests.AddRange(await RunMetricsStoreRetrievalTests());
                allTests.AddRange(await RunRestApiIntegrationTests());
                allTests.AddRange(await RunLoggingTests());

                // Print summary
                Console.WriteLine();
                Console.WriteLine("=".PadRight(60, '='));
                Console.WriteLine("TEST SUMMARY");
                Console.WriteLine("=".PadRight(60, '='));
                Console.WriteLine();

                int passedCount = allTests.Count(t => t.Passed);
                int failedCount = allTests.Count(t => !t.Passed);

                foreach (var test in allTests)
                {
                    // Only show details for failed tests
                    Console.Write(test.GetFormattedOutput(verbose: false));
                }

                Console.WriteLine();
                Console.WriteLine($"Total Tests: {allTests.Count}");
                Console.WriteLine($"Passed: {passedCount}");
                Console.WriteLine($"Failed: {failedCount}");
                Console.WriteLine();

                if (failedCount == 0)
                {
                    Console.WriteLine("OVERALL RESULT: PASS");
                    return 0;
                }
                else
                {
                    Console.WriteLine("OVERALL RESULT: FAIL");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        static void CleanupTestDatabases()
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine("Cleaning up test database files...");

                // Force garbage collection to release SQLite connection handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Give a brief moment for file handles to be released
                System.Threading.Thread.Sleep(100);

                string currentDirectory = Directory.GetCurrentDirectory();
                string[] dbFiles = Directory.GetFiles(currentDirectory, "metrics_*.db");

                int deletedCount = 0;
                int lockedCount = 0;

                foreach (string dbFile in dbFiles)
                {
                    try
                    {
                        File.Delete(dbFile);
                        deletedCount++;
                    }
                    catch (IOException)
                    {
                        // File is still locked, skip silently
                        lockedCount++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // File is locked or inaccessible, skip silently
                        lockedCount++;
                    }
                }

                if (deletedCount > 0)
                {
                    Console.WriteLine($"  Deleted {deletedCount} test database file(s).");
                }

                if (lockedCount > 0)
                {
                    Console.WriteLine($"  Skipped {lockedCount} locked database file(s) - will be cleaned on next run.");
                }

                if (deletedCount == 0 && lockedCount == 0)
                {
                    Console.WriteLine($"  No test database files to clean up.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Cleanup failed: {ex.Message}");
            }
        }

        static void PrintTestResult(string testName, bool passed, StepResultTypeEnum? resultType, string exitStepId, object data, string errorMessage = "")
        {
            if (passed)
            {
                Console.WriteLine($"PASS");
                Console.WriteLine($"  Exit Step: {TruncateId(exitStepId)}");
                Console.WriteLine($"  Result: {resultType}");
                if (data != null)
                {
                    string dataStr = data.ToString();
                    if (dataStr.Length > 50)
                        dataStr = dataStr.Substring(0, 50) + "...";
                    Console.WriteLine($"  Data: {dataStr}");
                }
            }
            else
            {
                Console.WriteLine($"FAIL");
                Console.WriteLine($"  Exit Step: {TruncateId(exitStepId)}");
                if (resultType.HasValue)
                    Console.WriteLine($"  Result: {resultType}");
                if (!string.IsNullOrEmpty(errorMessage))
                    Console.WriteLine($"  Error: {errorMessage}");
            }
        }

        static string TruncateId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "N/A";
            return id.Length > 24 ? id.Substring(0, 24) + "..." : id;
        }

        static async Task<List<TestResult>> RunWithoutMetricsStoreTests()
        {
            Console.WriteLine("--- Running Tests WITHOUT MetricsStore ---");
            Console.WriteLine();

            List<TestResult> results = new List<TestResult>();
            IdGenerator idGen = new IdGenerator();
            string tenantId = idGen.Generate("tenant", 64);

            // Test 1: Simple success flow with 10 steps
            results.Add(await TestSimpleSuccessFlow(tenantId, withMetrics: false));

            // Test 2: Flow with error handling (10+ steps with failures)
            results.Add(await TestErrorHandlingFlow(tenantId, withMetrics: false));

            // Test 3: Flow with exception handling
            results.Add(await TestExceptionHandlingFlow(tenantId, withMetrics: false));

            // Test 4: Flow with timeout
            results.Add(await TestTimeoutFlow(tenantId, withMetrics: false));

            // Test 5: Flow with max iterations exceeded
            results.Add(await TestMaxIterationsFlow(tenantId, withMetrics: false));

            // Test 6: Complex data transformation flow (15 steps)
            results.Add(await TestDataTransformationFlow(tenantId, withMetrics: false));

            // Test 7: Mixed success and error flow (12 steps)
            results.Add(await TestMixedFlow(tenantId, withMetrics: false));

            // Test 8: Multi-branch flow (10 steps with conditional paths)
            results.Add(await TestMultiBranchFlow(tenantId, withMetrics: false));

            // Test 9: Long chain flow (20 steps)
            results.Add(await TestLongChainFlow(tenantId, withMetrics: false));

            // Test 10: Error recovery flow (11 steps)
            results.Add(await TestErrorRecoveryFlow(tenantId, withMetrics: false));

            // Test 11: DataFlow timeout (10 steps)
            results.Add(await TestDataFlowTimeout(tenantId, withMetrics: false));

            return results;
        }

        static async Task<List<TestResult>> RunWithMetricsStoreTests()
        {
            Console.WriteLine();
            Console.WriteLine("--- Running Tests WITH MetricsStore ---");
            Console.WriteLine();

            List<TestResult> results = new List<TestResult>();
            IdGenerator idGen = new IdGenerator();
            string tenantId = idGen.Generate("tenant", 64);

            // Run same tests with metrics enabled
            results.Add(await TestSimpleSuccessFlow(tenantId, withMetrics: true));
            results.Add(await TestErrorHandlingFlow(tenantId, withMetrics: true));
            results.Add(await TestExceptionHandlingFlow(tenantId, withMetrics: true));
            results.Add(await TestTimeoutFlow(tenantId, withMetrics: true));
            results.Add(await TestMaxIterationsFlow(tenantId, withMetrics: true));
            results.Add(await TestDataTransformationFlow(tenantId, withMetrics: true));
            results.Add(await TestMixedFlow(tenantId, withMetrics: true));
            results.Add(await TestMultiBranchFlow(tenantId, withMetrics: true));
            results.Add(await TestLongChainFlow(tenantId, withMetrics: true));
            results.Add(await TestErrorRecoveryFlow(tenantId, withMetrics: true));
            results.Add(await TestDataFlowTimeout(tenantId, withMetrics: true));

            return results;
        }

        static async Task<List<TestResult>> RunMetricsStoreRetrievalTests()
        {
            Console.WriteLine();
            Console.WriteLine("--- Running MetricsStore Retrieval Tests ---");
            Console.WriteLine();

            List<TestResult> results = new List<TestResult>();

            results.Add(await TestMetricsRetrieval());
            results.Add(await TestMetricsEnumerationFull());
            results.Add(await TestMetricsEnumerationPaginated());
            results.Add(await TestMetricsEnumerationOrdering());

            return results;
        }

        static async Task<List<TestResult>> RunRestApiIntegrationTests()
        {
            Console.WriteLine();
            Console.WriteLine("--- Running REST API Integration Tests ---");
            Console.WriteLine();

            List<TestResult> results = new List<TestResult>();
            IdGenerator idGen = new IdGenerator();
            string tenantId = idGen.Generate("tenant", 64);

            results.Add(await TestRestApiRandomNumberAddition(tenantId));
            results.Add(await TestMethodBasedSteps(tenantId));
            results.Add(await TestMixedRestAndCodeAttributeSteps(tenantId));

            // Comprehensive mixed step type tests
            results.Add(await TestAllThreeStepTypesMixed(tenantId));
            results.Add(await TestAllThreeStepTypesWithBranching(tenantId));
            results.Add(await TestAllThreeStepTypesWithErrorRecovery(tenantId));

            // Exclusively single step type tests
            results.Add(await TestExclusivelyNativeCodeSteps(tenantId));
            results.Add(await TestExclusivelyRestSteps(tenantId));
            results.Add(await TestExclusivelyAttributeBasedSteps(tenantId));

            // Two-way combination tests (Native+REST, Native+Attribute already covered)
            results.Add(await TestNativeAndRestCombination(tenantId));
            results.Add(await TestNativeAndAttributeCombination(tenantId));

            return results;
        }

        static async Task<TestResult> TestSimpleSuccessFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Simple Success Flow (10 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create 10 success steps
                for (int i = 0; i < 10; i++)
                {
                    string stepId = idGen.Generate($"step_success_{i}", 64);
                    stepManager.Add(new SuccessStep(stepId, tenantId));
                }

                // Create data flow with linear chain
                DataFlow flow = new DataFlow { TenantId = tenantId };
                var steps = stepManager.All(tenantId).ToList();
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Run the flow
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "initial data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestErrorHandlingFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Error Handling Flow (12 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create steps: 3 success, 1 error, 3 success, 1 error, 4 success
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 3; i++)
                    steps.Add(new SuccessStep(idGen.Generate($"step_s{i}", 64), tenantId));

                steps.Add(new ErrorStep(idGen.Generate("step_err1", 64), tenantId));

                for (int i = 0; i < 3; i++)
                    steps.Add(new SuccessStep(idGen.Generate($"step_s2_{i}", 64), tenantId));

                steps.Add(new ErrorStep(idGen.Generate("step_err2", 64), tenantId));

                for (int i = 0; i < 4; i++)
                    steps.Add(new SuccessStep(idGen.Generate($"step_s3_{i}", 64), tenantId));

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow with error paths
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    string nextStep = i < steps.Count - 1 ? steps[i + 1].Identifier : null;
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = nextStep,
                        OnFailure = nextStep, // Continue on failure
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Should complete successfully even with errors in the middle
                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestExceptionHandlingFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Exception Handling Flow (10 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create steps with exception handling
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 3; i++)
                    steps.Add(new SuccessStep(idGen.Generate($"step_s{i}", 64), tenantId));

                string exceptionStepId = idGen.Generate("step_exception", 64);
                steps.Add(new ExceptionStep(exceptionStepId, tenantId));

                // Recovery steps
                for (int i = 0; i < 6; i++)
                    steps.Add(new RecoveryStep(idGen.Generate($"step_recovery_{i}", 64), tenantId));

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                // First 3 steps succeed and chain
                for (int i = 0; i < 3; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = steps[i + 1].Identifier,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Exception step routes to recovery
                flow.Steps[steps[3].Identifier] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = steps[4].Identifier // First recovery step
                };

                // Recovery steps chain
                for (int i = 4; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Should recover and complete successfully
                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success after recovery but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestTimeoutFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Timeout Flow (10 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create steps with timeout
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 3; i++)
                    steps.Add(new SuccessStep(idGen.Generate($"step_s{i}", 64), tenantId));

                string timeoutStepId = idGen.Generate("step_timeout", 64);
                steps.Add(new TimeoutStep(timeoutStepId, tenantId, maxRuntimeMs: 50));

                // Recovery steps
                for (int i = 0; i < 6; i++)
                    steps.Add(new RecoveryStep(idGen.Generate($"step_recovery_{i}", 64), tenantId));

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                // First 3 steps succeed
                for (int i = 0; i < 3; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = steps[i + 1].Identifier,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Timeout step routes to recovery on exception
                flow.Steps[steps[3].Identifier] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = steps[4].Identifier
                };

                // Recovery steps
                for (int i = 4; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Should timeout and then recover
                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success after timeout recovery but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMaxIterationsFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Max Iterations Flow (10 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create a counting step that will be called multiple times
                string countingStepId = idGen.Generate("step_counting", 64);
                stepManager.Add(new CountingStep(countingStepId, tenantId, successAfterCalls: 10));

                // Create other steps
                List<Step> otherSteps = new List<Step>();
                for (int i = 0; i < 9; i++)
                    otherSteps.Add(new SuccessStep(idGen.Generate($"step_s{i}", 64), tenantId));

                foreach (var step in otherSteps)
                    stepManager.Add(step);

                // Create flow with max iterations
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = countingStepId;

                // Counting step loops back to itself on error, limited to 3 iterations
                flow.Steps[countingStepId] = new StepTransition
                {
                    OnSuccess = otherSteps[0].Identifier,
                    OnFailure = countingStepId, // Loop back
                    OnException = null,
                    MaxTransitions = 3 // Will exceed this before reaching 10 calls
                };

                // Other steps chain
                for (int i = 0; i < otherSteps.Count; i++)
                {
                    flow.Steps[otherSteps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < otherSteps.Count - 1 ? otherSteps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Should hit max iterations
                result.Passed = flowResult.Result == StepResultTypeEnum.MaxIterationsExceeded;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected MaxIterationsExceeded but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, countingStepId, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestDataTransformationFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Data Transformation Flow (15 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create 15 transformation steps
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 15; i++)
                {
                    string stepId = idGen.Generate($"step_transform_{i}", 64);
                    steps.Add(new DataTransformStep(stepId, tenantId, $"->{i}"));
                }

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "START"
                };

                StepResult flowResult = await runner.Run(flow, request);

                string expectedData = "START->0->1->2->3->4->5->6->7->8->9->10->11->12->13->14";

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    result.Passed = false;
                }
                else if (flowResult.Data?.ToString() != expectedData)
                {
                    result.ErrorMessage = $"Expected data '{expectedData}' but got '{flowResult.Data}'";
                    result.Passed = false;
                }
                else
                {
                    result.Passed = true;
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMixedFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Mixed Flow (12 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Mix of different step types
                List<Step> steps = new List<Step>();
                steps.Add(new SuccessStep(idGen.Generate("step_s1", 64), tenantId));
                steps.Add(new DataTransformStep(idGen.Generate("step_t1", 64), tenantId, "->T1"));
                steps.Add(new SuccessStep(idGen.Generate("step_s2", 64), tenantId));
                steps.Add(new ErrorStep(idGen.Generate("step_e1", 64), tenantId));
                steps.Add(new RecoveryStep(idGen.Generate("step_r1", 64), tenantId));
                steps.Add(new DataTransformStep(idGen.Generate("step_t2", 64), tenantId, "->T2"));
                steps.Add(new SuccessStep(idGen.Generate("step_s3", 64), tenantId));
                steps.Add(new DataTransformStep(idGen.Generate("step_t3", 64), tenantId, "->T3"));
                steps.Add(new SuccessStep(idGen.Generate("step_s4", 64), tenantId));
                steps.Add(new DataTransformStep(idGen.Generate("step_t4", 64), tenantId, "->T4"));
                steps.Add(new SuccessStep(idGen.Generate("step_s5", 64), tenantId));
                steps.Add(new DataTransformStep(idGen.Generate("step_t5", 64), tenantId, "->T5"));

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = i < steps.Count - 1 ? steps[i + 1].Identifier : null, // Continue on failure
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "MIXED"
                };

                StepResult flowResult = await runner.Run(flow, request);

                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMultiBranchFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Multi-Branch Flow (10 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create branching structure
                List<Step> steps = new List<Step>();
                steps.Add(new SuccessStep(idGen.Generate("step_start", 64), tenantId));
                steps.Add(new ErrorStep(idGen.Generate("step_branch", 64), tenantId)); // Branches to failure path

                // Success branch (won't be taken)
                for (int i = 0; i < 3; i++)
                    steps.Add(new SuccessStep(idGen.Generate($"step_success_branch_{i}", 64), tenantId));

                // Failure branch (will be taken)
                for (int i = 0; i < 5; i++)
                    steps.Add(new RecoveryStep(idGen.Generate($"step_failure_branch_{i}", 64), tenantId));

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow with branches
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                // Start step -> branch step
                flow.Steps[steps[0].Identifier] = new StepTransition
                {
                    OnSuccess = steps[1].Identifier,
                    OnFailure = null,
                    OnException = null
                };

                // Branch step routes differently based on result
                flow.Steps[steps[1].Identifier] = new StepTransition
                {
                    OnSuccess = steps[2].Identifier, // Success branch
                    OnFailure = steps[5].Identifier, // Failure branch (will be taken)
                    OnException = null
                };

                // Success branch
                for (int i = 2; i < 5; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = null, // Terminates
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Failure branch
                for (int i = 5; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test"
                };

                StepResult flowResult = await runner.Run(flow, request);

                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestLongChainFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Long Chain Flow (20 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create 20 steps
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 20; i++)
                {
                    steps.Add(new SuccessStep(idGen.Generate($"step_{i}", 64), tenantId));
                }

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create linear flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test"
                };

                StepResult flowResult = await runner.Run(flow, request);

                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestErrorRecoveryFlow(string tenantId, bool withMetrics)
        {
            string testName = $"Error Recovery Flow (11 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create steps with multiple error/recovery cycles
                List<Step> steps = new List<Step>();
                steps.Add(new SuccessStep(idGen.Generate("step_s1", 64), tenantId));
                steps.Add(new ErrorStep(idGen.Generate("step_e1", 64), tenantId));
                steps.Add(new RecoveryStep(idGen.Generate("step_r1", 64), tenantId));
                steps.Add(new SuccessStep(idGen.Generate("step_s2", 64), tenantId));
                steps.Add(new ExceptionStep(idGen.Generate("step_ex1", 64), tenantId));
                steps.Add(new RecoveryStep(idGen.Generate("step_r2", 64), tenantId));
                steps.Add(new SuccessStep(idGen.Generate("step_s3", 64), tenantId));
                steps.Add(new ErrorStep(idGen.Generate("step_e2", 64), tenantId));
                steps.Add(new RecoveryStep(idGen.Generate("step_r3", 64), tenantId));
                steps.Add(new SuccessStep(idGen.Generate("step_s4", 64), tenantId));
                steps.Add(new SuccessStep(idGen.Generate("step_s5", 64), tenantId));

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow with error recovery
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                // s1 -> e1
                flow.Steps[steps[0].Identifier] = new StepTransition
                {
                    OnSuccess = steps[1].Identifier,
                    OnFailure = null,
                    OnException = null
                };

                // e1 -> r1 (on failure)
                flow.Steps[steps[1].Identifier] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = steps[2].Identifier,
                    OnException = null
                };

                // r1 -> s2
                flow.Steps[steps[2].Identifier] = new StepTransition
                {
                    OnSuccess = steps[3].Identifier,
                    OnFailure = null,
                    OnException = null
                };

                // s2 -> ex1
                flow.Steps[steps[3].Identifier] = new StepTransition
                {
                    OnSuccess = steps[4].Identifier,
                    OnFailure = null,
                    OnException = null
                };

                // ex1 -> r2 (on exception)
                flow.Steps[steps[4].Identifier] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = steps[5].Identifier
                };

                // r2 -> s3
                flow.Steps[steps[5].Identifier] = new StepTransition
                {
                    OnSuccess = steps[6].Identifier,
                    OnFailure = null,
                    OnException = null
                };

                // s3 -> e2
                flow.Steps[steps[6].Identifier] = new StepTransition
                {
                    OnSuccess = steps[7].Identifier,
                    OnFailure = null,
                    OnException = null
                };

                // e2 -> r3 (on failure)
                flow.Steps[steps[7].Identifier] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = steps[8].Identifier,
                    OnException = null
                };

                // r3 -> s4 -> s5
                for (int i = 8; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test"
                };

                StepResult flowResult = await runner.Run(flow, request);

                result.Passed = flowResult.Result == StepResultTypeEnum.Success;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Success after multiple recoveries but got {flowResult.Result}";
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestDataFlowTimeout(string tenantId, bool withMetrics)
        {
            string testName = $"DataFlow Timeout Test (10 steps){(withMetrics ? " [WITH METRICS]" : "")}";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                if (withMetrics)
                {
                    string dbFile = $"metrics_test_{Guid.NewGuid()}.db";
                    runner.MetricsStore = new SqliteMetricsStore(dbFile);
                }

                // Create 10 slow steps, each taking 100ms
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 10; i++)
                {
                    string stepId = idGen.Generate($"step_slow_{i}", 64);
                    steps.Add(new SlowStep(stepId, tenantId, sleepMs: 100));
                }

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow with timeout of 250ms (should timeout after ~2 steps @ 100ms each)
                DataFlow flow = new DataFlow
                {
                    TenantId = tenantId,
                    MaxRuntimeMs = 250
                };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = "test"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Should timeout before completing all steps
                result.Passed = flowResult.Result == StepResultTypeEnum.Timeout;
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected Timeout but got {flowResult.Result}";
                }

                string exitStepId = steps.Count > 0 ? steps[0].Identifier : "N/A";
                PrintTestResult(testName, result.Passed, flowResult.Result, exitStepId, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMetricsRetrieval()
        {
            string testName = "Metrics Retrieval Test";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                string tenantId = idGen.Generate("tenant", 64);
                string dbFile = $"metrics_retrieval_test_{Guid.NewGuid()}.db";
                SqliteMetricsStore metricsStore = new SqliteMetricsStore(dbFile);

                // Create and run a simple flow
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);
                runner.MetricsStore = metricsStore;

                // Create 5 steps
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 5; i++)
                {
                    steps.Add(new SuccessStep(idGen.Generate($"step_{i}", 64), tenantId));
                }

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                string requestId = idGen.Generate("request_", 64);
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = requestId,
                    Data = "test"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Now retrieve the metrics
                DataFlowRunDetails flowRun = await metricsStore.GetDataFlowRun(requestId);
                List<StepRunDetails> stepRuns = await metricsStore.GetDataFlowStepRuns(requestId);

                // Validate
                if (flowRun == null)
                {
                    result.ErrorMessage = "Failed to retrieve data flow run details";
                    result.Passed = false;
                }
                else if (flowRun.RequestId != requestId)
                {
                    result.ErrorMessage = $"Retrieved wrong flow run: expected {requestId}, got {flowRun.RequestId}";
                    result.Passed = false;
                }
                else if (stepRuns == null || stepRuns.Count != 5)
                {
                    result.ErrorMessage = $"Expected 5 step runs, got {stepRuns?.Count ?? 0}";
                    result.Passed = false;
                }
                else if (stepRuns.Any(sr => sr.Result != StepResultTypeEnum.Success))
                {
                    result.ErrorMessage = "Not all step runs returned Success result";
                    result.Passed = false;
                }
                else
                {
                    result.Passed = true;
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);

                // Cleanup - give SQLite time to release the file
                await Task.Delay(100);
                if (File.Exists(dbFile))
                {
                    try { File.Delete(dbFile); } catch { /* Ignore cleanup errors */ }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMetricsEnumerationFull()
        {
            string testName = "Metrics Full Enumeration Test";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                string tenantId = idGen.Generate("tenant", 64);
                string dbFile = $"metrics_full_enum_test_{Guid.NewGuid()}.db";
                SqliteMetricsStore metricsStore = new SqliteMetricsStore(dbFile);

                // Create and run multiple flows
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);
                runner.MetricsStore = metricsStore;

                // Create 3 steps
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 3; i++)
                {
                    steps.Add(new SuccessStep(idGen.Generate($"step_{i}", 64), tenantId));
                }

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Run the flow 8 times
                for (int i = 0; i < 8; i++)
                {
                    StepRequest request = new StepRequest
                    {
                        DataFlowId = flow.Identifier,
                        RequestId = idGen.Generate($"request_{i}", 64),
                        Data = $"test_{i}"
                    };

                    await runner.Run(flow, request);
                }

                // Test 1: Full enumeration of DataFlowRuns (all records in one request)
                EnumerationRequest fullRequest = new EnumerationRequest
                {
                    TenantId = tenantId,
                    MaxResults = 100, // Request more than available
                    Skip = 0,
                    Ordering = EnumerationOrderEnum.StartUtcDescending
                };

                EnumerationResult<DataFlowRunDetails> fullResult = await metricsStore.EnumerateDataFlowRuns(fullRequest);

                // Validate all EnumerationResult properties
                if (!fullResult.Success)
                {
                    result.ErrorMessage = "Full enumeration failed - Success is false";
                    result.Passed = false;
                }
                else if (fullResult.TotalRecords != 8)
                {
                    result.ErrorMessage = $"Expected TotalRecords=8, got {fullResult.TotalRecords}";
                    result.Passed = false;
                }
                else if (fullResult.Objects.Count != 8)
                {
                    result.ErrorMessage = $"Expected 8 objects in Objects list, got {fullResult.Objects.Count}";
                    result.Passed = false;
                }
                else if (fullResult.RecordsRemaining != 0)
                {
                    result.ErrorMessage = $"Expected RecordsRemaining=0, got {fullResult.RecordsRemaining}";
                    result.Passed = false;
                }
                else if (!fullResult.EndOfResults)
                {
                    result.ErrorMessage = "Expected EndOfResults=true";
                    result.Passed = false;
                }
                else if (fullResult.MaxResults != 100)
                {
                    result.ErrorMessage = $"Expected MaxResults=100, got {fullResult.MaxResults}";
                    result.Passed = false;
                }
                else
                {
                    // Verify all returned objects have correct tenant
                    bool allCorrectTenant = fullResult.Objects.All(o => o.TenantId == tenantId);
                    if (!allCorrectTenant)
                    {
                        result.ErrorMessage = "Not all objects have correct TenantId";
                        result.Passed = false;
                    }
                    else
                    {
                        // Test 2: Full enumeration of StepRuns
                        EnumerationResult<StepRunDetails> stepFullResult = await metricsStore.EnumerateStepRuns(fullRequest);

                        int expectedStepRecords = 8 * 3; // 8 flows * 3 steps each = 24

                        if (!stepFullResult.Success)
                        {
                            result.ErrorMessage = "Step full enumeration failed";
                            result.Passed = false;
                        }
                        else if (stepFullResult.TotalRecords != expectedStepRecords)
                        {
                            result.ErrorMessage = $"Expected TotalRecords={expectedStepRecords} for steps, got {stepFullResult.TotalRecords}";
                            result.Passed = false;
                        }
                        else if (stepFullResult.Objects.Count != expectedStepRecords)
                        {
                            result.ErrorMessage = $"Expected {expectedStepRecords} step objects, got {stepFullResult.Objects.Count}";
                            result.Passed = false;
                        }
                        else if (stepFullResult.RecordsRemaining != 0)
                        {
                            result.ErrorMessage = $"Expected RecordsRemaining=0 for steps, got {stepFullResult.RecordsRemaining}";
                            result.Passed = false;
                        }
                        else if (!stepFullResult.EndOfResults)
                        {
                            result.ErrorMessage = "Expected EndOfResults=true for steps";
                            result.Passed = false;
                        }
                        else
                        {
                            result.Passed = true;
                        }
                    }
                }

                PrintTestResult(testName, result.Passed, StepResultTypeEnum.Success, "EnumerationTest", "8 flows, 24 steps", result.ErrorMessage);

                // Cleanup
                await Task.Delay(100);
                if (File.Exists(dbFile))
                {
                    try { File.Delete(dbFile); } catch { /* Ignore cleanup errors */ }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMetricsEnumerationPaginated()
        {
            string testName = "Metrics Paginated Enumeration Test";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                string tenantId = idGen.Generate("tenant", 64);
                string dbFile = $"metrics_paged_enum_test_{Guid.NewGuid()}.db";
                SqliteMetricsStore metricsStore = new SqliteMetricsStore(dbFile);

                // Create and run multiple flows
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);
                runner.MetricsStore = metricsStore;

                // Create 4 steps
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 4; i++)
                {
                    steps.Add(new SuccessStep(idGen.Generate($"step_{i}", 64), tenantId));
                }

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Run the flow 10 times (will create 40 step records)
                for (int i = 0; i < 10; i++)
                {
                    StepRequest request = new StepRequest
                    {
                        DataFlowId = flow.Identifier,
                        RequestId = idGen.Generate($"request_{i}", 64),
                        Data = $"test_{i}"
                    };

                    await runner.Run(flow, request);
                }

                // Test paginated enumeration of StepRuns (40 total records)
                // Page 1: Get first 10 records
                EnumerationRequest page1Request = new EnumerationRequest
                {
                    TenantId = tenantId,
                    MaxResults = 10,
                    Skip = 0,
                    Ordering = EnumerationOrderEnum.StartUtcDescending
                };

                EnumerationResult<StepRunDetails> page1 = await metricsStore.EnumerateStepRuns(page1Request);

                // Validate Page 1
                if (!page1.Success)
                {
                    result.ErrorMessage = "Page 1 enumeration failed";
                    result.Passed = false;
                }
                else if (page1.TotalRecords != 40)
                {
                    result.ErrorMessage = $"Page 1: Expected TotalRecords=40, got {page1.TotalRecords}";
                    result.Passed = false;
                }
                else if (page1.Objects.Count != 10)
                {
                    result.ErrorMessage = $"Page 1: Expected 10 objects, got {page1.Objects.Count}";
                    result.Passed = false;
                }
                else if (page1.RecordsRemaining != 30)
                {
                    result.ErrorMessage = $"Page 1: Expected RecordsRemaining=30, got {page1.RecordsRemaining}";
                    result.Passed = false;
                }
                else if (page1.EndOfResults)
                {
                    result.ErrorMessage = "Page 1: Expected EndOfResults=false";
                    result.Passed = false;
                }
                else if (page1.MaxResults != 10)
                {
                    result.ErrorMessage = $"Page 1: Expected MaxResults=10, got {page1.MaxResults}";
                    result.Passed = false;
                }
                else
                {
                    // Page 2: Get next 10 records (skip 10)
                    EnumerationRequest page2Request = new EnumerationRequest
                    {
                        TenantId = tenantId,
                        MaxResults = 10,
                        Skip = 10,
                        Ordering = EnumerationOrderEnum.StartUtcDescending
                    };

                    EnumerationResult<StepRunDetails> page2 = await metricsStore.EnumerateStepRuns(page2Request);

                    if (!page2.Success)
                    {
                        result.ErrorMessage = "Page 2 enumeration failed";
                        result.Passed = false;
                    }
                    else if (page2.TotalRecords != 40)
                    {
                        result.ErrorMessage = $"Page 2: Expected TotalRecords=40, got {page2.TotalRecords}";
                        result.Passed = false;
                    }
                    else if (page2.Objects.Count != 10)
                    {
                        result.ErrorMessage = $"Page 2: Expected 10 objects, got {page2.Objects.Count}";
                        result.Passed = false;
                    }
                    else if (page2.RecordsRemaining != 20)
                    {
                        result.ErrorMessage = $"Page 2: Expected RecordsRemaining=20, got {page2.RecordsRemaining}";
                        result.Passed = false;
                    }
                    else if (page2.EndOfResults)
                    {
                        result.ErrorMessage = "Page 2: Expected EndOfResults=false";
                        result.Passed = false;
                    }
                    else
                    {
                        // Page 3: Get next 10 records (skip 20)
                        EnumerationRequest page3Request = new EnumerationRequest
                        {
                            TenantId = tenantId,
                            MaxResults = 10,
                            Skip = 20,
                            Ordering = EnumerationOrderEnum.StartUtcDescending
                        };

                        EnumerationResult<StepRunDetails> page3 = await metricsStore.EnumerateStepRuns(page3Request);

                        if (!page3.Success)
                        {
                            result.ErrorMessage = "Page 3 enumeration failed";
                            result.Passed = false;
                        }
                        else if (page3.TotalRecords != 40)
                        {
                            result.ErrorMessage = $"Page 3: Expected TotalRecords=40, got {page3.TotalRecords}";
                            result.Passed = false;
                        }
                        else if (page3.Objects.Count != 10)
                        {
                            result.ErrorMessage = $"Page 3: Expected 10 objects, got {page3.Objects.Count}";
                            result.Passed = false;
                        }
                        else if (page3.RecordsRemaining != 10)
                        {
                            result.ErrorMessage = $"Page 3: Expected RecordsRemaining=10, got {page3.RecordsRemaining}";
                            result.Passed = false;
                        }
                        else if (page3.EndOfResults)
                        {
                            result.ErrorMessage = "Page 3: Expected EndOfResults=false";
                            result.Passed = false;
                        }
                        else
                        {
                            // Page 4: Get final 10 records (skip 30)
                            EnumerationRequest page4Request = new EnumerationRequest
                            {
                                TenantId = tenantId,
                                MaxResults = 10,
                                Skip = 30,
                                Ordering = EnumerationOrderEnum.StartUtcDescending
                            };

                            EnumerationResult<StepRunDetails> page4 = await metricsStore.EnumerateStepRuns(page4Request);

                            if (!page4.Success)
                            {
                                result.ErrorMessage = "Page 4 enumeration failed";
                                result.Passed = false;
                            }
                            else if (page4.TotalRecords != 40)
                            {
                                result.ErrorMessage = $"Page 4: Expected TotalRecords=40, got {page4.TotalRecords}";
                                result.Passed = false;
                            }
                            else if (page4.Objects.Count != 10)
                            {
                                result.ErrorMessage = $"Page 4: Expected 10 objects, got {page4.Objects.Count}";
                                result.Passed = false;
                            }
                            else if (page4.RecordsRemaining != 0)
                            {
                                result.ErrorMessage = $"Page 4: Expected RecordsRemaining=0, got {page4.RecordsRemaining}";
                                result.Passed = false;
                            }
                            else if (!page4.EndOfResults)
                            {
                                result.ErrorMessage = "Page 4: Expected EndOfResults=true";
                                result.Passed = false;
                            }
                            else
                            {
                                // Verify no duplicate objects across pages
                                HashSet<string> allRowIds = new HashSet<string>();
                                foreach (var obj in page1.Objects) allRowIds.Add(obj.RowId);
                                foreach (var obj in page2.Objects) allRowIds.Add(obj.RowId);
                                foreach (var obj in page3.Objects) allRowIds.Add(obj.RowId);
                                foreach (var obj in page4.Objects) allRowIds.Add(obj.RowId);

                                if (allRowIds.Count != 40)
                                {
                                    result.ErrorMessage = $"Expected 40 unique RowIds across pages, got {allRowIds.Count}";
                                    result.Passed = false;
                                }
                                else
                                {
                                    result.Passed = true;
                                }
                            }
                        }
                    }
                }

                PrintTestResult(testName, result.Passed, StepResultTypeEnum.Success, "PaginationTest", "4 pages, 40 steps", result.ErrorMessage);

                // Cleanup
                await Task.Delay(100);
                if (File.Exists(dbFile))
                {
                    try { File.Delete(dbFile); } catch { /* Ignore cleanup errors */ }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMetricsEnumerationOrdering()
        {
            string testName = "Metrics Enumeration Ordering Test";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            try
            {
                IdGenerator idGen = new IdGenerator();
                string tenantId = idGen.Generate("tenant", 64);
                string dbFile = $"metrics_order_enum_test_{Guid.NewGuid()}.db";
                SqliteMetricsStore metricsStore = new SqliteMetricsStore(dbFile);

                // Create and run multiple flows
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);
                runner.MetricsStore = metricsStore;

                // Create 2 steps
                List<Step> steps = new List<Step>();
                for (int i = 0; i < 2; i++)
                {
                    steps.Add(new SuccessStep(idGen.Generate($"step_{i}", 64), tenantId));
                }

                foreach (var step in steps)
                    stepManager.Add(step);

                // Create flow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Run the flow 5 times with delays to ensure different timestamps
                for (int i = 0; i < 5; i++)
                {
                    StepRequest request = new StepRequest
                    {
                        DataFlowId = flow.Identifier,
                        RequestId = idGen.Generate($"request_{i}", 64),
                        Data = $"test_{i}"
                    };

                    await runner.Run(flow, request);
                    await Task.Delay(50); // Small delay to ensure different StartUtc values
                }

                // Test 1: StartUtcDescending (newest first)
                EnumerationRequest descRequest = new EnumerationRequest
                {
                    TenantId = tenantId,
                    MaxResults = 10,
                    Skip = 0,
                    Ordering = EnumerationOrderEnum.StartUtcDescending
                };

                EnumerationResult<DataFlowRunDetails> descResult = await metricsStore.EnumerateDataFlowRuns(descRequest);

                if (!descResult.Success || descResult.Objects.Count != 5)
                {
                    result.ErrorMessage = "Failed to retrieve records for ordering test";
                    result.Passed = false;
                }
                else
                {
                    // Verify descending order (each item should have StartUtc >= next item)
                    bool isDescending = true;
                    for (int i = 0; i < descResult.Objects.Count - 1; i++)
                    {
                        if (descResult.Objects[i].StartUtc < descResult.Objects[i + 1].StartUtc)
                        {
                            isDescending = false;
                            break;
                        }
                    }

                    if (!isDescending)
                    {
                        result.ErrorMessage = "StartUtcDescending order not respected";
                        result.Passed = false;
                    }
                    else
                    {
                        // Test 2: StartUtcAscending (oldest first)
                        EnumerationRequest ascRequest = new EnumerationRequest
                        {
                            TenantId = tenantId,
                            MaxResults = 10,
                            Skip = 0,
                            Ordering = EnumerationOrderEnum.StartUtcAscending
                        };

                        EnumerationResult<DataFlowRunDetails> ascResult = await metricsStore.EnumerateDataFlowRuns(ascRequest);

                        if (!ascResult.Success || ascResult.Objects.Count != 5)
                        {
                            result.ErrorMessage = "Failed to retrieve records for ascending test";
                            result.Passed = false;
                        }
                        else
                        {
                            // Verify ascending order
                            bool isAscending = true;
                            for (int i = 0; i < ascResult.Objects.Count - 1; i++)
                            {
                                if (ascResult.Objects[i].StartUtc > ascResult.Objects[i + 1].StartUtc)
                                {
                                    isAscending = false;
                                    break;
                                }
                            }

                            if (!isAscending)
                            {
                                result.ErrorMessage = "StartUtcAscending order not respected";
                                result.Passed = false;
                            }
                            else
                            {
                                // Verify ascending and descending are reverse of each other
                                if (ascResult.Objects[0].RowId != descResult.Objects[4].RowId ||
                                    ascResult.Objects[4].RowId != descResult.Objects[0].RowId)
                                {
                                    result.ErrorMessage = "Ascending and descending orders don't match expected reversal";
                                    result.Passed = false;
                                }
                                else
                                {
                                    result.Passed = true;
                                }
                            }
                        }
                    }
                }

                PrintTestResult(testName, result.Passed, StepResultTypeEnum.Success, "OrderingTest", "5 flows", result.ErrorMessage);

                // Cleanup
                await Task.Delay(100);
                if (File.Exists(dbFile))
                {
                    try { File.Delete(dbFile); } catch { /* Ignore cleanup errors */ }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestRestApiRandomNumberAddition(string tenantId)
        {
            string testName = "REST API Random Number Addition Test";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine(); // New line for detailed logging

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Setting up DataFlow with REST and code steps...");

                // Step A: REST step - Get random number from randomnumberapi.com
                string restStepId = "get_random_number";
                Console.WriteLine($"  Creating REST step '{restStepId}' to fetch first random number...");

                // Step B: Code step - Get another random number and add them
                string addStepId = idGen.Generate("add_numbers", 64);
                AddRandomNumbersStep addStep = new AddRandomNumbersStep(addStepId, tenantId);
                stepManager.Add(addStep);
                Console.WriteLine($"  Created code step '{TruncateId(addStepId)}' to fetch and add second number...");

                // Create data flow with REST step followed by code step
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = restStepId;

                // Configure REST step transition
                flow.Steps[restStepId] = new StepTransition
                {
                    Name = restStepId,
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1",
                        Headers = new Dictionary<string, string>
                        {
                            ["Accept"] = "application/json"
                        },
                        TimeoutMs = 60000
                    },
                    OnSuccess = addStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Configure code step transition
                flow.Steps[addStepId] = new StepTransition
                {
                    Name = addStepId,
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                Console.WriteLine($"  DataFlow configured with 2 steps.");
                Console.WriteLine();

                // Run the flow
                Console.WriteLine($"  Executing DataFlow...");
                Console.WriteLine($"  ----------------------------------------");
                Console.WriteLine($"  [REST Step] Calling randomnumberapi.com...");

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = null
                };

                StepResult flowResult = await runner.Run(flow, request);

                Console.WriteLine($"  ----------------------------------------");
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                // Validate results
                Console.WriteLine($"  Validating results...");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else if (flowResult.Data is Dictionary<string, object> resultDict)
                {
                    // Extract and validate the result data
                    if (!resultDict.TryGetValue("FirstNumber", out object firstNumObj) ||
                        !resultDict.TryGetValue("SecondNumber", out object secondNumObj) ||
                        !resultDict.TryGetValue("Sum", out object sumObj))
                    {
                        result.ErrorMessage = "Missing expected fields in result data";
                        result.Passed = false;
                    }
                    else
                    {
                        int firstNum = Convert.ToInt32(firstNumObj);
                        int secondNum = Convert.ToInt32(secondNumObj);
                        int sum = Convert.ToInt32(sumObj);
                        int expectedSum = firstNum + secondNum;

                        Console.WriteLine($"  First Number:  {firstNum}");
                        Console.WriteLine($"  Second Number: {secondNum}");
                        Console.WriteLine($"  Sum:           {sum}");
                        Console.WriteLine($"  Expected Sum:  {expectedSum}");

                        if (sum != expectedSum)
                        {
                            result.ErrorMessage = $"Sum calculation incorrect: {firstNum} + {secondNum} should equal {expectedSum}, got {sum}";
                            result.Passed = false;
                        }
                        else if (firstNum < 1 || firstNum > 100 || secondNum < 1 || secondNum > 100)
                        {
                            result.ErrorMessage = $"Numbers outside expected range (1-100): first={firstNum}, second={secondNum}";
                            result.Passed = false;
                        }
                        else
                        {
                            result.Passed = true;
                            Console.WriteLine($"  Validation: PASSED - Sum is correct!");
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = $"Unexpected data type: {flowResult.Data.GetType()}";
                    result.Passed = false;
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, addStepId, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMethodBasedSteps(string tenantId)
        {
            string testName = "Method-Based Steps Test";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Scanning assembly for [StepMethod] attributes...");

                // Use StepManager to scan and register method-based steps
                int registeredCount = stepManager.ScanCallingAssembly(tenantId);

                Console.WriteLine($"  Registered {registeredCount} method-based steps.");
                Console.WriteLine();

                // Create a flow using method-based steps
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = "method_validate";

                // Validate -> Multiply -> Format
                flow.Steps["method_validate"] = new StepTransition
                {
                    Name = "method_validate",
                    OnSuccess = "method_multiply",
                    OnFailure = null,
                    OnException = null
                };

                flow.Steps["method_multiply"] = new StepTransition
                {
                    Name = "method_multiply",
                    OnSuccess = "method_format",
                    OnFailure = null,
                    OnException = null
                };

                flow.Steps["method_format"] = new StepTransition
                {
                    Name = "method_format",
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                Console.WriteLine($"  Executing DataFlow with 3 method-based steps...");

                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = 42  // Start with number 42
                };

                StepResult flowResult = await runner.Run(flow, request);

                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                // Validate results
                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Input: 42");
                Console.WriteLine($"  After multiply: Should be 84");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                string expectedOutput = "Processed: 84";

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    result.Passed = false;
                }
                else if (flowResult.Data?.ToString() != expectedOutput)
                {
                    result.ErrorMessage = $"Expected '{expectedOutput}' but got '{flowResult.Data}'";
                    result.Passed = false;
                }
                else
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED!");
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "method_format", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestMixedRestAndCodeAttributeSteps(string tenantId)
        {
            string testName = "Mixed REST and CodeAttribute Steps Test (2 REST + 4 CodeAttribute)";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Scanning assembly for [StepMethod] attributes...");
                int registeredCount = stepManager.ScanCallingAssembly(tenantId);
                Console.WriteLine($"  Registered {registeredCount} CodeAttribute steps.");
                Console.WriteLine();

                // Create DataFlow: REST -> CodeAttribute -> REST -> CodeAttribute -> CodeAttribute -> CodeAttribute
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = "get_first_number_rest";

                Console.WriteLine($"  Building DataFlow with 2 REST + 4 CodeAttribute steps:");
                Console.WriteLine($"    1. REST: Get first random number");
                Console.WriteLine($"    2. CodeAttribute: Parse first number (parse_first_number)");
                Console.WriteLine($"    3. REST: Get second random number");
                Console.WriteLine($"    4. CodeAttribute: Parse second number (parse_second_number)");
                Console.WriteLine($"    5. CodeAttribute: Add two numbers (add_two_numbers)");
                Console.WriteLine($"    6. CodeAttribute: Format output (format_final_output)");
                Console.WriteLine();

                // Step 1: REST - Get first random number
                flow.Steps["get_first_number_rest"] = new StepTransition
                {
                    Name = "get_first_number_rest",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "parse_first_number",
                    OnFailure = null,
                    OnException = null
                };

                // Step 2: CodeAttribute - Parse first number
                flow.Steps["parse_first_number"] = new StepTransition
                {
                    Name = "parse_first_number",
                    OnSuccess = "get_second_number_rest",
                    OnFailure = null,
                    OnException = null
                };

                // Step 3: REST - Get second random number
                flow.Steps["get_second_number_rest"] = new StepTransition
                {
                    Name = "get_second_number_rest",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "parse_second_number",
                    OnFailure = null,
                    OnException = null
                };

                // Step 4: CodeAttribute - Parse second number
                flow.Steps["parse_second_number"] = new StepTransition
                {
                    Name = "parse_second_number",
                    OnSuccess = "add_two_numbers",
                    OnFailure = null,
                    OnException = null
                };

                // Step 5: CodeAttribute - Add two numbers
                flow.Steps["add_two_numbers"] = new StepTransition
                {
                    Name = "add_two_numbers",
                    OnSuccess = "format_final_output",
                    OnFailure = null,
                    OnException = null
                };

                // Step 6: CodeAttribute - Format final output
                flow.Steps["format_final_output"] = new StepTransition
                {
                    Name = "format_final_output",
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                // Execute the flow
                Console.WriteLine($"  Executing DataFlow...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = null
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                // Validate results
                Console.WriteLine($"  Validating results...");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else if (flowResult.Data is Dictionary<string, object> resultDict)
                {
                    if (!resultDict.TryGetValue("FirstNumber", out object firstNumObj) ||
                        !resultDict.TryGetValue("SecondNumber", out object secondNumObj) ||
                        !resultDict.TryGetValue("Sum", out object sumObj) ||
                        !resultDict.TryGetValue("Message", out object messageObj))
                    {
                        result.ErrorMessage = "Missing expected fields in result data";
                        result.Passed = false;
                    }
                    else
                    {
                        int firstNum = Convert.ToInt32(firstNumObj);
                        int secondNum = Convert.ToInt32(secondNumObj);
                        int sum = Convert.ToInt32(sumObj);
                        string message = messageObj.ToString();
                        int expectedSum = firstNum + secondNum;

                        Console.WriteLine($"  First Number:   {firstNum}");
                        Console.WriteLine($"  Second Number:  {secondNum}");
                        Console.WriteLine($"  Sum:            {sum}");
                        Console.WriteLine($"  Expected Sum:   {expectedSum}");
                        Console.WriteLine($"  Message:        {message}");

                        if (sum != expectedSum)
                        {
                            result.ErrorMessage = $"Sum calculation incorrect: {firstNum} + {secondNum} should equal {expectedSum}, got {sum}";
                            result.Passed = false;
                        }
                        else if (firstNum < 1 || firstNum > 100 || secondNum < 1 || secondNum > 100)
                        {
                            result.ErrorMessage = $"Numbers outside expected range (1-100): first={firstNum}, second={secondNum}";
                            result.Passed = false;
                        }
                        else
                        {
                            result.Passed = true;
                            Console.WriteLine($"  Validation: PASSED - All steps executed correctly!");
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = $"Unexpected data type: {flowResult.Data.GetType()}";
                    result.Passed = false;
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "format_final_output", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestAllThreeStepTypesMixed(string tenantId)
        {
            string testName = "All Three Step Types Mixed (Native + REST + Attribute) - 9 steps";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Scanning assembly for [StepMethod] attributes...");
                int registeredCount = stepManager.ScanCallingAssembly(tenantId);
                Console.WriteLine($"  Registered {registeredCount} CodeAttribute steps.");
                Console.WriteLine();

                // Create native code steps
                string validateStepId = idGen.Generate("validate_native", 64);
                string multiplyStepId = idGen.Generate("multiply_native", 64);
                string successStepId = idGen.Generate("success_native", 64);

                stepManager.Add(new ValidateNumberStep(validateStepId, tenantId, 1, 200));
                stepManager.Add(new MultiplyStep(multiplyStepId, tenantId));
                stepManager.Add(new SuccessStep(successStepId, tenantId));

                Console.WriteLine($"  Building DataFlow with all three step types:");
                Console.WriteLine($"    1. Native Code: Validate number is in range");
                Console.WriteLine($"    2. REST: Get random number from API");
                Console.WriteLine($"    3. CodeAttribute: Extract number from API response (extract_number_from_api)");
                Console.WriteLine($"    4. Native Code: Multiply by 3 (MultiplyStep)");
                Console.WriteLine($"    5. CodeAttribute: Add 10 (add_ten)");
                Console.WriteLine($"    6. REST: Get another random number");
                Console.WriteLine($"    7. CodeAttribute: Extract second number (extract_number_from_api)");
                Console.WriteLine($"    8. Native Code: Success step (pass through)");
                Console.WriteLine($"    9. CodeAttribute: Create summary (create_summary)");
                Console.WriteLine();

                // Create DataFlow mixing all three types
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = validateStepId;

                // Step 1: Native Code - Validate starting number
                flow.Steps[validateStepId] = new StepTransition
                {
                    Name = validateStepId,
                    OnSuccess = "get_random_rest_1",
                    OnFailure = null,
                    OnException = null
                };

                // Step 2: REST - Get random number
                flow.Steps["get_random_rest_1"] = new StepTransition
                {
                    Name = "get_random_rest_1",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=50&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "extract_number_from_api",
                    OnFailure = null,
                    OnException = null
                };

                // Step 3: CodeAttribute - Extract number from API
                flow.Steps["extract_number_from_api"] = new StepTransition
                {
                    Name = "extract_number_from_api",
                    OnSuccess = multiplyStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Step 4: Native Code - Multiply by 3
                flow.Steps[multiplyStepId] = new StepTransition
                {
                    Name = multiplyStepId,
                    OnSuccess = "add_ten",
                    OnFailure = null,
                    OnException = null
                };

                // Step 5: CodeAttribute - Add 10
                flow.Steps["add_ten"] = new StepTransition
                {
                    Name = "add_ten",
                    OnSuccess = "get_random_rest_2",
                    OnFailure = null,
                    OnException = null
                };

                // Step 6: REST - Get another random number (will be ignored, just testing the flow)
                flow.Steps["get_random_rest_2"] = new StepTransition
                {
                    Name = "get_random_rest_2",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = successStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Step 7: Native Code - Success step (pass through, ignoring REST result)
                flow.Steps[successStepId] = new StepTransition
                {
                    Name = successStepId,
                    OnSuccess = "create_summary",
                    OnFailure = null,
                    OnException = null
                };

                // Step 8: CodeAttribute - Create summary
                flow.Steps["create_summary"] = new StepTransition
                {
                    Name = "create_summary",
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                // Execute the flow starting with 100
                Console.WriteLine($"  Executing DataFlow with initial value: 100...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = 100
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                // Validate results
                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else if (flowResult.Data.ToString().StartsWith("Final result:"))
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - All three step types executed successfully!");
                }
                else
                {
                    result.ErrorMessage = $"Unexpected data format: {flowResult.Data}";
                    result.Passed = false;
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "create_summary", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestAllThreeStepTypesWithBranching(string tenantId)
        {
            string testName = "All Three Step Types with Branching (Native + REST + Attribute) - 12 steps";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Scanning assembly for [StepMethod] attributes...");
                int registeredCount = stepManager.ScanCallingAssembly(tenantId);
                Console.WriteLine($"  Registered {registeredCount} CodeAttribute steps.");
                Console.WriteLine();

                // Create native code steps
                string validateStepId = idGen.Generate("validate_branch", 64);
                string multiplyStepId = idGen.Generate("multiply_success", 64);
                string successStepId1 = idGen.Generate("success_1", 64);
                string successStepId2 = idGen.Generate("success_2", 64);
                string recoveryStepId = idGen.Generate("recovery", 64);

                stepManager.Add(new ValidateNumberStep(validateStepId, tenantId, 1, 50)); // Will fail if > 50
                stepManager.Add(new MultiplyStep(multiplyStepId, tenantId));
                stepManager.Add(new SuccessStep(successStepId1, tenantId));
                stepManager.Add(new SuccessStep(successStepId2, tenantId));
                stepManager.Add(new RecoveryStep(recoveryStepId, tenantId));

                Console.WriteLine($"  Building DataFlow with branching logic:");
                Console.WriteLine($"    Flow branches based on validation result:");
                Console.WriteLine($"      SUCCESS PATH: REST -> Native -> Attribute");
                Console.WriteLine($"      FAILURE PATH: Attribute -> REST -> Native");
                Console.WriteLine();

                // Create DataFlow with branching
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = "get_number_rest";

                // Step 1: REST - Get random number (may be > 50, causing validation to fail)
                flow.Steps["get_number_rest"] = new StepTransition
                {
                    Name = "get_number_rest",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "extract_number_from_api",
                    OnFailure = null,
                    OnException = null
                };

                // Step 2: CodeAttribute - Extract number
                flow.Steps["extract_number_from_api"] = new StepTransition
                {
                    Name = "extract_number_from_api",
                    OnSuccess = validateStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Step 3: Native Code - Validate (branches here)
                flow.Steps[validateStepId] = new StepTransition
                {
                    Name = validateStepId,
                    OnSuccess = multiplyStepId,        // Success path
                    OnFailure = "add_ten",              // Failure path
                    OnException = null
                };

                // SUCCESS PATH:
                // Step 4a: Native Code - Multiply
                flow.Steps[multiplyStepId] = new StepTransition
                {
                    Name = multiplyStepId,
                    OnSuccess = successStepId1,
                    OnFailure = null,
                    OnException = null
                };

                // Step 5a: Native Code - Success
                flow.Steps[successStepId1] = new StepTransition
                {
                    Name = successStepId1,
                    OnSuccess = "create_summary",
                    OnFailure = null,
                    OnException = null
                };

                // FAILURE PATH:
                // Step 4b: CodeAttribute - Add 10
                flow.Steps["add_ten"] = new StepTransition
                {
                    Name = "add_ten",
                    OnSuccess = recoveryStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Step 5b: Native Code - Recovery
                flow.Steps[recoveryStepId] = new StepTransition
                {
                    Name = recoveryStepId,
                    OnSuccess = "create_summary",
                    OnFailure = null,
                    OnException = null
                };

                // CONVERGENCE:
                // Step 6: CodeAttribute - Create summary (both paths lead here)
                flow.Steps["create_summary"] = new StepTransition
                {
                    Name = "create_summary",
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                // Execute the flow
                Console.WriteLine($"  Executing DataFlow...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = null
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                // Validate results
                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else if (flowResult.Data.ToString().StartsWith("Final result:"))
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - Branching logic with all three step types worked!");
                }
                else
                {
                    result.ErrorMessage = $"Unexpected data format: {flowResult.Data}";
                    result.Passed = false;
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "create_summary", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestAllThreeStepTypesWithErrorRecovery(string tenantId)
        {
            string testName = "All Three Step Types with Error Recovery (Native + REST + Attribute) - 11 steps";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Scanning assembly for [StepMethod] attributes...");
                int registeredCount = stepManager.ScanCallingAssembly(tenantId);
                Console.WriteLine($"  Registered {registeredCount} CodeAttribute steps.");
                Console.WriteLine();

                // Create native code steps (including error/exception steps)
                string successStepId1 = idGen.Generate("success_1", 64);
                string errorStepId = idGen.Generate("error_step", 64);
                string recoveryStepId1 = idGen.Generate("recovery_1", 64);
                string exceptionStepId = idGen.Generate("exception_step", 64);
                string recoveryStepId2 = idGen.Generate("recovery_2", 64);
                string multiplyStepId = idGen.Generate("multiply_final", 64);
                string successStepId2 = idGen.Generate("success_final", 64);

                stepManager.Add(new SuccessStep(successStepId1, tenantId));
                stepManager.Add(new ErrorStep(errorStepId, tenantId));
                stepManager.Add(new RecoveryStep(recoveryStepId1, tenantId));
                stepManager.Add(new ExceptionStep(exceptionStepId, tenantId));
                stepManager.Add(new RecoveryStep(recoveryStepId2, tenantId));
                stepManager.Add(new MultiplyStep(multiplyStepId, tenantId));
                stepManager.Add(new SuccessStep(successStepId2, tenantId));

                Console.WriteLine($"  Building DataFlow with error recovery across all step types:");
                Console.WriteLine($"    1. Native Code: Success");
                Console.WriteLine($"    2. CodeAttribute: Multiply (method_multiply)");
                Console.WriteLine($"    3. Native Code: Error (triggers failure path)");
                Console.WriteLine($"    4. Native Code: Recovery");
                Console.WriteLine($"    5. REST: Get random number");
                Console.WriteLine($"    6. CodeAttribute: Extract number");
                Console.WriteLine($"    7. Native Code: Exception (triggers exception path)");
                Console.WriteLine($"    8. CodeAttribute: Add 10 (recovery from exception)");
                Console.WriteLine($"    9. Native Code: Recovery");
                Console.WriteLine($"   10. Native Code: Multiply");
                Console.WriteLine($"   11. CodeAttribute: Create summary");
                Console.WriteLine();

                // Create DataFlow with error recovery
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = successStepId1;

                // Step 1: Native Code - Success
                flow.Steps[successStepId1] = new StepTransition
                {
                    Name = successStepId1,
                    OnSuccess = "method_multiply",
                    OnFailure = null,
                    OnException = null
                };

                // Step 2: CodeAttribute - Multiply
                flow.Steps["method_multiply"] = new StepTransition
                {
                    Name = "method_multiply",
                    OnSuccess = errorStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Step 3: Native Code - Error (will trigger OnFailure)
                flow.Steps[errorStepId] = new StepTransition
                {
                    Name = errorStepId,
                    OnSuccess = null,
                    OnFailure = recoveryStepId1,  // Error recovery path
                    OnException = null
                };

                // Step 4: Native Code - Recovery from error
                flow.Steps[recoveryStepId1] = new StepTransition
                {
                    Name = recoveryStepId1,
                    OnSuccess = "get_random_rest",
                    OnFailure = null,
                    OnException = null
                };

                // Step 5: REST - Get random number
                flow.Steps["get_random_rest"] = new StepTransition
                {
                    Name = "get_random_rest",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "extract_number_from_api",
                    OnFailure = null,
                    OnException = null
                };

                // Step 6: CodeAttribute - Extract number
                flow.Steps["extract_number_from_api"] = new StepTransition
                {
                    Name = "extract_number_from_api",
                    OnSuccess = exceptionStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Step 7: Native Code - Exception (will trigger OnException)
                flow.Steps[exceptionStepId] = new StepTransition
                {
                    Name = exceptionStepId,
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = "add_ten"  // Exception recovery path
                };

                // Step 8: CodeAttribute - Add 10 (recovery from exception)
                flow.Steps["add_ten"] = new StepTransition
                {
                    Name = "add_ten",
                    OnSuccess = recoveryStepId2,
                    OnFailure = null,
                    OnException = null
                };

                // Step 9: Native Code - Recovery
                flow.Steps[recoveryStepId2] = new StepTransition
                {
                    Name = recoveryStepId2,
                    OnSuccess = multiplyStepId,
                    OnFailure = null,
                    OnException = null
                };

                // Step 10: Native Code - Multiply
                flow.Steps[multiplyStepId] = new StepTransition
                {
                    Name = multiplyStepId,
                    OnSuccess = "create_summary",
                    OnFailure = null,
                    OnException = null
                };

                // Step 11: CodeAttribute - Create summary
                flow.Steps["create_summary"] = new StepTransition
                {
                    Name = "create_summary",
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                // Execute the flow
                Console.WriteLine($"  Executing DataFlow with initial value: 5...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = 5
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                // Validate results
                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success after error/exception recovery but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else if (flowResult.Data.ToString().StartsWith("Final result:"))
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - Error recovery across all three step types worked!");
                }
                else
                {
                    result.ErrorMessage = $"Unexpected data format: {flowResult.Data}";
                    result.Passed = false;
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "create_summary", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestExclusivelyNativeCodeSteps(string tenantId)
        {
            string testName = "Exclusively Native Code Steps (10 steps)";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Building DataFlow with only native code (class-based) steps:");
                Console.WriteLine($"    1. Success -> 2. Multiply -> 3. Success");
                Console.WriteLine($"    4. Multiply -> 5. Success -> 6. Multiply");
                Console.WriteLine($"    7. Multiply -> 8. Validate -> 9. Success");
                Console.WriteLine($"   10. DataTransform (final)");
                Console.WriteLine();

                // Create all native code steps
                string step1 = idGen.Generate("native_1", 64);
                string step2 = idGen.Generate("native_2", 64);
                string step3 = idGen.Generate("native_3", 64);
                string step4 = idGen.Generate("native_4", 64);
                string step5 = idGen.Generate("native_5", 64);
                string step6 = idGen.Generate("native_6", 64);
                string step7 = idGen.Generate("native_7", 64);
                string step8 = idGen.Generate("native_8", 64);
                string step9 = idGen.Generate("native_9", 64);
                string step10 = idGen.Generate("native_10", 64);

                stepManager.Add(new SuccessStep(step1, tenantId));
                stepManager.Add(new MultiplyStep(step2, tenantId));
                stepManager.Add(new SuccessStep(step3, tenantId));
                stepManager.Add(new MultiplyStep(step4, tenantId));
                stepManager.Add(new SuccessStep(step5, tenantId));
                stepManager.Add(new MultiplyStep(step6, tenantId));
                stepManager.Add(new MultiplyStep(step7, tenantId));
                stepManager.Add(new ValidateNumberStep(step8, tenantId, 1, 10000));
                stepManager.Add(new SuccessStep(step9, tenantId));
                stepManager.Add(new DataTransformStep(step10, tenantId, " [Native Code Only]"));

                // Create DataFlow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = step1;

                flow.Steps[step1] = new StepTransition { Name = step1, OnSuccess = step2, OnFailure = null, OnException = null };
                flow.Steps[step2] = new StepTransition { Name = step2, OnSuccess = step3, OnFailure = null, OnException = null };
                flow.Steps[step3] = new StepTransition { Name = step3, OnSuccess = step4, OnFailure = null, OnException = null };
                flow.Steps[step4] = new StepTransition { Name = step4, OnSuccess = step5, OnFailure = null, OnException = null };
                flow.Steps[step5] = new StepTransition { Name = step5, OnSuccess = step6, OnFailure = null, OnException = null };
                flow.Steps[step6] = new StepTransition { Name = step6, OnSuccess = step7, OnFailure = null, OnException = null };
                flow.Steps[step7] = new StepTransition { Name = step7, OnSuccess = step8, OnFailure = null, OnException = null };
                flow.Steps[step8] = new StepTransition { Name = step8, OnSuccess = step9, OnFailure = null, OnException = null };
                flow.Steps[step9] = new StepTransition { Name = step9, OnSuccess = step10, OnFailure = null, OnException = null };
                flow.Steps[step10] = new StepTransition { Name = step10, OnSuccess = null, OnFailure = null, OnException = null };

                Console.WriteLine($"  Executing DataFlow with initial value: 10...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = 10
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - All native code steps executed successfully!");
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, step10, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestExclusivelyRestSteps(string tenantId)
        {
            string testName = "Exclusively REST Steps (5 steps)";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Building DataFlow with only REST (inline) steps:");
                Console.WriteLine($"    1-5. All REST API calls to randomnumberapi.com");
                Console.WriteLine();

                // Create DataFlow with only REST steps
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = "rest_step_1";

                // Step 1: REST - Get random number
                flow.Steps["rest_step_1"] = new StepTransition
                {
                    Name = "rest_step_1",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=100&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "rest_step_2",
                    OnFailure = null,
                    OnException = null
                };

                // Step 2: REST - Get another random number
                flow.Steps["rest_step_2"] = new StepTransition
                {
                    Name = "rest_step_2",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=100&max=200&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "rest_step_3",
                    OnFailure = null,
                    OnException = null
                };

                // Step 3: REST - Get third random number
                flow.Steps["rest_step_3"] = new StepTransition
                {
                    Name = "rest_step_3",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=200&max=300&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "rest_step_4",
                    OnFailure = null,
                    OnException = null
                };

                // Step 4: REST - Get fourth random number
                flow.Steps["rest_step_4"] = new StepTransition
                {
                    Name = "rest_step_4",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=300&max=400&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = "rest_step_5",
                    OnFailure = null,
                    OnException = null
                };

                // Step 5: REST - Get fifth random number (final)
                flow.Steps["rest_step_5"] = new StepTransition
                {
                    Name = "rest_step_5",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=400&max=500&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                Console.WriteLine($"  Executing DataFlow with REST-only steps...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = null
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: REST response received");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - All REST steps executed successfully!");
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "rest_step_5", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestExclusivelyAttributeBasedSteps(string tenantId)
        {
            string testName = "Exclusively Attribute-Based Steps (3 steps)";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Scanning assembly for [StepMethod] attributes...");
                int registeredCount = stepManager.ScanCallingAssembly(tenantId);
                Console.WriteLine($"  Registered {registeredCount} CodeAttribute steps.");
                Console.WriteLine();

                Console.WriteLine($"  Building DataFlow with only attribute-based (method) steps:");
                Console.WriteLine($"    1. method_validate -> 2. method_multiply -> 3. method_format");
                Console.WriteLine();

                // Create DataFlow with only attribute-based steps
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = "method_validate";

                // Create a simple linear chain of attribute-based steps
                flow.Steps["method_validate"] = new StepTransition
                {
                    Name = "method_validate",
                    OnSuccess = "method_multiply",
                    OnFailure = null,
                    OnException = null
                };

                flow.Steps["method_multiply"] = new StepTransition
                {
                    Name = "method_multiply",
                    OnSuccess = "method_format",
                    OnFailure = null,
                    OnException = null
                };

                flow.Steps["method_format"] = new StepTransition
                {
                    Name = "method_format",
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                Console.WriteLine($"  Executing DataFlow with initial value: 5...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = 5
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else if (flowResult.Data.ToString().StartsWith("Processed:"))
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - All attribute-based steps executed successfully!");
                }
                else
                {
                    result.ErrorMessage = $"Unexpected data format: {flowResult.Data}";
                    result.Passed = false;
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "method_format", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestNativeAndRestCombination(string tenantId)
        {
            string testName = "Native + REST Combination (8 steps)";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Building DataFlow with Native and REST steps (no attribute-based):");
                Console.WriteLine($"    1. Native Success -> 2. REST API call");
                Console.WriteLine($"    3. Native Multiply -> 4. Native Success");
                Console.WriteLine($"    5. REST API call -> 6. Native AddRandomNumbers");
                Console.WriteLine($"    7. REST API call -> 8. Native DataTransform");
                Console.WriteLine();

                // Create native code steps
                string step1 = idGen.Generate("native_rest_1", 64);
                string step3 = idGen.Generate("native_rest_3", 64);
                string step4 = idGen.Generate("native_rest_4", 64);
                string step6 = idGen.Generate("native_rest_6", 64);
                string step8 = idGen.Generate("native_rest_8", 64);

                stepManager.Add(new SuccessStep(step1, tenantId));
                stepManager.Add(new MultiplyStep(step3, tenantId));
                stepManager.Add(new SuccessStep(step4, tenantId));
                stepManager.Add(new AddRandomNumbersStep(step6, tenantId));
                stepManager.Add(new DataTransformStep(step8, tenantId, " [Native+REST]"));

                // Create DataFlow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = step1;

                // Step 1: Native
                flow.Steps[step1] = new StepTransition { Name = step1, OnSuccess = "rest_step_2", OnFailure = null, OnException = null };

                // Step 2: REST
                flow.Steps["rest_step_2"] = new StepTransition
                {
                    Name = "rest_step_2",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=1&max=50&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = step3,
                    OnFailure = null,
                    OnException = null
                };

                // Step 3: Native
                flow.Steps[step3] = new StepTransition { Name = step3, OnSuccess = step4, OnFailure = null, OnException = null };

                // Step 4: Native
                flow.Steps[step4] = new StepTransition { Name = step4, OnSuccess = "rest_step_5", OnFailure = null, OnException = null };

                // Step 5: REST
                flow.Steps["rest_step_5"] = new StepTransition
                {
                    Name = "rest_step_5",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=50&max=100&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = step6,
                    OnFailure = null,
                    OnException = null
                };

                // Step 6: Native
                flow.Steps[step6] = new StepTransition { Name = step6, OnSuccess = "rest_step_7", OnFailure = null, OnException = null };

                // Step 7: REST
                flow.Steps["rest_step_7"] = new StepTransition
                {
                    Name = "rest_step_7",
                    StepType = StepTypeEnum.Rest,
                    Rest = new RestStepConfiguration
                    {
                        Method = "GET",
                        Url = "https://www.randomnumberapi.com/api/v1.0/random?min=100&max=150&count=1",
                        Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
                        TimeoutMs = 60000
                    },
                    OnSuccess = step8,
                    OnFailure = null,
                    OnException = null
                };

                // Step 8: Native
                flow.Steps[step8] = new StepTransition { Name = step8, OnSuccess = null, OnFailure = null, OnException = null };

                Console.WriteLine($"  Executing DataFlow with initial value: 7...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = 7
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - Native + REST combination executed successfully!");
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, step8, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestNativeAndAttributeCombination(string tenantId)
        {
            string testName = "Native + Attribute Combination (4 steps)";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");
            Console.WriteLine();

            try
            {
                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager();
                DataFlowRunner runner = new DataFlowRunner(stepManager);

                Console.WriteLine($"  Scanning assembly for [StepMethod] attributes...");
                int registeredCount = stepManager.ScanCallingAssembly(tenantId);
                Console.WriteLine($"  Registered {registeredCount} CodeAttribute steps.");
                Console.WriteLine();

                Console.WriteLine($"  Building DataFlow with Native and Attribute steps (no REST):");
                Console.WriteLine($"    1. Native Success -> 2. Attribute Multiply");
                Console.WriteLine($"    3. Native Multiply -> 4. Attribute Format");
                Console.WriteLine();

                // Create native code steps
                string step1 = idGen.Generate("native_attr_1", 64);
                string step3 = idGen.Generate("native_attr_3", 64);

                stepManager.Add(new SuccessStep(step1, tenantId));
                stepManager.Add(new MultiplyStep(step3, tenantId));

                // Create DataFlow
                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = step1;

                // Step 1: Native
                flow.Steps[step1] = new StepTransition { Name = step1, OnSuccess = "method_multiply", OnFailure = null, OnException = null };

                // Step 2: Attribute
                flow.Steps["method_multiply"] = new StepTransition { Name = "method_multiply", OnSuccess = step3, OnFailure = null, OnException = null };

                // Step 3: Native
                flow.Steps[step3] = new StepTransition { Name = step3, OnSuccess = "method_format", OnFailure = null, OnException = null };

                // Step 4: Attribute
                flow.Steps["method_format"] = new StepTransition { Name = "method_format", OnSuccess = null, OnFailure = null, OnException = null };

                Console.WriteLine($"  Executing DataFlow with initial value: 3...");
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = idGen.Generate("request_", 64),
                    Data = 3
                };

                StepResult flowResult = await runner.Run(flow, request);
                Console.WriteLine($"  DataFlow execution completed.");
                Console.WriteLine();

                Console.WriteLine($"  Validating results...");
                Console.WriteLine($"  Final output: {flowResult.Data}");

                if (flowResult.Result != StepResultTypeEnum.Success)
                {
                    result.ErrorMessage = $"Expected Success but got {flowResult.Result}";
                    if (flowResult.Exception != null)
                    {
                        result.ErrorMessage += $": {flowResult.Exception.Message}";
                    }
                    result.Passed = false;
                }
                else if (flowResult.Data == null)
                {
                    result.ErrorMessage = "No data returned from flow";
                    result.Passed = false;
                }
                else if (flowResult.Data.ToString().StartsWith("Processed:"))
                {
                    result.Passed = true;
                    Console.WriteLine($"  Validation: PASSED - Native + Attribute combination executed successfully!");
                }
                else
                {
                    result.ErrorMessage = $"Unexpected data format: {flowResult.Data}";
                    result.Passed = false;
                }

                Console.WriteLine();
                PrintTestResult(testName, result.Passed, flowResult.Result, "method_format", flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                Console.WriteLine($"  ERROR: {ex.Message}");
                Console.WriteLine();
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<List<TestResult>> RunLoggingTests()
        {
            Console.WriteLine();
            Console.WriteLine("--- Running Logging Tests ---");
            Console.WriteLine();

            List<TestResult> results = new List<TestResult>();
            IdGenerator idGen = new IdGenerator();
            string tenantId = idGen.Generate("tenant", 64);

            results.Add(await TestLoggingPerRequestLogFiles(tenantId));
            results.Add(await TestLoggingMultipleRequests(tenantId));
            results.Add(await TestLoggingContentFormat(tenantId));
            results.Add(await TestLoggingStepTimings(tenantId));
            results.Add(await TestLoggingWithErrors(tenantId));

            return results;
        }

        static async Task<TestResult> TestLoggingPerRequestLogFiles(string tenantId)
        {
            string testName = "Logging: Per-Request Log Files Created";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            string testLogDir = $"./test_logs_{Guid.NewGuid()}";
            string testRequestsDir = $"./test_requests_{Guid.NewGuid()}";

            try
            {
                // Create logger
                Directory.CreateDirectory(testLogDir);
                Directory.CreateDirectory(testRequestsDir);
                Tempo.Logs.SyslogLogger logger = new Tempo.Logs.SyslogLogger(
                    logDirectory: testLogDir,
                    logFilename: "tempo.log",
                    dfLogsDirectory: testRequestsDir,
                    console: false
                );

                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager(logger);
                DataFlowRunner runner = new DataFlowRunner(stepManager, logger);

                // Create simple flow with 3 steps
                for (int i = 0; i < 3; i++)
                {
                    string stepId = idGen.Generate($"step_{i}", 64);
                    stepManager.Add(new SuccessStep(stepId, tenantId));
                }

                DataFlow flow = new DataFlow { TenantId = tenantId };
                var steps = stepManager.All(tenantId).ToList();
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Run the flow
                string requestId = idGen.Generate("request_", 64);
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = requestId,
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Verify log file was created with correct name
                string expectedLogFile = Path.Combine(testRequestsDir, $"{requestId}.log");
                if (!File.Exists(expectedLogFile))
                {
                    result.Passed = false;
                    result.ErrorMessage = $"Expected log file not found: {expectedLogFile}";
                }
                else
                {
                    result.Passed = true;
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, steps[steps.Count - 1].Identifier, flowResult.Data, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestLoggingMultipleRequests(string tenantId)
        {
            string testName = "Logging: Multiple Requests Create Separate Log Files";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            string testLogDir = $"./test_logs_{Guid.NewGuid()}";
            string testRequestsDir = $"./test_requests_{Guid.NewGuid()}";

            try
            {
                // Create logger
                Directory.CreateDirectory(testLogDir);
                Directory.CreateDirectory(testRequestsDir);
                Tempo.Logs.SyslogLogger logger = new Tempo.Logs.SyslogLogger(
                    logDirectory: testLogDir,
                    logFilename: "tempo.log",
                    dfLogsDirectory: testRequestsDir,
                    console: false
                );

                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager(logger);
                DataFlowRunner runner = new DataFlowRunner(stepManager, logger);

                // Create simple flow
                for (int i = 0; i < 2; i++)
                {
                    string stepId = idGen.Generate($"step_{i}", 64);
                    stepManager.Add(new SuccessStep(stepId, tenantId));
                }

                DataFlow flow = new DataFlow { TenantId = tenantId };
                var steps = stepManager.All(tenantId).ToList();
                flow.StartStepId = steps[0].Identifier;

                for (int i = 0; i < steps.Count; i++)
                {
                    flow.Steps[steps[i].Identifier] = new StepTransition
                    {
                        OnSuccess = i < steps.Count - 1 ? steps[i + 1].Identifier : null,
                        OnFailure = null,
                        OnException = null
                    };
                }

                // Run the flow 3 times with different request IDs
                List<string> requestIds = new List<string>();
                for (int run = 0; run < 3; run++)
                {
                    string requestId = idGen.Generate($"request_{run}", 64);
                    requestIds.Add(requestId);

                    StepRequest request = new StepRequest
                    {
                        DataFlowId = flow.Identifier,
                        RequestId = requestId,
                        Data = $"test data {run}"
                    };

                    await runner.Run(flow, request);
                }

                // Verify all 3 log files were created
                int foundCount = 0;
                foreach (string requestId in requestIds)
                {
                    string expectedLogFile = Path.Combine(testRequestsDir, $"{requestId}.log");
                    if (File.Exists(expectedLogFile))
                    {
                        foundCount++;
                    }
                    else
                    {
                        result.Details.Add($"Missing log file: {expectedLogFile}");
                    }
                }

                result.Passed = (foundCount == 3);
                if (!result.Passed)
                {
                    result.ErrorMessage = $"Expected 3 log files, found {foundCount}";
                }

                PrintTestResult(testName, result.Passed, StepResultTypeEnum.Success, "N/A", $"{foundCount}/3 files", result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestLoggingContentFormat(string tenantId)
        {
            string testName = "Logging: Log Content Format Validation";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            string testLogDir = $"./test_logs_{Guid.NewGuid()}";
            string testRequestsDir = $"./test_requests_{Guid.NewGuid()}";

            try
            {
                // Create logger
                Directory.CreateDirectory(testLogDir);
                Directory.CreateDirectory(testRequestsDir);
                Tempo.Logs.SyslogLogger logger = new Tempo.Logs.SyslogLogger(
                    logDirectory: testLogDir,
                    logFilename: "tempo.log",
                    dfLogsDirectory: testRequestsDir,
                    console: false
                );

                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager(logger);
                DataFlowRunner runner = new DataFlowRunner(stepManager, logger);

                // Create flow with 2 steps
                string step1Id = idGen.Generate("step_1", 64);
                string step2Id = idGen.Generate("step_2", 64);
                stepManager.Add(new SuccessStep(step1Id, tenantId));
                stepManager.Add(new SuccessStep(step2Id, tenantId));

                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = step1Id;
                flow.Steps[step1Id] = new StepTransition
                {
                    OnSuccess = step2Id,
                    OnFailure = null,
                    OnException = null
                };
                flow.Steps[step2Id] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                // Run the flow
                string requestId = idGen.Generate("request_", 64);
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = requestId,
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Read and validate log content
                string logFile = Path.Combine(testRequestsDir, $"{requestId}.log");
                string[] logLines = File.ReadAllLines(logFile);

                // Expected log messages:
                // 1. Starting data flow execution
                // 2. Starting step 'step_1'
                // 3. Step 'step_1' completed (Result: Success, Runtime: Xms)
                // 4. Starting step 'step_2'
                // 5. Step 'step_2' completed (Result: Success, Runtime: Xms)
                // 6. Data flow execution completed

                bool hasDataFlowStart = logLines.Any(l => l.Contains("Starting data flow execution"));
                bool hasStep1Start = logLines.Any(l => l.Contains($"Starting step '{step1Id}'"));
                bool hasStep1Complete = logLines.Any(l => l.Contains($"Step '{step1Id}' completed") && l.Contains("Runtime:") && l.Contains("ms"));
                bool hasStep2Start = logLines.Any(l => l.Contains($"Starting step '{step2Id}'"));
                bool hasStep2Complete = logLines.Any(l => l.Contains($"Step '{step2Id}' completed") && l.Contains("Runtime:") && l.Contains("ms"));
                bool hasDataFlowComplete = logLines.Any(l => l.Contains("Data flow execution completed") && l.Contains("Runtime:") && l.Contains("ms"));

                result.Passed = hasDataFlowStart && hasStep1Start && hasStep1Complete && hasStep2Start && hasStep2Complete && hasDataFlowComplete;

                if (!result.Passed)
                {
                    result.ErrorMessage = "Log content validation failed";
                    if (!hasDataFlowStart) result.Details.Add("Missing: Data flow start message");
                    if (!hasStep1Start) result.Details.Add($"Missing: Step '{step1Id}' start message");
                    if (!hasStep1Complete) result.Details.Add($"Missing: Step '{step1Id}' complete message with runtime");
                    if (!hasStep2Start) result.Details.Add($"Missing: Step '{step2Id}' start message");
                    if (!hasStep2Complete) result.Details.Add($"Missing: Step '{step2Id}' complete message with runtime");
                    if (!hasDataFlowComplete) result.Details.Add("Missing: Data flow complete message with runtime");
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, step2Id, $"{logLines.Length} lines", result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestLoggingStepTimings(string tenantId)
        {
            string testName = "Logging: Step Timing Information";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            string testLogDir = $"./test_logs_{Guid.NewGuid()}";
            string testRequestsDir = $"./test_requests_{Guid.NewGuid()}";

            try
            {
                // Create logger
                Directory.CreateDirectory(testLogDir);
                Directory.CreateDirectory(testRequestsDir);
                Tempo.Logs.SyslogLogger logger = new Tempo.Logs.SyslogLogger(
                    logDirectory: testLogDir,
                    logFilename: "tempo.log",
                    dfLogsDirectory: testRequestsDir,
                    console: false
                );

                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager(logger);
                DataFlowRunner runner = new DataFlowRunner(stepManager, logger);

                // Create flow
                string stepId = idGen.Generate("step_1", 64);
                stepManager.Add(new SuccessStep(stepId, tenantId));

                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = stepId;
                flow.Steps[stepId] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                // Run the flow
                string requestId = idGen.Generate("request_", 64);
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = requestId,
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Read log and verify timing information
                string logFile = Path.Combine(testRequestsDir, $"{requestId}.log");
                string[] logLines = File.ReadAllLines(logFile);

                // Find step complete line and extract runtime
                string stepCompleteLine = logLines.FirstOrDefault(l => l.Contains($"Step '{stepId}' completed"));
                string flowCompleteLine = logLines.FirstOrDefault(l => l.Contains("Data flow execution completed"));

                bool hasStepRuntime = false;
                bool hasFlowRuntime = false;

                if (stepCompleteLine != null)
                {
                    // Extract runtime value (e.g., "Runtime: 5ms")
                    int runtimeIndex = stepCompleteLine.IndexOf("Runtime:");
                    if (runtimeIndex > 0)
                    {
                        string runtimePart = stepCompleteLine.Substring(runtimeIndex);
                        hasStepRuntime = runtimePart.Contains("ms") && runtimePart.Contains("Runtime:");
                    }
                }

                if (flowCompleteLine != null)
                {
                    int runtimeIndex = flowCompleteLine.IndexOf("Runtime:");
                    if (runtimeIndex > 0)
                    {
                        string runtimePart = flowCompleteLine.Substring(runtimeIndex);
                        hasFlowRuntime = runtimePart.Contains("ms") && runtimePart.Contains("Runtime:");
                    }
                }

                result.Passed = hasStepRuntime && hasFlowRuntime;

                if (!result.Passed)
                {
                    result.ErrorMessage = "Timing information validation failed";
                    if (!hasStepRuntime) result.Details.Add("Missing or invalid step runtime");
                    if (!hasFlowRuntime) result.Details.Add("Missing or invalid flow runtime");
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, stepId, "Timing validated", result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static async Task<TestResult> TestLoggingWithErrors(string tenantId)
        {
            string testName = "Logging: Error and Exception Handling";
            TestResult result = new TestResult { TestName = testName };

            Console.Write($"Running: {testName}... ");

            string testLogDir = $"./test_logs_{Guid.NewGuid()}";
            string testRequestsDir = $"./test_requests_{Guid.NewGuid()}";

            try
            {
                // Create logger
                Directory.CreateDirectory(testLogDir);
                Directory.CreateDirectory(testRequestsDir);
                Tempo.Logs.SyslogLogger logger = new Tempo.Logs.SyslogLogger(
                    logDirectory: testLogDir,
                    logFilename: "tempo.log",
                    dfLogsDirectory: testRequestsDir,
                    console: false
                );

                IdGenerator idGen = new IdGenerator();
                StepManager stepManager = new StepManager(logger);
                DataFlowRunner runner = new DataFlowRunner(stepManager, logger);

                // Create flow with error step
                string step1Id = idGen.Generate("step_1", 64);
                string step2Id = idGen.Generate("step_2_error", 64);
                stepManager.Add(new SuccessStep(step1Id, tenantId));
                stepManager.Add(new ErrorStep(step2Id, tenantId));

                DataFlow flow = new DataFlow { TenantId = tenantId };
                flow.StartStepId = step1Id;
                flow.Steps[step1Id] = new StepTransition
                {
                    OnSuccess = step2Id,
                    OnFailure = null,
                    OnException = null
                };
                flow.Steps[step2Id] = new StepTransition
                {
                    OnSuccess = null,
                    OnFailure = null,
                    OnException = null
                };

                // Run the flow
                string requestId = idGen.Generate("request_", 64);
                StepRequest request = new StepRequest
                {
                    DataFlowId = flow.Identifier,
                    RequestId = requestId,
                    Data = "test data"
                };

                StepResult flowResult = await runner.Run(flow, request);

                // Read log and verify error is logged
                string logFile = Path.Combine(testRequestsDir, $"{requestId}.log");
                string[] logLines = File.ReadAllLines(logFile);

                bool hasErrorStepComplete = logLines.Any(l => l.Contains($"Step '{step2Id}' completed") && l.Contains("Result: Error"));
                bool hasFlowComplete = logLines.Any(l => l.Contains("Data flow execution completed"));

                result.Passed = hasErrorStepComplete && hasFlowComplete;

                if (!result.Passed)
                {
                    result.ErrorMessage = "Error logging validation failed";
                    if (!hasErrorStepComplete) result.Details.Add("Missing step error completion message");
                    if (!hasFlowComplete) result.Details.Add("Missing flow completion message");
                }

                PrintTestResult(testName, result.Passed, flowResult.Result, step2Id, $"{logLines.Length} lines", result.ErrorMessage);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Details.Add(ex.ToString());
                result.Passed = false;
                PrintTestResult(testName, false, null, "N/A", null, ex.Message);
            }

            return result;
        }

        static void CleanupTestLogFiles()
        {
            try
            {
                Console.WriteLine("Cleaning up test log files...");

                string currentDirectory = Directory.GetCurrentDirectory();

                // Clean up test log directories
                string[] logDirs = Directory.GetDirectories(currentDirectory, "test_logs_*");
                string[] requestDirs = Directory.GetDirectories(currentDirectory, "test_requests_*");

                int deletedCount = 0;

                foreach (string dir in logDirs.Concat(requestDirs))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: Could not delete {dir}: {ex.Message}");
                    }
                }

                if (deletedCount > 0)
                {
                    Console.WriteLine($"  Deleted {deletedCount} test log director{(deletedCount == 1 ? "y" : "ies")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error during log cleanup: {ex.Message}");
            }
        }

#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.
    }
}