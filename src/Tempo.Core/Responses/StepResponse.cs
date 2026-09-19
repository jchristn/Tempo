namespace Tempo.Core.Responses
{
    using System;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;

    /// <summary>Public API response for a persisted step.</summary>
    public class StepResponse
    {
        /// <summary>Unique identifier of the step.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Identifier of the tenant that owns the step.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Key used to reference the step during execution.</summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>Human-readable name of the step.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description of the step. Default: null.</summary>
        public string? Description { get; set; } = null;

        /// <summary>Key identifying the runtime that executes the step.</summary>
        public RuntimeKey RuntimeKey { get; set; }

        /// <summary>Optional runtime configuration for the step. Default: null.</summary>
        public StepRuntimeConfig? RuntimeConfig { get; set; } = null;

        /// <summary>Contract type governing input/output validation. Default: Loose.</summary>
        public StepContractTypeEnum ContractType { get; set; } = StepContractTypeEnum.Loose;

        /// <summary>Optional JSON schema describing the step's input. Default: null.</summary>
        public string? InputSchema { get; set; } = null;

        /// <summary>Optional JSON schema describing the step's output. Default: null.</summary>
        public string? OutputSchema { get; set; } = null;

        /// <summary>Whether input is validated against the input schema. Default: false.</summary>
        public bool ValidateInput { get; set; } = false;

        /// <summary>Whether output is validated against the output schema. Default: false.</summary>
        public bool ValidateOutput { get; set; } = false;

        /// <summary>Optional identifier of the artifact bound to the step. Default: null.</summary>
        public string? ArtifactId { get; set; } = null;

        /// <summary>Optional version of the artifact bound to the step. Default: null.</summary>
        public string? ArtifactVersion { get; set; } = null;

        /// <summary>State of the step's runtime binding. Default: Unresolved.</summary>
        public StepRuntimeBindingStateEnum RuntimeBindingState { get; set; } = StepRuntimeBindingStateEnum.Unresolved;

        /// <summary>Optional message describing the runtime binding state. Default: null.</summary>
        public string? RuntimeBindingMessage { get; set; } = null;

        /// <summary>Maximum runtime in milliseconds (0 for no timeout). Default: 0.</summary>
        public int MaxRuntimeMs { get; set; } = 0;

        /// <summary>Whether the step is active. Default: true.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the step is protected from modification or deletion. Default: false.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Timestamp, in UTC, when the step was created. Default: current UTC time.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Timestamp, in UTC, when the step was last updated. Default: current UTC time.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Creates a response from a persisted step record.</summary>
        /// <param name="record">The step record to convert. Cannot be null.</param>
        /// <returns>A populated step response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when record is null.</exception>
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
