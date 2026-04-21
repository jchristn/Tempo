namespace Tempo.Sdk
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Handler contract for .NET process-backed Tempo step artifacts.</summary>
    public interface ITempoStepHandler
    {
        /// <summary>Handle one Tempo step request.</summary>
        Task<StepResult> RunAsync(StepRequest request, CancellationToken token);
    }
}
