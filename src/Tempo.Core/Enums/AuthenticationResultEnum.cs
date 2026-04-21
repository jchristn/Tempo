namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of an authentication attempt.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuthenticationResultEnum
    {
        /// <summary>Authentication has not been attempted yet.</summary>
        None,

        /// <summary>Authentication was successful.</summary>
        Success,

        /// <summary>No matching principal was located.</summary>
        NotFound,

        /// <summary>The principal exists but is disabled.</summary>
        Inactive,

        /// <summary>The submitted credentials were invalid.</summary>
        Invalid,

        /// <summary>The submitted token has expired.</summary>
        Expired
    }
}
