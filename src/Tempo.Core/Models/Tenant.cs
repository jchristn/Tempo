namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// Tenant within an account. Owns users, flows, steps, triggers, and runs.
    /// </summary>
    public class Tenant
    {
        /// <summary>Tenant identifier (prefix "ten_").</summary>
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

        /// <summary>Account identifier, or null when no account hierarchy is used.</summary>
        public string? AccountId { get; set; } = null;

        /// <summary>Tenant name.</summary>
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

        /// <summary>Optional geographic region tag.</summary>
        public string? Region { get; set; } = null;

        /// <summary>Whether the tenant is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the tenant is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateTenantId();
        private string _Name = "default";
    }
}
