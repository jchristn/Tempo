namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Many-to-many mapping between <see cref="User"/> and <see cref="Role"/>.
    /// </summary>
    public class UserRoleMap
    {
        /// <summary>Mapping identifier (prefix "urm_").</summary>
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

        /// <summary>User identifier.</summary>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
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

        /// <summary>Whether the mapping is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the mapping is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateUserRoleMapId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _RoleId = String.Empty;
    }
}
