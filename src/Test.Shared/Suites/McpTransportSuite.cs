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
                    new TestCaseDescriptor("McpTransport", "HttpHandshakeCapsProtocolVersion", "HTTP initialize never agrees to the stateless 2026-07-28 revision and negotiates 2025-11-25 instead", HttpHandshakeCapsProtocolVersionAsync),
                    new TestCaseDescriptor("McpTransport", "HttpSessionListsAndCallsTools", "HTTP session clients discover Tempo tools through tools/list and call them through tools/call", HttpSessionListsAndCallsToolsAsync),
                    new TestCaseDescriptor("McpTransport", "HttpSessionRejectsMissingRequiredArgument", "HTTP tools/call with a missing required argument returns JSON-RPC -32602 instead of calling Tempo.Server", HttpSessionRejectsMissingRequiredArgumentAsync),
                    new TestCaseDescriptor("McpTransport", "HttpStatelessListsAndCallsTools", "Stateless 2026-07-28 clients see every Tempo tool with resultType/ttlMs/cacheScope and can call tools without a session", HttpStatelessListsAndCallsToolsAsync),
                    new TestCaseDescriptor("McpTransport", "TcpListsAndCallsTools", "TCP clients discover Tempo tools through tools/list, call them through tools/call, and can still invoke them as direct methods", TcpListsAndCallsToolsAsync),
                    new TestCaseDescriptor("McpTransport", "WebSocketListsAndCallsTools", "WebSocket clients discover Tempo tools through tools/list, call them through tools/call, and can still invoke them as direct methods", WebSocketListsAndCallsToolsAsync)
                });
        }

        private static async Task InstallerTargetsStreamableHttpEndpointAsync(CancellationToken ct)
        {
            await Task.CompletedTask;
            Assert2.Equal("http://127.0.0.1:8910/mcp", TempoMcpInstaller.BuildClientUrl(new McpHttpSettings()), "default settings produce the /mcp URL");

            McpHttpSettings custom = new McpHttpSettings { Hostname = "mcp.example.local", Port = 9443, RpcPath = "/custom-rpc" };
            Assert2.Equal("http://mcp.example.local:9443/mcp", TempoMcpInstaller.BuildClientUrl(custom), "hostname and port honored, rpcPath ignored");
            Assert2.Throws<ArgumentNullException>(() => TempoMcpInstaller.BuildClientUrl(null!), "null settings rejected");
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
            string url = TempoMcpInstaller.BuildClientUrl(new McpHttpSettings { Hostname = "127.0.0.1", Port = harness.HttpPort });

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
