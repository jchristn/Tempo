namespace Tempo.Core.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Generic paged enumeration response.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>1-based page number.</summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>Page size.</summary>
        public int PageSize { get; set; } = 25;

        /// <summary>Total number of rows matching the filter across all pages.</summary>
        public int TotalCount { get; set; } = 0;

        /// <summary>Rows on this page.</summary>
        public List<T> Items { get; set; } = new List<T>();
    }
}
