namespace Tempo.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Logs;

    /// <summary>
    /// Step runner for code-based steps (Step instances).
    /// </summary>
    public class CodeStepRunner : StepRunner
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        private readonly Step _Step;

        /// <summary>
        /// Code step runner.
        /// </summary>
        /// <param name="step">Step instance to execute.</param>
        /// <param name="logger">Logger instance for logging (optional).</param>
        public CodeStepRunner(Step step, Logger logger = null)
        {
            _Step = step ?? throw new ArgumentNullException(nameof(step));
            Logger = logger;
        }

        /// <summary>
        /// Execute the code-based step.
        /// </summary>
        /// <param name="req">Step request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Step result.</returns>
        protected override async Task<StepResult> ExecuteInternal(StepRequest req, CancellationToken token)
        {
            return await _Step.Run(req).ConfigureAwait(false);
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
