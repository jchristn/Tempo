namespace Tempo.Core.Runtime
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    /// <summary>Applies registry-driven polymorphism for <see cref="StepRuntimeConfig"/>.</summary>
    public sealed class RuntimeRegistryJsonTypeInfoResolver : IJsonTypeInfoResolver
    {
        private readonly StepRuntimeRegistry _Registry;
        private readonly DefaultJsonTypeInfoResolver _DefaultResolver = new DefaultJsonTypeInfoResolver();

        public RuntimeRegistryJsonTypeInfoResolver(StepRuntimeRegistry registry)
        {
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type != typeof(StepRuntimeConfig)) return null;

            JsonTypeInfo info = _DefaultResolver.GetTypeInfo(type, options);
            JsonPolymorphismOptions poly = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "runtimeKey",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
            };

            foreach (IStepRuntimeProvider provider in _Registry.Providers)
            {
                poly.DerivedTypes.Add(new JsonDerivedType(provider.ConfigType, provider.RuntimeKey.ToString()));
            }

            info.PolymorphismOptions = poly;
            return info;
        }
    }
}
