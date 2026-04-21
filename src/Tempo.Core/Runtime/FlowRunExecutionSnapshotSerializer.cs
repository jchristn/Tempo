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

        public static string Serialize(FlowRunExecutionSnapshot snapshot)
        {
            return JsonSerializer.Serialize(snapshot, _Json);
        }

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
