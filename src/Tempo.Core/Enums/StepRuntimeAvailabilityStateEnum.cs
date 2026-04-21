namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>Current availability of a runtime provider.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StepRuntimeAvailabilityStateEnum
    {
        Available,
        DisabledBySettings,
        MissingDependency,
        UnsupportedPlatform,
        Preview
    }
}
