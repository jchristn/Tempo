namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Logical execution node kind that owned a flow-run assignment.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExecutionNodeKindEnum
    {
        /// <summary>The assignment was executed by the server pseudo-worker.</summary>
        Server,

        /// <summary>The assignment was executed by a remote worker.</summary>
        Worker
    }
}
