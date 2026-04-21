namespace Tempo.Core.Runtime
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    /// <summary>Serialization helpers for runtime config DTOs.</summary>
    public static class StepRuntimeSerialization
    {
        public static readonly StepRuntimeRegistry DefaultRegistry = StepRuntimeRegistry.CreateDefault();
        public static readonly JsonSerializerOptions Options = CreateOptions(DefaultRegistry);

        public static JsonSerializerOptions CreateOptions(StepRuntimeRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
                TypeInfoResolver = JsonTypeInfoResolver.Combine(new RuntimeRegistryJsonTypeInfoResolver(registry), new DefaultJsonTypeInfoResolver())
            };
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new RuntimeKeyJsonConverter());
            return options;
        }

        public static string SerializeConfig(StepRuntimeConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return JsonSerializer.Serialize(config, config.GetType(), Options);
        }

        public static StepRuntimeConfig? DeserializeConfig(RuntimeKey runtimeKey, string? json)
        {
            if (runtimeKey.IsEmpty) throw new ArgumentNullException(nameof(runtimeKey));
            if (string.IsNullOrWhiteSpace(json)) return null;
            Type? configType = DefaultRegistry.GetConfigType(runtimeKey);
            if (configType == null) throw new InvalidOperationException("Unknown runtime key: " + runtimeKey);
            return (StepRuntimeConfig?)JsonSerializer.Deserialize(json, configType, Options);
        }
    }
}
