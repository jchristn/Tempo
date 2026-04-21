namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Account data access methods.
    /// </summary>
    public interface IAccountMethods
    {
        /// <summary>Create an account.</summary>
        Task<Account> CreateAsync(Account account, CancellationToken token = default);

        /// <summary>Update an account.</summary>
        Task<Account> UpdateAsync(Account account, CancellationToken token = default);

        /// <summary>Read an account by identifier.</summary>
        Task<Account?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate all accounts.</summary>
        Task<EnumerationResult<Account>> EnumerateAsync(EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return every account as a flat list.</summary>
        Task<List<Account>> AllAsync(CancellationToken token = default);

        /// <summary>Delete an account. Cascades.</summary>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>Existence check.</summary>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);
    }
}
