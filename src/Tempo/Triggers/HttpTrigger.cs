namespace Tempo.Triggers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP trigger.
    /// </summary>
    public class HttpTrigger : Trigger
    {
        /// <summary>
        /// HTTP method.
        /// </summary>
        public HttpMethod Method
        {
            get => _Method;
            set => _Method = value != null ? value : throw new ArgumentNullException(nameof(Method));
        }

        /// <summary>
        /// URI.
        /// </summary>
        public Uri Uri
        {
            get => _Uri;
            set => _Uri = value != null ? value : throw new ArgumentNullException(nameof(Uri));
        }

        private HttpMethod _Method = HttpMethod.Post;
        private Uri _Uri = new Uri("http://localhost:8000/");

        /// <summary>
        /// HTTP trigger.
        /// </summary>
        public HttpTrigger()
        {

        }
    }
}
