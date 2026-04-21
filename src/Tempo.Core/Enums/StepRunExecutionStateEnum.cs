namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>Execution lifecycle state for a persisted step run row.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StepRunExecutionStateEnum
    {
        /// <summary>The step is waiting for external runtime capacity.</summary>
        AwaitingCapacity,

        /// <summary>The step is actively executing.</summary>
        Running,

        /// <summary>The step has completed and the result field is authoritative.</summary>
        Complete,

        /// <summary>The step was cancelled before completion.</summary>
        Cancelled
    }
}
