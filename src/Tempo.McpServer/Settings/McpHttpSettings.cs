namespace Tempo.McpServer.Settings
{
    /// <summary>
    /// HTTP MCP transport settings.
    /// </summary>
    public class McpHttpSettings
    {
        /// <summary>Enable the HTTP MCP transport.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>HTTP bind hostname.</summary>
        public string Hostname { get; set; } = "127.0.0.1";

        /// <summary>HTTP bind port.</summary>
        public int Port { get; set; } = 8910;

        /// <summary>
        /// Legacy JSON-RPC endpoint path. MCP clients should use the Streamable HTTP endpoint
        /// (<see cref="Constants.StreamableHttpPath"/>) instead; stateless 2026-07-28 clients see zero tools here.
        /// </summary>
        public string RpcPath { get; set; } = "/rpc";

        /// <summary>SSE events endpoint path.</summary>
        public string EventsPath { get; set; } = "/events";
    }
}
