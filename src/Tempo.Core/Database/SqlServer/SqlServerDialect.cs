namespace Tempo.Core.Database.SqlServer
{
    /// <summary>SQL Server dialect with OFFSET/FETCH paging and BIT booleans.</summary>
    public class SqlServerDialect : SqlDialect
    {
        /// <summary>Singleton instance.</summary>
        public static readonly SqlServerDialect Instance = new SqlServerDialect();

        /// <inheritdoc/>
        public override string Paging(int size, int offset)
        {
            return "OFFSET " + offset + " ROWS FETCH NEXT " + size + " ROWS ONLY";
        }
    }
}
