namespace Tempo.Core.Settings
{
    using System;
    using Tempo.Core.Enums;

    /// <summary>
    /// Database settings.
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>Database provider type. Default: Sqlite.</summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>SQLite filename. Only used when Type is Sqlite.</summary>
        public string? Filename { get; set; } = "./tempo.db";

        /// <summary>Server hostname for networked providers.</summary>
        public string? Server { get; set; } = null;

        /// <summary>Server port for networked providers. 0 means use provider default.</summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = Math.Clamp(value, 0, 65535);
            }
        }

        /// <summary>Database name for networked providers.</summary>
        public string? DatabaseName { get; set; } = null;

        /// <summary>Username for networked providers.</summary>
        public string? Username { get; set; } = null;

        /// <summary>Password for networked providers.</summary>
        public string? Password { get; set; } = null;

        /// <summary>Command timeout in seconds. Default: 30. Range: 1 to 3600.</summary>
        public int CommandTimeoutSeconds
        {
            get
            {
                return _CommandTimeoutSeconds;
            }
            set
            {
                _CommandTimeoutSeconds = Math.Clamp(value, 1, 3600);
            }
        }

        private int _Port = 0;
        private int _CommandTimeoutSeconds = 30;
    }
}
