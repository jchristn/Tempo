namespace Tempo
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Configuration for REST API step.
    /// </summary>
    public class RestStepConfiguration
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// HTTP method (GET, POST, PUT, DELETE, PATCH).
        /// </summary>
        public string Method
        {
            get => _Method;
            set => _Method = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Method)));
        }

        /// <summary>
        /// URL template with optional {tokens} for runtime substitution.
        /// </summary>
        public string Url
        {
            get => _Url;
            set => _Url = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Url)));
        }

        /// <summary>
        /// Default headers to include in requests.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Timeout in milliseconds (default 30000).
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = (value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(TimeoutMs)));
        }

        private string _Method = "GET";
        private string _Url = null;
        private int _TimeoutMs = 30000;

        /// <summary>
        /// REST step configuration.
        /// </summary>
        public RestStepConfiguration()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
