namespace Tempo
{
    using System;
    using PrettyId;

    /// <summary>
    /// A tenant is a collection of users and dataflows that are isolated from other tenants within Tempo.
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Identifier
        {
            get => _Identifier;
            set => _Identifier = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Identifier)));
        }

        /// <summary>
        /// Name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Name)));
        }

        /// <summary>
        /// Creation timestamp, in UTC.
        /// </summary>
        public DateTime CreatedUtc
        {
            get => _CreatedUtc;
            set => _CreatedUtc = value.ToUniversalTime();
        }

        private string _Identifier = new IdGenerator().Generate("tenant_", 64);
        private string _Name = "My tenant";
        private DateTime _CreatedUtc = DateTime.UtcNow;

        /// <summary>
        /// A tenant is a collection of users and dataflows that are isolated from other tenants within Tempo.
        /// </summary>
        public Tenant()
        {

        }
    }
}
