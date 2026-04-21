namespace Tempo.Core.Runtime
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Resolves a flow transition execution key to executable step metadata.</summary>
    public interface IStepExecutionResolver
    {
        Task<ResolvedStepExecution> ResolveAsync(string tenantId, string executionKey, FlowRunExecutionSnapshot snapshot, CancellationToken token = default);
    }
}
