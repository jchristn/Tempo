namespace Tempo.Core.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Resolves step metadata from a pre-built flow-run execution plan.
    /// </summary>
    public class ExecutionPlanStepResolver : IStepExecutionResolver
    {
        private readonly FlowRunExecutionPlan _Plan;

        /// <summary>Instantiate.</summary>
        public ExecutionPlanStepResolver(FlowRunExecutionPlan plan)
        {
            _Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        /// <inheritdoc/>
        public Task<ResolvedStepExecution> ResolveAsync(string tenantId, string executionKey, FlowRunExecutionSnapshot snapshot, CancellationToken token = default)
        {
            if (!_Plan.Steps.TryGetValue(executionKey, out FlowRunResolvedStep? resolved))
            {
                throw new InvalidOperationException("Step '" + executionKey + "' was not found in execution plan for flow run '" + _Plan.FlowRunId + "'.");
            }

            return Task.FromResult(new ResolvedStepExecution
            {
                Step = resolved.Step,
                Config = resolved.Step.RuntimeConfig
            });
        }
    }
}
