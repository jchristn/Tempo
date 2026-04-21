namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Step persistence methods.
    /// </summary>
    public interface IStepMethods
    {
        /// <summary>Create a step.</summary>
        Task<StepRecord> CreateAsync(StepRecord record, CancellationToken token = default);

        /// <summary>Update a step.</summary>
        Task<StepRecord> UpdateAsync(StepRecord record, CancellationToken token = default);

        /// <summary>Upsert a step (used by startup registration of code steps).</summary>
        Task<StepRecord> UpsertAsync(StepRecord record, CancellationToken token = default);

        /// <summary>Read a step by identifier (tenant-scoped).</summary>
        Task<StepRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Read a step by stable execution key (tenant-scoped).</summary>
        Task<StepRecord?> ReadByExecutionKeyAsync(string tenantId, string executionKey, CancellationToken token = default);

        /// <summary>Enumerate steps in a tenant.</summary>
        Task<EnumerationResult<StepRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all steps in a tenant.</summary>
        Task<List<StepRecord>> AllAsync(string tenantId, CancellationToken token = default);

        /// <summary>Delete a step.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
