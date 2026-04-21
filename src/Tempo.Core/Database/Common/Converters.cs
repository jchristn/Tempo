namespace Tempo.Core.Database.Common
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;

    /// <summary>
    /// Converts <see cref="DataRow"/> values to strongly typed model instances.
    /// </summary>
    public static class Converters
    {
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>Read a nullable string column.</summary>
        public static string? StringOrNull(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column)) return null;
            object v = row[column];
            if (v == null || v == DBNull.Value) return null;
            return v.ToString();
        }

        /// <summary>Read a required string column.</summary>
        public static string String(DataRow row, string column)
        {
            return StringOrNull(row, column) ?? string.Empty;
        }

        /// <summary>Read an int column with a default.</summary>
        public static int Int(DataRow row, string column, int defaultValue = 0)
        {
            if (!row.Table.Columns.Contains(column)) return defaultValue;
            object v = row[column];
            if (v == null || v == DBNull.Value) return defaultValue;
            return Convert.ToInt32(v);
        }

        /// <summary>Read a double column with a default.</summary>
        public static double Double(DataRow row, string column, double defaultValue = 0)
        {
            if (!row.Table.Columns.Contains(column)) return defaultValue;
            object v = row[column];
            if (v == null || v == DBNull.Value) return defaultValue;
            return Convert.ToDouble(v);
        }

        /// <summary>Read a long column with a default.</summary>
        public static long Long(DataRow row, string column, long defaultValue = 0)
        {
            if (!row.Table.Columns.Contains(column)) return defaultValue;
            object v = row[column];
            if (v == null || v == DBNull.Value) return defaultValue;
            return Convert.ToInt64(v);
        }

        /// <summary>Read a nullable long column.</summary>
        public static long? LongOrNull(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column)) return null;
            object v = row[column];
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt64(v);
        }

        /// <summary>Read a boolean column encoded as 0/1.</summary>
        public static bool Bool(DataRow row, string column, bool defaultValue = false)
        {
            if (!row.Table.Columns.Contains(column)) return defaultValue;
            object v = row[column];
            if (v == null || v == DBNull.Value) return defaultValue;
            if (v is bool b) return b;
            return Convert.ToInt32(v) != 0;
        }

        /// <summary>Read a UTC DateTime column.</summary>
        public static DateTime DateTime(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column)) return System.DateTime.UtcNow;
            object v = row[column];
            if (v == null || v == DBNull.Value) return System.DateTime.UtcNow;
            DateTime dt;
            if (v is DateTime dv) dt = dv;
            else dt = System.DateTime.Parse(v.ToString()!);
            return System.DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        /// <summary>Read a nullable UTC DateTime column.</summary>
        public static DateTime? DateTimeOrNull(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column)) return null;
            object v = row[column];
            if (v == null || v == DBNull.Value) return null;
            DateTime dt;
            if (v is DateTime dv) dt = dv;
            else dt = System.DateTime.Parse(v.ToString()!);
            return System.DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        /// <summary>Deserialize a JSON blob column into <typeparamref name="T"/>.</summary>
        public static T? Json<T>(DataRow row, string column) where T : class
        {
            string? s = StringOrNull(row, column);
            if (string.IsNullOrEmpty(s)) return null;
            try { return JsonSerializer.Deserialize<T>(s, _JsonOptions); }
            catch (JsonException) { return null; }
        }

        /// <summary>Deserialize a JSON blob column into a list of strings.</summary>
        public static List<string> JsonStringList(DataRow row, string column)
        {
            string? s = StringOrNull(row, column);
            if (string.IsNullOrEmpty(s)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(s, _JsonOptions) ?? new List<string>(); }
            catch (JsonException) { return new List<string>(); }
        }

        /// <summary>Serialize an object to JSON.</summary>
        public static string JsonSerialize(object? value)
        {
            if (value == null) return "null";
            return JsonSerializer.Serialize(value, _JsonOptions);
        }

        /// <summary>Parse a list of resource types from a JSON string column.</summary>
        public static List<ResourceTypeEnum> ResourceTypes(DataRow row, string column)
        {
            List<string> raw = JsonStringList(row, column);
            List<ResourceTypeEnum> result = new List<ResourceTypeEnum>();
            foreach (string s in raw)
            {
                if (Enum.TryParse<ResourceTypeEnum>(s, true, out ResourceTypeEnum v)) result.Add(v);
            }
            return result;
        }

        /// <summary>Parse a list of operation types from a JSON string column.</summary>
        public static List<OperationTypeEnum> OperationTypes(DataRow row, string column)
        {
            List<string> raw = JsonStringList(row, column);
            List<OperationTypeEnum> result = new List<OperationTypeEnum>();
            foreach (string s in raw)
            {
                if (Enum.TryParse<OperationTypeEnum>(s, true, out OperationTypeEnum v)) result.Add(v);
            }
            return result;
        }

        /// <summary>Read an enum column stored as a string.</summary>
        public static T EnumValue<T>(DataRow row, string column, T defaultValue) where T : struct, Enum
        {
            string? s = StringOrNull(row, column);
            if (string.IsNullOrEmpty(s)) return defaultValue;
            return Enum.TryParse<T>(s, true, out T v) ? v : defaultValue;
        }
    }
}
