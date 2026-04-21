namespace Tempo.McpServer.Services
{
    using System.Collections.Generic;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Response returned by the Tempo REST API client.
    /// </summary>
    public class TempoApiResponse
    {
        /// <summary>HTTP status code.</summary>
        public int StatusCode { get; set; }

        /// <summary>True when the status code indicates success.</summary>
        public bool Success { get; set; }

        /// <summary>Response content type.</summary>
        public string? ContentType { get; set; }

        /// <summary>Selected response headers.</summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>Parsed JSON body when the response is JSON.</summary>
        public JsonNode? Body { get; set; }

        /// <summary>Plain text body when the response is not JSON.</summary>
        public string? Text { get; set; }
    }
}
