namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Operation types used for RBAC authorization.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperationTypeEnum
    {
        /// <summary>Wildcard matching any operation.</summary>
        All,

        /// <summary>Create a new resource.</summary>
        Create,

        /// <summary>Read or list resources.</summary>
        Read,

        /// <summary>Update an existing resource.</summary>
        Update,

        /// <summary>Delete an existing resource.</summary>
        Delete,

        /// <summary>Execute or invoke an action on a resource.</summary>
        Execute
    }
}
