namespace Tempo.McpServer.Services
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using Tempo.McpServer.Settings;

    /// <summary>
    /// Builds the URLs MCP clients should use to reach each Tempo.McpServer transport from the configured bind settings.
    /// Wildcard bind hosts (<c>*</c>, <c>+</c>, <c>0.0.0.0</c>, <c>::</c>, or empty) are not connectable, so they are
    /// replaced with <c>127.0.0.1</c>; IPv6 literals are bracketed. All members are stateless and thread-safe.
    /// </summary>
    public static class McpEndpointUrls
    {
        /// <summary>Loopback host substituted for wildcard bind hosts.</summary>
        public const string LoopbackHost = "127.0.0.1";

        /// <summary>
        /// URL of the Streamable HTTP endpoint (<see cref="Constants.StreamableHttpPath"/>), the endpoint MCP clients
        /// such as Claude Code must be configured with. The legacy <c>rpcPath</c> is intentionally ignored.
        /// </summary>
        /// <param name="http">HTTP transport settings.</param>
        /// <returns>Client URL, for example <c>http://127.0.0.1:8910/mcp</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when http is null.</exception>
        public static string HttpClientUrl(McpHttpSettings http)
        {
            ArgumentNullException.ThrowIfNull(http);
            return "http://" + ClientHost(http.Hostname) + ":" + http.Port + Constants.StreamableHttpPath;
        }

        /// <summary>URL of the legacy JSON-RPC endpoint (<c>rpcPath</c>). Not suitable for stateless 2026-07-28 clients.</summary>
        /// <param name="http">HTTP transport settings.</param>
        /// <returns>Legacy URL, for example <c>http://127.0.0.1:8910/rpc</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when http is null.</exception>
        public static string HttpLegacyRpcUrl(McpHttpSettings http)
        {
            ArgumentNullException.ThrowIfNull(http);
            return "http://" + ClientHost(http.Hostname) + ":" + http.Port + http.RpcPath;
        }

        /// <summary>Address of the TCP transport.</summary>
        /// <param name="tcp">TCP transport settings.</param>
        /// <returns>Client address, for example <c>tcp://127.0.0.1:8911</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tcp is null.</exception>
        public static string TcpClientUrl(McpTcpSettings tcp)
        {
            ArgumentNullException.ThrowIfNull(tcp);
            return "tcp://" + ClientHost(tcp.Address) + ":" + tcp.Port;
        }

        /// <summary>URL of the WebSocket transport.</summary>
        /// <param name="webSocket">WebSocket transport settings.</param>
        /// <returns>Client URL, for example <c>ws://127.0.0.1:8912/mcp</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when webSocket is null.</exception>
        public static string WebSocketClientUrl(McpWebSocketSettings webSocket)
        {
            ArgumentNullException.ThrowIfNull(webSocket);
            return "ws://" + ClientHost(webSocket.Hostname) + ":" + webSocket.Port + webSocket.Path;
        }

        /// <summary>Convert a bind host into a host clients can connect to.</summary>
        /// <param name="bindHost">Configured bind host. Null, empty, and wildcard values map to <see cref="LoopbackHost"/>.</param>
        /// <returns>Connectable host, with IPv6 literals wrapped in brackets.</returns>
        public static string ClientHost(string? bindHost)
        {
            string host = (bindHost ?? string.Empty).Trim();
            if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal))
                host = host.Substring(1, host.Length - 2);

            if (host.Length == 0 || host == "*" || host == "+") return LoopbackHost;

            if (IPAddress.TryParse(host, out IPAddress? address) && address != null)
            {
                if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return LoopbackHost;
                if (address.AddressFamily == AddressFamily.InterNetworkV6) return "[" + host + "]";
            }

            return host;
        }
    }
}
