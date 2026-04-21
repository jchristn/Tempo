namespace Tempo.Core.Database
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Describes the SQL dialect details that vary across providers: string quoting,
    /// boolean literals, datetime formatting, paging syntax, and identifier quoting.
    /// </summary>
    public class SqlDialect
    {
        /// <summary>ANSI default dialect (SQLite/MySQL/PostgreSQL compatible).</summary>
        public static readonly SqlDialect Ansi = new SqlDialect();

        /// <summary>Escape an optional string value for inline use.</summary>
        public virtual string Sanitize(string? value)
        {
            if (value == null) return string.Empty;
            return value.Replace("'", "''");
        }

        /// <summary>Quote a nullable string. Null becomes <c>NULL</c>.</summary>
        public virtual string Quote(string? value)
        {
            if (value == null) return "NULL";
            return "'" + Sanitize(value) + "'";
        }

        /// <summary>Quote a nullable datetime. Null becomes <c>NULL</c>.</summary>
        public virtual string Quote(DateTime? value)
        {
            if (!value.HasValue) return "NULL";
            return "'" + value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";
        }

        /// <summary>Quote a nullable integer value. Null becomes <c>NULL</c>.</summary>
        public virtual string Quote(long? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
        }

        /// <summary>Render a boolean as <c>0</c> or <c>1</c>.</summary>
        public virtual string Bit(bool value) { return value ? "1" : "0"; }

        /// <summary>
        /// Compose a paging clause. By default emits <c>LIMIT {size} OFFSET {offset}</c>;
        /// SQL Server overrides with <c>OFFSET/FETCH</c>.
        /// </summary>
        /// <param name="size">Page size.</param>
        /// <param name="offset">Zero-based offset.</param>
        /// <returns>SQL fragment appended after the ORDER BY.</returns>
        public virtual string Paging(int size, int offset)
        {
            return "LIMIT " + size + " OFFSET " + offset;
        }

        /// <summary>
        /// Literal boolean value used in <c>WHERE ... = {lit}</c> clauses.
        /// Default returns <c>0</c>/<c>1</c>; PostgreSQL overrides to <c>FALSE</c>/<c>TRUE</c>.
        /// </summary>
        public virtual string BoolLiteral(bool value) { return Bit(value); }
    }
}
