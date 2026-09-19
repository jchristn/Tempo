namespace Tempo.Core.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Runners;

    /// <summary>Runtime provider contract for step execution extensions.</summary>
    public interface IStepRuntimeProvider
    {
        /// <summary>Runtime key that uniquely identifies this provider.</summary>
        RuntimeKey RuntimeKey { get; }

        /// <summary>CLR type of the strongly-typed configuration this provider expects.</summary>
        Type ConfigType { get; }

        /// <summary>
        /// Describes the runtime, including its availability, capabilities, and configuration properties.
        /// </summary>
        /// <returns>The runtime descriptor.</returns>
        StepRuntimeDescriptor Describe();

        /// <summary>
        /// Validates the supplied runtime configuration for a step.
        /// </summary>
        /// <param name="context">The validation context, including tenant and configuration.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>The validation result indicating success or the collected errors.</returns>
        Task<StepConfigValidationResult> ValidateAsync(StepRuntimeValidationContext context, CancellationToken token = default);

        /// <summary>
        /// Creates a step runner for executing a step with the supplied configuration.
        /// </summary>
        /// <param name="context">The step execution context.</param>
        /// <param name="step">The step record being executed.</param>
        /// <param name="config">The runtime configuration for the step.</param>
        /// <param name="token">Token used to cancel the operation.</param>
        /// <returns>A step runner capable of executing the step.</returns>
        Task<StepRunner> CreateRunnerAsync(StepExecutionContext context, StepRecord step, StepRuntimeConfig config, CancellationToken token = default);
    }
}
