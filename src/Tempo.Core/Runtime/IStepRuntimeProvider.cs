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
        RuntimeKey RuntimeKey { get; }
        Type ConfigType { get; }
        StepRuntimeDescriptor Describe();
        Task<StepConfigValidationResult> ValidateAsync(StepRuntimeValidationContext context, CancellationToken token = default);
        Task<StepRunner> CreateRunnerAsync(StepExecutionContext context, StepRecord step, StepRuntimeConfig config, CancellationToken token = default);
    }
}
