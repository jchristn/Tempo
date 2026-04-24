namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Dispatch lifecycle state tracked separately from the coarse flow-run state.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FlowRunDispatchStateEnum
    {
        /// <summary>The run is queued and waiting for an eligible executor.</summary>
        Pending,

        /// <summary>The run has been assigned to an executor.</summary>
        Assigned,

        /// <summary>The assignment completed successfully.</summary>
        Completed,

        /// <summary>The assignment completed with a failure or exception.</summary>
        Failed,

        /// <summary>The run was cancelled before assignment completed.</summary>
        Cancelled
    }
}
