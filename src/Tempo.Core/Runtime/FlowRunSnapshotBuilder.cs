namespace Tempo.Core.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Database;
    using Tempo.Core.Models;

    /// <summary>Builds run-start snapshots for artifact-backed steps.</summary>
    public static class FlowRunSnapshotBuilder
    {
        /// <summary>
        /// Builds a run-start execution snapshot by resolving artifact versions for every artifact-backed step in the flow.
        /// </summary>
        /// <param name="database">The database driver used to read steps and artifact versions. Cannot be null.</param>
        /// <param name="run">The flow run to build the snapshot for. Cannot be null.</param>
        /// <param name="flow">The data flow whose transitions are inspected. Cannot be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The populated flow-run execution snapshot.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/>, <paramref name="run"/>, or <paramref name="flow"/> is null.</exception>
        public static async Task<FlowRunExecutionSnapshot> BuildAsync(DatabaseDriverBase database, FlowRun run, DataFlowRecord flow, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (flow == null) throw new ArgumentNullException(nameof(flow));

            FlowRunExecutionSnapshot snapshot = FlowRunExecutionSnapshotSerializer.Deserialize(run.ExecutionSnapshotJson, run.Id);
            ArtifactVersionResolver resolver = new ArtifactVersionResolver(database);
            foreach (string executionKey in flow.Transitions.Keys)
            {
                token.ThrowIfCancellationRequested();
                StepRecord? step = await database.Steps.ReadByExecutionKeyAsync(run.TenantId, executionKey, token).ConfigureAwait(false);
                if (step == null) continue;
                if (step.RuntimeConfig is ArtifactProcessRuntimeConfig process)
                {
                    string? artifactId = process.ArtifactId ?? step.ArtifactId;
                    if (!string.IsNullOrWhiteSpace(artifactId))
                        await resolver.ResolveAsync(run.TenantId, artifactId, process.ArtifactVersion ?? step.ArtifactVersion, snapshot, process.Entrypoint, token).ConfigureAwait(false);
                }
                else if (step.RuntimeConfig is ArtifactPythonRuntimeConfig python)
                {
                    string? artifactId = python.ArtifactId ?? step.ArtifactId;
                    if (!string.IsNullOrWhiteSpace(artifactId))
                        await resolver.ResolveAsync(run.TenantId, artifactId, python.ArtifactVersion ?? step.ArtifactVersion, snapshot, python.Entrypoint, token).ConfigureAwait(false);
                }
                else if (step.RuntimeConfig is ArtifactJavaScriptRuntimeConfig javaScript)
                {
                    string? artifactId = javaScript.ArtifactId ?? step.ArtifactId;
                    if (!string.IsNullOrWhiteSpace(artifactId))
                        await resolver.ResolveAsync(run.TenantId, artifactId, javaScript.ArtifactVersion ?? step.ArtifactVersion, snapshot, javaScript.Entrypoint, token).ConfigureAwait(false);
                }
                else if (step.RuntimeConfig is ArtifactDotnetProcessRuntimeConfig dotnet)
                {
                    string? artifactId = dotnet.ArtifactId ?? step.ArtifactId;
                    if (!string.IsNullOrWhiteSpace(artifactId))
                        await resolver.ResolveAsync(run.TenantId, artifactId, dotnet.ArtifactVersion ?? step.ArtifactVersion, snapshot, dotnet.Entrypoint, token).ConfigureAwait(false);
                }
            }

            return snapshot;
        }
    }
}
