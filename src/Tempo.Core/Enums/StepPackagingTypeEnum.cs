namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>How a step runtime is packaged or hosted.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StepPackagingTypeEnum
    {
        Builtin,
        External,
        Artifact,
        Container,
        Host
    }
}
