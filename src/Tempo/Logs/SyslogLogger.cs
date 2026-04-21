namespace Tempo.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Padlocks;
    using SyslogLogging;

    /// <summary>
    /// Syslog logger.
    /// </summary>
    public class SyslogLogger : Logger
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private string _ServerIp = "127.0.0.1";
        private int _ServerPort = 514;
        private string _LogFileDirectory = "./logs/";
        private string _LogFilename = "tempo.log";
        private string _DataFlowLogDirectory = "./dataflowruns/";
        private bool _EnableConsole = true;

        private Padlock<string> _Lock = new Padlock<string>();
        private LoggingModule _Logging = null;

        /// <summary>
        /// Syslog logger
        /// </summary>
        /// <param name="serverIp">Syslog server IP.</param>
        /// <param name="port">Syslog server port.</param>
        /// <param name="logDirectory">Directory for logfiles.</param>
        /// <param name="logFilename">Log filename.</param>
        /// <param name="dfLogsDirectory">Data flow logs directory.</param>
        /// <param name="console">Enable or disable console logging.</param>
        public SyslogLogger(
            string serverIp = "127.0.0.1", 
            int port = 514, 
            string logDirectory = "./logs/", 
            string logFilename = "tempo.log", 
            string dfLogsDirectory = "./dataflowruns/",
            bool console = true)
        {
            _ServerIp = (!String.IsNullOrEmpty(serverIp) ? serverIp : throw new ArgumentNullException(nameof(serverIp)));
            _ServerPort = (port >= 0 && port <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port)));
            _LogFileDirectory = logDirectory;
            _LogFilename = logFilename;
            _DataFlowLogDirectory = (!String.IsNullOrEmpty(dfLogsDirectory) ? dfLogsDirectory : throw new ArgumentNullException(nameof(dfLogsDirectory)));
            _EnableConsole = console;

            if (!String.IsNullOrEmpty(_LogFileDirectory))
            {
                _LogFileDirectory = Path.GetFullPath(_LogFileDirectory);
                if (!Directory.Exists(_LogFileDirectory)) Directory.CreateDirectory(_LogFileDirectory);
            }

            _Logging = new LoggingModule(_ServerIp, _ServerPort, _EnableConsole);

            if (!String.IsNullOrEmpty(_LogFileDirectory))
            {
                _Logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
                _Logging.Settings.LogFilename = Path.Combine(_LogFileDirectory, _LogFilename);
            }

            if (!Directory.Exists(_DataFlowLogDirectory))
                Directory.CreateDirectory(_DataFlowLogDirectory);
        }

        /// <inheritdoc />
        public override async Task Alert(string requestIdentifier, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Alert($"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Alert(string msg) => _Logging.Alert(Normalize(msg));

        /// <inheritdoc />
        public override async Task Critical(string requestIdentifier, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Critical($"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Critical(string msg) => _Logging.Critical(Normalize(msg));

        /// <inheritdoc />
        public override async Task Debug(string requestIdentifier, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Debug($"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Debug(string msg) => _Logging.Debug(Normalize(msg));

        /// <inheritdoc />
        public override async Task Emergency(string requestIdentifier, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Emergency($"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Emergency(string msg) => _Logging.Emergency(Normalize(msg));

        /// <inheritdoc />
        public override async Task Error(string requestIdentifier, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Error($"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Error(string msg) => _Logging.Error(Normalize(msg));

        /// <inheritdoc />
        public override async Task Info(string requestIdentifier, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Info($"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Info(string msg) => _Logging.Info(Normalize(msg));

        /// <inheritdoc />
        public override async Task Log(string requestIdentifier, SeverityEnum sev, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Log(
                (Severity)(Enum.Parse(typeof(Severity), sev.ToString())),
                $"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Log(SeverityEnum sev, string msg)
        {
            msg = Normalize(msg);
            _Logging.Log(
                (Severity)(Enum.Parse(typeof(Severity), sev.ToString())),
                msg);
        }

        /// <inheritdoc />
        public override async Task Warn(string requestIdentifier, string msg)
        {
            if (String.IsNullOrEmpty(requestIdentifier)) throw new ArgumentNullException(nameof(requestIdentifier));
            msg = Normalize(msg);
            using (_Lock.Lock(requestIdentifier))
            {
                await File.AppendAllLinesAsync(
                    GetFilename(requestIdentifier),
                    new List<string> { msg });
            }

            _Logging.Warn($"[{requestIdentifier}] {msg}");
        }

        /// <inheritdoc />
        public override async Task Warn(string msg) => _Logging.Warn(Normalize(msg));

        private static string Normalize(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return string.Empty;
            return msg.TrimEnd().TrimEnd('.');
        }

        private string GetFilename(string requestIdentifier)
        {
            return Path.Combine(_DataFlowLogDirectory, (requestIdentifier + ".log"));
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
