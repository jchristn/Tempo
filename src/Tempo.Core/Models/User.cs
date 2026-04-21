namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Tenant-scoped user.
    /// </summary>
    public class User
    {
        /// <summary>User identifier (prefix "usr_").</summary>
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

        /// <summary>First name.</summary>
        public string FirstName { get; set; } = String.Empty;

        /// <summary>Last name.</summary>
        public string LastName { get; set; } = String.Empty;

        /// <summary>Email address (unique within tenant).</summary>
        public string Email
        {
            get
            {
                return _Email;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Email));
                _Email = value;
            }
        }

        /// <summary>SHA-256 password hash (lowercase hex).</summary>
        public string PasswordSha256 { get; set; } = String.Empty;

        /// <summary>When true, the user has global system-wide admin access.</summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>When true, the user has full access within their tenant (skips RBAC).</summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>Whether the user is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the user is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateUserId();
        private string _TenantId = String.Empty;
        private string _Email = "user@tempo.local";
    }
}
