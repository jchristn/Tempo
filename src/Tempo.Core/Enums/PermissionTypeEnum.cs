namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Whether a permission permits or explicitly denies access.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PermissionTypeEnum
    {
        /// <summary>The permission grants access.</summary>
        Permit,

        /// <summary>The permission explicitly denies access.</summary>
        Deny
    }
}
