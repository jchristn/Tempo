namespace Tempo.Core.Requests
{
    using System.Collections.Generic;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;

    /// <summary>Request body for updating a persisted step.</summary>
    public class StepUpdateRequest
    {
        public string? ExecutionKey { get; set; } = null;
        public string? Name { get; set; } = null;
        public string? Description { get; set; } = null;
        public RuntimeKey RuntimeKey { get; set; }
        public StepRuntimeConfig? RuntimeConfig { get; set; } = null;
        public StepContractTypeEnum? ContractType { get; set; } = null;
        public string? InputSchema { get; set; } = null;
        public string? OutputSchema { get; set; } = null;
        public bool? ValidateInput { get; set; } = null;
        public bool? ValidateOutput { get; set; } = null;
        public int? MaxRuntimeMs { get; set; } = null;
        public bool? Active { get; set; } = null;
        public bool? IsProtected { get; set; } = null;

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
