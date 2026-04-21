namespace Tempo.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>How core validates step inputs and outputs.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StepContractTypeEnum
    {
        Loose,
        Schema,
        Typed,
        Binary
    }
}
