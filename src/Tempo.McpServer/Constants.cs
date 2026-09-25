namespace Tempo.McpServer
{
    /// <summary>
    /// MCP server constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>Product name.</summary>
        public const string ProductName = "Tempo MCP Server";

        /// <summary>Software version.</summary>
        public const string Version = "0.3.0";

        /// <summary>Default settings filename.</summary>
        public const string DefaultSettingsFile = "./tempo.mcp.json";

        /// <summary>
        /// Streamable HTTP MCP endpoint path served by the HTTP transport. Fixed by Voltaic and not configurable.
        /// This is the endpoint MCP clients such as Claude Code must use: it serves both session-based handshakes and
        /// the stateless 2026-07-28 revision. The legacy <c>rpcPath</c> endpoint does not apply the 2026-07-28 result
        /// shape, so stateless clients connected to it see zero tools.
        /// </summary>
        public const string StreamableHttpPath = "/mcp";

        /// <summary>Default Tempo API endpoint.</summary>
        public const string DefaultTempoEndpoint = "http://localhost:8901";

        /// <summary>Environment variable for the Tempo API endpoint.</summary>
        public const string TempoEndpointEnvironmentVariable = "TEMPO_ENDPOINT";

        /// <summary>Environment variable for Tempo bearer or x-token authentication.</summary>
        public const string TempoTokenEnvironmentVariable = "TEMPO_TOKEN";

        /// <summary>Environment variable for Tempo x-api-key authentication.</summary>
        public const string TempoApiKeyEnvironmentVariable = "TEMPO_API_KEY";

        /// <summary>Environment variable for Tempo x-access-key authentication.</summary>
        public const string TempoAccessKeyEnvironmentVariable = "TEMPO_ACCESS_KEY";

        /// <summary>Environment variable for the default Tempo tenant identifier.</summary>
        public const string TempoTenantIdEnvironmentVariable = "TEMPO_TENANT_ID";

        /// <summary>Environment variable for MCP HTTP hostname.</summary>
        public const string McpHttpHostnameEnvironmentVariable = "TEMPO_MCP_HTTP_HOSTNAME";

        /// <summary>Environment variable for MCP HTTP port.</summary>
        public const string McpHttpPortEnvironmentVariable = "TEMPO_MCP_HTTP_PORT";

        /// <summary>Environment variable for MCP TCP address.</summary>
        public const string McpTcpAddressEnvironmentVariable = "TEMPO_MCP_TCP_ADDRESS";

        /// <summary>Environment variable for MCP TCP port.</summary>
        public const string McpTcpPortEnvironmentVariable = "TEMPO_MCP_TCP_PORT";

        /// <summary>Environment variable for MCP WebSocket hostname.</summary>
        public const string McpWebSocketHostnameEnvironmentVariable = "TEMPO_MCP_WS_HOSTNAME";

        /// <summary>Environment variable for MCP WebSocket port.</summary>
        public const string McpWebSocketPortEnvironmentVariable = "TEMPO_MCP_WS_PORT";
    }
}
