namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Runtime;

    /// <summary>Finds cross-resource references that should block destructive deletes.</summary>
    public class DeletionDependencyService
    {
        private readonly DatabaseDriverBase _Database;

        /// <summary>Instantiate.</summary>
        public DeletionDependencyService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>Find flows that reference a step execution key.</summary>
        public async Task<DeletionDependencyResult> FindStepReferencesAsync(string tenantId, string executionKey, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(executionKey)) throw new ArgumentNullException(nameof(executionKey));
            DeletionDependencyResult result = new DeletionDependencyResult();
            List<DataFlowRecord> flows = await _Database.DataFlows.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (DataFlowRecord flow in flows)
            {
                if (FlowReferencesStep(flow, executionKey)) result.References.Add(FlowReference(flow));
            }
            return result;
        }

        /// <summary>Find triggers that target a data flow.</summary>
        public async Task<DeletionDependencyResult> FindDataFlowReferencesAsync(string tenantId, string flowId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(flowId)) throw new ArgumentNullException(nameof(flowId));
            DeletionDependencyResult result = new DeletionDependencyResult();
            List<TriggerRecord> triggers = await _Database.Triggers.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (TriggerRecord trigger in triggers)
            {
                if (string.Equals(trigger.DataFlowId, flowId, StringComparison.Ordinal))
                    result.References.Add("trigger '" + trigger.Name + "' (" + trigger.Id + ")");
            }
            return result;
        }

        /// <summary>Find flows that reference a trigger identifier.</summary>
        public async Task<DeletionDependencyResult> FindTriggerReferencesAsync(string tenantId, string triggerId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(triggerId)) throw new ArgumentNullException(nameof(triggerId));
            DeletionDependencyResult result = new DeletionDependencyResult();
            List<DataFlowRecord> flows = await _Database.DataFlows.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (DataFlowRecord flow in flows)
            {
                if (string.Equals(flow.TriggerId, triggerId, StringComparison.Ordinal)) result.References.Add(FlowReference(flow));
            }
            return result;
        }

        /// <summary>Find steps that reference an artifact.</summary>
        public async Task<DeletionDependencyResult> FindArtifactReferencesAsync(string tenantId, string artifactId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            DeletionDependencyResult result = new DeletionDependencyResult();
            List<StepRecord> steps = await _Database.Steps.AllAsync(tenantId, token).ConfigureAwait(false);
            foreach (StepRecord step in steps)
            {
                if (string.Equals(StepArtifactId(step), artifactId, StringComparison.Ordinal))
                    result.References.Add(StepReference(step));
            }
            return result;
        }

        /// <summary>Find steps or retained run snapshots that reference an artifact version.</summary>
        public async Task<DeletionDependencyResult> FindArtifactVersionReferencesAsync(string tenantId, ArtifactVersionRecord version, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (version == null) throw new ArgumentNullException(nameof(version));
            DeletionDependencyResult result = new DeletionDependencyResult();
            List<StepRecord> steps = await _Database.Steps.AllAsync(tenantId, token).ConfigureAwait(false);
            ArtifactVersionRecord? latest = await LatestActiveVersionAsync(tenantId, version.ArtifactId, token).ConfigureAwait(false);
            foreach (StepRecord step in steps)
            {
                if (!string.Equals(StepArtifactId(step), version.ArtifactId, StringComparison.Ordinal)) continue;
                string? requested = StepArtifactVersion(step);
                if (string.IsNullOrWhiteSpace(requested) || string.Equals(requested, "latest", StringComparison.OrdinalIgnoreCase))
                {
                    if (latest != null && string.Equals(latest.Id, version.Id, StringComparison.Ordinal)) result.References.Add(StepReference(step));
                    continue;
                }
                if (string.Equals(requested, version.Version, StringComparison.Ordinal) || string.Equals(requested, version.Id, StringComparison.Ordinal))
                    result.References.Add(StepReference(step));
            }

            await AddFlowRunSnapshotReferencesAsync(tenantId, version, result, token).ConfigureAwait(false);
            return result;
        }

        private async Task<ArtifactVersionRecord?> LatestActiveVersionAsync(string tenantId, string artifactId, CancellationToken token)
        {
            ArtifactVersionRecord? current = await _Database.ArtifactVersions.ReadByVersionAsync(tenantId, artifactId, Constants.MutableArtifactVersion, token).ConfigureAwait(false);
            if (current != null && current.Active) return current;
            List<ArtifactVersionRecord> versions = await _Database.ArtifactVersions.AllAsync(tenantId, artifactId, token).ConfigureAwait(false);
            return versions.Where(v => v.Active).OrderByDescending(v => v.LastUpdateUtc).FirstOrDefault();
        }

        private async Task AddFlowRunSnapshotReferencesAsync(string tenantId, ArtifactVersionRecord version, DeletionDependencyResult result, CancellationToken token)
        {
            const int pageSize = 1000;
            for (int page = 1; ; page++)
            {
                var runs = await _Database.FlowRuns.EnumerateAsync(new FlowRunFilter
                {
                    TenantId = tenantId,
                    PageNumber = page,
                    PageSize = pageSize
                }, token).ConfigureAwait(false);

                foreach (FlowRun run in runs.Items)
                {
                    FlowRunExecutionSnapshot snapshot = FlowRunExecutionSnapshotSerializer.Deserialize(run.ExecutionSnapshotJson, run.Id);
                    foreach (ArtifactVersionSnapshot artifact in snapshot.ArtifactVersions.Values)
                    {
                        if (!string.Equals(artifact.ArtifactId, version.ArtifactId, StringComparison.Ordinal)) continue;
                        if (string.Equals(artifact.VersionId, version.Id, StringComparison.Ordinal) ||
                            string.Equals(artifact.Version, version.Version, StringComparison.Ordinal))
                        {
                            result.References.Add("flow run " + run.Id);
                            break;
                        }
                    }
                }

                if (runs.Items.Count == 0 || page * pageSize >= runs.TotalCount) break;
            }
        }

        private static bool FlowReferencesStep(DataFlowRecord flow, string executionKey)
        {
            if (string.Equals(flow.StartStepId, executionKey, StringComparison.Ordinal)) return true;
            if (flow.Transitions.ContainsKey(executionKey)) return true;
            foreach (Tempo.StepTransition transition in flow.Transitions.Values)
            {
                if (transition == null) continue;
                if (string.Equals(transition.OnSuccess, executionKey, StringComparison.Ordinal)) return true;
                if (string.Equals(transition.OnFailure, executionKey, StringComparison.Ordinal)) return true;
                if (string.Equals(transition.OnException, executionKey, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string? StepArtifactId(StepRecord step)
        {
            if (step.RuntimeConfig is ArtifactProcessRuntimeConfig process) return FirstNonEmpty(process.ArtifactId, step.ArtifactId);
            if (step.RuntimeConfig is ArtifactPythonRuntimeConfig python) return FirstNonEmpty(python.ArtifactId, step.ArtifactId);
            if (step.RuntimeConfig is ArtifactJavaScriptRuntimeConfig javaScript) return FirstNonEmpty(javaScript.ArtifactId, step.ArtifactId);
            if (step.RuntimeConfig is ArtifactDotnetProcessRuntimeConfig dotnet) return FirstNonEmpty(dotnet.ArtifactId, step.ArtifactId);
            return step.ArtifactId;
        }

        private static string? StepArtifactVersion(StepRecord step)
        {
            if (step.RuntimeConfig is ArtifactProcessRuntimeConfig process) return FirstNonEmpty(process.ArtifactVersion, step.ArtifactVersion);
            if (step.RuntimeConfig is ArtifactPythonRuntimeConfig python) return FirstNonEmpty(python.ArtifactVersion, step.ArtifactVersion);
            if (step.RuntimeConfig is ArtifactJavaScriptRuntimeConfig javaScript) return FirstNonEmpty(javaScript.ArtifactVersion, step.ArtifactVersion);
            if (step.RuntimeConfig is ArtifactDotnetProcessRuntimeConfig dotnet) return FirstNonEmpty(dotnet.ArtifactVersion, step.ArtifactVersion);
            return step.ArtifactVersion;
        }

        private static string? FirstNonEmpty(string? first, string? second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second;
        }

        private static string FlowReference(DataFlowRecord flow)
        {
            return "flow '" + flow.Name + "' (" + flow.Id + ")";
        }

        private static string StepReference(StepRecord step)
        {
            return "step '" + step.Name + "' (" + step.Id + ")";
        }
    }
}
