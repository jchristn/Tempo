namespace Tempo.Server.Serialization
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using Tempo.Core.Runtime;

    /// <summary>
    /// JSON serialization defaults used by the server's routes.
    /// </summary>
    public static class Serializer
    {
        /// <summary>Default serialization options.</summary>
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            TypeInfoResolver = JsonTypeInfoResolver.Combine(new RuntimeRegistryJsonTypeInfoResolver(StepRuntimeSerialization.DefaultRegistry), new DefaultJsonTypeInfoResolver()),
            Converters = { new JsonStringEnumConverter(), new RuntimeKeyJsonConverter() }
        };

        /// <summary>Serialize an object to JSON.</summary>
        public static string Serialize(object? value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        /// <summary>Deserialize JSON into <typeparamref name="T"/>.</summary>
        public static T? Deserialize<T>(string? json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }
}
