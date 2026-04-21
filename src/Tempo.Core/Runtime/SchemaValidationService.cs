namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>Minimal JSON schema validator for step input/output contracts.</summary>
    public class SchemaValidationService
    {
        /// <summary>Validate a value against a JSON schema subset.</summary>
        public IReadOnlyList<string> Validate(string? schemaJson, object? value, string label)
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(schemaJson)) return errors;

            JsonDocument schemaDoc;
            try { schemaDoc = JsonDocument.Parse(schemaJson); }
            catch (JsonException ex)
            {
                errors.Add(label + " schema is invalid JSON: " + ex.Message);
                return errors;
            }

            using (schemaDoc)
            {
                JsonElement valueElement = ToJsonElement(value);
                ValidateElement(schemaDoc.RootElement, valueElement, string.IsNullOrWhiteSpace(label) ? "value" : label, errors);
            }

            return errors;
        }

        private static JsonElement ToJsonElement(object? value)
        {
            if (value is JsonElement element) return element.Clone();
            return JsonSerializer.SerializeToElement(value);
        }

        private static void ValidateElement(JsonElement schema, JsonElement value, string path, List<string> errors)
        {
            if (schema.ValueKind != JsonValueKind.Object) return;

            if (schema.TryGetProperty("type", out JsonElement typeElement) && !MatchesType(typeElement, value))
            {
                errors.Add(path + " must be " + TypeDescription(typeElement) + ".");
                return;
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                ValidateRequired(schema, value, path, errors);
                ValidateProperties(schema, value, path, errors);
            }

            if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out JsonElement items))
            {
                int index = 0;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    ValidateElement(items, item, path + "[" + index + "]", errors);
                    index++;
                }
            }
        }

        private static void ValidateRequired(JsonElement schema, JsonElement value, string path, List<string> errors)
        {
            if (!schema.TryGetProperty("required", out JsonElement required) || required.ValueKind != JsonValueKind.Array) return;
            foreach (JsonElement item in required.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                string? name = item.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !value.TryGetProperty(name, out _))
                    errors.Add(path + "." + name + " is required.");
            }
        }

        private static void ValidateProperties(JsonElement schema, JsonElement value, string path, List<string> errors)
        {
            if (!schema.TryGetProperty("properties", out JsonElement properties) || properties.ValueKind != JsonValueKind.Object) return;
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                if (!value.TryGetProperty(property.Name, out JsonElement child)) continue;
                ValidateElement(property.Value, child, path + "." + property.Name, errors);
            }
        }

        private static bool MatchesType(JsonElement typeElement, JsonElement value)
        {
            if (typeElement.ValueKind == JsonValueKind.String) return MatchesTypeName(typeElement.GetString(), value);
            if (typeElement.ValueKind != JsonValueKind.Array) return true;
            foreach (JsonElement item in typeElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && MatchesTypeName(item.GetString(), value)) return true;
            }
            return false;
        }

        private static bool MatchesTypeName(string? type, JsonElement value)
        {
            return type switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "number" => value.ValueKind == JsonValueKind.Number,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "boolean" => value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
                "null" => value.ValueKind == JsonValueKind.Null,
                _ => true
            };
        }

        private static string TypeDescription(JsonElement typeElement)
        {
            if (typeElement.ValueKind == JsonValueKind.String) return typeElement.GetString() ?? "the declared type";
            if (typeElement.ValueKind != JsonValueKind.Array) return "the declared type";
            List<string> names = new List<string>();
            foreach (JsonElement item in typeElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) names.Add(item.GetString()!);
            }
            return names.Count == 0 ? "the declared type" : string.Join(" or ", names);
        }
    }
}
