namespace Test.Shared
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Settings;
    using McpBootstrapper = Tempo.McpServer.Bootstrapper;
    using Tempo.McpServer.Services;
    using Tempo.McpServer.Settings;
    using Tempo.Server;
    using Voltaic.Mcp;

    /// <summary>
    /// Boots an in-process Tempo.Server plus the Tempo.McpServer HTTP, TCP, and WebSocket transports on free loopback
    /// ports so tests can drive the MCP surface end to end with real MCP clients. Not thread-safe; one harness per test.
    /// </summary>
    public sealed class McpTransportHarness : IAsyncDisposable
    {
        /// <summary>Admin API key the in-process Tempo.Server accepts and the MCP server forwards.</summary>
        public const string AdminApiKey = "tempo-mcp-suite-admin-key";

        /// <summary>Streamable HTTP MCP endpoint path.</summary>
        public const string McpPath = Tempo.McpServer.Constants.StreamableHttpPath;

        /// <summary>WebSocket MCP endpoint path.</summary>
        public const string WebSocketPath = "/mcp";

        /// <summary>Admin API key principal name reported by Tempo.Server.</summary>
        public const string AdminApiKeyPrincipal = "admin-api-key";

        /// <summary>Tempo API client the MCP transports forward tool calls through.</summary>
        public TempoApiClient ApiClient => _ApiClient ?? throw new InvalidOperationException("Harness has not started.");

        /// <summary>Base URL of the HTTP MCP transport, for example <c>http://127.0.0.1:50123</c>.</summary>
        public string HttpBaseUrl => "http://127.0.0.1:" + _HttpPort;

        /// <summary>HTTP MCP transport port on 127.0.0.1.</summary>
        public int HttpPort => _HttpPort;

        /// <summary>TCP MCP transport port on 127.0.0.1.</summary>
        public int TcpPort => _TcpPort;

        /// <summary>WebSocket MCP transport URL.</summary>
        public string WebSocketUrl => "ws://127.0.0.1:" + _WebSocketPort + WebSocketPath;

        private readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private SqliteDatabaseDriver? _Driver;
        private TempoServer? _TempoServer;
        private TempoApiClient? _ApiClient;
        private McpHttpServer? _HttpServer;
        private McpTcpServer? _TcpServer;
        private McpWebsocketsServer? _WebSocketServer;
        private int _HttpPort;
        private int _TcpPort;
        private int _WebSocketPort;

        private McpTransportHarness()
        {
        }

        /// <summary>Start Tempo.Server and every MCP transport, waiting until each is accepting connections.</summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A started harness; dispose it to stop all servers.</returns>
        /// <exception cref="TimeoutException">Thrown when a transport does not start listening in time.</exception>
        public static async Task<McpTransportHarness> StartAsync(CancellationToken token)
        {
            McpTransportHarness harness = new McpTransportHarness();
            try
            {
                await harness.StartInternalAsync(token).ConfigureAwait(false);
                return harness;
            }
            catch
            {
                await harness.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>Stop all MCP transports and Tempo.Server and release the temporary database.</summary>
        /// <returns>Task.</returns>
        public async ValueTask DisposeAsync()
        {
            _TokenSource.Cancel();
            try { _HttpServer?.Stop(); } catch { /* ignore */ }
            try { _TcpServer?.Stop(); } catch { /* ignore */ }
            try { _WebSocketServer?.Stop(); } catch { /* ignore */ }
            try { _HttpServer?.Dispose(); } catch { /* ignore */ }
            try { _TcpServer?.Dispose(); } catch { /* ignore */ }
            try { _WebSocketServer?.Dispose(); } catch { /* ignore */ }
            try { _ApiClient?.Dispose(); } catch { /* ignore */ }
            try { _TempoServer?.Dispose(); } catch { /* ignore */ }
            if (_Driver != null) await TempTestStore.DisposeAsync(_Driver).ConfigureAwait(false);
            _TokenSource.Dispose();
        }

        private async Task StartInternalAsync(CancellationToken token)
        {
            _Driver = await TempTestStore.CreateAsync(token).ConfigureAwait(false);

            int tempoPort = FreePort();
            Settings settings = new Settings();
            settings.Rest.Hostname = "127.0.0.1";
            settings.Rest.Port = tempoPort;
            settings.Auth.AdminApiKey = AdminApiKey;
            settings.Logging.FileLogging = false;
            settings.Logging.ConsoleLogging = false;
            settings.Hydration.SeedDefaults = false;
            settings.Engine.ServerCanExecuteWorkload = false;
            settings.RequestHistory.Enabled = false;

            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            _TempoServer = new TempoServer(settings, logging, _Driver, new Tempo.StepManager());
            await _TempoServer.StartAsync().ConfigureAwait(false);

            // "localhost" on purpose: Tempo.Server binds IPv4 loopback only, which is the configuration where resolving
            // ::1 first used to add about 2 seconds to every MCP tool call on Windows.
            TempoEndpointSettings endpoint = new TempoEndpointSettings
            {
                Endpoint = "http://localhost:" + tempoPort,
                ApiKey = AdminApiKey,
                TimeoutMs = 15000
            };
            _ApiClient = new TempoApiClient(endpoint);

            // Built through the same factories Tempo.McpServer uses at startup so name, version, and tool wiring match production.
            _HttpPort = FreePort();
            _HttpServer = McpBootstrapper.CreateHttpServer(new McpHttpSettings { Hostname = "127.0.0.1", Port = _HttpPort }, _ApiClient);
            _ = _HttpServer.StartAsync(_TokenSource.Token);

            _TcpPort = FreePort();
            _TcpServer = McpBootstrapper.CreateTcpServer(new McpTcpSettings { Address = "127.0.0.1", Port = _TcpPort }, _ApiClient);
            _ = _TcpServer.StartAsync(_TokenSource.Token);

            _WebSocketPort = FreePort();
            _WebSocketServer = McpBootstrapper.CreateWebSocketServer(new McpWebSocketSettings { Hostname = "127.0.0.1", Port = _WebSocketPort, Path = WebSocketPath }, _ApiClient);
            _ = _WebSocketServer.StartAsync(_TokenSource.Token);

            await WaitForListenerAsync(_HttpPort, token).ConfigureAwait(false);
            await WaitForListenerAsync(_TcpPort, token).ConfigureAwait(false);
            await WaitForListenerAsync(_WebSocketPort, token).ConfigureAwait(false);
        }

        private static async Task WaitForListenerAsync(int port, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 10000)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using TcpClient probe = new TcpClient();
                    await probe.ConnectAsync(IPAddress.Loopback, port, token).ConfigureAwait(false);
                    return;
                }
                catch (SocketException)
                {
                    await Task.Delay(50, token).ConfigureAwait(false);
                }
            }

            throw new TimeoutException("MCP transport on port " + port + " did not start listening within 10 seconds.");
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
