namespace Tempo.Core.Runtime
{
    using System.Collections.Generic;
    using Tempo.Core.Enums;

    /// <summary>Describes a runtime provider for APIs and dashboards.</summary>
    public class StepRuntimeDescriptor
    {
        /// <summary>Runtime key identifying this provider.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>Human-readable display name. Default: empty string.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Description of the runtime provider. Default: empty string.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Packaging type used by this runtime.</summary>
        public StepPackagingTypeEnum PackagingType { get; set; }

        /// <summary>Contract types supported by this runtime. Default: empty list.</summary>
        public List<StepContractTypeEnum> SupportedContractTypes { get; set; } = new List<StepContractTypeEnum>();

        /// <summary>Name of the concrete configuration DTO type. Default: empty string.</summary>
        public string ConfigTypeName { get; set; } = string.Empty;

        /// <summary>Descriptors for the configuration properties. Default: empty list.</summary>
        public List<StepRuntimeConfigPropertyDescriptor> ConfigProperties { get; set; } = new List<StepRuntimeConfigPropertyDescriptor>();

        /// <summary>Whether the runtime supports artifacts. Default: false.</summary>
        public bool SupportsArtifacts { get; set; } = false;

        /// <summary>Whether the runtime supports versioning. Default: false.</summary>
        public bool SupportsVersioning { get; set; } = false;

        /// <summary>Availability state of the runtime. Default: <see cref="StepRuntimeAvailabilityStateEnum.Available"/>.</summary>
        public StepRuntimeAvailabilityStateEnum Availability { get; set; } = StepRuntimeAvailabilityStateEnum.Available;

        /// <summary>Optional security notes for the runtime. Default: null.</summary>
        public string? SecurityNotes { get; set; }
    }

    /// <summary>Describes a concrete config DTO property.</summary>
    public class StepRuntimeConfigPropertyDescriptor
    {
        /// <summary>Property name. Default: empty string.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Property type name. Default: empty string.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Whether the property is required. Default: false.</summary>
        public bool Required { get; set; } = false;

        /// <summary>Optional property description. Default: null.</summary>
        public string? Description { get; set; }
    }
}
