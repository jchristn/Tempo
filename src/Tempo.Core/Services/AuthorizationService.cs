namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Security;

    /// <summary>
    /// Evaluates RBAC permissions for a <see cref="RequestContext"/>.
    /// Bypass order: admin row / IsAdmin → IsTenantAdmin → Deny-then-Permit permission eval.
    /// </summary>
    public class AuthorizationService
    {
        private readonly DatabaseDriverBase _Database;

        /// <summary>Instantiate.</summary>
        public AuthorizationService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Decide whether the current principal is allowed to perform the operation.
        /// </summary>
        /// <param name="context">Authenticated request context.</param>
        /// <param name="resource">Resource under evaluation.</param>
        /// <param name="operation">Operation under evaluation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when authorized.</returns>
        public async Task<bool> AuthorizeAsync(RequestContext context, ResourceTypeEnum resource, OperationTypeEnum operation, CancellationToken token = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.ResourceType = resource;
            context.OperationType = operation;

            if (!context.IsAuthenticated)
            {
                context.AuthorizationResult = AuthorizationResultEnum.DeniedImplicit;
                return false;
            }

            if (context.IsAdmin)
            {
                context.AuthorizationResult = AuthorizationResultEnum.Permitted;
                return true;
            }

            if (context.IsTenantAdmin)
            {
                context.AuthorizationResult = AuthorizationResultEnum.Permitted;
                return true;
            }

            if (string.IsNullOrEmpty(context.TenantId) || string.IsNullOrEmpty(context.UserId))
            {
                context.AuthorizationResult = AuthorizationResultEnum.DeniedImplicit;
                return false;
            }

            List<Permission> matching = await _Database.Permissions.ResolveForUserAsync(
                context.TenantId, context.UserId, resource, operation, token).ConfigureAwait(false);

            bool hasPermit = false;
            foreach (Permission p in matching)
            {
                context.EvaluatedPermissions.Add(p.Id);
                if (p.PermissionType == PermissionTypeEnum.Deny)
                {
                    context.AuthorizationResult = AuthorizationResultEnum.DeniedExplicit;
                    return false;
                }
                if (p.PermissionType == PermissionTypeEnum.Permit) hasPermit = true;
            }

            if (hasPermit)
            {
                context.AuthorizationResult = AuthorizationResultEnum.Permitted;
                return true;
            }

            context.AuthorizationResult = AuthorizationResultEnum.DeniedImplicit;
            return false;
        }

        /// <summary>
        /// Whether the current context may act on behalf of the given tenant. Tenant-scoped operations
        /// use this to prevent cross-tenant access.
        /// </summary>
        public bool CanActOnTenant(RequestContext context, string tenantId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrEmpty(tenantId)) return false;
            if (context.IsAdmin) return true;
            return string.Equals(context.TenantId, tenantId, StringComparison.Ordinal);
        }
    }
}
