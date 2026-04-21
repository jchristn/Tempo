namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Persisted step kind. Describes how the server hydrates a step at run time.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PersistedStepTypeEnum
    {
        /// <summary>Step is a code-based step registered in process (class or attribute).</summary>
        Code,

        /// <summary>Step is a REST step configured via <see cref="Tempo.RestStepConfiguration"/>.</summary>
        Rest
    }
}
