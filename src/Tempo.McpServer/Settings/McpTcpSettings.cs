namespace Tempo.McpServer.Settings
{
    /// <summary>
    /// TCP MCP transport settings.
    /// </summary>
    public class McpTcpSettings
    {
        /// <summary>Enable the TCP MCP transport.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>TCP bind address.</summary>
        public string Address { get; set; } = "127.0.0.1";

        /// <summary>TCP bind port.</summary>
        public int Port { get; set; } = 8911;
    }
}
