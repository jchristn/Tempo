namespace Tempo.McpServer.Tools
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Helper methods for reading MCP tool JSON arguments.
    /// </summary>
    public static class JsonArgumentReader
    {
        /// <summary>Read a required string property.</summary>
        /// <param name="args">Arguments.</param>
        /// <param name="propertyName">Property name.</param>
        /// <returns>Property value.</returns>
        public static string RequiredString(JsonElement? args, string propertyName)
        {
            string? value = OptionalString(args, propertyName);
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(propertyName + " is required");
            return value;
        }

        /// <summary>Read an optional string property.</summary>
        /// <param name="args">Arguments.</param>
        /// <param name="propertyName">Property name.</param>
        /// <returns>Property value.</returns>
        public static string? OptionalString(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement property)) return null;
            if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined) return null;
            if (property.ValueKind == JsonValueKind.String) return property.GetString();
            return property.GetRawText();
        }

        /// <summary>Read an optional integer property.</summary>
        /// <param name="args">Arguments.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <returns>Property value.</returns>
        public static int OptionalInt(JsonElement? args, string propertyName, int defaultValue)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement property)) return defaultValue;
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int number)) return number;
            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out int parsed)) return parsed;
            return defaultValue;
        }

        /// <summary>Read an optional boolean property.</summary>
        /// <param name="args">Arguments.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <returns>Property value.</returns>
        public static bool OptionalBool(JsonElement? args, string propertyName, bool defaultValue)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement property)) return defaultValue;
            if (property.ValueKind == JsonValueKind.True) return true;
            if (property.ValueKind == JsonValueKind.False) return false;
            if (property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out bool parsed)) return parsed;
            return defaultValue;
        }

        /// <summary>Read an optional JSON node property.</summary>
        /// <param name="args">Arguments.</param>
        /// <param name="propertyName">Property name.</param>
        /// <returns>Property value.</returns>
        public static JsonNode? OptionalNode(JsonElement? args, string propertyName)
        {
            if (!TryGetProperty(args, propertyName, out JsonElement property)) return null;
            if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined) return null;
            if (property.ValueKind == JsonValueKind.String)
            {
                string? value = property.GetString();
                if (string.IsNullOrWhiteSpace(value)) return null;
                return JsonNode.Parse(value);
            }

            return JsonNode.Parse(property.GetRawText());
        }

        /// <summary>Return true if the argument object contains a property.</summary>
        /// <param name="args">Arguments.</param>
        /// <param name="propertyName">Property name.</param>
        /// <returns>True if found.</returns>
        public static bool HasProperty(JsonElement? args, string propertyName)
        {
            return TryGetProperty(args, propertyName, out JsonElement ignored);
        }

        private static bool TryGetProperty(JsonElement? args, string propertyName, out JsonElement property)
        {
            property = default;
            if (!args.HasValue) return false;
            if (args.Value.ValueKind != JsonValueKind.Object) return false;
            return args.Value.TryGetProperty(propertyName, out property);
        }
    }
}
