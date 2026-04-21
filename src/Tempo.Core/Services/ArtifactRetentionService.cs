namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;

    /// <summary>Marks orphaned artifact versions for GC and sweeps eligible blobs outside request paths.</summary>
    public class ArtifactRetentionService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly IArtifactBlobStore _BlobStore;
        private readonly ArtifactSettings _Settings;
        private readonly ExternalExecutionSettings? _RuntimeSettings;

        /// <summary>Instantiate.</summary>
        public ArtifactRetentionService(DatabaseDriverBase database, IArtifactBlobStore blobStore, ArtifactSettings settings, ExternalExecutionSettings? runtimeSettings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _BlobStore = blobStore ?? throw new ArgumentNullException(nameof(blobStore));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _RuntimeSettings = runtimeSettings;
        }

        /// <summary>Mark an entire artifact and its versions deleted, leaving physical cleanup for scheduled GC.</summary>
        public async Task<int> MarkArtifactDeletedAsync(string tenantId, string artifactId, DateTime? utcNow = null, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
            ArtifactRecord? artifact = await _Database.Artifacts.ReadAsync(tenantId, artifactId, token).ConfigureAwait(false);
            if (artifact == null) return 0;
            artifact.Active = false;
            await _Database.Artifacts.UpdateAsync(artifact, token).ConfigureAwait(false);

            int marked = 0;
            DateTime now = (utcNow ?? DateTime.UtcNow).ToUniversalTime();
            List<ArtifactVersionRecord> versions = await _Database.ArtifactVersions.AllAsync(tenantId, artifactId, token).ConfigureAwait(false);
            foreach (ArtifactVersionRecord version in versions)
            {
                if (MarkVersion(version, now)) marked++;
                await _Database.ArtifactVersions.UpdateAsync(version, token).ConfigureAwait(false);
            }
            return marked;
        }

        /// <summary>Mark one version deleted, leaving physical cleanup for scheduled GC.</summary>
        public async Task<bool> MarkVersionDeletedAsync(ArtifactVersionRecord version, DateTime? utcNow = null, CancellationToken token = default)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));
            bool changed = MarkVersion(version, (utcNow ?? DateTime.UtcNow).ToUniversalTime());
            if (changed) await _Database.ArtifactVersions.UpdateAsync(version, token).ConfigureAwait(false);
            return changed;
        }

        /// <summary>Mark orphaned/over-limit versions and sweep eligible rows/blobs.</summary>
        public async Task<ArtifactGcResult> RunOnceAsync(DateTime? utcNow = null, CancellationToken token = default)
        {
            DateTime now = (utcNow ?? DateTime.UtcNow).ToUniversalTime();
            ArtifactGcResult result = await MarkOrphansAsync(now, token).ConfigureAwait(false);
            ArtifactGcResult sweep = await SweepEligibleAsync(now, _Settings.GcBatchSize, token).ConfigureAwait(false);
            Merge(result, sweep);
            return result;
        }

        /// <summary>Mark deleted or over-limit versions as GC eligible after the configured grace period.</summary>
        public async Task<ArtifactGcResult> MarkOrphansAsync(DateTime utcNow, CancellationToken token = default)
        {
            ArtifactGcResult result = new ArtifactGcResult();
            List<Tenant> tenants = await _Database.Tenants.AllAsync(token).ConfigureAwait(false);
            foreach (Tenant tenant in tenants)
            {
                token.ThrowIfCancellationRequested();
                result.TenantsScanned++;
                List<StepRecord> steps = await _Database.Steps.AllAsync(tenant.Id, token).ConfigureAwait(false);
                Dictionary<string, HashSet<string>> retainedRunReferences = await RetainedFlowRunReferencesAsync(tenant.Id, utcNow, token).ConfigureAwait(false);
                List<ArtifactRecord> artifacts = await _Database.Artifacts.AllAsync(tenant.Id, token).ConfigureAwait(false);
                foreach (ArtifactRecord artifact in artifacts)
                {
                    result.ArtifactsScanned++;
                    List<ArtifactVersionRecord> versions = await _Database.ArtifactVersions.AllAsync(tenant.Id, artifact.Id, token).ConfigureAwait(false);
                    result.VersionsScanned += versions.Count;
                    HashSet<string> protectedVersionLabels = ActiveStepReferences(steps, artifact.Id);
                    MergeReferences(protectedVersionLabels, retainedRunReferences, artifact.Id);
                    HashSet<string> overLimitMarked = ApplyVersionLimit(versions, protectedVersionLabels, utcNow, result);
                    foreach (ArtifactVersionRecord version in versions)
                    {
                        if (IsProtected(version, protectedVersionLabels))
                        {
                            result.VersionsProtected++;
                            if (version.GcEligibleUtc.HasValue)
                            {
                                version.GcEligibleUtc = null;
                                await _Database.ArtifactVersions.UpdateAsync(version, token).ConfigureAwait(false);
                            }
                            continue;
                        }

                        if (!version.Active || version.DeletedUtc.HasValue)
                        {
                            if (MarkVersion(version, utcNow) || overLimitMarked.Contains(version.Id))
                            {
                                if (!overLimitMarked.Contains(version.Id)) result.VersionsMarked++;
                                await _Database.ArtifactVersions.UpdateAsync(version, token).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>Sweep physically eligible versions and their unreferenced blobs.</summary>
        public async Task<ArtifactGcResult> SweepEligibleAsync(DateTime utcNow, int maxResults, CancellationToken token = default)
        {
            ArtifactGcResult result = new ArtifactGcResult();
            List<ArtifactVersionRecord> eligible = await _Database.ArtifactVersions.GcEligibleAsync(utcNow.ToUniversalTime(), maxResults, token).ConfigureAwait(false);
            foreach (ArtifactVersionRecord version in eligible)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    List<StepRecord> steps = await _Database.Steps.AllAsync(version.TenantId, token).ConfigureAwait(false);
                    HashSet<string> protectedVersionLabels = ActiveStepReferences(steps, version.ArtifactId);
                    Dictionary<string, HashSet<string>> retainedRunReferences = await RetainedFlowRunReferencesAsync(version.TenantId, utcNow, token).ConfigureAwait(false);
                    MergeReferences(protectedVersionLabels, retainedRunReferences, version.ArtifactId);
                    if (IsProtected(version, protectedVersionLabels))
                    {
                        result.VersionsProtected++;
                        version.GcEligibleUtc = null;
                        await _Database.ArtifactVersions.UpdateAsync(version, token).ConfigureAwait(false);
                        continue;
                    }

                    long byteLength = version.ByteLength;
                    await _Database.ArtifactVersions.DeleteAsync(version.TenantId, version.Id, token).ConfigureAwait(false);
                    result.VersionsDeleted++;

                    List<ArtifactVersionRecord> remaining = await _Database.ArtifactVersions.FindBySha256Async(version.TenantId, version.Sha256, token).ConfigureAwait(false);
                    if (remaining.Count == 0 && await _BlobStore.DeleteAsync(version.TenantId, version.Sha256, token).ConfigureAwait(false))
                    {
                        result.BlobsDeleted++;
                        result.BytesDeleted += byteLength;
                        DeleteRuntimeCaches(version);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(version.Id + ": " + ex.Message);
                }
            }
            return result;
        }

        private void DeleteRuntimeCaches(ArtifactVersionRecord version)
        {
            if (_RuntimeSettings == null) return;
            try { new ArtifactPackageCache(_BlobStore, _RuntimeSettings).DeleteCache(version.TenantId, version.Sha256); } catch { }
            try { new PythonEnvironmentCache(_RuntimeSettings).DeleteCache(version.Sha256); } catch { }
        }

        private HashSet<string> ApplyVersionLimit(List<ArtifactVersionRecord> versions, HashSet<string> protectedVersionLabels, DateTime utcNow, ArtifactGcResult result)
        {
            HashSet<string> marked = new HashSet<string>(StringComparer.Ordinal);
            if (_Settings.MaxVersionsPerArtifact <= 0) return marked;
            List<ArtifactVersionRecord> activeUnprotected = versions
                .Where(v => v.Active && !IsProtected(v, protectedVersionLabels))
                .OrderByDescending(v => v.CreatedUtc)
                .ToList();
            for (int i = _Settings.MaxVersionsPerArtifact; i < activeUnprotected.Count; i++)
            {
                if (MarkVersion(activeUnprotected[i], utcNow))
                {
                    result.VersionsMarked++;
                    marked.Add(activeUnprotected[i].Id);
                }
            }
            return marked;
        }

        private bool MarkVersion(ArtifactVersionRecord version, DateTime utcNow)
        {
            bool changed = false;
            if (version.Active) { version.Active = false; changed = true; }
            if (!version.DeletedUtc.HasValue) { version.DeletedUtc = utcNow; changed = true; }
            DateTime eligible = version.DeletedUtc.Value.AddDays(_Settings.VersionGracePeriodDays);
            if (!version.GcEligibleUtc.HasValue || version.GcEligibleUtc.Value != eligible)
            {
                version.GcEligibleUtc = eligible;
                changed = true;
            }
            return changed;
        }

        private async Task<Dictionary<string, HashSet<string>>> RetainedFlowRunReferencesAsync(string tenantId, DateTime utcNow, CancellationToken token)
        {
            Dictionary<string, HashSet<string>> references = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            DateTime fromUtc = utcNow.ToUniversalTime().AddDays(-_Settings.FlowRunReplayRetentionDays);
            const int pageSize = 1000;
            for (int page = 1; ; page++)
            {
                EnumerationResult<FlowRun> runs = await _Database.FlowRuns.EnumerateAsync(new FlowRunFilter
                {
                    TenantId = tenantId,
                    FromUtc = fromUtc,
                    PageNumber = page,
                    PageSize = pageSize
                }, token).ConfigureAwait(false);

                foreach (FlowRun run in runs.Items)
                {
                    FlowRunExecutionSnapshot snapshot = FlowRunExecutionSnapshotSerializer.Deserialize(run.ExecutionSnapshotJson, run.Id);
                    foreach (ArtifactVersionSnapshot artifact in snapshot.ArtifactVersions.Values)
                    {
                        if (string.IsNullOrWhiteSpace(artifact.ArtifactId)) continue;
                        if (!references.TryGetValue(artifact.ArtifactId, out HashSet<string>? labels))
                        {
                            labels = new HashSet<string>(StringComparer.Ordinal);
                            references[artifact.ArtifactId] = labels;
                        }
                        if (!string.IsNullOrWhiteSpace(artifact.Version)) labels.Add(artifact.Version);
                        if (!string.IsNullOrWhiteSpace(artifact.VersionId)) labels.Add(artifact.VersionId);
                    }
                }

                if (runs.Items.Count == 0 || page * pageSize >= runs.TotalCount) break;
            }

            return references;
        }

        private static void MergeReferences(HashSet<string> target, Dictionary<string, HashSet<string>> references, string artifactId)
        {
            if (!references.TryGetValue(artifactId, out HashSet<string>? labels)) return;
            foreach (string label in labels) target.Add(label);
        }

        private static HashSet<string> ActiveStepReferences(List<StepRecord> steps, string artifactId)
        {
            HashSet<string> labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (StepRecord step in steps)
            {
                if (!step.Active) continue;
                if (!string.Equals(step.ArtifactId, artifactId, StringComparison.Ordinal)) continue;
                if (!string.IsNullOrWhiteSpace(step.ArtifactVersion)) labels.Add(step.ArtifactVersion);
            }
            return labels;
        }

        private static bool IsProtected(ArtifactVersionRecord version, HashSet<string> protectedVersionLabels)
        {
            return version.IsProtected ||
                protectedVersionLabels.Contains(version.Version) ||
                protectedVersionLabels.Contains(version.Id);
        }

        private static void Merge(ArtifactGcResult target, ArtifactGcResult source)
        {
            target.TenantsScanned += source.TenantsScanned;
            target.ArtifactsScanned += source.ArtifactsScanned;
            target.VersionsScanned += source.VersionsScanned;
            target.VersionsProtected += source.VersionsProtected;
            target.VersionsMarked += source.VersionsMarked;
            target.VersionsDeleted += source.VersionsDeleted;
            target.BlobsDeleted += source.BlobsDeleted;
            target.BytesDeleted += source.BytesDeleted;
            target.Errors.AddRange(source.Errors);
        }
    }
}
