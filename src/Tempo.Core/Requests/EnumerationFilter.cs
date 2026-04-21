namespace Tempo.Core.Requests
{
    using System;

    /// <summary>
    /// Generic paging/filter request.
    /// </summary>
    public class EnumerationFilter
    {
        /// <summary>Page number, 1-indexed. Default: 1. Minimum: 1.</summary>
        public int PageNumber
        {
            get
            {
                return _PageNumber;
            }
            set
            {
                _PageNumber = value >= 1 ? value : 1;
            }
        }

        /// <summary>Page size. Default: 25. Range: 1 to 1000.</summary>
        public int PageSize
        {
            get
            {
                return _PageSize;
            }
            set
            {
                _PageSize = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>Include inactive rows. Default: false.</summary>
        public bool IncludeInactive { get; set; } = false;

        private int _PageNumber = 1;
        private int _PageSize = 25;
    }
}
