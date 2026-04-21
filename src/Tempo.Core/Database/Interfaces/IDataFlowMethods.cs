namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Data flow persistence methods.
    /// </summary>
    public interface IDataFlowMethods
    {
        /// <summary>Create a flow.</summary>
        Task<DataFlowRecord> CreateAsync(DataFlowRecord record, CancellationToken token = default);

        /// <summary>Update a flow.</summary>
        Task<DataFlowRecord> UpdateAsync(DataFlowRecord record, CancellationToken token = default);

        /// <summary>Read a flow by identifier (tenant-scoped).</summary>
        Task<DataFlowRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Read a flow by identifier without a tenant scope (used by the queue worker).</summary>
        Task<DataFlowRecord?> ReadGlobalAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate flows.</summary>
        Task<EnumerationResult<DataFlowRecord>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all flows in a tenant.</summary>
        Task<List<DataFlowRecord>> AllAsync(string tenantId, CancellationToken token = default);

        /// <summary>Delete a flow.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
