namespace Tempo.Server
{
    using System;
    using System.Diagnostics;
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
    using Tempo.Hosting;
    using Tempo.Telemetry;
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

            TelemetryHost telemetry = TelemetryHost.Start(settings.Telemetry, "tempo-server", "server", logging);
            PublishConfiguration(settings);

            DatabaseDriverBase database;
            long databaseStart = Stopwatch.GetTimestamp();
            using (Activity? databaseActivity = TempoTelemetry.StartTask("database_init"))
            {
                try
                {
                    database = await DatabaseDriverFactory.CreateAndInitializeAsync(settings.Database).ConfigureAwait(false);
                    TempoTelemetry.SetOk(databaseActivity);
                    TempoTelemetry.RecordTask("database_init", TelemetryConstants.OutcomeSuccess, TempoTelemetry.SecondsSince(databaseStart));
                    logging.Info("[Bootstrapper] database initialized (" + settings.Database.Type + ")");
                }
                catch (Exception ex)
                {
                    TempoTelemetry.RecordException(databaseActivity, ex);
                    TempoTelemetry.RecordTask("database_init", TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(databaseStart));
                    logging.Alert(LogMessages.WithoutTerminalPeriod("[Bootstrapper] database initialization failed: " + ex.Message));
                    telemetry.Dispose();
                    return;
                }
            }

            TempoStepManager stepManager = new TempoStepManager();
            try { stepManager.Add(new Tempo.Server.Runtime.StartupSampleClassStep()); } catch { /* sample already registered */ }
            try { stepManager.ScanEntryAssembly(); } catch { /* no attribute steps */ }

            await RunStartupTaskAsync("hydration", logging, "hydration error", async () =>
            {
                HydrationService hydration = new HydrationService(database, settings.Hydration, logging, settings.Artifacts, settings.Runtimes, stepManager, restSettings: settings.Rest);
                await hydration.HydrateAsync().ConfigureAwait(false);
                if (hydration.DefaultCredential != null)
                {
                    logging.Info("[Bootstrapper] default credential: " + hydration.DefaultCredential.AccessKey);
                }
            }).ConfigureAwait(false);

            // Bridge logs to the telemetry pipeline only after the default credential line above, so that secret
            // never leaves the host's own console and log file.
            telemetry.BridgeLogs(logging);

            await RunStartupTaskAsync("inline_rest_migration", logging, "inline REST migration error", async () =>
            {
                StepCompatibilityMigrator migrator = new StepCompatibilityMigrator(database);
                StepCompatibilityMigrationResult migration = await migrator.MigrateAllTenantsAsync().ConfigureAwait(false);
                TempoTelemetry.RecordTaskItems("inline_rest_migration", "flows_updated", migration.FlowsUpdated);
                logging.Info("[Bootstrapper] inline REST migration scanned " + migration.FlowsScanned + " flow(s), updated " + migration.FlowsUpdated + ", created " + migration.StepsCreated + " step(s)");
            }).ConfigureAwait(false);

            await RunStartupTaskAsync("builtin_reconciliation", logging, "built-in step reconciliation error", async () =>
            {
                BuiltinStepReconciler reconciler = new BuiltinStepReconciler(database, stepManager);
                BuiltinStepReconciliationResult reconciliation = await reconciler.ReconcileAllTenantsAsync().ConfigureAwait(false);
                TempoTelemetry.RecordTaskItems("builtin_reconciliation", "ambiguous", reconciliation.Ambiguous);
                TempoTelemetry.RecordTaskItems("builtin_reconciliation", "orphaned", reconciliation.Orphaned);
                logging.Info("[Bootstrapper] built-in step reconciliation scanned " + reconciliation.Scanned + " step(s), resolved " + reconciliation.Resolved + ", ambiguous " + reconciliation.Ambiguous + ", orphaned " + reconciliation.Orphaned);
            }).ConfigureAwait(false);

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
                try { telemetry.Dispose(); } catch { /* ignore */ }
                try { logging.Dispose(); } catch { /* ignore */ }
            }
        }

        private static async Task RunStartupTaskAsync(string task, LoggingModule logging, string failureLabel, Func<Task> work)
        {
            long start = Stopwatch.GetTimestamp();
            using Activity? activity = TempoTelemetry.StartTask(task);
            try
            {
                await work().ConfigureAwait(false);
                TempoTelemetry.SetOk(activity);
                TempoTelemetry.RecordTask(task, TelemetryConstants.OutcomeSuccess, TempoTelemetry.SecondsSince(start));
            }
            catch (Exception ex)
            {
                TempoTelemetry.RecordException(activity, ex);
                TempoTelemetry.RecordTask(task, TelemetryConstants.OutcomeException, TempoTelemetry.SecondsSince(start));
                TempoTelemetry.RecordError(task, ex);
                logging.Warn(LogMessages.WithoutTerminalPeriod("[Bootstrapper] " + failureLabel + ": " + ex.Message));
            }
        }

        private static void PublishConfiguration(Settings settings)
        {
            TempoTelemetry.SetConfig("engine.max_concurrent_runs", settings.Engine.MaxConcurrentRuns);
            TempoTelemetry.SetConfig("engine.poll_interval_ms", settings.Engine.PollIntervalMs);
            TempoTelemetry.SetConfig("engine.lease_duration_ms", settings.Engine.LeaseDurationMs);
            TempoTelemetry.SetConfig("engine.worker_heartbeat_timeout_ms", settings.Engine.WorkerHeartbeatTimeoutMs);
            TempoTelemetry.SetConfig("engine.max_assignment_attempts", settings.Engine.MaxAssignmentAttempts);
            TempoTelemetry.SetConfig("engine.queue_enabled", settings.Engine.QueueEnabled ? 1 : 0);
            TempoTelemetry.SetConfig("engine.server_can_execute_workload", settings.Engine.ServerCanExecuteWorkload ? 1 : 0);
            TempoTelemetry.SetConfig("external.max_processes_server_wide", settings.Runtimes.ExternalExecution.MaxConcurrentProcessesServerWide);
            TempoTelemetry.SetConfig("external.max_processes_per_tenant", settings.Runtimes.ExternalExecution.MaxConcurrentProcessesPerTenant);
            TempoTelemetry.SetConfig("request_history.enabled", settings.RequestHistory.Enabled ? 1 : 0);
            TempoTelemetry.SetConfig("request_history.retention_days", settings.RequestHistory.RetentionDays);
            TempoTelemetry.SetConfig("run_logs.enabled", settings.RunLogs.Enabled ? 1 : 0);
            TempoTelemetry.SetConfig("run_logs.retention_days", settings.RunLogs.RetentionDays);
            TempoTelemetry.SetConfig("telemetry.sampling_ratio", settings.Telemetry.SamplingRatio);
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
