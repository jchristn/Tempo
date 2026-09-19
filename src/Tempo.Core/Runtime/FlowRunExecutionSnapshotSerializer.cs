namespace Tempo.Core.Runtime
{
    using System.Text.Json;

    /// <summary>JSON helpers for persisted flow-run execution snapshots.</summary>
    public static class FlowRunExecutionSnapshotSerializer
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        /// <summary>
        /// Serializes a flow-run execution snapshot to JSON.
        /// </summary>
        /// <param name="snapshot">The snapshot to serialize.</param>
        /// <returns>The JSON representation of the snapshot.</returns>
        public static string Serialize(FlowRunExecutionSnapshot snapshot)
        {
            return JsonSerializer.Serialize(snapshot, _Json);
        }

        /// <summary>
        /// Deserializes a flow-run execution snapshot from JSON, returning a new snapshot when the input is empty.
        /// </summary>
        /// <param name="json">The JSON to deserialize. May be null or whitespace.</param>
        /// <param name="flowRunId">The flow-run identifier applied when the snapshot lacks one.</param>
        /// <returns>The deserialized snapshot, or a new snapshot bound to <paramref name="flowRunId"/> when input is empty.</returns>
        public static FlowRunExecutionSnapshot Deserialize(string? json, string flowRunId)
        {
            if (string.IsNullOrWhiteSpace(json)) return new FlowRunExecutionSnapshot { FlowRunId = flowRunId };
            FlowRunExecutionSnapshot? snapshot = JsonSerializer.Deserialize<FlowRunExecutionSnapshot>(json, _Json);
            if (snapshot == null) return new FlowRunExecutionSnapshot { FlowRunId = flowRunId };
            if (string.IsNullOrWhiteSpace(snapshot.FlowRunId)) snapshot.FlowRunId = flowRunId;
            return snapshot;
        }
    }
}
