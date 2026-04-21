namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;
    using Tempo.Core.Enums;

    /// <summary>Describes a runtime provider for APIs and dashboards.</summary>
    public class StepRuntimeDescriptor
    {
        public RuntimeKey RuntimeKey { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public StepPackagingTypeEnum PackagingType { get; set; }
        public List<StepContractTypeEnum> SupportedContractTypes { get; set; } = new List<StepContractTypeEnum>();
        public string ConfigTypeName { get; set; } = string.Empty;
        public List<StepRuntimeConfigPropertyDescriptor> ConfigProperties { get; set; } = new List<StepRuntimeConfigPropertyDescriptor>();
        public bool SupportsArtifacts { get; set; } = false;
        public bool SupportsVersioning { get; set; } = false;
        public StepRuntimeAvailabilityStateEnum Availability { get; set; } = StepRuntimeAvailabilityStateEnum.Available;
        public string? SecurityNotes { get; set; }
    }

    /// <summary>Describes a concrete config DTO property.</summary>
    public class StepRuntimeConfigPropertyDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; } = false;
        public string? Description { get; set; }
    }
}
