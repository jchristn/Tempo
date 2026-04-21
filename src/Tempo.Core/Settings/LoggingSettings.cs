namespace Tempo.Core.Settings
{
    using System;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>Whether to log to the console. Default: true.</summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>Whether to log to a file. Default: true.</summary>
        public bool FileLogging { get; set; } = true;

        /// <summary>Directory in which log files are written. Default: "./logs".</summary>
        public string LogDirectory
        {
            get
            {
                return _LogDirectory;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LogDirectory));
                _LogDirectory = value;
            }
        }

        /// <summary>Log file base name. Default: "tempo.log".</summary>
        public string LogFilename
        {
            get
            {
                return _LogFilename;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LogFilename));
                _LogFilename = value;
            }
        }

        private string _LogDirectory = "./logs";
        private string _LogFilename = "tempo.log";
    }
}
