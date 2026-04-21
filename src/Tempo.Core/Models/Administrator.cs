namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// System administrator with global access across accounts and tenants.
    /// </summary>
    public class Administrator
    {
        /// <summary>Administrator identifier (prefix "adm_").</summary>
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

        /// <summary>Account identifier, or null for account-less administrators.</summary>
        public string? AccountId { get; set; } = null;

        /// <summary>First name.</summary>
        public string FirstName { get; set; } = String.Empty;

        /// <summary>Last name.</summary>
        public string LastName { get; set; } = String.Empty;

        /// <summary>Email address.</summary>
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

        /// <summary>Optional telephone number.</summary>
        public string? Telephone { get; set; } = null;

        /// <summary>Whether the administrator is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the administrator is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateAdminId();
        private string _Email = "admin@tempo.local";
    }
}
