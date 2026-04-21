namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// RBAC role. Container for permissions.
    /// </summary>
    public class Role
    {
        /// <summary>Role identifier (prefix "rol_").</summary>
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

        /// <summary>Role name.</summary>
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

        /// <summary>Optional description.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Whether the role is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the role is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateRoleId();
        private string _TenantId = String.Empty;
        private string _Name = "role";
    }
}
