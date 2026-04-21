namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Helpers;
    using Tempo.Core.Protocol;
    using Tempo.Enums;
    using Tempo.Metrics;
    using Tempo.Runners;

    /// <summary>Runs data flows through the runtime registry and execution resolver.</summary>
#pragma warning disable CS8600
#pragma warning disable CS8601
#pragma warning disable CS8625
    public class RegistryDataFlowRunner
    {
        private readonly IStepExecutionResolver _Resolver;
        private readonly StepRuntimeRegistry _Registry;

        /// <summary>Metrics store.</summary>
        public MetricsStore? MetricsStore { get; set; }

        /// <summary>Instantiate.</summary>
        public RegistryDataFlowRunner(IStepExecutionResolver resolver, StepRuntimeRegistry registry)
        {
            _Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>Run a data flow.</summary>
        public async Task<StepResult> Run(Tempo.DataFlow flow, StepRequest req, FlowRunExecutionSnapshot? snapshot = null, CancellationToken token = default)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (req == null) throw new ArgumentNullException(nameof(req));
            req.ProtocolVersion = new ProtocolNegotiator().EnsureSupported(req.ProtocolVersion);
            req.TenantId = flow.TenantId;
            req.FlowRunId ??= req.RequestId;
            snapshot ??= new FlowRunExecutionSnapshot { FlowRunId = req.RequestId ?? string.Empty };

            if (!flow.ValidateStartingStep())
                throw new InvalidOperationException("The specified starting step " + flow.StartStepId + " could not be found in flow " + flow.Identifier + ".");

            if (!flow.ValidateStepReferences(out List<string> validationErrors))
                throw new InvalidOperationException("One or more steps defined in flow " + flow.Identifier + " failed validation: " + string.Join(", ", validationErrors));

            DataFlowRunDetails flowRunDetails = new DataFlowRunDetails
            {
                TenantId = flow.TenantId,
                DataFlowId = flow.Identifier,
                RequestId = req.RequestId,
                StartUtc = DateTime.UtcNow,
                Result = StepResultTypeEnum.Success
            };

            DateTime flowStartTime = DateTime.UtcNow;
            string currentStepId = flow.StartStepId;
            StepResult? lastResult = null;
            Dictionary<string, int> transitionCounts = new Dictionary<string, int>();

            while (!string.IsNullOrEmpty(currentStepId))
            {
                token.ThrowIfCancellationRequested();

                if (flow.MaxRuntimeMs > 0 && (DateTime.UtcNow - flowStartTime).TotalMilliseconds > flow.MaxRuntimeMs)
                {
                    lastResult = new StepResult
                    {
                        ProtocolVersion = req.ProtocolVersion,
                        TenantId = req.TenantId,
                        DataFlowId = req.DataFlowId,
                        FlowRunId = req.FlowRunId,
                        StepRunId = req.StepRunId,
                        RequestId = req.RequestId,
                        Result = StepResultTypeEnum.Timeout,
                        Data = req.Data,
                        Metadata = req.Metadata,
                        Exception = new TimeoutException("DataFlow '" + flow.Identifier + "' exceeded maximum runtime of " + flow.MaxRuntimeMs + "ms")
                    };
                    break;
                }

                if (!flow.Steps.TryGetValue(currentStepId, out StepTransition? stepTransition))
                    throw new InvalidOperationException("Step '" + currentStepId + "' not found in flow '" + flow.Identifier + "'.");

                if (!transitionCounts.ContainsKey(currentStepId)) transitionCounts[currentStepId] = 0;
                transitionCounts[currentStepId]++;
                if (stepTransition.MaxTransitions > 0 && transitionCounts[currentStepId] > stepTransition.MaxTransitions)
                {
                    lastResult = new StepResult
                    {
                        ProtocolVersion = req.ProtocolVersion,
                        TenantId = req.TenantId,
                        DataFlowId = req.DataFlowId,
                        FlowRunId = req.FlowRunId,
                        StepRunId = req.StepRunId,
                        RequestId = req.RequestId,
                        Result = StepResultTypeEnum.MaxIterationsExceeded,
                        Data = req.Data,
                        Metadata = req.Metadata
                    };
                    break;
                }

                ResolvedStepExecution resolved = await ResolveAsync(flow.TenantId, currentStepId, stepTransition, snapshot, token).ConfigureAwait(false);
                StepRuntimeConfig config = resolved.Config ?? throw new InvalidOperationException("Step '" + currentStepId + "' has no runtime config.");
                StepConfigValidationResult validation = await _Registry.ValidateAsync(flow.TenantId, resolved.Step.RuntimeKey, config, token).ConfigureAwait(false);
                if (!validation.Valid) throw new InvalidOperationException("Step '" + currentStepId + "' runtime config is invalid: " + string.Join("; ", validation.Errors));
                ValidateContract(resolved.Step, req.Data, input: true);

                IStepRuntimeProvider provider = _Registry.Get(resolved.Step.RuntimeKey)
                    ?? throw new InvalidOperationException("Runtime provider '" + resolved.Step.RuntimeKey + "' is not registered.");

                StepRunner stepRunner = await provider.CreateRunnerAsync(
                    new StepExecutionContext { TenantId = flow.TenantId, ExecutionKey = currentStepId, Snapshot = snapshot },
                    resolved.Step,
                    config,
                    token).ConfigureAwait(false);

                StepRunDetails stepRunDetails = new StepRunDetails
                {
                    RowId = IdGenerator.GenerateStepRunId(),
                    TenantId = flow.TenantId,
                    DataFlowId = flow.Identifier,
                    StepId = currentStepId,
                    RequestId = req.RequestId,
                    ProtocolVersion = req.ProtocolVersion,
                    StartUtc = DateTime.UtcNow
                };
                req.StepRunId = stepRunDetails.RowId;

                int maxRuntimeMs = resolved.Step.MaxRuntimeMs;
                if (maxRuntimeMs == 0 && config is ExternalRestRuntimeConfig restConfig) maxRuntimeMs = restConfig.TimeoutMs;
                if (maxRuntimeMs == 0 && config is LegacyInlineRestRuntimeConfig legacyRestConfig) maxRuntimeMs = legacyRestConfig.TimeoutMs;
                if (config is ArtifactProcessRuntimeConfig || config is ArtifactPythonRuntimeConfig || config is ArtifactDotnetProcessRuntimeConfig) maxRuntimeMs = 0;

                lastResult = await stepRunner.Execute(currentStepId, req, maxRuntimeMs, token).ConfigureAwait(false);
                if (lastResult == null) throw new InvalidOperationException("Step '" + currentStepId + "' returned null result.");
                ValidateContract(resolved.Step, lastResult.Data, input: false);

                bool isException = lastResult.Result == StepResultTypeEnum.Exception || lastResult.Exception != null;
                currentStepId = ProcessStepResult(lastResult, stepTransition, req, stepRunDetails, isException);
                ApplyArtifactDiagnostics(stepRunner, stepRunDetails);
                await WriteStepRunDetailsAsync(stepRunDetails).ConfigureAwait(false);
            }

            flowRunDetails.EndUtc = DateTime.UtcNow;
            if (lastResult != null) flowRunDetails.Result = lastResult.Result;
            if (MetricsStore != null) await MetricsStore.WriteDataFlowRun(flowRunDetails).ConfigureAwait(false);
            return lastResult ?? throw new InvalidOperationException("Data flow '" + flow.Identifier + "' completed without producing a result.");
        }

        private async Task<ResolvedStepExecution> ResolveAsync(string tenantId, string executionKey, StepTransition transition, FlowRunExecutionSnapshot snapshot, CancellationToken token)
        {
            if (transition.StepType.HasValue)
            {
                if (transition.StepType.Value != StepTypeEnum.Rest)
                    throw new InvalidOperationException("Inline step type '" + transition.StepType.Value + "' is not supported.");
                if (transition.Rest == null)
                    throw new InvalidOperationException("Inline REST step '" + executionKey + "' requires Rest configuration.");

                LegacyInlineRestRuntimeConfig config = new LegacyInlineRestRuntimeConfig
                {
                    Method = transition.Rest.Method,
                    Url = transition.Rest.Url,
                    Headers = new Dictionary<string, string>(transition.Rest.Headers),
                    TimeoutMs = transition.Rest.TimeoutMs
                };
                return new ResolvedStepExecution
                {
                    Config = config,
                    Step = new Tempo.Core.Models.StepRecord
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
                    }
                };
            }

            return await _Resolver.ResolveAsync(tenantId, executionKey, snapshot, token).ConfigureAwait(false);
        }

        private static void ValidateContract(Tempo.Core.Models.StepRecord step, object? value, bool input)
        {
            bool enabled = input ? step.ValidateInput : step.ValidateOutput;
            if (!enabled) return;

            string? schema = input ? step.InputSchema : step.OutputSchema;
            IReadOnlyList<string> errors = new SchemaValidationService().Validate(schema, value, input ? "input" : "output");
            if (errors.Count > 0)
                throw new InvalidOperationException("Step '" + step.ExecutionKey + "' " + (input ? "input" : "output") + " contract failed: " + string.Join("; ", errors));
        }

        private static string? ProcessStepResult(StepResult result, StepTransition stepTransition, StepRequest req, StepRunDetails stepRunDetails, bool isException = false)
        {
            string? nextStepId;
            if (isException)
            {
                req.Data = null;
                req.Metadata = result.Exception;
                req.PreviousResult = StepResultTypeEnum.Exception;
                nextStepId = stepTransition.OnException;
            }
            else
            {
                req.Data = result.Data;
                req.Metadata = result.Metadata;
                req.PreviousResult = result.Result;
                nextStepId = result.Result switch
                {
                    StepResultTypeEnum.Success => stepTransition.OnSuccess,
                    StepResultTypeEnum.Error => stepTransition.OnFailure,
                    StepResultTypeEnum.Exception => stepTransition.OnException,
                    StepResultTypeEnum.Timeout => stepTransition.OnException,
                    _ => null
                };
            }

            stepRunDetails.EndUtc = DateTime.UtcNow;
            stepRunDetails.Result = result.Result;
            stepRunDetails.NextStepId = nextStepId;
            stepRunDetails.ProtocolVersion = result.ProtocolVersion;
            stepRunDetails.ExceptionMessage = result.ExceptionMessage;
            return nextStepId;
        }

        private static void ApplyArtifactDiagnostics(StepRunner runner, StepRunDetails details)
        {
            if (runner is not IArtifactRuntimeDiagnostics diagnostics) return;
            details.ArtifactId = diagnostics.ArtifactId;
            details.ArtifactVersionId = diagnostics.ArtifactVersionId;
            details.ArtifactVersion = diagnostics.ArtifactVersion;
            details.ArtifactSha256 = diagnostics.ArtifactSha256;
            details.ManifestEntrypoint = diagnostics.ManifestEntrypoint;
            details.CapacityQueuedUtc = diagnostics.CapacityQueuedUtc;
            details.CapacityAcquiredUtc = diagnostics.CapacityAcquiredUtc;
            details.CapacityWaitMs = diagnostics.CapacityWaitMs;
        }

        private async Task WriteStepRunDetailsAsync(StepRunDetails stepRunDetails)
        {
            if (MetricsStore != null) await MetricsStore.WriteStepRun(stepRunDetails).ConfigureAwait(false);
        }
    }
#pragma warning restore CS8625
#pragma warning restore CS8601
#pragma warning restore CS8600
}
