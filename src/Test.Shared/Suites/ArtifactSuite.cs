namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
#if NET10_0
    using SyslogLogging;
#endif
    using Tempo.Core;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
#if NET10_0
    using Tempo.Server;
#endif
    using Touchstone.Core;

    /// <summary>Artifact metadata database tests.</summary>
    public static class ArtifactSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Artifacts",
                displayName: "Artifact metadata persistence",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Artifacts", "ArtifactCrud", "Artifacts create, read, update, enumerate, and delete", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            ArtifactRecord created = await driver.Artifacts.CreateAsync(new ArtifactRecord
                            {
                                TenantId = tenant.Id,
                                Name = "tooling",
                                Description = "build tools"
                            }, ct);

                            ArtifactRecord? byId = await driver.Artifacts.ReadAsync(tenant.Id, created.Id, ct);
                            ArtifactRecord? byName = await driver.Artifacts.ReadByNameAsync(tenant.Id, "tooling", ct);
                            Assert2.NotNull(byId, "read by id");
                            Assert2.NotNull(byName, "read by name");
                            Assert2.Equal(created.Id, byName!.Id, "same artifact");

                            created.Name = "tooling-renamed";
                            created.Description = "updated";
                            created.Active = false;
                            await driver.Artifacts.UpdateAsync(created, ct);

                            ArtifactRecord? updated = await driver.Artifacts.ReadAsync(tenant.Id, created.Id, ct);
                            Assert2.Equal("tooling-renamed", updated!.Name, "name updated");
                            Assert2.Equal("updated", updated.Description!, "description updated");
                            Assert2.True(!updated.Active, "active updated");

                            var activeOnly = await driver.Artifacts.EnumerateAsync(tenant.Id, new EnumerationFilter(), ct);
                            Assert2.Equal(0, activeOnly.Items.Count, "inactive omitted by default");
                            var includeInactive = await driver.Artifacts.EnumerateAsync(tenant.Id, new EnumerationFilter { IncludeInactive = true }, ct);
                            Assert2.Equal(1, includeInactive.Items.Count, "inactive included");

                            await driver.Artifacts.DeleteAsync(tenant.Id, created.Id, ct);
                            Assert2.IsNull(await driver.Artifacts.ReadAsync(tenant.Id, created.Id, ct), "deleted");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Artifacts", "ArtifactTenantUniqueness", "Artifact names are unique per tenant and reusable across tenants", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenantA = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant A" }, ct);
                            Tenant tenantB = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant B" }, ct);
                            await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantA.Id, Name = "shared" }, ct);
                            ArtifactRecord tenantBArtifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantB.Id, Name = "shared" }, ct);

                            bool duplicateRejected = false;
                            try { await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantA.Id, Name = "shared" }, ct); }
                            catch (Exception) { duplicateRejected = true; }

                            Assert2.True(duplicateRejected, "same-tenant duplicate rejected");
                            ArtifactRecord? readB = await driver.Artifacts.ReadByNameAsync(tenantB.Id, "shared", ct);
                            Assert2.Equal(tenantBArtifact.Id, readB!.Id, "cross-tenant reuse allowed");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Artifacts", "VersionCrud", "Artifact versions create, read, update, enumerate, and delete", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "runtime" }, ct);
                            ArtifactVersionRecord version = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id,
                                ArtifactId = artifact.Id,
                                Version = "1.0.0",
                                Sha256 = new string('A', 64),
                                ByteLength = 1234,
                                ContentType = "application/zip",
                                OriginalFileName = "runtime.zip",
                                ManifestJson = "{\"runtimeKey\":\"Artifact.Process\"}",
                                StorageKey = tenant.Id + "/sha"
                            }, ct);

                            Assert2.Equal(new string('a', 64), version.Sha256, "sha normalized");
                            ArtifactVersionRecord? byId = await driver.ArtifactVersions.ReadAsync(tenant.Id, version.Id, ct);
                            ArtifactVersionRecord? byVersion = await driver.ArtifactVersions.ReadByVersionAsync(tenant.Id, artifact.Id, "1.0.0", ct);
                            Assert2.NotNull(byId, "read by id");
                            Assert2.NotNull(byVersion, "read by version");
                            Assert2.Equal(version.Id, byVersion!.Id, "same version");
                            Assert2.Equal("runtime.zip", byId!.OriginalFileName!, "filename roundtrip");

                            version.ByteLength = 4321;
                            version.Active = false;
                            version.DeletedUtc = DateTime.UtcNow;
                            version.GcEligibleUtc = DateTime.UtcNow.AddDays(1);
                            await driver.ArtifactVersions.UpdateAsync(version, ct);
                            ArtifactVersionRecord? updated = await driver.ArtifactVersions.ReadAsync(tenant.Id, version.Id, ct);
                            Assert2.Equal(4321L, updated!.ByteLength, "byte length updated");
                            Assert2.True(!updated.Active, "active updated");
                            Assert2.NotNull(updated.DeletedUtc, "deleted timestamp stored");

                            var activeOnly = await driver.ArtifactVersions.EnumerateAsync(tenant.Id, artifact.Id, new EnumerationFilter(), ct);
                            Assert2.Equal(0, activeOnly.Items.Count, "inactive version omitted by default");
                            var includeInactive = await driver.ArtifactVersions.EnumerateAsync(tenant.Id, artifact.Id, new EnumerationFilter { IncludeInactive = true }, ct);
                            Assert2.Equal(1, includeInactive.Items.Count, "inactive version included");

                            await driver.ArtifactVersions.DeleteAsync(tenant.Id, version.Id, ct);
                            Assert2.IsNull(await driver.ArtifactVersions.ReadAsync(tenant.Id, version.Id, ct), "version deleted");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Artifacts", "VersionIndexesAndIsolation", "Versions enforce tenant/artifact uniqueness and tenant-scoped SHA lookup", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            string sha = Repeat('b');
                            Tenant tenantA = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant A" }, ct);
                            Tenant tenantB = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant B" }, ct);
                            ArtifactRecord artifactA = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantA.Id, Name = "runtime" }, ct);
                            ArtifactRecord artifactB = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantB.Id, Name = "runtime" }, ct);

                            ArtifactVersionRecord versionA = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord { TenantId = tenantA.Id, ArtifactId = artifactA.Id, Version = "1", Sha256 = sha, ByteLength = 1 }, ct);
                            ArtifactVersionRecord versionB = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord { TenantId = tenantB.Id, ArtifactId = artifactB.Id, Version = "1", Sha256 = sha, ByteLength = 1 }, ct);

                            bool duplicateRejected = false;
                            try { await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord { TenantId = tenantA.Id, ArtifactId = artifactA.Id, Version = "1", Sha256 = Repeat('c'), ByteLength = 1 }, ct); }
                            catch (Exception) { duplicateRejected = true; }

                            List<ArtifactVersionRecord> foundA = await driver.ArtifactVersions.FindBySha256Async(tenantA.Id, sha, ct);
                            List<ArtifactVersionRecord> foundB = await driver.ArtifactVersions.FindBySha256Async(tenantB.Id, sha, ct);
                            Assert2.True(duplicateRejected, "duplicate artifact version rejected");
                            Assert2.Equal(1, foundA.Count, "tenant A SHA results");
                            Assert2.Equal(1, foundB.Count, "tenant B SHA results");
                            Assert2.Equal(versionA.Id, foundA[0].Id, "tenant A isolated");
                            Assert2.Equal(versionB.Id, foundB[0].Id, "tenant B isolated");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Artifacts", "GcEligibility", "GC eligibility returns due versions in oldest-first order", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            DateTime now = DateTime.UtcNow;
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "runtime" }, ct);
                            ArtifactVersionRecord oldest = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id, ArtifactId = artifact.Id, Version = "oldest", Sha256 = Repeat('1'), ByteLength = 1,
                                Active = false, DeletedUtc = now.AddDays(-3), GcEligibleUtc = now.AddDays(-2)
                            }, ct);
                            ArtifactVersionRecord newer = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id, ArtifactId = artifact.Id, Version = "newer", Sha256 = Repeat('2'), ByteLength = 1,
                                Active = false, DeletedUtc = now.AddDays(-2), GcEligibleUtc = now.AddDays(-1)
                            }, ct);
                            await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id, ArtifactId = artifact.Id, Version = "future", Sha256 = Repeat('3'), ByteLength = 1,
                                Active = false, DeletedUtc = now, GcEligibleUtc = now.AddDays(1)
                            }, ct);

                            List<ArtifactVersionRecord> due = await driver.ArtifactVersions.GcEligibleAsync(now, 10, ct);
                            List<string> ids = due.Select(x => x.Id).ToList();
                            Assert2.Equal(2, due.Count, "two due versions");
                            Assert2.Equal(oldest.Id, ids[0], "oldest first");
                            Assert2.Equal(newer.Id, ids[1], "newer second");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Artifacts", "Cascades", "Artifact and tenant deletes remove version metadata", async ct =>
                    {
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "runtime" }, ct);
                            await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord { TenantId = tenant.Id, ArtifactId = artifact.Id, Version = "1", Sha256 = Repeat('4'), ByteLength = 1 }, ct);
                            await driver.Artifacts.DeleteAsync(tenant.Id, artifact.Id, ct);
                            Assert2.Equal(0, (await driver.ArtifactVersions.AllAsync(tenant.Id, artifact.Id, ct)).Count, "artifact delete removed versions");

                            ArtifactRecord tenantArtifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "runtime2" }, ct);
                            await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord { TenantId = tenant.Id, ArtifactId = tenantArtifact.Id, Version = "1", Sha256 = Repeat('5'), ByteLength = 1 }, ct);
                            await driver.Tenants.DeleteAsync(tenant.Id, ct);
                            Assert2.Equal(0, (await driver.Artifacts.AllAsync(tenant.Id, ct)).Count, "tenant delete removed artifacts");
                            Assert2.Equal(0, (await driver.ArtifactVersions.AllAsync(tenant.Id, tenantArtifact.Id, ct)).Count, "tenant delete removed versions");
                        }
                        finally { await TempTestStore.DisposeAsync(driver); }
                    }),
                    new TestCaseDescriptor("Artifacts", "LocalBlobStore", "Local filesystem blob store validates SHA, size, quota, and tenant paths", async ct =>
                    {
                        string root = TempDirectory();
                        try
                        {
                            ArtifactSettings settings = new ArtifactSettings { RootPath = root, MaxUploadBytes = 1024, MaxBytesPerTenant = 1024 };
                            LocalFilesystemArtifactBlobStore store = new LocalFilesystemArtifactBlobStore(settings);
                            byte[] bytes = Encoding.UTF8.GetBytes("artifact payload");
                            string sha = Sha256Hex(bytes);
                            using (MemoryStream input = new MemoryStream(bytes, writable: false))
                            {
                                ArtifactBlobWriteResult written = await store.PutAsync("ten_blob", sha, input, bytes.Length, ct);
                                Assert2.Equal("ten_blob/" + sha, written.StorageKey, "storage key");
                                Assert2.Equal(bytes.Length, (int)written.ByteLength, "byte length");
                            }

                            Assert2.True(await store.ExistsAsync("ten_blob", sha, ct), "blob exists");
                            Assert2.Equal(bytes.Length, (int)await store.TenantBytesAsync("ten_blob", ct), "tenant bytes");
                            using (Stream read = await store.OpenReadAsync("ten_blob", sha, ct))
                            using (MemoryStream copy = new MemoryStream())
                            {
                                await read.CopyToAsync(copy, ct);
                                Assert2.True(bytes.SequenceEqual(copy.ToArray()), "downloaded bytes match");
                            }

                            bool mismatchRejected = false;
                            try
                            {
                                using MemoryStream bad = new MemoryStream(bytes, writable: false);
                                await store.PutAsync("ten_blob", Repeat('0'), bad, bytes.Length, ct);
                            }
                            catch (InvalidOperationException) { mismatchRejected = true; }
                            Assert2.True(mismatchRejected, "sha mismatch rejected");

                            bool traversalRejected = false;
                            try
                            {
                                using MemoryStream bad = new MemoryStream(bytes, writable: false);
                                await store.PutAsync("..\\bad", sha, bad, bytes.Length, ct);
                            }
                            catch (ArgumentException) { traversalRejected = true; }
                            Assert2.True(traversalRejected, "tenant traversal rejected");

                            LocalFilesystemArtifactBlobStore quotaStore = new LocalFilesystemArtifactBlobStore(new ArtifactSettings { RootPath = root, MaxUploadBytes = 1024, MaxBytesPerTenant = 4 });
                            bool quotaRejected = false;
                            try
                            {
                                using MemoryStream quota = new MemoryStream(bytes, writable: false);
                                await quotaStore.PutAsync("ten_quota", sha, quota, bytes.Length, ct);
                            }
                            catch (InvalidOperationException) { quotaRejected = true; }
                            Assert2.True(quotaRejected, "tenant quota rejected");

                            Assert2.True(await store.DeleteAsync("ten_blob", sha, ct), "delete returns true");
                            Assert2.True(!await store.ExistsAsync("ten_blob", sha, ct), "blob deleted");
                        }
                        finally { DeleteDirectory(root); }
                    }),
                    new TestCaseDescriptor("Artifacts", "RetentionProtectsActiveStepReferences", "Artifact GC keeps deleted versions referenced by active steps", async ct =>
                    {
                        string root = TempDirectory();
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            ArtifactSettings settings = new ArtifactSettings { RootPath = root, VersionGracePeriodDays = 0, GcBatchSize = 10 };
                            LocalFilesystemArtifactBlobStore store = new LocalFilesystemArtifactBlobStore(settings);
                            ArtifactRetentionService retention = new ArtifactRetentionService(driver, store, settings);
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "runtime" }, ct);
                            ArtifactVersionRecord version = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id,
                                ArtifactId = artifact.Id,
                                Version = "1",
                                Sha256 = Repeat('6'),
                                ByteLength = 1,
                                Active = false,
                                DeletedUtc = DateTime.UtcNow.AddDays(-1),
                                GcEligibleUtc = DateTime.UtcNow.AddDays(-1)
                            }, ct);
                            await driver.Steps.CreateAsync(new StepRecord { TenantId = tenant.Id, Name = "artifact step", ArtifactId = artifact.Id, ArtifactVersion = "1", Active = true }, ct);

                            ArtifactGcResult result = await retention.SweepEligibleAsync(DateTime.UtcNow, 10, ct);
                            ArtifactVersionRecord? read = await driver.ArtifactVersions.ReadAsync(tenant.Id, version.Id, ct);
                            Assert2.NotNull(read, "referenced version retained");
                            Assert2.True(read!.GcEligibleUtc == null, "gc eligibility cleared");
                            Assert2.Equal(0, result.VersionsDeleted, "no protected delete");
                            Assert2.Equal(1, result.VersionsProtected, "protected count");
                        }
                        finally
                        {
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    }),
                    new TestCaseDescriptor("Artifacts", "RetentionProtectsFlowRunSnapshots", "Artifact GC keeps deleted versions referenced by retained flow-run snapshots", async ct =>
                    {
                        string root = TempDirectory();
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            ArtifactSettings settings = new ArtifactSettings { RootPath = root, VersionGracePeriodDays = 0, FlowRunReplayRetentionDays = 30, GcBatchSize = 10 };
                            LocalFilesystemArtifactBlobStore store = new LocalFilesystemArtifactBlobStore(settings);
                            ArtifactRetentionService retention = new ArtifactRetentionService(driver, store, settings);
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            DataFlowRecord flow = await driver.DataFlows.CreateAsync(new DataFlowRecord { TenantId = tenant.Id, Name = "Flow", StartStepId = "artifact-step" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "runtime" }, ct);
                            ArtifactVersionRecord version = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id,
                                ArtifactId = artifact.Id,
                                Version = "1",
                                Sha256 = Repeat('7'),
                                ByteLength = 1,
                                Active = false,
                                DeletedUtc = DateTime.UtcNow.AddDays(-1),
                                GcEligibleUtc = DateTime.UtcNow.AddDays(-1)
                            }, ct);

                            FlowRunExecutionSnapshot snapshot = new FlowRunExecutionSnapshot { FlowRunId = "run_snapshot" };
                            snapshot.ArtifactVersions[FlowRunExecutionSnapshot.ArtifactKey(artifact.Id, "1")] = new ArtifactVersionSnapshot
                            {
                                ArtifactId = artifact.Id,
                                RequestedVersion = "1",
                                VersionId = version.Id,
                                Version = version.Version,
                                Sha256 = version.Sha256
                            };
                            await driver.FlowRuns.CreateAsync(new FlowRun
                            {
                                TenantId = tenant.Id,
                                DataFlowId = flow.Id,
                                ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(snapshot)
                            }, ct);

                            ArtifactGcResult result = await retention.SweepEligibleAsync(DateTime.UtcNow, 10, ct);
                            ArtifactVersionRecord? read = await driver.ArtifactVersions.ReadAsync(tenant.Id, version.Id, ct);
                            Assert2.NotNull(read, "snapshot-referenced version retained");
                            Assert2.True(read!.GcEligibleUtc == null, "gc eligibility cleared");
                            Assert2.Equal(0, result.VersionsDeleted, "no protected delete");
                            Assert2.Equal(1, result.VersionsProtected, "protected count");
                        }
                        finally
                        {
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    }),
                    new TestCaseDescriptor("Artifacts", "RetentionMarksAndSweeps", "Retention marks over-limit versions and scheduled GC removes rows and unreferenced blobs", async ct =>
                    {
                        string root = TempDirectory();
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            ArtifactSettings settings = new ArtifactSettings { RootPath = root, VersionGracePeriodDays = 0, MaxVersionsPerArtifact = 1, GcBatchSize = 10 };
                            LocalFilesystemArtifactBlobStore store = new LocalFilesystemArtifactBlobStore(settings);
                            ArtifactRetentionService retention = new ArtifactRetentionService(driver, store, settings);
                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Tenant" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "runtime" }, ct);

                            byte[] oldBytes = Encoding.UTF8.GetBytes("old artifact");
                            string oldSha = Sha256Hex(oldBytes);
                            using (MemoryStream oldBody = new MemoryStream(oldBytes, writable: false))
                                await store.PutAsync(tenant.Id, oldSha, oldBody, oldBytes.Length, ct);
                            ArtifactVersionRecord oldVersion = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id, ArtifactId = artifact.Id, Version = "1", Sha256 = oldSha, ByteLength = oldBytes.Length, StorageKey = store.GetStorageKey(tenant.Id, oldSha)
                            }, ct);

                            await Task.Delay(20, ct);

                            byte[] newBytes = Encoding.UTF8.GetBytes("new artifact");
                            string newSha = Sha256Hex(newBytes);
                            using (MemoryStream newBody = new MemoryStream(newBytes, writable: false))
                                await store.PutAsync(tenant.Id, newSha, newBody, newBytes.Length, ct);
                            ArtifactVersionRecord newVersion = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id, ArtifactId = artifact.Id, Version = "2", Sha256 = newSha, ByteLength = newBytes.Length, StorageKey = store.GetStorageKey(tenant.Id, newSha)
                            }, ct);

                            ArtifactGcResult mark = await retention.MarkOrphansAsync(DateTime.UtcNow, ct);
                            ArtifactVersionRecord? markedOld = await driver.ArtifactVersions.ReadAsync(tenant.Id, oldVersion.Id, ct);
                            ArtifactVersionRecord? retainedNew = await driver.ArtifactVersions.ReadAsync(tenant.Id, newVersion.Id, ct);
                            Assert2.True(mark.VersionsMarked >= 1, "old version marked");
                            Assert2.True(!markedOld!.Active, "old version inactive");
                            Assert2.NotNull(markedOld.GcEligibleUtc, "old version gc eligible");
                            Assert2.True(retainedNew!.Active, "new version remains active");

                            ArtifactGcResult sweep = await retention.SweepEligibleAsync(DateTime.UtcNow.AddMinutes(1), 10, ct);
                            Assert2.Equal(1, sweep.VersionsDeleted, "one version swept");
                            Assert2.Equal(1, sweep.BlobsDeleted, "one blob deleted");
                            Assert2.True(!await store.ExistsAsync(tenant.Id, oldSha, ct), "old blob deleted");
                            Assert2.True(await store.ExistsAsync(tenant.Id, newSha, ct), "new blob retained");
                            Assert2.IsNull(await driver.ArtifactVersions.ReadAsync(tenant.Id, oldVersion.Id, ct), "old db row deleted");
                        }
                        finally
                        {
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    }),
#if NET10_0
                    new TestCaseDescriptor("Artifacts", "RoutesLifecycle", "Artifact routes create metadata, upload, download, and delete blobs", async ct =>
                    {
                        string root = TempDirectory();
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.AdminApiKey = "artifact-route-key";
                            settings.RequestHistory.Enabled = false;
                            settings.Artifacts.RootPath = root;
                            settings.Artifacts.MaxUploadBytes = 1024 * 1024;
                            settings.Artifacts.MaxBytesPerTenant = 1024 * 1024;

                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderApiKey, "artifact-route-key");

                            Tenant tenant = await driver.Tenants.CreateAsync(new Tenant { Name = "Route Tenant" }, ct);
                            HttpResponseMessage createResp = await client.PostAsync(
                                "/v1.0/tenants/" + tenant.Id + "/artifacts",
                                new StringContent("{\"name\":\"tool\",\"description\":\"runtime tool\"}", Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.Created, createResp.StatusCode, "artifact created status");
                            ArtifactRecord artifact = Deserialize<ArtifactRecord>(await createResp.Content.ReadAsStringAsync(ct));
                            Assert2.Equal("tool", artifact.Name, "artifact name");

                            byte[] payload = Encoding.UTF8.GetBytes("route artifact payload");
                            string sha = Sha256Hex(payload);
                            string uploadPath = "/v1.0/tenants/" + tenant.Id + "/artifacts/" + artifact.Id + "/versions" +
                                "?version=1.0.0&sha256=" + sha +
                                "&originalFileName=tool.zip&contentType=" + Uri.EscapeDataString("application/octet-stream");
                            using ByteArrayContent upload = new ByteArrayContent(payload);
                            upload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                            HttpResponseMessage uploadResp = await client.PostAsync(uploadPath, upload, ct);
                            Assert2.Equal(HttpStatusCode.Created, uploadResp.StatusCode, "version uploaded status");
                            ArtifactVersionRecord version = Deserialize<ArtifactVersionRecord>(await uploadResp.Content.ReadAsStringAsync(ct));
                            Assert2.Equal(sha, version.Sha256, "version sha");
                            Assert2.Equal(payload.Length, (int)version.ByteLength, "version length");
                            Assert2.True(await server.ArtifactBlobStore.ExistsAsync(tenant.Id, sha, ct), "blob exists after upload");

                            HttpResponseMessage listResp = await client.GetAsync("/v1.0/tenants/" + tenant.Id + "/artifacts/" + artifact.Id + "/versions", ct);
                            Assert2.Equal(HttpStatusCode.OK, listResp.StatusCode, "versions list status");
                            string listJson = await listResp.Content.ReadAsStringAsync(ct);
                            Assert2.True(listJson.Contains(version.Id, StringComparison.Ordinal), "version listed");

                            HttpResponseMessage downloadResp = await client.GetAsync("/v1.0/tenants/" + tenant.Id + "/artifacts/" + artifact.Id + "/versions/1.0.0/download", ct);
                            Assert2.Equal(HttpStatusCode.OK, downloadResp.StatusCode, "download status");
                            byte[] downloaded = await downloadResp.Content.ReadAsByteArrayAsync(ct);
                            Assert2.True(payload.SequenceEqual(downloaded), "download bytes");

                            HttpResponseMessage deleteVersionResp = await client.DeleteAsync("/v1.0/tenants/" + tenant.Id + "/artifacts/" + artifact.Id + "/versions/1.0.0", ct);
                            Assert2.Equal(HttpStatusCode.NoContent, deleteVersionResp.StatusCode, "delete version status");
                            Assert2.True(await server.ArtifactBlobStore.ExistsAsync(tenant.Id, sha, ct), "blob retained until scheduled gc");
                            ArtifactVersionRecord? deletedVersion = await driver.ArtifactVersions.ReadAsync(tenant.Id, version.Id, ct);
                            Assert2.NotNull(deletedVersion, "version row retained until gc");
                            Assert2.True(!deletedVersion!.Active, "version marked inactive");
                            Assert2.NotNull(deletedVersion.GcEligibleUtc, "version marked gc eligible");

                            ArtifactGcResult gc = await server.ArtifactRetention.SweepEligibleAsync(DateTime.UtcNow.AddDays(8), 10, ct);
                            Assert2.Equal(1, gc.VersionsDeleted, "scheduled gc deletes version");
                            Assert2.True(!await server.ArtifactBlobStore.ExistsAsync(tenant.Id, sha, ct), "blob removed by scheduled gc");

                            HttpResponseMessage deleteArtifactResp = await client.DeleteAsync("/v1.0/tenants/" + tenant.Id + "/artifacts/" + artifact.Id, ct);
                            Assert2.Equal(HttpStatusCode.NoContent, deleteArtifactResp.StatusCode, "delete artifact status");
                        }
                        finally
                        {
                            try { server?.Dispose(); } catch { }
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    }),
                    new TestCaseDescriptor("Artifacts", "RoutesRejectCrossTenantReads", "Artifact routes reject cross-tenant reads and do not leak foreign ids through the caller tenant", async ct =>
                    {
                        string root = TempDirectory();
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.SigningKey = "artifact-cross-tenant-read-signing-key-0123456789";
                            settings.RequestHistory.Enabled = false;
                            settings.Artifacts.RootPath = root;

                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            Tenant tenantA = await driver.Tenants.CreateAsync(new Tenant { Name = "A" }, ct);
                            Tenant tenantB = await driver.Tenants.CreateAsync(new Tenant { Name = "B" }, ct);
                            ArtifactRecord artifactB = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantB.Id, Name = "tenant-b-tool" }, ct);
                            User userA = await driver.Users.CreateAsync(new User { TenantId = tenantA.Id, Email = "artifact-reader-a@example.com" }, ct);
                            Role role = await driver.Roles.CreateAsync(new Role { TenantId = tenantA.Id, Name = "artifact-reader" }, ct);
                            Permission read = await driver.Permissions.CreateAsync(new Permission
                            {
                                TenantId = tenantA.Id,
                                Name = "artifact-read",
                                ResourceTypes = new List<ResourceTypeEnum> { ResourceTypeEnum.Artifact },
                                OperationTypes = new List<OperationTypeEnum> { OperationTypeEnum.Read },
                                PermissionType = PermissionTypeEnum.Permit
                            }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = tenantA.Id, UserId = userA.Id, RoleId = role.Id }, ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenantA.Id, RoleId = role.Id, PermissionId = read.Id }, ct);

                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderToken, new TokenService(settings.Auth).IssueUserToken(tenantA.Id, userA.Id));

                            HttpResponseMessage foreignTenant = await client.GetAsync("/v1.0/tenants/" + tenantB.Id + "/artifacts/" + artifactB.Id, ct);
                            Assert2.Equal(HttpStatusCode.Forbidden, foreignTenant.StatusCode, "foreign tenant route rejected");

                            HttpResponseMessage foreignIdInOwnTenant = await client.GetAsync("/v1.0/tenants/" + tenantA.Id + "/artifacts/" + artifactB.Id, ct);
                            Assert2.Equal(HttpStatusCode.NotFound, foreignIdInOwnTenant.StatusCode, "foreign artifact id not found in own tenant");
                        }
                        finally
                        {
                            try { server?.Dispose(); } catch { }
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    })
#endif
                });
        }

        private static string Repeat(char c)
        {
            return new string(c, 64);
        }

        private static string Sha256Hex(byte[] data)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            char[] chars = new char[hash.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < hash.Length; i++)
            {
                chars[i * 2] = hex[hash[i] >> 4];
                chars[i * 2 + 1] = hex[hash[i] & 0xF];
            }
            return new string(chars);
        }

        private static string TempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "tempo-artifacts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

#if NET10_0
        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static T Deserialize<T>(string json)
        {
            T? value = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (value == null) throw new InvalidOperationException("Could not deserialize response as " + typeof(T).Name + ": " + json);
            return value;
        }
#endif
    }
}
