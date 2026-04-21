namespace Tempo.Core.Requests
{
    using System.Collections.Generic;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;

    /// <summary>Request body for creating a persisted step.</summary>
    public class StepCreateRequest
    {
        public string? ExecutionKey { get; set; } = null;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = null;
        public RuntimeKey RuntimeKey { get; set; }
        public StepRuntimeConfig? RuntimeConfig { get; set; } = null;
        public StepContractTypeEnum ContractType { get; set; } = StepContractTypeEnum.Loose;
        public string? InputSchema { get; set; } = null;
        public string? OutputSchema { get; set; } = null;
        public bool ValidateInput { get; set; } = false;
        public bool ValidateOutput { get; set; } = false;
        public int MaxRuntimeMs { get; set; } = 0;
        public bool Active { get; set; } = true;

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
