namespace Test.Shared.Suites
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Settings;
    using Tempo.Server;
    using Touchstone.Core;

    public static class LogManagementSuite
    {
        private const string AdminApiKey = "tempo-log-suite-admin-key";

        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "LogManagement",
                displayName: "Admin log viewer routes and storage normalization",
                cases: new[]
                {
                    new TestCaseDescriptor("LogManagement", "AdminRequired", "Log routes require administrator authentication", AdminRequiredAsync),
                    new TestCaseDescriptor("LogManagement", "ListSourcesAndFiles", "Server and worker sources enumerate their visible log files", ListSourcesAndFilesAsync),
                    new TestCaseDescriptor("LogManagement", "ReadAndDownload", "Bounded log reads and raw downloads return expected content", ReadAndDownloadAsync),
                    new TestCaseDescriptor("LogManagement", "DeleteBehaviors", "Current logs are truncated while archived logs are deleted", DeleteBehaviorsAsync),
                    new TestCaseDescriptor("LogManagement", "TraversalRejectedAndOpenApiRegistered", "Traversal is rejected and log routes are published in OpenAPI", TraversalRejectedAndOpenApiRegisteredAsync)
                });
        }

        private static async Task AdminRequiredAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-log-admin");
            try
            {
                await SeedLogFilesAsync(root).ConfigureAwait(false);
                int port = FreePort();
                server = await StartServerAsync(driver, CreateSettings(root, port), ct).ConfigureAwait(false);

                using HttpClient client = new HttpClient();
                using HttpResponseMessage response = await client.GetAsync("http://127.0.0.1:" + port + "/v1.0/logs/sources", ct).ConfigureAwait(false);
                Assert2.Equal(HttpStatusCode.Unauthorized, response.StatusCode, "unauthorized without admin auth");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task ListSourcesAndFilesAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-log-sources");
            try
            {
                await SeedLogFilesAsync(root).ConfigureAwait(false);
                int port = FreePort();
                server = await StartServerAsync(driver, CreateSettings(root, port), ct).ConfigureAwait(false);
                await server.DispatchCoordinator.RotateWorkerTokenAsync("wrk_logs_1", "Worker Logs", ct).ConfigureAwait(false);

                using HttpClient client = CreateAdminClient(port);
                using JsonDocument sources = await ReadJsonAsync(client, "/v1.0/logs/sources", ct).ConfigureAwait(false);

                JsonElement serverSource = sources.RootElement.EnumerateArray().First(item =>
                    item.GetProperty("sourceKind").GetString() == "server");
                JsonElement workerSource = sources.RootElement.EnumerateArray().First(item =>
                    item.GetProperty("sourceKind").GetString() == "worker" &&
                    item.GetProperty("sourceId").GetString() == "wrk_logs_1");

                Assert2.True(serverSource.GetProperty("fileCount").GetInt32() >= 2, "server source includes both current and archived files");
                Assert2.True(workerSource.GetProperty("fileCount").GetInt32() >= 2, "worker source includes both current and archived files");

                using JsonDocument workerFiles = await ReadJsonAsync(client, "/v1.0/logs/files?sourceKind=worker&sourceId=wrk_logs_1", ct).ConfigureAwait(false);
                Assert2.True(workerFiles.RootElement.GetArrayLength() >= 2, "worker files returned");
                Assert2.True(workerFiles.RootElement.EnumerateArray().Any(file => file.GetProperty("fileName").GetString() == "tempo-worker.log"), "current worker log listed");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task ReadAndDownloadAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-log-read");
            try
            {
                await SeedLogFilesAsync(root).ConfigureAwait(false);
                int port = FreePort();
                server = await StartServerAsync(driver, CreateSettings(root, port), ct).ConfigureAwait(false);

                using HttpClient client = CreateAdminClient(port);
                string readPath = "/v1.0/logs/files/content?sourceKind=server&sourceId=server&path=" +
                    Uri.EscapeDataString("tempo.log") +
                    "&tailLines=2&maxBytes=64";

                using JsonDocument read = await ReadJsonAsync(client, readPath, ct).ConfigureAwait(false);
                string content = read.RootElement.GetProperty("content").GetString() ?? string.Empty;
                Assert2.True(content.Contains("server line 2", StringComparison.Ordinal), "tail includes last line");
                Assert2.True(read.RootElement.GetProperty("truncated").GetBoolean(), "read is marked truncated");

                using HttpResponseMessage download = await client.GetAsync(
                    "/v1.0/logs/files/download?sourceKind=server&sourceId=server&path=" + Uri.EscapeDataString("tempo.log"),
                    ct).ConfigureAwait(false);
                string downloadBody = await download.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                Assert2.Equal(HttpStatusCode.OK, download.StatusCode, "download succeeded");
                Assert2.True(downloadBody.Contains("server line 1", StringComparison.Ordinal), "download contains full file");
                Assert2.True(download.Content.Headers.ContentType?.MediaType == "text/plain", "download content-type is text/plain");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task DeleteBehaviorsAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-log-delete");
            try
            {
                await SeedLogFilesAsync(root).ConfigureAwait(false);
                int port = FreePort();
                server = await StartServerAsync(driver, CreateSettings(root, port), ct).ConfigureAwait(false);

                string serverCurrent = Path.Combine(root, "server-logs", "tempo.log");
                string serverArchived = Path.Combine(root, "server-logs", "tempo.20260420.log");
                long originalCurrentBytes = new FileInfo(serverCurrent).Length;
                Assert2.True(originalCurrentBytes > 0, "current log seeded");

                using HttpClient client = CreateAdminClient(port);
                using JsonDocument currentDelete = await DeleteJsonAsync(
                    client,
                    "/v1.0/logs/files/content?sourceKind=server&sourceId=server&path=" + Uri.EscapeDataString("tempo.log"),
                    ct).ConfigureAwait(false);
                Assert2.Equal("Truncated", currentDelete.RootElement.GetProperty("action").GetString(), "current file is truncated");
                Assert2.True(File.Exists(serverCurrent), "current file still exists after clear");
                Assert2.Equal(0L, new FileInfo(serverCurrent).Length, "current file truncated to zero bytes");

                using JsonDocument archivedDelete = await DeleteJsonAsync(
                    client,
                    "/v1.0/logs/files/content?sourceKind=server&sourceId=server&path=" + Uri.EscapeDataString("tempo.20260420.log"),
                    ct).ConfigureAwait(false);
                Assert2.Equal("Deleted", archivedDelete.RootElement.GetProperty("action").GetString(), "archived file is deleted");
                Assert2.True(!File.Exists(serverArchived), "archived file removed from disk");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task TraversalRejectedAndOpenApiRegisteredAsync(CancellationToken ct)
        {
            SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct).ConfigureAwait(false);
            TempoServer? server = null;
            string root = NewTempRoot("tempo-log-openapi");
            try
            {
                await SeedLogFilesAsync(root).ConfigureAwait(false);
                int port = FreePort();
                server = await StartServerAsync(driver, CreateSettings(root, port), ct).ConfigureAwait(false);

                using HttpClient client = CreateAdminClient(port);
                using HttpResponseMessage traversal = await client.GetAsync(
                    "/v1.0/logs/files/content?sourceKind=server&sourceId=server&path=" + Uri.EscapeDataString("../secret.txt"),
                    ct).ConfigureAwait(false);
                string traversalBody = await traversal.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                Assert2.Equal(HttpStatusCode.BadRequest, traversal.StatusCode, "path traversal rejected");
                Assert2.True(traversalBody.Contains("traverse parent", StringComparison.OrdinalIgnoreCase), "traversal error message returned");

                using HttpClient anonymous = new HttpClient();
                using HttpResponseMessage openApi = await anonymous.GetAsync("http://127.0.0.1:" + port + "/openapi.json", ct).ConfigureAwait(false);
                string openApiJson = await openApi.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                Assert2.Equal(HttpStatusCode.OK, openApi.StatusCode, "openapi succeeded");
                Assert2.True(openApiJson.Contains("/v1.0/logs/files/download", StringComparison.Ordinal), "download route is published");
                Assert2.True(openApiJson.Contains("/v1.0/logs/sources", StringComparison.Ordinal), "sources route is published");
            }
            finally
            {
                try { server?.Dispose(); } catch { /* ignore */ }
                await TempTestStore.DisposeAsync(driver).ConfigureAwait(false);
                DeleteDirectory(root);
            }
        }

        private static async Task SeedLogFilesAsync(string root)
        {
            string serverLogs = Path.Combine(root, "server-logs");
            string workerLogs = Path.Combine(root, "worker-logs", "wrk_logs_1");
            Directory.CreateDirectory(serverLogs);
            Directory.CreateDirectory(workerLogs);

            await File.WriteAllTextAsync(Path.Combine(serverLogs, "tempo.log"), "server line 0\nserver line 1\nserver line 2\n").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(serverLogs, "tempo.20260420.log"), "server archived\n").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workerLogs, "tempo-worker.log"), "worker line 0\nworker line 1\n").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workerLogs, "tempo-worker.1.log"), "worker archived\n").ConfigureAwait(false);
        }

        private static Settings CreateSettings(string root, int port)
        {
            Settings settings = new Settings();
            settings.Rest.Hostname = "127.0.0.1";
            settings.Rest.Port = port;
            settings.Auth.AdminApiKey = AdminApiKey;
            settings.Logging.FileLogging = false;
            settings.Logging.ConsoleLogging = false;
            settings.Logging.LogDirectory = Path.Combine(root, "server-logs");
            settings.Logging.LogFilename = "tempo.log";
            settings.LogViewer.WorkerRootPath = Path.Combine(root, "worker-logs");
            settings.LogViewer.WorkerLogFilename = "tempo-worker.log";
            settings.Hydration.SeedDefaults = false;
            settings.Engine.ServerCanExecuteWorkload = false;
            settings.Engine.QueueEnabled = true;
            settings.Engine.PollIntervalMs = 25;
            settings.RequestHistory.Enabled = false;
            return settings;
        }

        private static async Task<TempoServer> StartServerAsync(SqliteDatabaseDriver driver, Settings settings, CancellationToken token)
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            TempoServer server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
            await server.StartAsync().ConfigureAwait(false);
            return server;
        }

        private static HttpClient CreateAdminClient(int port)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:" + port, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(15)
            };
            client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, AdminApiKey);
            return client;
        }

        private static async Task<JsonDocument> ReadJsonAsync(HttpClient client, string relativeUrl, CancellationToken token)
        {
            using HttpResponseMessage response = await client.GetAsync(relativeUrl, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "GET succeeded for " + relativeUrl);
            return JsonDocument.Parse(content);
        }

        private static async Task<JsonDocument> DeleteJsonAsync(HttpClient client, string relativeUrl, CancellationToken token)
        {
            using HttpResponseMessage response = await client.DeleteAsync(relativeUrl, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "DELETE succeeded for " + relativeUrl);
            return JsonDocument.Parse(content);
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string NewTempRoot(string prefix)
        {
            string path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Ignore temp cleanup failures.
            }
        }
    }
}
