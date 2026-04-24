namespace Tempo.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Enums;
    using Tempo.Logs;
    using Tempo.Protocol;

    /// <summary>
    /// Abstract base class for step runners.
    /// </summary>
    public abstract class StepRunner
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8601 // Possible null reference assignment.

        /// <summary>
        /// Logger instance for logging step execution.
        /// </summary>
        protected Logger? Logger { get; set; }

        /// <summary>
        /// Execute a step.
        /// </summary>
        /// <param name="stepId">Step identifier (for error messages).</param>
        /// <param name="req">Step request.</param>
        /// <param name="maxRuntimeMs">Maximum runtime in milliseconds (0 for no timeout).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Step result.</returns>
        public async Task<StepResult> Execute(string stepId, StepRequest req, int maxRuntimeMs = 0, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(stepId);
            ArgumentNullException.ThrowIfNull(req);
            req.ProtocolVersion = ProtocolVersions.Normalize(req.ProtocolVersion);

            DateTime startTime = DateTime.UtcNow;

            // Log step start
            if (Logger != null && !String.IsNullOrEmpty(req.RequestId))
            {
                await Logger.Info(req.RequestId, $"Starting step {stepId}").ConfigureAwait(false);
            }

            StepResult result;

            try
            {
                // Execute with optional timeout
                if (maxRuntimeMs > 0)
                {
                    result = await ExecuteWithTimeout(stepId, req, maxRuntimeMs, token).ConfigureAwait(false);
                }
                else
                {
                    result = await ExecuteInternal(req, token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Catch any exceptions and return as exception result
                result = new StepResult
                {
                    DataFlowId = req.DataFlowId,
                    RequestId = req.RequestId,
                    Result = StepResultTypeEnum.Exception,
                    Exception = ex,
                    Data = null,
                    Metadata = req.Metadata
                };
            }

            result = NormalizeResult(result, req);

            // Calculate runtime
            TimeSpan runtime = DateTime.UtcNow - startTime;
            long runtimeMs = (long)runtime.TotalMilliseconds;

            // Log step completion
            if (Logger != null && !String.IsNullOrEmpty(req.RequestId))
            {
                string resultStr = result?.Result.ToString() ?? "Unknown";
                await Logger.Info(req.RequestId, $"Step {stepId} completed (Result: {resultStr}, Runtime: {runtimeMs}ms)").ConfigureAwait(false);
            }

            return result ?? throw new InvalidOperationException($"Step '{stepId}' execution failed to produce a result.");
        }

        /// <summary>
        /// Normalize protocol and correlation fields after runner execution.
        /// </summary>
        /// <param name="result">Step result.</param>
        /// <param name="req">Step request.</param>
        /// <returns>Normalized step result.</returns>
        private static StepResult NormalizeResult(StepResult result, StepRequest req)
        {
            if (result == null) throw new InvalidOperationException("Step execution failed to produce a result.");
            result.ProtocolVersion = req.ProtocolVersion;
            result.TenantId = req.TenantId;
            result.DataFlowId = req.DataFlowId;
            result.FlowRunId = req.FlowRunId;
            result.StepRunId = req.StepRunId;
            result.RequestId = req.RequestId;
            return result;
        }

        /// <summary>
        /// Execute step with timeout.
        /// </summary>
        /// <param name="stepId">Step identifier.</param>
        /// <param name="req">Step request.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Step result.</returns>
        private async Task<StepResult> ExecuteWithTimeout(string stepId, StepRequest req, int timeoutMs, CancellationToken token)
        {
            using (CancellationTokenSource timeoutCts = new CancellationTokenSource())
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token))
            {
                Task<StepResult> stepTask = ExecuteInternal(req, linkedCts.Token);
                Task timeoutTask = Task.Delay(timeoutMs, timeoutCts.Token);

                // Race the step execution against the timeout
                Task completedTask = await Task.WhenAny(stepTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    // Timeout occurred
                    return new StepResult
                    {
                        DataFlowId = req.DataFlowId,
                        RequestId = req.RequestId,
                        Result = StepResultTypeEnum.Timeout,
                        Data = req.Data,
                        Metadata = req.Metadata,
                        Exception = new TimeoutException($"Step '{stepId}' exceeded maximum runtime of {timeoutMs}ms")
                    };
                }
                else
                {
                    // Step completed within timeout
                    timeoutCts.Cancel(); // Cancel the timeout task
                    return await stepTask.ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Internal execution method to be implemented by derived classes.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Step result.</returns>
        protected abstract Task<StepResult> ExecuteInternal(StepRequest req, CancellationToken token);

#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
