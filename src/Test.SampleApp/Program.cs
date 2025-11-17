using System;
using System.IO;
using System.Threading.Tasks;
using Tempo;
using Tempo.Logs;
using Tempo.Runners;
using Test.SampleApp;

namespace Test.SampleApp
{
    class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        static async Task Main(string[] args)
        {
            // Parse number of runs from command line argument
            int numberOfRuns = 1;
            if (args.Length > 0 && int.TryParse(args[0], out int parsedRuns))
            {
                numberOfRuns = parsedRuns;
            }

            Console.WriteLine("=== Test Data Flow ===");
            Console.WriteLine($"Running data flow {numberOfRuns} time(s)");
            Console.WriteLine();

            // Create logger
            Directory.CreateDirectory("./logs");
            Directory.CreateDirectory("./dataflowruns");
            SyslogLogger logger = new SyslogLogger(
                logDirectory: "./logs",
                logFilename: "tempo.log",
                dfLogsDirectory: "./dataflowruns",
                console: true
            );

            // Create step instances
            StepA stepA = new StepA { Identifier = "step-a", TenantId = "test-tenant" };
            StepB stepB = new StepB { Identifier = "step-b", TenantId = "test-tenant" };
            StepC stepC = new StepC { Identifier = "step-c", TenantId = "test-tenant" };
            StepD stepD = new StepD { Identifier = "step-d", TenantId = "test-tenant" };
            StepE stepE = new StepE { Identifier = "step-e", TenantId = "test-tenant" };

            // Create step manager and add steps
            StepManager stepManager = new StepManager(logger);
            stepManager.Add(stepA);
            stepManager.Add(stepB);
            stepManager.Add(stepC);
            stepManager.Add(stepD);
            stepManager.Add(stepE);

            // Create data flow
            DataFlow dataFlow = new DataFlow
            {
                Identifier = "test-dataflow",
                TenantId = "test-tenant",
                Name = "Test Data Flow",
                StartStepId = "step-a",
                Steps = new Dictionary<string, StepTransition>
                {
                    ["step-a"] = new StepTransition
                    {
                        Name = "Step A Transition",
                        OnSuccess = "step-c",
                        OnFailure = "step-b",
                        OnException = "step-e"
                    },
                    ["step-b"] = new StepTransition
                    {
                        Name = "Step B Transition",
                        OnSuccess = null,
                        OnFailure = null,
                        OnException = null
                    },
                    ["step-c"] = new StepTransition
                    {
                        Name = "Step C Transition",
                        OnSuccess = "step-d",
                        OnFailure = null,
                        OnException = null
                    },
                    ["step-d"] = new StepTransition
                    {
                        Name = "Step D Transition",
                        OnSuccess = null,
                        OnFailure = "step-b",
                        OnException = null
                    },
                    ["step-e"] = new StepTransition
                    {
                        Name = "Step E Transition",
                        OnSuccess = null,
                        OnFailure = null,
                        OnException = null
                    }
                }
            };

            // Create data flow runner
            DataFlowRunner runner = new DataFlowRunner(stepManager, logger);

            // Run the data flow multiple times
            for (int i = 1; i <= numberOfRuns; i++)
            {
                if (numberOfRuns > 1)
                {
                    Console.WriteLine($"==================== RUN {i}/{numberOfRuns} ====================");
                    Console.WriteLine();
                }

                // Create step request for this run
                StepRequest request = new StepRequest
                {
                    DataFlowId = dataFlow.Identifier,
                    RequestId = $"test-request-{i:D3}"
                };

                // Run the data flow
                Console.WriteLine("Starting data flow execution...");
                Console.WriteLine();

                try
                {
                    StepResult result = await runner.Run(dataFlow, request);

                    Console.WriteLine();
                    Console.WriteLine("=== Data Flow Completed ===");
                    if (result != null)
                    {
                        Console.WriteLine($"Final Result Type: {result.Result}");
                        Console.WriteLine($"Final Data: {result.Data}");
                    }
                    else
                    {
                        Console.WriteLine("Flow completed with null result (terminal step)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== Data Flow Failed ===");
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }

                Console.WriteLine();
            }
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
