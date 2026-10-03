namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Enums;

    /// <summary>
    /// Attribute-style step methods used by the telemetry suite to drive success, error, exception, and timeout paths.
    /// </summary>
    public static class TelemetryTestSteps
    {
        /// <summary>Succeed, passing the input through.</summary>
        /// <param name="req">Step request.</param>
        /// <returns>A success result.</returns>
        public static Task<StepResult> Succeed(StepRequest req)
        {
            return Task.FromResult(new StepResult { Result = StepResultTypeEnum.Success, Data = req.Data });
        }

        /// <summary>Return an expected failure (routes down OnFailure).</summary>
        /// <param name="req">Step request.</param>
        /// <returns>An error result.</returns>
        public static Task<StepResult> Fail(StepRequest req)
        {
            return Task.FromResult(new StepResult { Result = StepResultTypeEnum.Error, Data = req.Data });
        }

        /// <summary>Throw an unexpected exception (routes down OnException).</summary>
        /// <param name="req">Step request.</param>
        /// <returns>Never returns.</returns>
        /// <exception cref="InvalidOperationException">Always thrown.</exception>
        public static Task<StepResult> Throw(StepRequest req)
        {
            throw new InvalidOperationException("telemetry test step failure");
        }

        /// <summary>Run longer than any short step timeout.</summary>
        /// <param name="req">Step request.</param>
        /// <returns>A success result after two seconds.</returns>
        public static async Task<StepResult> Slow(StepRequest req)
        {
            await Task.Delay(2000).ConfigureAwait(false);
            return new StepResult { Result = StepResultTypeEnum.Success, Data = req.Data };
        }
    }
}
