namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.McpServer.Services;
    using Tempo.McpServer.Settings;
    using Touchstone.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// End-to-end tests for the Tempo MCP surface: every transport is booted in-process against a real Tempo.Server
    /// and driven the way MCP clients drive it (handshake, <c>tools/list</c>, <c>tools/call</c>), including the
    /// stateless 2026-07-28 Streamable HTTP revision used by current Claude Code releases.
    /// </summary>
    public static class McpTransportSuite
    {
        private const string HandshakeProtocolVersion = "2025-11-25";
        private const string StatelessProtocolVersion = "2026-07-28";

        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "McpTransport",
                displayName: "MCP transports end to end",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("McpTransport", "InstallerTargetsStreamableHttpEndpoint", "The Claude Code installer points clients at the Streamable HTTP /mcp endpoint rather than the legacy /rpc endpoint", InstallerTargetsStreamableHttpEndpointAsync),
                    new TestCaseDescriptor("McpTransport", "EndpointUrlsMapWildcardBindHosts", "Client URLs replace wildcard bind hosts (*, +, 0.0.0.0, ::) with 127.0.0.1 and bracket IPv6 literals", EndpointUrlsMapWildcardBindHostsAsync),
                    new TestCaseDescriptor("McpTransport", "ServerReportsBuildVersion", "Every transport advertises the built assembly version in serverInfo instead of a hard-coded constant", ServerReportsBuildVersionAsync),
                    new TestCaseDescriptor("McpTransport", "MeReportsAdminApiKeyPrincipal", "tempo_me identifies the admin API key principal instead of reporting anonymous", MeReportsAdminApiKeyPrincipalAsync),
                    new TestCaseDescriptor("McpTransport", "LocalhostEndpointAvoidsIpv6Fallback", "A localhost Tempo endpoint connects over IPv4 loopback first, so tool calls do not pay a refused-IPv6 delay", LocalhostEndpointAvoidsIpv6FallbackAsync),
                    new TestCaseDescriptor("McpTransport", "ToolFailuresReturnIsErrorResults", "Tool execution failures (invalid argument values, Tempo.Server unreachable) return isError results with a readable message instead of JSON-RPC -32603", ToolFailuresReturnIsErrorResultsAsync),
                    new TestCaseDescriptor("McpTransport", "HttpHandshakeCapsProtocolVersion", "HTTP initialize never agrees to the stateless 2026-07-28 revision and negotiates 2025-11-25 instead", HttpHandshakeCapsProtocolVersionAsync),
                    new TestCaseDescriptor("McpTransport", "HttpSessionListsAndCallsTools", "HTTP session clients discover Tempo tools through tools/list and call them through tools/call", HttpSessionListsAndCallsToolsAsync),
                    new TestCaseDescriptor("McpTransport", "HttpSessionRejectsMissingRequiredArgument", "HTTP tools/call with a missing required argument returns JSON-RPC -32602 instead of calling Tempo.Server", HttpSessionRejectsMissingRequiredArgumentAsync),
                    new TestCaseDescriptor("McpTransport", "HttpStatelessListsAndCallsTools", "Stateless 2026-07-28 clients see every Tempo tool with resultType/ttlMs/cacheScope and can call tools without a session", HttpStatelessListsAndCallsToolsAsync),
                    new TestCaseDescriptor("McpTransport", "TcpListsAndCallsTools", "TCP clients discover Tempo tools through tools/list, call them through tools/call, and can still invoke them as direct methods", TcpListsAndCallsToolsAsync),
                    new TestCaseDescriptor("McpTransport", "WebSocketListsAndCallsTools", "WebSocket clients discover Tempo tools through tools/list, call them through tools/call, and can still invoke them as direct methods", WebSocketListsAndCallsToolsAsync),
                    new TestCaseDescriptor("McpTransport", "ToolsListContainsOnlyTempoTools", "Every transport lists exactly the Tempo tools; Voltaic's demo tools (ping, echo, getTime, getSessions, getClients) are not published", ToolsListContainsOnlyTempoToolsAsync),
                    new TestCaseDescriptor("McpTransport", "PingReturnsEmptyResult", "The MCP protocol ping returns an empty result ({} or resultType-only under 2026-07-28) on every transport instead of \"pong\"", PingReturnsEmptyResultAsync),
                    new TestCaseDescriptor("McpTransport", "DemoToolsAreNotCallable", "tools/call for a Voltaic demo tool name fails with -32602 not found, and bare demo method calls fail with -32601 on TCP and WebSocket", DemoToolsAreNotCallableAsync),
                    new TestCaseDescriptor("McpTransport", "HttpRejectsBareToolMethodCalls", "HTTP clients cannot bypass tools/call by sending a Tempo tool name as a bare JSON-RPC method (-32601)", HttpRejectsBareToolMethodCallsAsync)
                });
        }

        private static readonly string[] _VoltaicDemoTools = new[] { "ping", "echo", "getTime", "getSessions", "getClients" };

        private static async Task ToolsListContainsOnlyTempoToolsAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            List<string> expected = Tempo.McpServer.Tools.TempoToolRegistrar.CreateDefinitions(harness.ApiClient).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            using HttpClient http = CreateHttpClient();
            string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;
            Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);
            using JsonDocument sessionList = await PostJsonAsync(http, url, Request(2, "tools/list", new { }), headers, ct).ConfigureAwait(false);
            AssertExactToolSet(RequireResult(sessionList, "session tools/list"), expected, "HTTP session");

            using JsonDocument statelessList = await PostStatelessAsync(http, url, 3, "tools/list", null, new Dictionary<string, object>(), ct).ConfigureAwait(false);
            AssertExactToolSet(RequireResult(statelessList, "stateless tools/list"), expected, "HTTP stateless");

            using McpTcpClient tcp = new McpTcpClient();
            Assert2.True(await tcp.ConnectAsync("127.0.0.1", harness.TcpPort, ct).ConfigureAwait(false), "TCP client connects");
            await tcp.CallAsync<JsonElement>("initialize", InitializeParams(HandshakeProtocolVersion), 15000, ct).ConfigureAwait(false);
            AssertExactToolSet(await tcp.CallAsync<JsonElement>("tools/list", new { }, 15000, ct).ConfigureAwait(false), expected, "TCP");

            using McpWebsocketsClient ws = new McpWebsocketsClient();
            Assert2.True(await ws.ConnectAsync(harness.WebSocketUrl, ct).ConfigureAwait(false), "WebSocket client connects");
            await ws.CallAsync<JsonElement>("initialize", InitializeParams(HandshakeProtocolVersion), 15000, ct).ConfigureAwait(false);
            AssertExactToolSet(await ws.CallAsync<JsonElement>("tools/list", new { }, 15000, ct).ConfigureAwait(false), expected, "WebSocket");
        }

        private static async Task PingReturnsEmptyResultAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;

            Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);
            using JsonDocument sessionPing = await PostJsonAsync(http, url, Request(2, "ping", new { }), headers, ct).ConfigureAwait(false);
            AssertEmptyObject(RequireResult(sessionPing, "session ping"), Array.Empty<string>(), "HTTP session ping");

            using JsonDocument statelessPing = await PostStatelessAsync(http, url, 3, "ping", null, new Dictionary<string, object>(), ct).ConfigureAwait(false);
            JsonElement statelessResult = RequireResult(statelessPing, "stateless ping");
            AssertEmptyObject(statelessResult, new[] { "resultType" }, "HTTP stateless ping");
            Assert2.Equal("complete", statelessResult.GetProperty("resultType").GetString()!, "stateless ping carries resultType complete");

            using McpTcpClient tcp = new McpTcpClient();
            Assert2.True(await tcp.ConnectAsync("127.0.0.1", harness.TcpPort, ct).ConfigureAwait(false), "TCP client connects");
            AssertEmptyObject(await tcp.CallAsync<JsonElement>("ping", new { }, 15000, ct).ConfigureAwait(false), Array.Empty<string>(), "TCP ping");

            using McpWebsocketsClient ws = new McpWebsocketsClient();
            Assert2.True(await ws.ConnectAsync(harness.WebSocketUrl, ct).ConfigureAwait(false), "WebSocket client connects");
            await ws.PingAsync(15000, ct).ConfigureAwait(false);
            AssertEmptyObject(await ws.CallAsync<JsonElement>("ping", new { }, 15000, ct).ConfigureAwait(false), Array.Empty<string>(), "WebSocket ping");
        }

        private static async Task DemoToolsAreNotCallableAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;
            Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);

            int id = 10;
            foreach (string name in _VoltaicDemoTools)
            {
                using JsonDocument call = await PostJsonAsync(http, url, Request(id++, "tools/call", new { name = name, arguments = new { } }), headers, ct).ConfigureAwait(false);
                Assert2.True(call.RootElement.TryGetProperty("error", out JsonElement error), "HTTP tools/call " + name + " is rejected");
                Assert2.Equal(-32602, error.GetProperty("code").GetInt32(), "HTTP tools/call " + name + " error code");
                Assert2.True(error.GetProperty("message").GetString()!.Contains("not found", StringComparison.Ordinal), "HTTP tools/call " + name + " reports the tool was not found");
            }

            using McpTcpClient tcp = new McpTcpClient();
            Assert2.True(await tcp.ConnectAsync("127.0.0.1", harness.TcpPort, ct).ConfigureAwait(false), "TCP client connects");
            using McpWebsocketsClient ws = new McpWebsocketsClient();
            Assert2.True(await ws.ConnectAsync(harness.WebSocketUrl, ct).ConfigureAwait(false), "WebSocket client connects");

            foreach (string name in new[] { "echo", "getTime", "getClients" })
            {
                string tcpError = await CaptureRpcErrorAsync(() => tcp.CallAsync<JsonElement>(name, new { message = "hi" }, 15000, ct)).ConfigureAwait(false);
                Assert2.True(tcpError.Contains("-32601", StringComparison.Ordinal), "TCP bare " + name + " is method-not-found: " + tcpError);

                string wsError = await CaptureRpcErrorAsync(() => ws.CallAsync<JsonElement>(name, new { message = "hi" }, 15000, ct)).ConfigureAwait(false);
                Assert2.True(wsError.Contains("-32601", StringComparison.Ordinal), "WebSocket bare " + name + " is method-not-found: " + wsError);
            }
        }

        private static async Task HttpRejectsBareToolMethodCallsAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;
            Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);

            using JsonDocument bare = await PostJsonAsync(http, url, Request(2, "tempo_health", new { }), headers, ct).ConfigureAwait(false);
            Assert2.True(bare.RootElement.TryGetProperty("error", out JsonElement error), "bare tool method call is rejected");
            Assert2.Equal(-32601, error.GetProperty("code").GetInt32(), "bare tool method call is method-not-found");
            Assert2.False(bare.RootElement.TryGetProperty("result", out _), "bare tool method call does not reach Tempo.Server");

            using JsonDocument viaToolsCall = await PostJsonAsync(http, url, Request(3, "tools/call", new { name = "tempo_health", arguments = new { } }), headers, ct).ConfigureAwait(false);
            AssertSuccessfulTempoCall(RequireResult(viaToolsCall, "tools/call tempo_health"), "same tool through tools/call");
        }

        private static void AssertExactToolSet(JsonElement listResult, List<string> expected, string transport)
        {
            List<string> actual = listResult.GetProperty("tools").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString() ?? string.Empty)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            foreach (string demo in _VoltaicDemoTools)
            {
                Assert2.False(actual.Contains(demo), transport + " tools/list does not publish Voltaic demo tool " + demo);
            }

            Assert2.Equal(string.Join(",", expected), string.Join(",", actual), transport + " tools/list contains exactly the Tempo tools");
        }

        private static void AssertEmptyObject(JsonElement result, string[] allowedProperties, string context)
        {
            Assert2.Equal(JsonValueKind.Object, result.ValueKind, context + " returns a JSON object, not \"pong\"");
            foreach (JsonProperty property in result.EnumerateObject())
            {
                Assert2.True(allowedProperties.Contains(property.Name), context + " result has no unexpected property " + property.Name);
            }
        }

        private static async Task<string> CaptureRpcErrorAsync(Func<Task<JsonElement>> call)
        {
            try
            {
                JsonElement unexpected = await call().ConfigureAwait(false);
                return "no error; result " + unexpected.GetRawText();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ex.Message;
            }
        }

        private static async Task InstallerTargetsStreamableHttpEndpointAsync(CancellationToken ct)
        {
            await Task.CompletedTask;
            Assert2.Equal("http://127.0.0.1:8910/mcp", McpEndpointUrls.HttpClientUrl(new McpHttpSettings()), "default settings produce the /mcp URL");

            McpHttpSettings custom = new McpHttpSettings { Hostname = "mcp.example.local", Port = 9443, RpcPath = "/custom-rpc" };
            Assert2.Equal("http://mcp.example.local:9443/mcp", McpEndpointUrls.HttpClientUrl(custom), "hostname and port honored, rpcPath ignored");
            Assert2.Throws<ArgumentNullException>(() => McpEndpointUrls.HttpClientUrl(null!), "null settings rejected");
        }

        private static async Task EndpointUrlsMapWildcardBindHostsAsync(CancellationToken ct)
        {
            await Task.CompletedTask;
            foreach (string wildcard in new[] { "*", "+", "0.0.0.0", "::", "[::]", "", "  " })
            {
                Assert2.Equal("http://127.0.0.1:8910/mcp", McpEndpointUrls.HttpClientUrl(new McpHttpSettings { Hostname = wildcard }), "HTTP wildcard '" + wildcard + "' maps to loopback");
            }

            Assert2.Equal("http://[::1]:8910/mcp", McpEndpointUrls.HttpClientUrl(new McpHttpSettings { Hostname = "::1" }), "IPv6 literal bracketed");
            Assert2.Equal("http://127.0.0.1:8910/rpc", McpEndpointUrls.HttpLegacyRpcUrl(new McpHttpSettings { Hostname = "*" }), "legacy RPC URL uses rpcPath");
            Assert2.Equal("tcp://127.0.0.1:8911", McpEndpointUrls.TcpClientUrl(new McpTcpSettings { Address = "0.0.0.0" }), "TCP wildcard maps to loopback");
            Assert2.Equal("ws://127.0.0.1:8912/mcp", McpEndpointUrls.WebSocketClientUrl(new McpWebSocketSettings { Hostname = "*" }), "WebSocket wildcard maps to loopback");
            Assert2.Equal("ws://mcp.example.local:8912/mcp", McpEndpointUrls.WebSocketClientUrl(new McpWebSocketSettings { Hostname = "mcp.example.local" }), "named host preserved");
        }

        private static async Task ServerReportsBuildVersionAsync(CancellationToken ct)
        {
            string expected = typeof(McpEndpointUrls).Assembly.GetName().Version!.ToString(3);
            Assert2.Equal(expected, Tempo.McpServer.Constants.Version, "Constants.Version matches the assembly version");

            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            using JsonDocument init = await PostJsonAsync(http, harness.HttpBaseUrl + McpTransportHarness.McpPath, InitializeRequest(HandshakeProtocolVersion), null, ct).ConfigureAwait(false);
            Assert2.Equal(expected, RequireResult(init, "initialize").GetProperty("serverInfo").GetProperty("version").GetString()!, "HTTP serverInfo.version");

            using McpTcpClient tcp = new McpTcpClient();
            Assert2.True(await tcp.ConnectAsync("127.0.0.1", harness.TcpPort, ct).ConfigureAwait(false), "TCP client connects");
            JsonElement tcpInit = await tcp.CallAsync<JsonElement>("initialize", InitializeParams(HandshakeProtocolVersion), 15000, ct).ConfigureAwait(false);
            Assert2.Equal(expected, tcpInit.GetProperty("serverInfo").GetProperty("version").GetString()!, "TCP serverInfo.version");

            using McpWebsocketsClient ws = new McpWebsocketsClient();
            Assert2.True(await ws.ConnectAsync(harness.WebSocketUrl, ct).ConfigureAwait(false), "WebSocket client connects");
            JsonElement wsInit = await ws.CallAsync<JsonElement>("initialize", InitializeParams(HandshakeProtocolVersion), 15000, ct).ConfigureAwait(false);
            Assert2.Equal(expected, wsInit.GetProperty("serverInfo").GetProperty("version").GetString()!, "WebSocket serverInfo.version");
        }

        private static async Task MeReportsAdminApiKeyPrincipalAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;
            Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);

            using JsonDocument call = await PostJsonAsync(http, url, Request(2, "tools/call", new { name = "tempo_me", arguments = new { } }), headers, ct).ConfigureAwait(false);
            JsonElement result = RequireResult(call, "tools/call tempo_me");
            AssertSuccessfulTempoCall(result, "tempo_me");

            using JsonDocument response = JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!);
            JsonElement body = response.RootElement.GetProperty("Body");
            Assert2.Equal("adminApiKey", body.GetProperty("type").GetString()!, "principal type identifies the admin API key");
            Assert2.Equal(McpTransportHarness.AdminApiKeyPrincipal, body.GetProperty("id").GetString()!, "principal id names the admin API key");
            Assert2.True(body.GetProperty("isAdmin").GetBoolean(), "admin API key principal is an administrator");
        }

        private static async Task LocalhostEndpointAvoidsIpv6FallbackAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            await harness.ApiClient.GetAsync("/v1.0/api/health", ct).ConfigureAwait(false);

            // Tempo.Server closes each connection, so every call reconnects. With ::1 tried first this took about
            // 2 seconds per call on Windows; IPv4-first loopback takes milliseconds. 5 calls in 3 seconds leaves wide margin.
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                Tempo.McpServer.Services.TempoApiResponse response = await harness.ApiClient.GetAsync("/v1.0/api/health", ct).ConfigureAwait(false);
                Assert2.Equal(200, response.StatusCode, "health call " + i + " succeeds over localhost");
            }

            Assert2.True(stopwatch.ElapsedMilliseconds < 3000, "5 localhost calls completed in " + stopwatch.ElapsedMilliseconds + "ms (expected well under 3000ms)");
        }

        private static async Task ToolFailuresReturnIsErrorResultsAsync(CancellationToken ct)
        {
            await using (McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false))
            {
                using HttpClient http = CreateHttpClient();
                string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;
                Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);

                using JsonDocument emptyId = await PostJsonAsync(http, url, Request(2, "tools/call", new { name = "readWorker", arguments = new { id = "" } }), headers, ct).ConfigureAwait(false);
                AssertToolError(RequireResult(emptyId, "readWorker with empty id"), "invalid arguments: id is required", "empty id");

                using JsonDocument badPath = await PostJsonAsync(http, url, Request(3, "tools/call", new { name = "tempo_request", arguments = new { method = "GET", path = "/etc/passwd" } }), headers, ct).ConfigureAwait(false);
                AssertToolError(RequireResult(badPath, "tempo_request outside /v1.0"), "API path must be / or start with /v1.0/", "path outside the API");
            }

            int deadPort = FreePort();
            int httpPort = FreePort();
            using Tempo.McpServer.Services.TempoApiClient deadClient = new Tempo.McpServer.Services.TempoApiClient(new TempoEndpointSettings { Endpoint = "http://127.0.0.1:" + deadPort, TimeoutMs = 5000 });
            using McpHttpServer server = Tempo.McpServer.Bootstrapper.CreateHttpServer(new McpHttpSettings { Hostname = "127.0.0.1", Port = httpPort }, deadClient);
            using CancellationTokenSource serverToken = new CancellationTokenSource();
            _ = server.StartAsync(serverToken.Token);
            try
            {
                using HttpClient http = CreateHttpClient();
                string url = "http://127.0.0.1:" + httpPort + McpTransportHarness.McpPath;
                Dictionary<string, string> headers = await OpenSessionWithRetryAsync(http, url, ct).ConfigureAwait(false);
                using JsonDocument call = await PostJsonAsync(http, url, Request(4, "tools/call", new { name = "tempo_health", arguments = new { } }), headers, ct).ConfigureAwait(false);
                AssertToolError(RequireResult(call, "tempo_health with Tempo.Server down"), "could not reach Tempo.Server at http://127.0.0.1:" + deadPort, "unreachable backend");
            }
            finally
            {
                serverToken.Cancel();
                try { server.Stop(); } catch { /* ignore */ }
            }
        }

        private static void AssertToolError(JsonElement callResult, string expectedText, string context)
        {
            Assert2.True(callResult.TryGetProperty("isError", out JsonElement isError) && isError.GetBoolean(), context + " is reported as an isError tool result");
            string text = callResult.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
            Assert2.True(text.Contains(expectedText, StringComparison.Ordinal), context + " message explains the failure: " + text);
        }

        private static async Task<Dictionary<string, string>> OpenSessionWithRetryAsync(HttpClient http, string url, CancellationToken ct)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await OpenSessionAsync(http, url, ct).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (attempt < 100)
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                }
            }
        }

        private static int FreePort()
        {
            System.Net.Sockets.TcpListener listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task HttpHandshakeCapsProtocolVersionAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();

            using JsonDocument init = await PostJsonAsync(http, harness.HttpBaseUrl + McpTransportHarness.McpPath, InitializeRequest(StatelessProtocolVersion), null, ct).ConfigureAwait(false);
            JsonElement result = RequireResult(init, "initialize");
            Assert2.Equal(HandshakeProtocolVersion, result.GetProperty("protocolVersion").GetString()!, "handshake negotiates at most 2025-11-25");
            Assert2.Equal("Tempo.McpServer", result.GetProperty("serverInfo").GetProperty("name").GetString()!, "server name advertised");
        }

        private static async Task HttpSessionListsAndCallsToolsAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;
            Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);

            using JsonDocument list = await PostJsonAsync(http, url, Request(2, "tools/list", new { }), headers, ct).ConfigureAwait(false);
            AssertTempoToolsListed(RequireResult(list, "tools/list"), "HTTP session");

            using JsonDocument call = await PostJsonAsync(http, url, Request(3, "tools/call", new { name = "tempo_health", arguments = new { } }), headers, ct).ConfigureAwait(false);
            AssertSuccessfulTempoCall(RequireResult(call, "tools/call"), "HTTP session tempo_health");

            using JsonDocument workers = await PostJsonAsync(http, url, Request(4, "tools/call", new { name = "listWorkers", arguments = new { } }), headers, ct).ConfigureAwait(false);
            AssertSuccessfulTempoCall(RequireResult(workers, "tools/call"), "HTTP session listWorkers forwards the admin API key");
        }

        private static async Task HttpSessionRejectsMissingRequiredArgumentAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            string url = harness.HttpBaseUrl + McpTransportHarness.McpPath;
            Dictionary<string, string> headers = await OpenSessionAsync(http, url, ct).ConfigureAwait(false);

            using JsonDocument call = await PostJsonAsync(http, url, Request(5, "tools/call", new { name = "readWorker", arguments = new { } }), headers, ct).ConfigureAwait(false);
            Assert2.True(call.RootElement.TryGetProperty("error", out JsonElement error), "missing required argument yields a JSON-RPC error");
            Assert2.Equal(-32602, error.GetProperty("code").GetInt32(), "invalid params error code");
            Assert2.True(error.GetProperty("message").GetString()!.Contains("id", StringComparison.Ordinal), "error names the missing property");
        }

        private static async Task HttpStatelessListsAndCallsToolsAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using HttpClient http = CreateHttpClient();
            string url = McpEndpointUrls.HttpClientUrl(new McpHttpSettings { Hostname = "127.0.0.1", Port = harness.HttpPort });

            using JsonDocument discover = await PostStatelessAsync(http, url, 1, "server/discover", null, new Dictionary<string, object>(), ct).ConfigureAwait(false);
            JsonElement discoverResult = RequireResult(discover, "server/discover");
            Assert2.True(discoverResult.GetProperty("supportedVersions").EnumerateArray().Any(v => v.GetString() == StatelessProtocolVersion), "server/discover advertises 2026-07-28");

            using JsonDocument list = await PostStatelessAsync(http, url, 2, "tools/list", null, new Dictionary<string, object>(), ct).ConfigureAwait(false);
            JsonElement listResult = RequireResult(list, "stateless tools/list");
            AssertTempoToolsListed(listResult, "stateless HTTP");
            Assert2.Equal("complete", listResult.GetProperty("resultType").GetString()!, "stateless results carry resultType");
            Assert2.True(listResult.TryGetProperty("ttlMs", out _), "cacheable list results carry ttlMs");
            Assert2.True(listResult.TryGetProperty("cacheScope", out _), "cacheable list results carry cacheScope");

            Dictionary<string, object> callParams = new Dictionary<string, object>
            {
                { "name", "tempo_health" },
                { "arguments", new { } }
            };
            using JsonDocument call = await PostStatelessAsync(http, url, 3, "tools/call", "tempo_health", callParams, ct).ConfigureAwait(false);
            JsonElement callResult = RequireResult(call, "stateless tools/call");
            Assert2.Equal("complete", callResult.GetProperty("resultType").GetString()!, "stateless tool results carry resultType");
            AssertSuccessfulTempoCall(callResult, "stateless tempo_health");
        }

        private static async Task TcpListsAndCallsToolsAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using McpTcpClient client = new McpTcpClient();
            Assert2.True(await client.ConnectAsync("127.0.0.1", harness.TcpPort, ct).ConfigureAwait(false), "TCP client connects");

            JsonElement init = await client.CallAsync<JsonElement>("initialize", InitializeParams(HandshakeProtocolVersion), 15000, ct).ConfigureAwait(false);
            Assert2.Equal(HandshakeProtocolVersion, init.GetProperty("protocolVersion").GetString()!, "TCP handshake version");

            JsonElement list = await client.CallAsync<JsonElement>("tools/list", new { }, 15000, ct).ConfigureAwait(false);
            AssertTempoToolsListed(list, "TCP");

            JsonElement call = await client.CallAsync<JsonElement>("tools/call", new { name = "tempo_health", arguments = new { } }, 15000, ct).ConfigureAwait(false);
            AssertSuccessfulTempoCall(call, "TCP tools/call tempo_health");

            JsonElement direct = await client.CallAsync<JsonElement>("tempo_health", new { }, 15000, ct).ConfigureAwait(false);
            Assert2.Equal(200, direct.GetProperty("StatusCode").GetInt32(), "TCP direct method invocation still reaches Tempo.Server");
        }

        private static async Task WebSocketListsAndCallsToolsAsync(CancellationToken ct)
        {
            await using McpTransportHarness harness = await McpTransportHarness.StartAsync(ct).ConfigureAwait(false);
            using McpWebsocketsClient client = new McpWebsocketsClient();
            Assert2.True(await client.ConnectAsync(harness.WebSocketUrl, ct).ConfigureAwait(false), "WebSocket client connects");

            JsonElement init = await client.CallAsync<JsonElement>("initialize", InitializeParams(HandshakeProtocolVersion), 15000, ct).ConfigureAwait(false);
            Assert2.Equal(HandshakeProtocolVersion, init.GetProperty("protocolVersion").GetString()!, "WebSocket handshake version");

            JsonElement list = await client.CallAsync<JsonElement>("tools/list", new { }, 15000, ct).ConfigureAwait(false);
            AssertTempoToolsListed(list, "WebSocket");

            JsonElement call = await client.CallAsync<JsonElement>("tools/call", new { name = "tempo_health", arguments = new { } }, 15000, ct).ConfigureAwait(false);
            AssertSuccessfulTempoCall(call, "WebSocket tools/call tempo_health");

            JsonElement direct = await client.CallAsync<JsonElement>("tempo_health", new { }, 15000, ct).ConfigureAwait(false);
            Assert2.Equal(200, direct.GetProperty("StatusCode").GetInt32(), "WebSocket direct method invocation still reaches Tempo.Server");
        }

        private static void AssertTempoToolsListed(JsonElement listResult, string transport)
        {
            HashSet<string> names = new HashSet<string>(
                listResult.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString() ?? string.Empty),
                StringComparer.Ordinal);

            foreach (string expected in new[] { "tempo_health", "tempo_me", "tempo_request", "listWorkers", "readWorker" })
            {
                Assert2.True(names.Contains(expected), transport + " tools/list includes " + expected);
            }

            JsonElement readWorker = listResult.GetProperty("tools").EnumerateArray().First(t => t.GetProperty("name").GetString() == "readWorker");
            Assert2.True(readWorker.TryGetProperty("inputSchema", out JsonElement schema), transport + " tools advertise an input schema");
            Assert2.True(schema.GetProperty("required").EnumerateArray().Any(r => r.GetString() == "id"), transport + " readWorker schema requires id");
        }

        private static void AssertSuccessfulTempoCall(JsonElement callResult, string context)
        {
            Assert2.False(callResult.TryGetProperty("isError", out JsonElement isError) && isError.GetBoolean(), context + " is not a tool error");
            string text = callResult.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
            using JsonDocument response = JsonDocument.Parse(text);
            Assert2.Equal(200, response.RootElement.GetProperty("StatusCode").GetInt32(), context + " reaches Tempo.Server with HTTP 200");
            Assert2.True(response.RootElement.GetProperty("Success").GetBoolean(), context + " reports Success");
        }

        private static async Task<Dictionary<string, string>> OpenSessionAsync(HttpClient http, string url, CancellationToken ct)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(JsonSerializer.Serialize(InitializeRequest(HandshakeProtocolVersion)), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "initialize succeeds");
            Assert2.True(response.Headers.TryGetValues("Mcp-Session-Id", out IEnumerable<string>? values), "initialize issues a session id");
            string sessionId = values!.First();

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Mcp-Session-Id", sessionId },
                { "MCP-Protocol-Version", HandshakeProtocolVersion }
            };

            using HttpRequestMessage initialized = new HttpRequestMessage(HttpMethod.Post, url);
            foreach (KeyValuePair<string, string> header in headers) initialized.Headers.TryAddWithoutValidation(header.Key, header.Value);
            initialized.Content = new StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", Encoding.UTF8, "application/json");
            using HttpResponseMessage initializedResponse = await http.SendAsync(initialized, ct).ConfigureAwait(false);
            Assert2.True(initializedResponse.IsSuccessStatusCode, "notifications/initialized accepted");
            return headers;
        }

        private static async Task<JsonDocument> PostStatelessAsync(HttpClient http, string url, int id, string method, string? name, Dictionary<string, object> parameters, CancellationToken ct)
        {
            parameters["_meta"] = new Dictionary<string, object>
            {
                { "io.modelcontextprotocol/protocolVersion", StatelessProtocolVersion },
                { "io.modelcontextprotocol/clientInfo", new { name = "Test.Shared", version = "1.0.0" } },
                { "io.modelcontextprotocol/clientCapabilities", new { } }
            };

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "MCP-Protocol-Version", StatelessProtocolVersion },
                { "Mcp-Method", method }
            };
            if (name != null) headers["Mcp-Name"] = name;

            JsonDocument document = await PostJsonAsync(http, url, Request(id, method, parameters), headers, ct).ConfigureAwait(false);
            return document;
        }

        private static async Task<JsonDocument> PostJsonAsync(HttpClient http, string url, object body, Dictionary<string, string>? headers, CancellationToken ct)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Assert2.Equal(HttpStatusCode.OK, response.StatusCode, "MCP POST succeeds: " + content);
            return JsonDocument.Parse(content);
        }

        private static JsonElement RequireResult(JsonDocument document, string context)
        {
            if (document.RootElement.TryGetProperty("error", out JsonElement error))
                throw new InvalidOperationException(context + " returned a JSON-RPC error: " + error.GetRawText());

            return document.RootElement.GetProperty("result");
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
            return http;
        }

        private static object Request(int id, string method, object parameters)
        {
            return new Dictionary<string, object>
            {
                { "jsonrpc", "2.0" },
                { "id", id },
                { "method", method },
                { "params", parameters }
            };
        }

        private static object InitializeRequest(string protocolVersion)
        {
            return Request(1, "initialize", InitializeParams(protocolVersion));
        }

        private static object InitializeParams(string protocolVersion)
        {
            return new
            {
                protocolVersion = protocolVersion,
                capabilities = new { },
                clientInfo = new { name = "Test.Shared", version = "1.0.0" }
            };
        }
    }
}
