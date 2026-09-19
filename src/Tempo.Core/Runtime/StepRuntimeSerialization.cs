namespace Tempo.Core.Runtime
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    /// <summary>Serialization helpers for runtime config DTOs.</summary>
    public static class StepRuntimeSerialization
    {
        /// <summary>The default runtime registry used when none is supplied.</summary>
        public static readonly StepRuntimeRegistry DefaultRegistry = StepRuntimeRegistry.CreateDefault();

        /// <summary>The default JSON serializer options built from <see cref="DefaultRegistry"/>.</summary>
        public static readonly JsonSerializerOptions Options = CreateOptions(DefaultRegistry);

        /// <summary>
        /// Creates JSON serializer options configured for the supplied runtime registry.
        /// </summary>
        /// <param name="registry">The runtime registry that supplies polymorphic type information. Cannot be null.</param>
        /// <returns>Serializer options wired to the registry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
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

        /// <summary>
        /// Serializes a runtime configuration to JSON using its concrete type.
        /// </summary>
        /// <param name="config">The runtime configuration to serialize. Cannot be null.</param>
        /// <returns>The JSON representation of the configuration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        public static string SerializeConfig(StepRuntimeConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return JsonSerializer.Serialize(config, config.GetType(), Options);
        }

        /// <summary>
        /// Deserializes a runtime configuration from JSON for the specified runtime key.
        /// </summary>
        /// <param name="runtimeKey">The runtime key identifying the concrete configuration type. Cannot be empty.</param>
        /// <param name="json">The JSON to deserialize. May be null or whitespace, in which case null is returned.</param>
        /// <returns>The deserialized runtime configuration, or null when <paramref name="json"/> is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="runtimeKey"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the runtime key is not registered.</exception>
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
