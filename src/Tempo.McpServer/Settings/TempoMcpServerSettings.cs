namespace Tempo.McpServer.Settings
{
    using System;

    /// <summary>
    /// Root settings for the Tempo MCP server.
    /// </summary>
    public class TempoMcpServerSettings
    {
        /// <summary>Creation timestamp.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Software version.</summary>
        public string SoftwareVersion { get; set; } = Constants.Version;

        /// <summary>Node metadata.</summary>
        public NodeSettings Node { get; set; } = new NodeSettings();

        /// <summary>Tempo API connection settings.</summary>
        public TempoEndpointSettings Tempo { get; set; } = new TempoEndpointSettings();

        /// <summary>HTTP MCP transport settings.</summary>
        public McpHttpSettings Http { get; set; } = new McpHttpSettings();

        /// <summary>TCP MCP transport settings.</summary>
        public McpTcpSettings Tcp { get; set; } = new McpTcpSettings();

        /// <summary>WebSocket MCP transport settings.</summary>
        public McpWebSocketSettings WebSocket { get; set; } = new McpWebSocketSettings();
    }
}
