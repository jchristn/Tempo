namespace Tempo.Core.Settings
{
    using System;

    /// <summary>
    /// Authentication settings.
    /// </summary>
    public class AuthSettings
    {
        /// <summary>Issuer name embedded in tokens. Default: "tempo".</summary>
        public string Issuer
        {
            get
            {
                return _Issuer;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Issuer));
                _Issuer = value;
            }
        }

        /// <summary>
        /// AES-256 signing key used for token encryption (32 bytes = 64 hex chars).
        /// When the string is not 32 bytes of UTF-8 it is SHA-256 hashed to derive the key.
        /// Defaults to a development placeholder; override via <c>TEMPO_AUTH_SIGNING_KEY</c>.
        /// </summary>
        public string SigningKey
        {
            get
            {
                return _SigningKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SigningKey));
                _SigningKey = value;
            }
        }

        /// <summary>Token lifetime in minutes. Default: 1440 (24h). Range: 5 to 525600.</summary>
        public int TokenExpirationMinutes
        {
            get
            {
                return _TokenExpirationMinutes;
            }
            set
            {
                _TokenExpirationMinutes = Math.Clamp(value, 5, 525600);
            }
        }

        /// <summary>
        /// System administrator API key used to bypass token auth for out-of-band operations.
        /// When set and provided via <c>x-api-key</c>, the request authenticates as a global admin.
        /// Defaults to empty which disables the bypass. Override via <c>TEMPO_AUTH_ADMIN_API_KEY</c>.
        /// </summary>
        public string AdminApiKey { get; set; } = string.Empty;

        private string _Issuer = "tempo";
        private string _SigningKey = "tempo-development-key-override-for-production-use";
        private int _TokenExpirationMinutes = 1440;
    }
}
