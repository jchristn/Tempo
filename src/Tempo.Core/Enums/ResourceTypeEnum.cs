namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Resource types used for RBAC authorization.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ResourceTypeEnum
    {
        /// <summary>Wildcard matching any resource.</summary>
        All,

        /// <summary>Accounts.</summary>
        Account,

        /// <summary>Administrators.</summary>
        Administrator,

        /// <summary>Tenants.</summary>
        Tenant,

        /// <summary>Users.</summary>
        User,

        /// <summary>Credentials.</summary>
        Credential,

        /// <summary>Roles.</summary>
        Role,

        /// <summary>Role or user role mappings.</summary>
        RoleMap,

        /// <summary>Permissions.</summary>
        Permission,

        /// <summary>Data flow definitions.</summary>
        DataFlow,

        /// <summary>Step definitions.</summary>
        Step,

        /// <summary>Triggers.</summary>
        Trigger,

        /// <summary>Flow runs and step runs.</summary>
        FlowRun,

        /// <summary>Captured HTTP request history.</summary>
        RequestHistory,

        /// <summary>Tenant-owned runtime artifacts.</summary>
        Artifact
    }
}
