namespace Tempo.Core.Requests
{
    using System.Text.Json;

    /// <summary>
    /// Enqueue a flow run.
    /// </summary>
    public class FlowRunRequest
    {
        /// <summary>Optional request identifier. Generated if not supplied.</summary>
        public string? RequestId { get; set; } = null;

        /// <summary>Optional input payload handed to the starting step as <c>Data</c>.</summary>
        public JsonElement? Data { get; set; } = null;

        /// <summary>Optional metadata handed to the starting step as <c>Metadata</c>.</summary>
        public JsonElement? Metadata { get; set; } = null;
    }
}
