namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Current state of a flow run.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FlowRunStateEnum
    {
        /// <summary>The run is queued but has not started yet.</summary>
        Queued,

        /// <summary>The run is currently executing.</summary>
        Running,

        /// <summary>The run completed successfully.</summary>
        Succeeded,

        /// <summary>The run terminated with a failure result.</summary>
        Failed,

        /// <summary>The run terminated with an unhandled exception or timeout.</summary>
        Exception,

        /// <summary>The run was cancelled before completion.</summary>
        Cancelled
    }
}
