namespace Tempo.McpServer.Tools
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A Tempo MCP tool definition.
    /// </summary>
    public class TempoToolDefinition
    {
        /// <summary>Tool name.</summary>
        public string Name { get; }

        /// <summary>Tool description.</summary>
        public string Description { get; }

        /// <summary>JSON schema for tool input.</summary>
        public object InputSchema { get; }

        /// <summary>Tool handler.</summary>
        public Func<JsonElement?, CancellationToken, Task<object>> Handler { get; }

        /// <summary>Instantiate.</summary>
        /// <param name="name">Tool name.</param>
        /// <param name="description">Tool description.</param>
        /// <param name="inputSchema">Input schema.</param>
        /// <param name="handler">Tool handler.</param>
        public TempoToolDefinition(string name, string description, object inputSchema, Func<JsonElement?, CancellationToken, Task<object>> handler)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;
            Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentNullException(nameof(description)) : description;
            InputSchema = inputSchema ?? throw new ArgumentNullException(nameof(inputSchema));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }
    }
}
