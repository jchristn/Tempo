namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// User data access methods.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>Create a user.</summary>
        Task<User> CreateAsync(User user, CancellationToken token = default);

        /// <summary>Update a user.</summary>
        Task<User> UpdateAsync(User user, CancellationToken token = default);

        /// <summary>Read a user by identifier (tenant-scoped).</summary>
        Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>Read a user by email within a tenant.</summary>
        Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>Enumerate users within a tenant.</summary>
        Task<EnumerationResult<User>> EnumerateAsync(string tenantId, EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return all users in a tenant.</summary>
        Task<List<User>> AllAsync(string tenantId, CancellationToken token = default);

        /// <summary>Delete a user. Cascades to credentials and role maps.</summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
