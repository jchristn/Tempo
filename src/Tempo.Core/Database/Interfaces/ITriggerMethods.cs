namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Trigger persistence methods.
    /// </summary>
    public interface ITriggerMethods
    {
        /// <summary>Create a trigger.</summary>
        Task<TriggerRecord> CreateAsync(TriggerRecord record, CancellationToken token = default);

        /// <summary>Update a trigger.</summary>
        Task<TriggerRecord> UpdateAsync(TriggerRecord record, CancellationToken token = default);

        /// <summary>Read a trigger by identifier (tenant-scoped).</summary>
        Task<TriggerRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Read a trigger without a tenant scope (used by public HTTP intake).</summary>
        Task<TriggerRecord?> ReadGlobalAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate triggers within a tenant.</summary>
        Task<EnumerationResult<TriggerRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all triggers in a tenant.</summary>
        Task<List<TriggerRecord>> AllAsync(string tenantId, CancellationToken token = default);

        /// <summary>Delete a trigger.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
