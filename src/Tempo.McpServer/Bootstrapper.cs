namespace Tempo.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.McpServer.Services;
    using Tempo.McpServer.Settings;
    using Tempo.McpServer.Tools;
    using Voltaic;

    /// <summary>
    /// Composition root for the Tempo MCP server.
    /// </summary>
    public static class Bootstrapper
    {
        /// <summary>Run the MCP server.</summary>
        /// <param name="args">Command-line arguments.</param>
        public static async Task RunAsync(string[] args)
        {
            CommandLineOptions options = CommandLineOptions.Parse(args);
            if (options.Help)
            {
                ShowHelp();
                return;
            }

            string settingsPath = string.IsNullOrWhiteSpace(options.SettingsPath) ? Constants.DefaultSettingsFile : options.SettingsPath!;
            bool settingsFileExists = System.IO.File.Exists(settingsPath);
            TempoMcpServerSettings settings = SettingsLoader.Load(settingsPath);
            settings.SoftwareVersion = Constants.Version;
            settings.Node.LastStartUtc = DateTime.UtcNow;
            SettingsLoader.ApplyEnvironment(settings);
            SettingsLoader.Save(settings, settingsPath);

            if (!settingsFileExists) Console.WriteLine("Generated default settings at " + settingsPath);

            if (options.ShowConfiguration)
            {
                Console.WriteLine(SettingsLoader.Serialize(settings));
                return;
            }

            if (options.Install)
            {
                TempoMcpInstaller.Install(settings, options.DryRun);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(Constants.ProductName);
            Console.WriteLine("(c)2026 Joel Christner");
            Console.WriteLine();
            Console.WriteLine("Tempo endpoint: " + settings.Tempo.Endpoint);

            using TempoApiClient client = new TempoApiClient(settings.Tempo);
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken token = tokenSource.Token;

            List<IDisposable> servers = new List<IDisposable>();
            List<Task> tasks = new List<Task>();

            ConsoleCancelEventHandler? cancelHandler = null;
            cancelHandler = (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                Console.WriteLine("Shutdown requested");
                tokenSource.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                InitializeServers(settings, client, servers, tasks, token);
                if (tasks.Count == 0) throw new InvalidOperationException("At least one MCP transport must be enabled");

                Task allServersTask = Task.WhenAll(tasks);
                Task shutdownTask = Task.Delay(Timeout.Infinite, token);
                Task completedTask = await Task.WhenAny(allServersTask, shutdownTask).ConfigureAwait(false);
                if (completedTask == allServersTask) await allServersTask.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
                foreach (IDisposable server in servers)
                {
                    StopAndDispose(server);
                }
            }
        }

        private static void InitializeServers(TempoMcpServerSettings settings, TempoApiClient client, List<IDisposable> servers, List<Task> tasks, CancellationToken token)
        {
            if (settings.Http.Enabled)
            {
                McpHttpServer httpServer = new McpHttpServer(settings.Http.Hostname, settings.Http.Port, settings.Http.RpcPath, settings.Http.EventsPath, includeDefaultMethods: true);
                httpServer.ServerName = "Tempo.McpServer";
                httpServer.ServerVersion = Constants.Version;
                httpServer.Log += (sender, message) => Console.WriteLine("[HTTP] " + message);
                TempoToolRegistrar.Register(httpServer, client);
                servers.Add(httpServer);
                tasks.Add(httpServer.StartAsync(token));
                Console.WriteLine("HTTP MCP: http://" + settings.Http.Hostname + ":" + settings.Http.Port + settings.Http.RpcPath);
            }

            if (settings.Tcp.Enabled)
            {
                IPAddress tcpAddress = ResolveTcpAddress(settings.Tcp.Address);
                McpTcpServer tcpServer = new McpTcpServer(tcpAddress, settings.Tcp.Port, includeDefaultMethods: true);
                tcpServer.ServerName = "Tempo.McpServer";
                tcpServer.ServerVersion = Constants.Version;
                TempoToolRegistrar.Register(tcpServer, client);
                servers.Add(tcpServer);
                tasks.Add(tcpServer.StartAsync(token));
                Console.WriteLine("TCP MCP: tcp://" + tcpAddress + ":" + settings.Tcp.Port);
            }

            if (settings.WebSocket.Enabled)
            {
                McpWebsocketsServer webSocketServer = new McpWebsocketsServer(settings.WebSocket.Hostname, settings.WebSocket.Port, settings.WebSocket.Path, includeDefaultMethods: true);
                webSocketServer.ServerName = "Tempo.McpServer";
                webSocketServer.ServerVersion = Constants.Version;
                webSocketServer.Log += (sender, message) => Console.WriteLine("[WS] " + message);
                TempoToolRegistrar.Register(webSocketServer, client);
                servers.Add(webSocketServer);
                tasks.Add(webSocketServer.StartAsync(token));
                Console.WriteLine("WebSocket MCP: ws://" + settings.WebSocket.Hostname + ":" + settings.WebSocket.Port + settings.WebSocket.Path);
            }
        }

        private static IPAddress ResolveTcpAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return IPAddress.Loopback;
            if (address.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;
            if (IPAddress.TryParse(address, out IPAddress? parsed) && parsed != null) return parsed;
            IPHostEntry hostEntry = Dns.GetHostEntry(address);
            foreach (IPAddress candidate in hostEntry.AddressList)
            {
                if (candidate.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return candidate;
            }

            throw new InvalidOperationException("Could not resolve TCP address " + address);
        }

        private static void StopAndDispose(IDisposable server)
        {
            try
            {
                if (server is McpHttpServer httpServer) httpServer.Stop();
                if (server is McpTcpServer tcpServer) tcpServer.Stop();
                if (server is McpWebsocketsServer webSocketServer) webSocketServer.Stop();
            }
            catch
            {
                // Ignore shutdown failures.
            }

            try { server.Dispose(); }
            catch { /* ignore */ }
        }

        private static void ShowHelp()
        {
            Console.WriteLine(Constants.ProductName);
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Tempo.McpServer [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --config=<file>        Settings file path (default: ./tempo.mcp.json)");
            Console.WriteLine("  --showconfig           Display configuration and exit");
            Console.WriteLine("  install                Configure Claude Code MCP and agent files");
            Console.WriteLine("  install --dry-run      Preview install changes without writing files");
            Console.WriteLine("  --help, -h             Show this help");
            Console.WriteLine();
            Console.WriteLine("Environment overrides:");
            Console.WriteLine("  TEMPO_ENDPOINT, TEMPO_TOKEN, TEMPO_API_KEY");
            Console.WriteLine("  TEMPO_ACCESS_KEY, TEMPO_TENANT_ID");
            Console.WriteLine("  TEMPO_MCP_HTTP_HOSTNAME, TEMPO_MCP_HTTP_PORT");
            Console.WriteLine("  TEMPO_MCP_TCP_ADDRESS, TEMPO_MCP_TCP_PORT");
            Console.WriteLine("  TEMPO_MCP_WS_HOSTNAME, TEMPO_MCP_WS_PORT");
        }
    }
}
