namespace Tempo.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;

    /// <summary>
    /// Administrator data access methods.
    /// </summary>
    public interface IAdministratorMethods
    {
        /// <summary>Create an administrator.</summary>
        Task<Administrator> CreateAsync(Administrator administrator, CancellationToken token = default);

        /// <summary>Update an administrator.</summary>
        Task<Administrator> UpdateAsync(Administrator administrator, CancellationToken token = default);

        /// <summary>Read an administrator by identifier.</summary>
        Task<Administrator?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>Read an administrator by email.</summary>
        Task<Administrator?> ReadByEmailAsync(string email, CancellationToken token = default);

        /// <summary>Enumerate administrators.</summary>
        Task<EnumerationResult<Administrator>> EnumerateAsync(EnumerationFilter filter, CancellationToken token = default);

        /// <summary>Return every administrator.</summary>
        Task<List<Administrator>> AllAsync(CancellationToken token = default);

        /// <summary>Delete an administrator.</summary>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);
    }
}
