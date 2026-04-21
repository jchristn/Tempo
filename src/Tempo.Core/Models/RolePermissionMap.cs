namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Many-to-many mapping between <see cref="Role"/> and <see cref="Permission"/>.
    /// </summary>
    public class RolePermissionMap
    {
        /// <summary>Mapping identifier (prefix "rpm_").</summary>
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

        /// <summary>Role identifier.</summary>
        public string RoleId
        {
            get
            {
                return _RoleId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(RoleId));
                _RoleId = value;
            }
        }

        /// <summary>Permission identifier.</summary>
        public string PermissionId
        {
            get
            {
                return _PermissionId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PermissionId));
                _PermissionId = value;
            }
        }

        /// <summary>Whether the mapping is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the mapping is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateRolePermissionMapId();
        private string _TenantId = String.Empty;
        private string _RoleId = String.Empty;
        private string _PermissionId = String.Empty;
    }
}
