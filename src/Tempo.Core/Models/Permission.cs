namespace Tempo.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Enums;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Granular permission definition applied to a role via <see cref="RolePermissionMap"/>.
    /// </summary>
    public class Permission
    {
        /// <summary>Permission identifier (prefix "prm_").</summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>Tenant identifier.</summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>Display name.</summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>Resource types this permission applies to.</summary>
        public List<ResourceTypeEnum> ResourceTypes { get; set; } = new List<ResourceTypeEnum> { ResourceTypeEnum.All };

        /// <summary>Operations this permission applies to.</summary>
        public List<OperationTypeEnum> OperationTypes { get; set; } = new List<OperationTypeEnum> { OperationTypeEnum.All };

        /// <summary>Whether this permission permits or denies access.</summary>
        public PermissionTypeEnum PermissionType { get; set; } = PermissionTypeEnum.Permit;

        /// <summary>Whether this permission is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether this permission is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GeneratePermissionId();
        private string _TenantId = String.Empty;
        private string _Name = "permission";
    }
}
