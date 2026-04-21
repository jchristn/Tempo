namespace Tempo.Core.Settings
{
    using System;

    /// <summary>
    /// REST server settings.
    /// </summary>
    public class RestSettings
    {
        /// <summary>
        /// Hostname on which to listen. Default: "127.0.0.1".
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// TCP port on which to listen. Default: 8901. Range: 1 to 65535.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = Math.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// Whether TLS is enabled. Default: false.
        /// </summary>
        public bool Ssl { get; set; } = false;

        private string _Hostname = "127.0.0.1";
        private int _Port = 8901;
    }
}
