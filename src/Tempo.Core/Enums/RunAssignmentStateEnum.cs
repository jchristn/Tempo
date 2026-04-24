namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// State of an individual run assignment attempt.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RunAssignmentStateEnum
    {
        /// <summary>The assignment has been created and delivered to an executor.</summary>
        Assigned,

        /// <summary>The assignment completed successfully.</summary>
        Succeeded,

        /// <summary>The assignment completed with a non-exception failure.</summary>
        Failed,

        /// <summary>The assignment completed with an exception or timeout.</summary>
        Exception,

        /// <summary>The assignment was cancelled.</summary>
        Cancelled,

        /// <summary>The assignment lease expired and recovery may requeue the run.</summary>
        LeaseExpired
    }
}
