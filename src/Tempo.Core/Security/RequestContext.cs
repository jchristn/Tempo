namespace Tempo.Core.Security
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Helpers;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;

    /// <summary>
    /// Authenticated request context surfaced to route handlers and service methods.
    /// </summary>
    public class RequestContext
    {
        /// <summary>Correlation identifier.</summary>
        public string RequestId { get; set; } = IdGenerator.GenerateRequestHistoryId();

        /// <summary>Whether the request is authenticated.</summary>
        public bool IsAuthenticated { get; set; } = false;

        /// <summary>Authentication result.</summary>
        public AuthenticationResultEnum AuthenticationResult { get; set; } = AuthenticationResultEnum.None;

        /// <summary>Authorization result.</summary>
        public AuthorizationResultEnum AuthorizationResult { get; set; } = AuthorizationResultEnum.None;

        /// <summary>Request classification used for authorization.</summary>
        public RequestTypeEnum RequestType { get; set; } = RequestTypeEnum.Unknown;

        /// <summary>Administrator identifier, if any.</summary>
        public string? AdministratorId { get; set; } = null;

        /// <summary>Account identifier, if any.</summary>
        public string? AccountId { get; set; } = null;

        /// <summary>Tenant identifier, if any.</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>User identifier, if any.</summary>
        public string? UserId { get; set; } = null;

        /// <summary>Credential identifier, if authentication used an access/secret key pair.</summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>Whether the principal is a global administrator (admins table or user IsAdmin flag).</summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>Whether the principal is a tenant-scoped administrator (user IsTenantAdmin flag).</summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>Email of the authenticated principal.</summary>
        public string? Email { get; set; } = null;

        /// <summary>Display name of the authenticated principal, if resolvable.</summary>
        public string? PrincipalName { get; set; } = null;

        /// <summary>Resource type in play for the authorization decision.</summary>
        public ResourceTypeEnum ResourceType { get; set; } = ResourceTypeEnum.All;

        /// <summary>Operation in play for the authorization decision.</summary>
        public OperationTypeEnum OperationType { get; set; } = OperationTypeEnum.Read;

        /// <summary>Resolved administrator model, if any.</summary>
        public Administrator? Administrator { get; set; } = null;

        /// <summary>Resolved user model, if any.</summary>
        public User? User { get; set; } = null;

        /// <summary>Resolved credential model, if any.</summary>
        public Credential? Credential { get; set; } = null;

        /// <summary>Resolved tenant model, if any.</summary>
        public Tenant? Tenant { get; set; } = null;

        /// <summary>Permissions that were evaluated, for diagnostics.</summary>
        public List<string> EvaluatedPermissions { get; set; } = new List<string>();
    }
}
