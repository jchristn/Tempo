namespace Tempo.Core.Responses
{
    using System;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;

    /// <summary>Public API response for a persisted step.</summary>
    public class StepResponse
    {
        public string Id { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ExecutionKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = null;
        public RuntimeKey RuntimeKey { get; set; }
        public StepRuntimeConfig? RuntimeConfig { get; set; } = null;
        public StepContractTypeEnum ContractType { get; set; } = StepContractTypeEnum.Loose;
        public string? InputSchema { get; set; } = null;
        public string? OutputSchema { get; set; } = null;
        public bool ValidateInput { get; set; } = false;
        public bool ValidateOutput { get; set; } = false;
        public string? ArtifactId { get; set; } = null;
        public string? ArtifactVersion { get; set; } = null;
        public StepRuntimeBindingStateEnum RuntimeBindingState { get; set; } = StepRuntimeBindingStateEnum.Unresolved;
        public string? RuntimeBindingMessage { get; set; } = null;
        public int MaxRuntimeMs { get; set; } = 0;
        public bool Active { get; set; } = true;
        public bool IsProtected { get; set; } = false;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        public static StepResponse FromRecord(StepRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            return new StepResponse
            {
                Id = record.Id,
                TenantId = record.TenantId,
                ExecutionKey = record.ExecutionKey,
                Name = record.Name,
                Description = record.Description,
                RuntimeKey = record.RuntimeKey,
                RuntimeConfig = record.RuntimeConfig,
                ContractType = record.ContractType,
                InputSchema = record.InputSchema,
                OutputSchema = record.OutputSchema,
                ValidateInput = record.ValidateInput,
                ValidateOutput = record.ValidateOutput,
                ArtifactId = record.ArtifactId,
                ArtifactVersion = record.ArtifactVersion,
                RuntimeBindingState = record.RuntimeBindingState,
                RuntimeBindingMessage = record.RuntimeBindingMessage,
                MaxRuntimeMs = record.MaxRuntimeMs,
                Active = record.Active,
                IsProtected = record.IsProtected,
                CreatedUtc = record.CreatedUtc,
                LastUpdateUtc = record.LastUpdateUtc
            };
        }
    }
}
