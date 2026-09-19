namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>Current availability of a runtime provider.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StepRuntimeAvailabilityStateEnum
    {
        /// <summary>The runtime is available and ready for use.</summary>
        Available,
        /// <summary>The runtime is disabled by configuration settings.</summary>
        DisabledBySettings,
        /// <summary>The runtime is unavailable because a required dependency is missing.</summary>
        MissingDependency,
        /// <summary>The runtime is not supported on the current platform.</summary>
        UnsupportedPlatform,
        /// <summary>The runtime is available as a preview feature.</summary>
        Preview
    }
}
