namespace Tempo.Core.Runtime
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Resolves a flow transition execution key to executable step metadata.</summary>
    public interface IStepExecutionResolver
    {
        /// <summary>
        /// Resolves a flow transition execution key to executable step metadata.
        /// </summary>
        /// <param name="tenantId">The tenant identifier that owns the step.</param>
        /// <param name="executionKey">The transition execution key identifying the step.</param>
        /// <param name="snapshot">The flow-run execution snapshot providing resolved artifact versions.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The resolved step execution metadata.</returns>
        Task<ResolvedStepExecution> ResolveAsync(string tenantId, string executionKey, FlowRunExecutionSnapshot snapshot, CancellationToken token = default);
    }
}
