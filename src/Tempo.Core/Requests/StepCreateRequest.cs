namespace Tempo.Core.Requests
{
    using System.Collections.Generic;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;

    /// <summary>Request body for creating a persisted step.</summary>
    public class StepCreateRequest
    {
        /// <summary>Optional execution key. When null or empty, the step name is used.</summary>
        public string? ExecutionKey { get; set; } = null;

        /// <summary>Step name. Required. Default: empty string.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional step description. Default: null.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Runtime key. Required.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>Runtime configuration. Required. Default: null.</summary>
        public StepRuntimeConfig? RuntimeConfig { get; set; } = null;

        /// <summary>Contract type. Default: <see cref="StepContractTypeEnum.Loose"/>.</summary>
        public StepContractTypeEnum ContractType { get; set; } = StepContractTypeEnum.Loose;

        /// <summary>Optional input schema. Default: null.</summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>Optional output schema. Default: null.</summary>
        public string? OutputSchema { get; set; } = null;

        /// <summary>Whether to validate step input. Default: false.</summary>
        public bool ValidateInput { get; set; } = false;

        /// <summary>Whether to validate step output. Default: false.</summary>
        public bool ValidateOutput { get; set; } = false;

        /// <summary>Maximum runtime in milliseconds (0 for no timeout). Default: 0.</summary>
        public int MaxRuntimeMs { get; set; } = 0;

        /// <summary>Whether the step is active. Default: true.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Validate the request.</summary>
        /// <returns>A read-only list of validation error messages; empty when the request is valid.</returns>
        public IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Name)) errors.Add("name is required.");
            if (RuntimeKey.IsEmpty) errors.Add("runtimeKey is required.");
            if (RuntimeConfig == null) errors.Add("runtimeConfig is required.");
            else if (!RuntimeKey.IsEmpty && RuntimeConfig.RuntimeKey != RuntimeKey)
                errors.Add("runtimeConfig runtimeKey '" + RuntimeConfig.RuntimeKey + "' does not match runtimeKey '" + RuntimeKey + "'.");
            if (MaxRuntimeMs < 0) errors.Add("maxRuntimeMs must be 0 or greater.");
            return errors;
        }

        /// <summary>Create a persisted step record from this request for the specified tenant.</summary>
        /// <param name="tenantId">The tenant identifier that owns the new step record.</param>
        /// <returns>A new step record populated from this request.</returns>
        public StepRecord ToRecord(string tenantId)
        {
            return new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = string.IsNullOrWhiteSpace(ExecutionKey) ? Name : ExecutionKey!,
                Name = Name,
                Description = Description,
                RuntimeKey = RuntimeKey,
                RuntimeConfig = RuntimeConfig,
                ContractType = ContractType,
                InputSchema = InputSchema,
                OutputSchema = OutputSchema,
                ValidateInput = ValidateInput,
                ValidateOutput = ValidateOutput,
                MaxRuntimeMs = MaxRuntimeMs,
                Active = Active
            };
        }
    }
}
