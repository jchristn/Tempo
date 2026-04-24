namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Authentication policy used when a flow is invoked through an HTTP trigger.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DataFlowInvocationAuthModeEnum
    {
        /// <summary>HTTP trigger invocation only requires a valid trigger identifier.</summary>
        Public,

        /// <summary>HTTP trigger invocation requires standard Tempo API authentication and tenant access.</summary>
        ApiAuthenticated
    }
}
