namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of an authorization evaluation.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuthorizationResultEnum
    {
        /// <summary>Authorization has not been evaluated yet.</summary>
        None,

        /// <summary>Access is permitted.</summary>
        Permitted,

        /// <summary>Access was denied explicitly by a matching Deny permission.</summary>
        DeniedExplicit,

        /// <summary>Access was denied implicitly because no permissions matched.</summary>
        DeniedImplicit,

        /// <summary>The requested resource was not found.</summary>
        NotFound,

        /// <summary>A conflict occurred during evaluation.</summary>
        Conflict
    }
}
