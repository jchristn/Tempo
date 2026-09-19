namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>How core validates step inputs and outputs.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StepContractTypeEnum
    {
        /// <summary>No strict validation of step inputs and outputs.</summary>
        Loose,
        /// <summary>Inputs and outputs are validated against a JSON schema.</summary>
        Schema,
        /// <summary>Inputs and outputs are validated against a strongly typed contract.</summary>
        Typed,
        /// <summary>Inputs and outputs are treated as opaque binary payloads.</summary>
        Binary
    }
}
