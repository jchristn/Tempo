namespace Tempo.Core.Database
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Tracked, idempotent schema migration. Migrations are applied in ascending <see cref="Version"/> order
    /// and recorded in the <c>schema_migrations</c> table.
    /// </summary>
    public class SchemaMigration
    {
        /// <summary>Monotonically increasing version number. Must be unique per provider.</summary>
        public int Version
        {
            get
            {
                return _Version;
            }
            set
            {
                _Version = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(Version));
            }
        }

        /// <summary>Human-readable description stored in the tracking table.</summary>
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Description));
                _Description = value;
            }
        }

        /// <summary>SQL statements applied in order for this migration. Each must be idempotent.</summary>
        public List<string> Statements { get; set; } = new List<string>();

        private int _Version = 1;
        private string _Description = "migration";
    }
}
