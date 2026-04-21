namespace Tempo.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Request history data access methods.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>Insert a captured request record.</summary>
        Task CreateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>
        /// Read a single entry by identifier. Tenant scope is enforced when <paramref name="tenantId"/> is non-null.
        /// </summary>
        Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>Page through entries matching a filter.</summary>
        Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>Return bucketed counts and averages for chart rendering.</summary>
        Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>Delete a single entry.</summary>
        Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>Delete all entries matching a filter. Returns the affected row count.</summary>
        Task<int> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>Prune entries older than the given UTC cutoff. Returns the affected row count.</summary>
        Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken token = default);
    }
}
