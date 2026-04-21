namespace Tempo.McpServer.Settings
{
    using System;

    /// <summary>
    /// Local node metadata.
    /// </summary>
    public class NodeSettings
    {
        /// <summary>Node display name.</summary>
        public string Name { get; set; } = Environment.MachineName;

        /// <summary>Last process start timestamp.</summary>
        public DateTime LastStartUtc { get; set; } = DateTime.UtcNow;
    }
}
