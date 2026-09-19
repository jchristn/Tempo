namespace Tempo.Core.Requests
{
    using System.Collections.Generic;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;

    /// <summary>Request body for updating a persisted step.</summary>
    public class StepUpdateRequest
    {
        /// <summary>Execution key. When null or empty, the existing value is preserved.</summary>
        public string? ExecutionKey { get; set; } = null;

        /// <summary>Step name. When null or empty, the existing value is preserved.</summary>
        public string? Name { get; set; } = null;

        /// <summary>Optional step description. When null, clears the existing description.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Runtime key. When empty, the existing value is preserved.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>Runtime configuration. When null, the existing value is preserved.</summary>
        public StepRuntimeConfig? RuntimeConfig { get; set; } = null;

        /// <summary>Contract type. When null, the existing value is preserved.</summary>
        public StepContractTypeEnum? ContractType { get; set; } = null;

        /// <summary>Optional input schema. When null, clears the existing schema.</summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>Optional output schema. When null, clears the existing schema.</summary>
        public string? OutputSchema { get; set; } = null;

        /// <summary>Whether to validate step input. When null, the existing value is preserved.</summary>
        public bool? ValidateInput { get; set; } = null;

        /// <summary>Whether to validate step output. When null, the existing value is preserved.</summary>
        public bool? ValidateOutput { get; set; } = null;

        /// <summary>Maximum runtime in milliseconds (0 for no timeout). When null, the existing value is preserved.</summary>
        public int? MaxRuntimeMs { get; set; } = null;

        /// <summary>Whether the step is active. When null, the existing value is preserved.</summary>
        public bool? Active { get; set; } = null;

        /// <summary>Whether the step is protected from deletion. When null, the existing value is preserved.</summary>
        public bool? IsProtected { get; set; } = null;

        /// <summary>Validate the requested update against an existing step record.</summary>
        /// <param name="existing">The existing step record being updated.</param>
        /// <returns>A read-only list of validation error messages; empty when the request is valid.</returns>
        public IReadOnlyList<string> Validate(StepRecord existing)
        {
            List<string> errors = new List<string>();
            RuntimeKey runtimeKey = RuntimeKey.IsEmpty ? existing.RuntimeKey : RuntimeKey;
            StepRuntimeConfig? config = RuntimeConfig ?? existing.RuntimeConfig;
            if (string.IsNullOrWhiteSpace(Name ?? existing.Name)) errors.Add("name is required.");
            if (runtimeKey.IsEmpty) errors.Add("runtimeKey is required.");
            if (config == null) errors.Add("runtimeConfig is required.");
            else if (!runtimeKey.IsEmpty && config.RuntimeKey != runtimeKey)
                errors.Add("runtimeConfig runtimeKey '" + config.RuntimeKey + "' does not match runtimeKey '" + runtimeKey + "'.");
            if (MaxRuntimeMs.HasValue && MaxRuntimeMs.Value < 0) errors.Add("maxRuntimeMs must be 0 or greater.");
            return errors;
        }

        /// <summary>Apply the requested changes onto an existing step record, producing a new record.</summary>
        /// <param name="existing">The existing step record to apply changes to.</param>
        /// <returns>A new step record reflecting the applied changes.</returns>
        public StepRecord ApplyTo(StepRecord existing)
        {
            RuntimeKey runtimeKey = RuntimeKey.IsEmpty ? existing.RuntimeKey : RuntimeKey;
            StepRuntimeConfig? config = RuntimeConfig ?? existing.RuntimeConfig;
            return new StepRecord
            {
                Id = existing.Id,
                TenantId = existing.TenantId,
                ExecutionKey = string.IsNullOrWhiteSpace(ExecutionKey) ? existing.ExecutionKey : ExecutionKey!,
                Name = string.IsNullOrWhiteSpace(Name) ? existing.Name : Name!,
                Description = Description,
                RuntimeKey = runtimeKey,
                RuntimeConfig = config,
                ContractType = ContractType ?? existing.ContractType,
                InputSchema = InputSchema,
                OutputSchema = OutputSchema,
                ValidateInput = ValidateInput ?? existing.ValidateInput,
                ValidateOutput = ValidateOutput ?? existing.ValidateOutput,
                MaxRuntimeMs = MaxRuntimeMs ?? existing.MaxRuntimeMs,
                Active = Active ?? existing.Active,
                IsProtected = IsProtected ?? existing.IsProtected,
                RuntimeBindingState = existing.RuntimeBindingState,
                RuntimeBindingMessage = existing.RuntimeBindingMessage,
                CreatedUtc = existing.CreatedUtc
            };
        }
    }
}
