namespace Tempo.McpServer.Settings
{
    /// <summary>
    /// WebSocket MCP transport settings.
    /// </summary>
    public class McpWebSocketSettings
    {
        /// <summary>Enable the WebSocket MCP transport.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>WebSocket bind hostname.</summary>
        public string Hostname { get; set; } = "127.0.0.1";

        /// <summary>WebSocket bind port.</summary>
        public int Port { get; set; } = 8912;

        /// <summary>WebSocket endpoint path.</summary>
        public string Path { get; set; } = "/mcp";
    }
}
