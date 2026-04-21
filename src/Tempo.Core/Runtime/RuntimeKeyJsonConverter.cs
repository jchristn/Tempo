namespace Tempo.Core.Runtime
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>Serializes <see cref="RuntimeKey"/> values as strings.</summary>
    public sealed class RuntimeKeyJsonConverter : JsonConverter<RuntimeKey>
    {
        /// <inheritdoc/>
        public override RuntimeKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return default;
            string? value = reader.GetString();
            return string.IsNullOrWhiteSpace(value) ? default : new RuntimeKey(value);
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, RuntimeKey value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
