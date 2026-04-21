namespace Tempo.Core.Database.Postgresql
{
    /// <summary>PostgreSQL dialect with boolean literals TRUE/FALSE.</summary>
    public class PostgresqlDialect : SqlDialect
    {
        /// <summary>Singleton instance.</summary>
        public static readonly PostgresqlDialect Instance = new PostgresqlDialect();

        /// <inheritdoc/>
        public override string Bit(bool value) { return value ? "TRUE" : "FALSE"; }

        /// <inheritdoc/>
        public override string BoolLiteral(bool value) { return value ? "TRUE" : "FALSE"; }
    }
}
