namespace Tempo.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database.Interfaces;
    using Tempo.Core.Enums;

    /// <summary>
    /// Abstract base class for database drivers. Subclasses set the interface properties from their
    /// constructor and override the execute/initialize methods.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        /// <summary>Database provider type.</summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        /// <summary>Account methods.</summary>
        public IAccountMethods Accounts { get; protected set; } = null!;

        /// <summary>Administrator methods.</summary>
        public IAdministratorMethods Administrators { get; protected set; } = null!;

        /// <summary>Tenant methods.</summary>
        public ITenantMethods Tenants { get; protected set; } = null!;

        /// <summary>User methods.</summary>
        public IUserMethods Users { get; protected set; } = null!;

        /// <summary>Credential methods.</summary>
        public ICredentialMethods Credentials { get; protected set; } = null!;

        /// <summary>Role methods.</summary>
        public IRoleMethods Roles { get; protected set; } = null!;

        /// <summary>User-role mapping methods.</summary>
        public IUserRoleMapMethods UserRoleMaps { get; protected set; } = null!;

        /// <summary>Permission methods.</summary>
        public IPermissionMethods Permissions { get; protected set; } = null!;

        /// <summary>Role-permission mapping methods.</summary>
        public IRolePermissionMapMethods RolePermissionMaps { get; protected set; } = null!;

        /// <summary>Data flow methods.</summary>
        public IDataFlowMethods DataFlows { get; protected set; } = null!;

        /// <summary>Step methods.</summary>
        public IStepMethods Steps { get; protected set; } = null!;

        /// <summary>Artifact metadata methods.</summary>
        public IArtifactMethods Artifacts { get; protected set; } = null!;

        /// <summary>Artifact version metadata methods.</summary>
        public IArtifactVersionMethods ArtifactVersions { get; protected set; } = null!;

        /// <summary>Editable artifact file methods.</summary>
        public IArtifactFileMethods ArtifactFiles { get; protected set; } = null!;

        /// <summary>Trigger methods.</summary>
        public ITriggerMethods Triggers { get; protected set; } = null!;

        /// <summary>Flow run methods.</summary>
        public IFlowRunMethods FlowRuns { get; protected set; } = null!;

        /// <summary>Request history methods.</summary>
        public IRequestHistoryMethods RequestHistory { get; protected set; } = null!;

        /// <summary>Initialize the driver and apply migrations.</summary>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>Execute a single query and return the result as a <see cref="DataTable"/>.</summary>
        public abstract Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default);

        /// <summary>Execute multiple queries.</summary>
        public abstract Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default);

        /// <summary>Close the driver.</summary>
        public abstract Task CloseAsync(CancellationToken token = default);

        /// <summary>Dispose pattern entry point.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Dispose pattern implementation.</summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>Async dispose pattern entry point.</summary>
        public virtual async ValueTask DisposeAsync()
        {
            await CloseAsync().ConfigureAwait(false);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
