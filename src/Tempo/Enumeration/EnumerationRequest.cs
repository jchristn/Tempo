namespace Tempo.Enumeration
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Reflection.Emit;
    using System.Text.Json.Serialization;
    using Timestamps;

    /// <summary>
    /// Object used to request enumeration.
    /// </summary>
    public class EnumerationRequest
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(TenantId)));
        }

        /// <summary>
        /// Order by.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.StartUtcDescending;

        /// <summary>
        /// Maximum number of results to retrieve.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                if (value < 1) throw new ArgumentException("MaxResults must be greater than zero.");
                if (value > 1000) throw new ArgumentException("MaxResults must be one thousand or less.");
                _MaxResults = value;
            }
        }

        /// <summary>
        /// The number of records to skip.
        /// </summary>
        public int Skip
        {
            get
            {
                return _Skip;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Skip));
                _Skip = value;
            }
        }

        private string _TenantId = null;
        private int _MaxResults = 1000;
        private int _Skip = 0;
        private List<string> _Labels = new List<string>();
        private NameValueCollection _Tags = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationRequest()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
