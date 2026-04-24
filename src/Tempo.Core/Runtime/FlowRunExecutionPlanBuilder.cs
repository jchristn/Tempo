namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Database;
    using Tempo.Core.Models;

    /// <summary>
    /// Builds serializable execution plans for flow-run assignments.
    /// </summary>
    public class FlowRunExecutionPlanBuilder
    {
        private readonly DatabaseDriverBase _Database;
        private readonly IStepExecutionResolver _Resolver;

        /// <summary>Instantiate.</summary>
        public FlowRunExecutionPlanBuilder(DatabaseDriverBase database, StepManager stepManager)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            if (stepManager == null) throw new ArgumentNullException(nameof(stepManager));
            _Resolver = new CompositeStepExecutionResolver(new DatabaseStepExecutionResolver(_Database), new InMemoryStepExecutionResolver(stepManager));
        }

        /// <summary>Build a new execution plan for the supplied run.</summary>
        public async Task<FlowRunExecutionPlan> BuildAsync(FlowRun run, CancellationToken token = default)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));

            DataFlowRecord? record = await _Database.DataFlows.ReadAsync(run.TenantId, run.DataFlowId, token).ConfigureAwait(false);
            if (record == null) throw new InvalidOperationException("Flow '" + run.DataFlowId + "' was not found for tenant '" + run.TenantId + "'.");

            Tempo.DataFlow flow = Services.FlowDispatchService.Hydrate(record);
            FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(_Database, run, record, token).ConfigureAwait(false);

            FlowRunExecutionPlan plan = new FlowRunExecutionPlan
            {
                FlowRunId = run.Id,
                TenantId = run.TenantId,
                DataFlowId = run.DataFlowId,
                TriggerContext = new FlowRunTriggerContext
                {
                    TriggerId = run.TriggerId,
                    TriggeredByUserId = run.TriggeredByUserId
                },
                Flow = flow,
                InitialInputData = run.InputData,
                PlacementLabel = string.IsNullOrWhiteSpace(record.RoutingHintLabel) ? null : record.RoutingHintLabel.Trim(),
                ExecutionSnapshot = snapshot,
                Budget = new FlowRunExecutionBudget
                {
                    MaxRuntimeMs = flow.MaxRuntimeMs
                }
            };

            Dictionary<string, FlowRunCapabilityRequirement> capabilities = new Dictionary<string, FlowRunCapabilityRequirement>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Tempo.StepTransition> transition in flow.Steps)
            {
                token.ThrowIfCancellationRequested();
                FlowRunResolvedStep resolved = await ResolveStepAsync(run.TenantId, transition.Key, transition.Value, snapshot, token).ConfigureAwait(false);
                plan.Steps[transition.Key] = resolved;

                string capabilityKey = resolved.ExecutionKey + "|" + resolved.TenantScope + "|" + resolved.SourceKind + "|" + resolved.RuntimeKey + "|" + resolved.SignatureHash;
                if (!capabilities.ContainsKey(capabilityKey))
                {
                    capabilities[capabilityKey] = new FlowRunCapabilityRequirement
                    {
                        ExecutionKey = resolved.ExecutionKey,
                        TenantScope = resolved.TenantScope,
                        SourceKind = resolved.SourceKind,
                        SignatureHash = resolved.SignatureHash,
                        RuntimeKey = resolved.RuntimeKey
                    };
                }
            }

            plan.RequiredCapabilities.AddRange(capabilities.Values);
            return plan;
        }

        private async Task<FlowRunResolvedStep> ResolveStepAsync(
            string tenantId,
            string executionKey,
            Tempo.StepTransition transition,
            FlowRunExecutionSnapshot snapshot,
            CancellationToken token)
        {
            ResolvedStepExecution resolved = transition.StepType.HasValue
                ? ResolveInlineStep(tenantId, executionKey, transition)
                : await _Resolver.ResolveAsync(tenantId, executionKey, snapshot, token).ConfigureAwait(false);

            StepRecord step = resolved.Step ?? throw new InvalidOperationException("Resolved step '" + executionKey + "' did not produce a step record.");
            if (step.RuntimeConfig != null && string.IsNullOrWhiteSpace(step.RuntimeConfigJson))
            {
                step.RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(step.RuntimeConfig);
            }

            ArtifactReference? artifact = ResolveArtifactReference(step);
            string tenantScope = string.IsNullOrWhiteSpace(step.TenantId) ? tenantId : step.TenantId;
            string sourceKind = ResolveSourceKind(transition, step, artifact);
            string signatureHash = ComputeSignatureHash(step, transition, tenantScope, sourceKind, artifact);

            return new FlowRunResolvedStep
            {
                ExecutionKey = executionKey,
                TenantScope = tenantScope,
                SourceKind = sourceKind,
                SignatureHash = signatureHash,
                RuntimeKey = step.RuntimeKey,
                Step = step,
                InlineRestConfiguration = transition.Rest,
                ArtifactReference = artifact
            };
        }

        private static ResolvedStepExecution ResolveInlineStep(string tenantId, string executionKey, Tempo.StepTransition transition)
        {
            if (transition.StepType.GetValueOrDefault() != Tempo.Enums.StepTypeEnum.Rest || transition.Rest == null)
            {
                throw new InvalidOperationException("Inline step '" + executionKey + "' is not supported.");
            }

            LegacyInlineRestRuntimeConfig config = new LegacyInlineRestRuntimeConfig
            {
                Method = transition.Rest.Method,
                Url = transition.Rest.Url,
                Headers = new Dictionary<string, string>(transition.Rest.Headers),
                TimeoutMs = transition.Rest.TimeoutMs
            };

            StepRecord step = new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = executionKey,
                Name = string.IsNullOrWhiteSpace(transition.Name) ? executionKey : transition.Name,
                RuntimeKey = StepRuntimeKeys.LegacyInlineRest,
                RuntimeConfig = config,
                RuntimeConfigJson = StepRuntimeSerialization.SerializeConfig(config),
                StepType = Tempo.Core.Enums.PersistedStepTypeEnum.Rest,
                Rest = transition.Rest,
                MaxRuntimeMs = transition.Rest.TimeoutMs
            };

            return new ResolvedStepExecution
            {
                Step = step,
                Config = config
            };
        }

        private static ArtifactReference? ResolveArtifactReference(StepRecord step)
        {
            string? artifactId = step.ArtifactId;
            string? version = step.ArtifactVersion;

            if (step.RuntimeConfig is ArtifactProcessRuntimeConfig process)
            {
                artifactId ??= process.ArtifactId;
                version ??= process.ArtifactVersion;
            }
            else if (step.RuntimeConfig is ArtifactPythonRuntimeConfig python)
            {
                artifactId ??= python.ArtifactId;
                version ??= python.ArtifactVersion;
            }
            else if (step.RuntimeConfig is ArtifactJavaScriptRuntimeConfig javaScript)
            {
                artifactId ??= javaScript.ArtifactId;
                version ??= javaScript.ArtifactVersion;
            }
            else if (step.RuntimeConfig is ArtifactDotnetProcessRuntimeConfig dotnet)
            {
                artifactId ??= dotnet.ArtifactId;
                version ??= dotnet.ArtifactVersion;
            }

            return string.IsNullOrWhiteSpace(artifactId)
                ? null
                : new ArtifactReference
                {
                    ArtifactId = artifactId,
                    Version = string.IsNullOrWhiteSpace(version) ? "latest" : version
                };
        }

        private static string ResolveSourceKind(Tempo.StepTransition transition, StepRecord step, ArtifactReference? artifact)
        {
            if (transition.StepType.HasValue || transition.Rest != null) return "Inline";
            if (artifact != null) return "Artifact";
            if (step.RuntimeKey == StepRuntimeKeys.BuiltinClass || step.RuntimeKey == StepRuntimeKeys.BuiltinMethod) return "Builtin";
            return "Registry";
        }

        private static string ComputeSignatureHash(
            StepRecord step,
            Tempo.StepTransition transition,
            string tenantScope,
            string sourceKind,
            ArtifactReference? artifact)
        {
            string payload = JsonSerializer.Serialize(new
            {
                executionKey = step.ExecutionKey,
                tenantScope,
                sourceKind,
                runtimeKey = step.RuntimeKey.ToString(),
                runtimeConfig = step.RuntimeConfigJson,
                stepType = step.StepType.ToString(),
                inlineRest = transition.Rest,
                artifactId = artifact?.ArtifactId,
                artifactVersion = artifact?.Version,
                validateInput = step.ValidateInput,
                validateOutput = step.ValidateOutput,
                inputSchema = step.InputSchema,
                outputSchema = step.OutputSchema
            }, StepRuntimeSerialization.Options);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
