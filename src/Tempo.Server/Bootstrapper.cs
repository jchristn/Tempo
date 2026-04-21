namespace Tempo.Server
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core;
    using Tempo.Core.Database;
    using Tempo.Core.Helpers;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using TempoStepManager = Tempo.StepManager;

    /// <summary>
    /// Composition root. Loads settings, initializes dependencies, starts the server, and waits for shutdown.
    /// </summary>
    public static class Bootstrapper
    {
        /// <summary>Run the server.</summary>
        /// <param name="args">Command-line arguments.</param>
        public static async Task RunAsync(string[] args)
        {
            string? settingsPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--config=")) settingsPath = args[i].Substring("--config=".Length);
                else if (args[i] == "--config" && i + 1 < args.Length) settingsPath = args[i + 1];
            }

            Console.WriteLine();
            Console.WriteLine(Constants.Logo);
            Console.WriteLine(Constants.ProductName);
            Console.WriteLine(Constants.Copyright);
            Console.WriteLine();

            Settings settings = SettingsLoader.Load(settingsPath);

            if (!File.Exists(settingsPath ?? Constants.DefaultSettingsFile))
            {
                string pathToWrite = settingsPath ?? Constants.DefaultSettingsFile;
                try
                {
                    SettingsLoader.Save(settings, pathToWrite);
                    Console.WriteLine("Generated default settings at " + pathToWrite);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not write default settings: " + ex.Message);
                }
            }

            LoggingModule logging = CreateLogger(settings.Logging);
            logging.Info("[Bootstrapper] starting Tempo Server");

            DatabaseDriverBase database;
            try
            {
                database = await DatabaseDriverFactory.CreateAndInitializeAsync(settings.Database).ConfigureAwait(false);
                logging.Info("[Bootstrapper] database initialized (" + settings.Database.Type + ")");
            }
            catch (Exception ex)
            {
                logging.Alert(LogMessages.WithoutTerminalPeriod("[Bootstrapper] database initialization failed: " + ex.Message));
                return;
            }

            TempoStepManager stepManager = new TempoStepManager();
            try { stepManager.Add(new Tempo.Server.Runtime.StartupSampleClassStep()); } catch { /* sample already registered */ }
            try { stepManager.ScanEntryAssembly(); } catch { /* no attribute steps */ }

            try
            {
                HydrationService hydration = new HydrationService(database, settings.Hydration, logging, settings.Artifacts, settings.Runtimes, stepManager, restSettings: settings.Rest);
                await hydration.HydrateAsync().ConfigureAwait(false);
                if (hydration.DefaultCredential != null)
                {
                    logging.Info("[Bootstrapper] default credential: " + hydration.DefaultCredential.AccessKey);
                }
            }
            catch (Exception ex)
            {
                logging.Warn(LogMessages.WithoutTerminalPeriod("[Bootstrapper] hydration error: " + ex.Message));
            }

            try
            {
                StepCompatibilityMigrator migrator = new StepCompatibilityMigrator(database);
                StepCompatibilityMigrationResult migration = await migrator.MigrateAllTenantsAsync().ConfigureAwait(false);
                logging.Info("[Bootstrapper] inline REST migration scanned " + migration.FlowsScanned + " flow(s), updated " + migration.FlowsUpdated + ", created " + migration.StepsCreated + " step(s)");
            }
            catch (Exception ex)
            {
                logging.Warn(LogMessages.WithoutTerminalPeriod("[Bootstrapper] inline REST migration error: " + ex.Message));
            }

            try
            {
                BuiltinStepReconciler reconciler = new BuiltinStepReconciler(database, stepManager);
                BuiltinStepReconciliationResult reconciliation = await reconciler.ReconcileAllTenantsAsync().ConfigureAwait(false);
                logging.Info("[Bootstrapper] built-in step reconciliation scanned " + reconciliation.Scanned + " step(s), resolved " + reconciliation.Resolved + ", ambiguous " + reconciliation.Ambiguous + ", orphaned " + reconciliation.Orphaned);
            }
            catch (Exception ex)
            {
                logging.Warn(LogMessages.WithoutTerminalPeriod("[Bootstrapper] built-in step reconciliation error: " + ex.Message));
            }

            string resolvedPath = settingsPath ?? Constants.DefaultSettingsFile;
            Tempo.Server.Services.SettingsStore settingsStore = new Tempo.Server.Services.SettingsStore(settings, resolvedPath);
            TempoServer server = new TempoServer(settings, logging, database, stepManager, settingsStore);

            CancellationTokenSource shutdownCts = new CancellationTokenSource();
            int shutdownTriggered = 0;
            ConsoleCancelEventHandler? cancelHandler = null;
            EventHandler? exitHandler = null;

            void RequestShutdown(string reason)
            {
                if (Interlocked.Exchange(ref shutdownTriggered, 1) != 0) return;
                try { logging.Info("[Bootstrapper] " + reason + ", shutting down"); } catch { /* logger may already be disposed */ }
                try { shutdownCts.Cancel(); } catch (ObjectDisposedException) { /* already disposed */ }
            }

            cancelHandler = (s, e) => { e.Cancel = true; RequestShutdown("CTRL+C received"); };
            exitHandler = (s, e) => RequestShutdown("process exit received");
            Console.CancelKeyPress += cancelHandler;
            AppDomain.CurrentDomain.ProcessExit += exitHandler;

            try
            {
                await server.StartAsync().ConfigureAwait(false);

                try { await Task.Delay(Timeout.Infinite, shutdownCts.Token).ConfigureAwait(false); }
                catch (TaskCanceledException) { /* expected */ }

                server.Stop();
                server.Dispose();

                try { await database.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
                database.Dispose();
                logging.Info("[Bootstrapper] stopped");
            }
            finally
            {
                // Unhook before disposing the CTS/logger so late-firing handlers do not hit disposed state.
                try { Console.CancelKeyPress -= cancelHandler; } catch { /* ignore */ }
                try { AppDomain.CurrentDomain.ProcessExit -= exitHandler; } catch { /* ignore */ }
                try { shutdownCts.Dispose(); } catch { /* ignore */ }
                try { logging.Dispose(); } catch { /* ignore */ }
            }
        }

        private static LoggingModule CreateLogger(Tempo.Core.Settings.LoggingSettings settings)
        {
            LoggingModule module = new LoggingModule();

            if (settings.FileLogging)
            {
                try
                {
                    if (!Directory.Exists(settings.LogDirectory)) Directory.CreateDirectory(settings.LogDirectory);
                    string logPath = Path.Combine(settings.LogDirectory, settings.LogFilename);
                    module.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                    module.Settings.LogFilename = logPath;
                }
                catch { /* ignore */ }
            }

            module.Settings.EnableConsole = settings.ConsoleLogging;
            return module;
        }
    }
}
