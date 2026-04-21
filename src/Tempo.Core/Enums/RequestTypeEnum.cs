namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// High-level request classification used to select the resource/operation pair for RBAC evaluation.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestTypeEnum
    {
        /// <summary>Unknown or unclassified request.</summary>
        Unknown,

        /// <summary>Anonymous or health-check request that skips authorization.</summary>
        Anonymous,

        /// <summary>Read an account.</summary>
        AccountRead,
        /// <summary>Create or update an account.</summary>
        AccountWrite,
        /// <summary>Delete an account.</summary>
        AccountDelete,

        /// <summary>Read an administrator.</summary>
        AdministratorRead,
        /// <summary>Create or update an administrator.</summary>
        AdministratorWrite,
        /// <summary>Delete an administrator.</summary>
        AdministratorDelete,

        /// <summary>Read a tenant.</summary>
        TenantRead,
        /// <summary>Create or update a tenant.</summary>
        TenantWrite,
        /// <summary>Delete a tenant.</summary>
        TenantDelete,

        /// <summary>Read users.</summary>
        UserRead,
        /// <summary>Create or update users.</summary>
        UserWrite,
        /// <summary>Delete users.</summary>
        UserDelete,

        /// <summary>Read credentials.</summary>
        CredentialRead,
        /// <summary>Create or update credentials.</summary>
        CredentialWrite,
        /// <summary>Delete credentials.</summary>
        CredentialDelete,

        /// <summary>Read roles.</summary>
        RoleRead,
        /// <summary>Create or update roles.</summary>
        RoleWrite,
        /// <summary>Delete roles.</summary>
        RoleDelete,

        /// <summary>Read permissions.</summary>
        PermissionRead,
        /// <summary>Create or update permissions.</summary>
        PermissionWrite,
        /// <summary>Delete permissions.</summary>
        PermissionDelete,

        /// <summary>Read data flows.</summary>
        DataFlowRead,
        /// <summary>Create or update data flows.</summary>
        DataFlowWrite,
        /// <summary>Delete data flows.</summary>
        DataFlowDelete,
        /// <summary>Execute a data flow.</summary>
        DataFlowExecute,

        /// <summary>Read steps.</summary>
        StepRead,
        /// <summary>Create or update steps.</summary>
        StepWrite,
        /// <summary>Delete steps.</summary>
        StepDelete,

        /// <summary>Read triggers.</summary>
        TriggerRead,
        /// <summary>Create or update triggers.</summary>
        TriggerWrite,
        /// <summary>Delete triggers.</summary>
        TriggerDelete,
        /// <summary>Fire a trigger.</summary>
        TriggerExecute,

        /// <summary>Read flow runs.</summary>
        FlowRunRead,
        /// <summary>Cancel a flow run.</summary>
        FlowRunCancel,

        /// <summary>Read request history.</summary>
        RequestHistoryRead,
        /// <summary>Delete request history.</summary>
        RequestHistoryDelete
    }
}
