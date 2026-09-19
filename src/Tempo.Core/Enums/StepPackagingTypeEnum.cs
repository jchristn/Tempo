namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>How a step runtime is packaged or hosted.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StepPackagingTypeEnum
    {
        /// <summary>A step runtime built into the host process.</summary>
        Builtin,
        /// <summary>A step runtime provided by an external process.</summary>
        External,
        /// <summary>A step runtime packaged as an uploaded artifact.</summary>
        Artifact,
        /// <summary>A step runtime hosted inside a container.</summary>
        Container,
        /// <summary>A step runtime backed by a host executable.</summary>
        Host
    }
}
