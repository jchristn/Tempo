namespace Tempo.Core.Settings
{
    /// <summary>
    /// Controls first-boot hydration of the database from configuration.
    /// </summary>
    public class HydrationSettings
    {
        /// <summary>Whether to seed default tenants/admins/credentials when the database is empty. Default: true.</summary>
        public bool SeedDefaults { get; set; } = true;

        /// <summary>Default tenant name when seeding. Default: "Default Tenant".</summary>
        public string DefaultTenantName { get; set; } = "Default Tenant";

        /// <summary>Default administrator email when seeding. Default: "admin@tempo.local".</summary>
        public string DefaultAdminEmail { get; set; } = "admin@tempo.local";

        /// <summary>Default administrator password when seeding. SHA-256 hashed at insert time.</summary>
        public string DefaultAdminPassword { get; set; } = "password";

        /// <summary>Default tenant user email when seeding.</summary>
        public string DefaultUserEmail { get; set; } = "user@tempo.local";

        /// <summary>Default tenant user password when seeding. SHA-256 hashed at insert time.</summary>
        public string DefaultUserPassword { get; set; } = "password";

        /// <summary>Optional path to a hydration JSON file containing flows, steps, and triggers to load.</summary>
        public string? HydrationFile { get; set; } = null;
    }
}
