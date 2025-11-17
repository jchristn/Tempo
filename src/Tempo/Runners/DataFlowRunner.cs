namespace Tempo.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Enums;
    using Tempo.Logs;
    using Tempo.Metrics;

    /// <summary>
    /// Data flow runner.
    /// </summary>
    public class DataFlowRunner
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8603 // Possible null reference return.

        /// <summary>
        /// Metrics store.
        /// </summary>
        public MetricsStore MetricsStore
        {
            get => _MetricsStore;
            set => _MetricsStore = value;
        }

        private readonly StepManager _StepManager;
        private MetricsStore _MetricsStore = null;
        private Logger _Logger = null;

        /// <summary>
        /// Data flow runner.
        /// </summary>
        /// <param name="stepManager">Step manager.</param>
        /// <param name="logger">Logger instance for logging data flow execution (optional).</param>
        public DataFlowRunner(StepManager stepManager, Logger logger = null)
        {
            _StepManager = stepManager ?? throw new ArgumentNullException(nameof(stepManager));
            _Logger = logger;
        }

        /// <summary>
        /// Run a data flow.
        /// </summary>
        /// <param name="flow">Data flow.</param>
        /// <param name="req">Step request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Step result.</returns>
        public async Task<StepResult> Run(DataFlow flow, StepRequest req, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(flow);
            ArgumentNullException.ThrowIfNull(req);

            if (!flow.ValidateStartingStep())
                throw new InvalidOperationException("The specified starting step " + flow.StartStepId + " could not be found in flow " + flow.Identifier + ".");

            if (!flow.ValidateStepReferences(out List<string> validationErrors))
                throw new InvalidOperationException("One or more steps defined in flow " + flow.Identifier + " failed validation: " + string.Join(", ", validationErrors));

            // Create DataFlowRunDetails to track this execution
            DataFlowRunDetails flowRunDetails = new DataFlowRunDetails
            {
                TenantId = flow.TenantId,
                DataFlowId = flow.Identifier,
                RequestId = req.RequestId,
                StartUtc = DateTime.UtcNow,
                Result = StepResultTypeEnum.Success
            };

            // Track flow start time for timeout
            DateTime flowStartTime = DateTime.UtcNow;

            // Log data flow start
            if (_Logger != null)
            {
                await _Logger.Info(req.RequestId, $"Starting data flow execution (DataFlowId: {flow.Identifier})").ConfigureAwait(false);
            }

            string currentStepId = flow.StartStepId;
            StepResult lastResult = null;
            Dictionary<string, int> transitionCounts = new Dictionary<string, int>();
            List<StepRunDetails> stepRunDetailsList = new List<StepRunDetails>();

            while (!string.IsNullOrEmpty(currentStepId))
            {
                token.ThrowIfCancellationRequested();

                // Check DataFlow timeout
                if (flow.MaxRuntimeMs > 0)
                {
                    TimeSpan elapsed = DateTime.UtcNow - flowStartTime;
                    if (elapsed.TotalMilliseconds > flow.MaxRuntimeMs)
                    {
                        lastResult = new StepResult
                        {
                            DataFlowId = req.DataFlowId,
                            RequestId = req.RequestId,
                            Result = StepResultTypeEnum.Timeout,
                            Data = req.Data,
                            Metadata = req.Metadata,
                            Exception = new TimeoutException($"DataFlow '{flow.Identifier}' exceeded maximum runtime of {flow.MaxRuntimeMs}ms")
                        };
                        break;
                    }
                }

                // Get the step transition definition
                if (!flow.Steps.TryGetValue(currentStepId, out StepTransition stepTransition))
                {
                    throw new InvalidOperationException($"Step '{currentStepId}' not found in flow '{flow.Identifier}'.");
                }

                // Track transition count for this step
                if (!transitionCounts.ContainsKey(currentStepId))
                {
                    transitionCounts[currentStepId] = 0;
                }
                transitionCounts[currentStepId]++;

                // Check if MaxTransitions has been exceeded
                if (stepTransition.MaxTransitions > 0 && transitionCounts[currentStepId] > stepTransition.MaxTransitions)
                {
                    lastResult = new StepResult
                    {
                        DataFlowId = req.DataFlowId,
                        RequestId = req.RequestId,
                        Result = StepResultTypeEnum.MaxIterationsExceeded,
                        Data = req.Data,
                        Metadata = req.Metadata
                    };
                    break;
                }

                // Resolve the step runner (either from StepManager or inline definition)
                StepRunner stepRunner = ResolveStepRunner(stepTransition, currentStepId, flow.TenantId);

                // Create StepRunDetails to track this step execution
                StepRunDetails stepRunDetails = new StepRunDetails
                {
                    TenantId = flow.TenantId,
                    DataFlowId = flow.Identifier,
                    StepId = currentStepId,
                    RequestId = req.RequestId,
                    StartUtc = DateTime.UtcNow
                };

                // Determine max runtime (from step if code-based, or from transition config)
                int maxRuntimeMs = 0;
                if (stepRunner is CodeStepRunner || stepRunner is CodeAttributeStepRunner)
                {
                    // For code steps (both regular and attribute-based), get from StepManager
                    maxRuntimeMs = _StepManager.GetMaxRuntimeMs(currentStepId, flow.TenantId);
                }
                else if (stepRunner is RestStepRunner && stepTransition.Rest != null)
                {
                    // For REST steps, use config timeout
                    maxRuntimeMs = stepTransition.Rest.TimeoutMs;
                }

                // Execute the step using the runner
                lastResult = await stepRunner.Execute(currentStepId, req, maxRuntimeMs, token).ConfigureAwait(false);

                if (lastResult == null)
                {
                    throw new InvalidOperationException($"Step '{currentStepId}' returned null result.");
                }

                // Process result and update for next step
                bool isException = lastResult.Result == StepResultTypeEnum.Exception || lastResult.Exception != null;
                currentStepId = ProcessStepResult(lastResult, stepTransition, req, stepRunDetails, isException);

                // Write step run to metrics store if configured
                await WriteStepRunDetailsAsync(stepRunDetails).ConfigureAwait(false);
            }

            // Update flow run details with final information
            flowRunDetails.EndUtc = DateTime.UtcNow;
            if (lastResult != null)
            {
                flowRunDetails.Result = lastResult.Result;
            }

            // Calculate runtime
            TimeSpan runtime = flowRunDetails.EndUtc - flowRunDetails.StartUtc;
            long runtimeMs = (long)runtime.TotalMilliseconds;

            // Log data flow completion
            if (_Logger != null)
            {
                string resultStr = lastResult?.Result.ToString() ?? "Unknown";
                await _Logger.Info(req.RequestId, $"Data flow execution completed (DataFlowId: {flow.Identifier}, Result: {resultStr}, Runtime: {runtimeMs}ms)").ConfigureAwait(false);
            }

            // Write flow run to metrics store if configured
            if (_MetricsStore != null)
            {
                await _MetricsStore.WriteDataFlowRun(flowRunDetails).ConfigureAwait(false);
            }

            // Return the final result
            return lastResult ?? throw new InvalidOperationException($"Data flow '{flow.Identifier}' completed without producing a result.");
        }

        /// <summary>
        /// Resolve a step runner from a step transition.
        /// </summary>
        /// <param name="stepTransition">Step transition.</param>
        /// <param name="stepId">Step identifier.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>StepRunner instance.</returns>
        private StepRunner ResolveStepRunner(StepTransition stepTransition, string stepId, string tenantId)
        {
            // Check if there's a step type (inline step definition)
            if (stepTransition.StepType.HasValue)
            {
                // Create runner based on step type
                return stepTransition.StepType.Value switch
                {
                    StepTypeEnum.Rest => RestStepRunner.FromConfig(stepTransition.Rest
                        ?? throw new InvalidOperationException($"Step type '{StepTypeEnum.Rest}' requires Rest configuration."),
                        _Logger),
                    StepTypeEnum.Code => throw new InvalidOperationException($"Step type '{StepTypeEnum.Code}' should not be used with inline definitions. Use StepManager instead."),
                    _ => throw new NotSupportedException($"Step type '{stepTransition.StepType.Value}' is not supported.")
                };
            }
            else
            {
                // Look up code-based step from StepManager (checks both regular steps and attribute-based methods)
                StepRunner runner = _StepManager.GetStepRunner(stepId, tenantId);
                if (runner == null)
                {
                    throw new InvalidOperationException($"Step '{stepId}' not found in step manager for tenant '{tenantId}'.");
                }

                return runner;
            }
        }

        /// <summary>
        /// Process step result, update request for next step, and update step run details.
        /// </summary>
        /// <param name="result">Step result.</param>
        /// <param name="stepTransition">Step transition.</param>
        /// <param name="req">Step request to update.</param>
        /// <param name="stepRunDetails">Step run details to update.</param>
        /// <param name="isException">Whether this is an exception scenario.</param>
        /// <returns>Next step identifier.</returns>
        private string ProcessStepResult(StepResult result, StepTransition stepTransition, StepRequest req, StepRunDetails stepRunDetails, bool isException = false)
        {
            string nextStepId;

            if (isException)
            {
                // For exceptions, update request with exception info
                req.Data = null;
                req.Metadata = result.Exception;
                req.PreviousResult = StepResultTypeEnum.Exception;
                nextStepId = stepTransition.OnException;
            }
            else
            {
                // Update request data with result data for next step
                req.Data = result.Data;
                req.Metadata = result.Metadata;
                req.PreviousResult = result.Result;

                // Determine next step based on result
                nextStepId = result.Result switch
                {
                    StepResultTypeEnum.Success => stepTransition.OnSuccess,
                    StepResultTypeEnum.Error => stepTransition.OnFailure,
                    StepResultTypeEnum.Exception => stepTransition.OnException,
                    StepResultTypeEnum.Timeout => stepTransition.OnException,
                    _ => null
                };
            }

            // Update step run details
            stepRunDetails.EndUtc = DateTime.UtcNow;
            stepRunDetails.Result = result.Result;
            stepRunDetails.NextStepId = nextStepId;

            return nextStepId;
        }

        /// <summary>
        /// Write step run details to metrics store if configured.
        /// </summary>
        /// <param name="stepRunDetails">Step run details.</param>
        private async Task WriteStepRunDetailsAsync(StepRunDetails stepRunDetails)
        {
            if (_MetricsStore != null)
            {
                await _MetricsStore.WriteStepRun(stepRunDetails).ConfigureAwait(false);
            }
        }

#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
