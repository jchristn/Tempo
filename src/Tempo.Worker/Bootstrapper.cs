namespace Tempo.Worker
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core.Services;

    /// <summary>
    /// Worker composition root.
    /// </summary>
    public static class Bootstrapper
    {
        /// <summary>Run the worker process.</summary>
        public static async Task RunAsync(string[] args)
        {
            string? settingsPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--config=", StringComparison.Ordinal)) settingsPath = args[i].Substring("--config=".Length);
                else if (args[i] == "--config" && i + 1 < args.Length) settingsPath = args[i + 1];
            }

            WorkerSettings settings = WorkerSettingsLoader.Load(settingsPath);
            string resolvedPath = settingsPath ?? WorkerSettingsLoader.DefaultSettingsFile;
            if (!File.Exists(resolvedPath))
            {
                WorkerSettingsLoader.Save(settings, resolvedPath);
                Console.WriteLine("Generated default worker settings at " + resolvedPath);
            }

            LoggingModule logging = CreateLogger(settings);
            logging.Info("[Tempo.Worker] starting worker");

            WorkerNode worker = new WorkerNode(settings, logging);
            using CancellationTokenSource shutdown = new CancellationTokenSource();

            ConsoleCancelEventHandler? cancelHandler = null;
            EventHandler? exitHandler = null;

            void RequestShutdown(string reason)
            {
                try { logging.Info("[Tempo.Worker] " + reason + ", shutting down"); } catch { /* ignore */ }
                try { shutdown.Cancel(); } catch { /* ignore */ }
            }

            cancelHandler = (s, e) => { e.Cancel = true; RequestShutdown("CTRL+C received"); };
            exitHandler = (s, e) => RequestShutdown("process exit received");
            Console.CancelKeyPress += cancelHandler;
            AppDomain.CurrentDomain.ProcessExit += exitHandler;

            try
            {
                await worker.RunAsync(shutdown.Token).ConfigureAwait(false);
            }
            finally
            {
                try { Console.CancelKeyPress -= cancelHandler; } catch { /* ignore */ }
                try { AppDomain.CurrentDomain.ProcessExit -= exitHandler; } catch { /* ignore */ }
                try { logging.Dispose(); } catch { /* ignore */ }
            }
        }

        private static LoggingModule CreateLogger(WorkerSettings settings)
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = settings.Logging.ConsoleLogging;

            if (settings.Logging.FileLogging)
            {
                try
                {
                    if (!Directory.Exists(settings.Logging.LogDirectory))
                        Directory.CreateDirectory(settings.Logging.LogDirectory);
                    logging.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                    logging.Settings.LogFilename = Path.Combine(settings.Logging.LogDirectory, settings.Logging.LogFilename);
                }
                catch
                {
                    // Ignore logging bootstrap failures and fall back to console.
                }
            }

            return logging;
        }
    }
}
