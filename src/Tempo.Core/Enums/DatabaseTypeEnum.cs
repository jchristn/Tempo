namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Supported database provider types.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseTypeEnum
    {
        /// <summary>SQLite file-backed database.</summary>
        Sqlite,

        /// <summary>MySQL server.</summary>
        Mysql,

        /// <summary>PostgreSQL server.</summary>
        Postgresql,

        /// <summary>Microsoft SQL Server.</summary>
        SqlServer
    }
}
