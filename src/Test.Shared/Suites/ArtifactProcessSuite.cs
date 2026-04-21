namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
#if NET10_0
    using SyslogLogging;
#endif
    using Tempo;
    using Tempo.Core;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database.Sqlite;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Runtime;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Tempo.Enums;
    using Tempo.Protocol;
#if NET10_0
    using Tempo.Server;
#endif
    using Touchstone.Core;
    using CoreTenant = Tempo.Core.Models.Tenant;

    public static class ArtifactProcessSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "ArtifactProcess",
                displayName: "Artifact process and Python runtimes",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ArtifactProcess", "ManifestValidation", "Artifact manifests require safe relative entrypoints and supported protocol versions", async _ =>
                    {
                        await Task.CompletedTask;
                        ArtifactManifest ok = ProcessManifest("success");
                        Assert2.Equal(0, ArtifactManifestService.Validate(ok).Count, "valid manifest");
                        ArtifactManifest dotnetOk = DotnetManifest("success");
                        Assert2.Equal(0, ArtifactManifestService.Validate(dotnetOk).Count, "valid dotnet manifest");
                        ArtifactManifest javaScriptOk = JavaScriptManifest();
                        Assert2.Equal(0, ArtifactManifestService.Validate(javaScriptOk).Count, "valid javascript manifest");

                        ok.Entrypoints["main"].Command = "../escape.exe";
                        IReadOnlyList<string> errors = ArtifactManifestService.Validate(ok);
                        Assert2.True(errors.Any(e => e.Contains("relative artifact path", StringComparison.OrdinalIgnoreCase)), "unsafe path rejected");

                        javaScriptOk.Entrypoints["main"].Module = "../escape.js";
                        errors = ArtifactManifestService.Validate(javaScriptOk);
                        Assert2.True(errors.Any(e => e.Contains("relative artifact path", StringComparison.OrdinalIgnoreCase)), "javascript unsafe module path rejected");

                        dotnetOk.Entrypoints["main"].HandlerType = "";
                        errors = ArtifactManifestService.Validate(dotnetOk);
                        Assert2.True(errors.Any(e => e.Contains("handlerType", StringComparison.OrdinalIgnoreCase)), "dotnet handlerType required");

                        ArtifactManifest unsupportedProtocol = ProcessManifest("success");
                        unsupportedProtocol.SupportedProtocolVersions = new List<string> { "9.9" };
                        errors = ArtifactManifestService.Validate(unsupportedProtocol);
                        Assert2.True(errors.Any(e => e.Contains("protocol", StringComparison.OrdinalIgnoreCase)), "unsupported protocol rejected");
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "ArchiveSafetyRejectsTraversal", "Artifact package cache rejects archive traversal entries", async ct =>
                    {
                        string root = TempDirectory("tempo-artifact-process-");
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "bad" }, ct);
                            byte[] zip = ZipWithEntries(new Dictionary<string, byte[]> { ["../escape.txt"] = Encoding.UTF8.GetBytes("bad") });
                            LocalFilesystemArtifactBlobStore store = new LocalFilesystemArtifactBlobStore(new ArtifactSettings { RootPath = Path.Combine(root, "artifacts") });
                            string sha = Sha256Hex(zip);
                            using MemoryStream ms = new MemoryStream(zip);
                            ArtifactBlobWriteResult write = await store.PutAsync(tenant.Id, sha, ms, zip.Length, ct);
                            ArtifactVersionRecord version = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id,
                                ArtifactId = artifact.Id,
                                Version = "1",
                                Sha256 = sha,
                                ByteLength = zip.Length,
                                StorageKey = write.StorageKey
                            }, ct);

                            ArtifactPackageCache cache = new ArtifactPackageCache(store, ExternalSettings(root));
                            bool rejected = false;
                            try { await cache.PrepareAsync(version, ct); }
                            catch (InvalidOperationException) { rejected = true; }
                            Assert2.True(rejected, "zip traversal rejected");

                            byte[] symlinkZip = ZipWithSymlinkEntry("link", "target");
                            string symlinkSha = Sha256Hex(symlinkZip);
                            using MemoryStream symlinkMs = new MemoryStream(symlinkZip);
                            ArtifactBlobWriteResult symlinkWrite = await store.PutAsync(tenant.Id, symlinkSha, symlinkMs, symlinkZip.Length, ct);
                            ArtifactVersionRecord symlinkVersion = await driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                            {
                                TenantId = tenant.Id,
                                ArtifactId = artifact.Id,
                                Version = "2",
                                Sha256 = symlinkSha,
                                ByteLength = symlinkZip.Length,
                                StorageKey = symlinkWrite.StorageKey
                            }, ct);
                            bool symlinkRejected = false;
                            try { await cache.PrepareAsync(symlinkVersion, ct); }
                            catch (InvalidOperationException) { symlinkRejected = true; }
                            Assert2.True(symlinkRejected, "zip symlink rejected");
                        }
                        finally
                        {
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "ProcessSuccessPersistsArtifactMetadata", "Artifact.Process executes a packaged protocol fixture and records artifact/capacity metadata", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreateProcessArtifactAsync(tenant.Id, "tool", "1", "success", ct);
                            StepResult result = await runtime.RunArtifactProcessStepAsync(tenant.Id, version.ArtifactId, "1", "success-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "process result");

                            List<StepRun> steps = await runtime.Driver.FlowRuns.EnumerateStepRunsAsync(tenant.Id, runtime.LastRunId!, ct);
                            Assert2.Equal(1, steps.Count, "one step run");
                            StepRun step = steps[0];
                            Assert2.Equal(version.ArtifactId, step.ArtifactId!, "artifact id recorded");
                            Assert2.Equal(version.Id, step.ArtifactVersionId!, "artifact version id recorded");
                            Assert2.Equal("1", step.ArtifactVersion!, "artifact version recorded");
                            Assert2.Equal(version.Sha256, step.ArtifactSha256!, "artifact sha recorded");
                            Assert2.Equal("main", step.ManifestEntrypoint!, "entrypoint recorded");
                            Assert2.NotNull(step.CapacityQueuedUtc, "capacity queued recorded");
                            Assert2.NotNull(step.CapacityAcquiredUtc, "capacity acquired recorded");
                            Assert2.NotNull(step.CapacityWaitMs, "capacity wait recorded");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "DotnetProcessSuccess", "Artifact.DotnetProcess executes a packaged SDK handler through dotnet", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreateDotnetArtifactAsync(tenant.Id, "dotnet-tool", "1", "success", ct);
                            StepResult result = await runtime.RunArtifactDotnetProcessStepAsync(tenant.Id, version.ArtifactId, "1", "dotnet-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "dotnet result");

                            string json = JsonSerializer.Serialize(result.Data);
                            Assert2.True(json.Contains("\"fixture\":true", StringComparison.Ordinal), "dotnet fixture data");
                            Assert2.True(json.Contains("\"protocolEnvironment\":\"" + ProtocolVersions.Current + "\"", StringComparison.Ordinal), "protocol env exposed");
                            Assert2.True(json.Contains("\"supportedProtocolEnvironment\":\"" + ProtocolVersions.Current + "\"", StringComparison.Ordinal), "supported protocol env exposed");
                            string metadataJson = JsonSerializer.Serialize(result.Metadata);
                            Assert2.True(metadataJson.Contains("\"sdk\":\"dotnet\"", StringComparison.Ordinal), "dotnet sdk metadata");
                            List<StepRun> steps = await runtime.Driver.FlowRuns.EnumerateStepRunsAsync(tenant.Id, runtime.LastRunId!, ct);
                            Assert2.Equal(version.Id, steps[0].ArtifactVersionId!, "dotnet artifact version recorded");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "ProcessFailuresMapToException", "Artifact.Process maps non-zero exit and invalid stdout to exception results", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord exitVersion = await runtime.CreateProcessArtifactAsync(tenant.Id, "exit-tool", "1", "exit", ct);
                            StepResult exit = await runtime.RunArtifactProcessStepAsync(tenant.Id, exitVersion.ArtifactId, "1", "exit-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Exception, exit.Result, "exit result");
                            Assert2.True((exit.ExceptionMessage ?? "").Contains("code 7", StringComparison.Ordinal), "exit code diagnostic");

                            ArtifactVersionRecord invalidVersion = await runtime.CreateProcessArtifactAsync(tenant.Id, "invalid-tool", "1", "invalid", ct);
                            StepResult invalid = await runtime.RunArtifactProcessStepAsync(tenant.Id, invalidVersion.ArtifactId, "1", "invalid-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Exception, invalid.Result, "invalid stdout result");
                            Assert2.True((invalid.ExceptionMessage ?? "").Contains("stdout", StringComparison.OrdinalIgnoreCase), "stdout diagnostic");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "TimeoutKillsProcess", "Artifact.Process enforces runtime timeout and records kill count", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct, maxRuntimeMs: 200);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreateProcessArtifactAsync(tenant.Id, "slow-tool", "1", "sleep", ct);
                            StepResult result = await runtime.RunArtifactProcessStepAsync(tenant.Id, version.ArtifactId, "1", "slow-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Timeout, result.Result, "timeout result");
                            Assert2.True(runtime.Capacity.Snapshot().ProcessKillCount >= 1, "process kill recorded");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "CancellationKillsProcess", "Artifact.Process kills the child process when execution is cancelled", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct, maxRuntimeMs: 5000);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreateProcessArtifactAsync(tenant.Id, "cancel-tool", "1", "sleep", ct);
                            (FlowRun run, DataFlowRecord flow) = await runtime.CreateArtifactFlowRunAsync(tenant.Id, version.ArtifactId, "1", "cancel-step", ct);
                            FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(runtime.Driver, run, flow, ct);
                            using CancellationTokenSource cancel = new CancellationTokenSource(200);
                            StepResult result = await runtime.RunExistingAsync(tenant.Id, flow, run, snapshot, cancel.Token);
                            Assert2.Equal(StepResultTypeEnum.Exception, result.Result, "cancellation result");
                            Assert2.True(runtime.Capacity.Snapshot().ProcessKillCount >= 1, "process kill recorded");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "SecretDiagnosticsAreRedacted", "Artifact.Process redacts allowed environment values from diagnostics", async ct =>
                    {
                        string secret = "super-secret-value-" + Guid.NewGuid().ToString("N");
                        Environment.SetEnvironmentVariable("TEMPO_TEST_SECRET", secret);
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            runtime.RuntimeSettings.ExternalExecution.EnvironmentAllowList.Add("TEMPO_TEST_SECRET");
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreateProcessArtifactAsync(tenant.Id, "secret-tool", "1", "secret", ct, new[] { "TEMPO_TEST_SECRET" });
                            StepResult result = await runtime.RunArtifactProcessStepAsync(tenant.Id, version.ArtifactId, "1", "secret-step", ct, envRefs: new[] { "TEMPO_TEST_SECRET" });
                            Assert2.Equal(StepResultTypeEnum.Exception, result.Result, "secret result");
                            string message = result.ExceptionMessage ?? "";
                            Assert2.True(message.Contains("[redacted]", StringComparison.Ordinal), "secret redacted");
                            Assert2.False(message.Contains(secret, StringComparison.Ordinal), "secret value absent");
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable("TEMPO_TEST_SECRET", null);
                            runtime.Dispose();
                        }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "LatestVersionPinnedAtRunStart", "Run-start snapshot pins latest artifact version before later uploads", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord v1 = await runtime.CreateProcessArtifactAsync(tenant.Id, "pinned-tool", "1", "success", ct);
                            (FlowRun run, DataFlowRecord flow) = await runtime.CreateArtifactFlowRunAsync(tenant.Id, v1.ArtifactId, "latest", "pinned-step", ct);
                            FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(runtime.Driver, run, flow, ct);
                            run.ExecutionSnapshotJson = FlowRunExecutionSnapshotSerializer.Serialize(snapshot);
                            await runtime.Driver.FlowRuns.UpdateAsync(run, ct);

                            await runtime.CreateProcessArtifactAsync(tenant.Id, "pinned-tool", "2", "success", ct, artifactId: v1.ArtifactId);
                            StepResult result = await runtime.RunExistingAsync(tenant.Id, flow, run, snapshot, ct);
                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "run result");
                            List<StepRun> steps = await runtime.Driver.FlowRuns.EnumerateStepRunsAsync(tenant.Id, run.Id, ct);
                            Assert2.Equal("1", steps[0].ArtifactVersion!, "pinned original version");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "PythonShimExecutesEnvelope", "Artifact.Python executes through the generated SDK-style envelope when Python is available", async ct =>
                    {
                        if (!await PythonAvailableAsync(ct)) return;
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreatePythonArtifactAsync(tenant.Id, "py-tool", "1", ct);
                            StepResult result = await runtime.RunArtifactPythonStepAsync(tenant.Id, version.ArtifactId, "1", "python-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "python result");
                            Assert2.Equal(version.Id, (await runtime.Driver.FlowRuns.EnumerateStepRunsAsync(tenant.Id, runtime.LastRunId!, ct))[0].ArtifactVersionId!, "python artifact version recorded");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "JavaScriptShimExecutesEnvelope", "Artifact.JavaScript executes through the generated Node.js SDK-style envelope when Node is available", async ct =>
                    {
                        if (!await NodeAvailableAsync(ct)) return;
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreateJavaScriptArtifactAsync(tenant.Id, "js-tool", "1", ct);
                            StepResult result = await runtime.RunArtifactJavaScriptStepAsync(tenant.Id, version.ArtifactId, "1", "javascript-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Success, result.Result, "javascript result");
                            string json = JsonSerializer.Serialize(result.Data);
                            Assert2.True(json.Contains("\"javascript\":true", StringComparison.Ordinal), "javascript fixture data");
                            Assert2.Equal(version.Id, (await runtime.Driver.FlowRuns.EnumerateStepRunsAsync(tenant.Id, runtime.LastRunId!, ct))[0].ArtifactVersionId!, "javascript artifact version recorded");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "SourceStepServiceCreatesRunnableSteps", "Pasted source files can be packaged into runnable Python, JavaScript, and C# artifact-backed steps", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            SourceStepPackageService service = new SourceStepPackageService(runtime.Driver, runtime.BlobStore);

                            if (await PythonAvailableAsync(ct))
                            {
                                await service.CreateAsync(tenant.Id, new SourceStepCreateRequest
                                {
                                    ExecutionKey = "source-python-step",
                                    Name = "Source Python",
                                    Language = "Python",
                                    FileName = "handler.py",
                                    Function = "run",
                                    Code = "def run(req):\n    return {\"source\": \"python\", \"value\": req.get(\"data\", {}).get(\"value\")}\n"
                                }, ct);
                                StepResult py = await runtime.RunExistingStepAsync(tenant.Id, "source-python-step", ct);
                                Assert2.Equal(StepResultTypeEnum.Success, py.Result, "source python result");
                            }

                            if (await NodeAvailableAsync(ct))
                            {
                                await service.CreateAsync(tenant.Id, new SourceStepCreateRequest
                                {
                                    ExecutionKey = "source-javascript-step",
                                    Name = "Source JavaScript",
                                    Language = "JavaScript",
                                    FileName = "handler.js",
                                    Function = "run",
                                    Code = "exports.run = async (req) => ({ source: \"javascript\", value: req.data.value });\n"
                                }, ct);
                                StepResult js = await runtime.RunExistingStepAsync(tenant.Id, "source-javascript-step", ct);
                                Assert2.Equal(StepResultTypeEnum.Success, js.Result, "source javascript result");
                            }

                            await service.CreateAsync(tenant.Id, new SourceStepCreateRequest
                            {
                                ExecutionKey = "source-csharp-step",
                                Name = "Source CSharp",
                                Language = "CSharp",
                                FileName = "Handler.cs",
                                HandlerType = "Tempo.UserSteps.Handler",
                                Code = "using System.Threading;\nusing System.Threading.Tasks;\nusing Tempo;\nusing Tempo.Protocol;\nnamespace Tempo.UserSteps;\npublic sealed class Handler : ITempoStepHandler\n{\n    public Task<StepResult> RunAsync(StepRequest request, CancellationToken token = default)\n    {\n        return Task.FromResult(TempoStepHost.Success(request, new { source = \"csharp\", value = 123 }));\n    }\n}\n"
                            }, ct);
                            StepResult cs = await runtime.RunExistingStepAsync(tenant.Id, "source-csharp-step", ct);
                            Assert2.Equal(StepResultTypeEnum.Success, cs.Result, "source csharp result");
                        }
                        finally { runtime.Dispose(); }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "PythonDependencyInstallPolicyFailure", "Artifact.Python refuses dependency installation unless the operator enables it", async ct =>
                    {
                        using TestRuntime runtime = await TestRuntime.CreateAsync(ct);
                        try
                        {
                            CoreTenant tenant = await runtime.Driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactVersionRecord version = await runtime.CreatePythonArtifactAsync(tenant.Id, "py-deps-tool", "1", ct, includeRequirements: true);
                            bool blocked = false;
                            try
                            {
                                await runtime.RunArtifactPythonStepAsync(tenant.Id, version.ArtifactId, "1", "python-deps-step", ct);
                            }
                            catch (InvalidOperationException ex) when (ex.Message.Contains("dependency installation is disabled", StringComparison.OrdinalIgnoreCase))
                            {
                                blocked = true;
                            }
                            Assert2.True(blocked, "dependency install blocked");
                        }
                        finally { runtime.Dispose(); }
                    })
#if NET10_0
                    ,
                    new TestCaseDescriptor("ArtifactProcess", "StepRouteRequiresArtifactRead", "Artifact-backed step writes require Step write permission plus Artifact read permission", async ct =>
                    {
                        string root = TempDirectory("tempo-artifact-route-auth-");
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.SigningKey = "artifact-route-auth-signing-key-0123456789";
                            settings.RequestHistory.Enabled = false;
                            settings.Artifacts.RootPath = Path.Combine(root, "artifacts");

                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            Tempo.Core.Models.User user = await driver.Users.CreateAsync(new Tempo.Core.Models.User
                            {
                                TenantId = tenant.Id,
                                Email = "artifact-step-writer@example.com"
                            }, ct);
                            Role role = await driver.Roles.CreateAsync(new Role { TenantId = tenant.Id, Name = "step-writer" }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = tenant.Id, UserId = user.Id, RoleId = role.Id }, ct);
                            Permission stepCreate = await driver.Permissions.CreateAsync(PermissionFor(tenant.Id, "step-create", ResourceTypeEnum.Step, OperationTypeEnum.Create), ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenant.Id, RoleId = role.Id, PermissionId = stepCreate.Id }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "auth-tool" }, ct);

                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderToken, new TokenService(settings.Auth).IssueUserToken(tenant.Id, user.Id));

                            string body = ArtifactStepCreateJson("auth-step", artifact.Id);
                            HttpResponseMessage forbidden = await client.PostAsync(
                                "/v1.0/tenants/" + tenant.Id + "/steps",
                                new StringContent(body, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode, "artifact read required");

                            Permission artifactRead = await driver.Permissions.CreateAsync(PermissionFor(tenant.Id, "artifact-read", ResourceTypeEnum.Artifact, OperationTypeEnum.Read), ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenant.Id, RoleId = role.Id, PermissionId = artifactRead.Id }, ct);

                            HttpResponseMessage created = await client.PostAsync(
                                "/v1.0/tenants/" + tenant.Id + "/steps",
                                new StringContent(body, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.Created, created.StatusCode, "step created after artifact read grant");
                        }
                        finally
                        {
                            try { server?.Dispose(); } catch { }
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "StepRouteRejectsCrossTenantArtifactReference", "Artifact-backed step writes cannot reference another tenant's artifact", async ct =>
                    {
                        string root = TempDirectory("tempo-artifact-cross-tenant-step-");
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.SigningKey = "artifact-cross-tenant-signing-key-0123456789";
                            settings.RequestHistory.Enabled = false;
                            settings.Artifacts.RootPath = Path.Combine(root, "artifacts");

                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            CoreTenant tenantA = await driver.Tenants.CreateAsync(new CoreTenant { Name = "A" }, ct);
                            CoreTenant tenantB = await driver.Tenants.CreateAsync(new CoreTenant { Name = "B" }, ct);
                            ArtifactRecord tenantBArtifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantB.Id, Name = "foreign-tool" }, ct);
                            Tempo.Core.Models.User user = await driver.Users.CreateAsync(new Tempo.Core.Models.User
                            {
                                TenantId = tenantA.Id,
                                Email = "cross-tenant-step-writer@example.com"
                            }, ct);
                            Role role = await driver.Roles.CreateAsync(new Role { TenantId = tenantA.Id, Name = "artifact-step-writer" }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = tenantA.Id, UserId = user.Id, RoleId = role.Id }, ct);
                            Permission stepCreate = await driver.Permissions.CreateAsync(PermissionFor(tenantA.Id, "step-create", ResourceTypeEnum.Step, OperationTypeEnum.Create), ct);
                            Permission artifactRead = await driver.Permissions.CreateAsync(PermissionFor(tenantA.Id, "artifact-read", ResourceTypeEnum.Artifact, OperationTypeEnum.Read), ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenantA.Id, RoleId = role.Id, PermissionId = stepCreate.Id }, ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenantA.Id, RoleId = role.Id, PermissionId = artifactRead.Id }, ct);

                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderToken, new TokenService(settings.Auth).IssueUserToken(tenantA.Id, user.Id));

                            HttpResponseMessage response = await client.PostAsync(
                                "/v1.0/tenants/" + tenantA.Id + "/steps",
                                new StringContent(ArtifactStepCreateJson("cross-tenant-artifact", tenantBArtifact.Id), Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.BadRequest, response.StatusCode, "foreign tenant artifact rejected");
                            StepRecord? step = await driver.Steps.ReadByExecutionKeyAsync(tenantA.Id, "cross-tenant-artifact", ct);
                            Assert2.IsNull(step, "cross-tenant artifact step not persisted");
                        }
                        finally
                        {
                            try { server?.Dispose(); } catch { }
                            await TempTestStore.DisposeAsync(driver);
                            DeleteDirectory(root);
                        }
                    }),
                    new TestCaseDescriptor("ArtifactProcess", "RuntimeValidationRequiresArtifactRead", "Artifact runtime validation requires Step update plus Artifact read permission", async ct =>
                    {
                        string root = TempDirectory("tempo-artifact-runtime-validate-auth-");
                        SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(ct);
                        TempoServer? server = null;
                        try
                        {
                            int port = FreePort();
                            Settings settings = new Settings();
                            settings.Rest.Port = port;
                            settings.Rest.Hostname = "127.0.0.1";
                            settings.Auth.SigningKey = "artifact-runtime-validate-auth-signing-key-0123456789";
                            settings.RequestHistory.Enabled = false;
                            settings.Artifacts.RootPath = Path.Combine(root, "artifacts");

                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            server = new TempoServer(settings, logging, driver, new Tempo.StepManager());
                            await server.StartAsync();

                            CoreTenant tenant = await driver.Tenants.CreateAsync(new CoreTenant { Name = "T" }, ct);
                            ArtifactRecord artifact = await driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenant.Id, Name = "validate-tool" }, ct);
                            Tempo.Core.Models.User user = await driver.Users.CreateAsync(new Tempo.Core.Models.User
                            {
                                TenantId = tenant.Id,
                                Email = "runtime-validator@example.com"
                            }, ct);
                            Role role = await driver.Roles.CreateAsync(new Role { TenantId = tenant.Id, Name = "runtime-validator" }, ct);
                            await driver.UserRoleMaps.CreateAsync(new UserRoleMap { TenantId = tenant.Id, UserId = user.Id, RoleId = role.Id }, ct);
                            Permission stepUpdate = await driver.Permissions.CreateAsync(PermissionFor(tenant.Id, "step-update", ResourceTypeEnum.Step, OperationTypeEnum.Update), ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenant.Id, RoleId = role.Id, PermissionId = stepUpdate.Id }, ct);

                            using HttpClient client = new HttpClient();
                            client.BaseAddress = new Uri("http://127.0.0.1:" + port);
                            client.DefaultRequestHeaders.Add(Constants.HeaderToken, new TokenService(settings.Auth).IssueUserToken(tenant.Id, user.Id));

                            string body = RuntimeValidationJson(artifact.Id);
                            HttpResponseMessage forbidden = await client.PostAsync(
                                "/v1.0/tenants/" + tenant.Id + "/runtimes/validate",
                                new StringContent(body, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode, "artifact read required for validation");

                            Permission artifactRead = await driver.Permissions.CreateAsync(PermissionFor(tenant.Id, "artifact-read", ResourceTypeEnum.Artifact, OperationTypeEnum.Read), ct);
                            await driver.RolePermissionMaps.CreateAsync(new RolePermissionMap { TenantId = tenant.Id, RoleId = role.Id, PermissionId = artifactRead.Id }, ct);

                            HttpResponseMessage ok = await client.PostAsync(
                                "/v1.0/tenants/" + tenant.Id + "/runtimes/validate",
                                new StringContent(body, Encoding.UTF8, "application/json"), ct);
                            Assert2.Equal(HttpStatusCode.OK, ok.StatusCode, "validation permitted after artifact read grant");
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

        private sealed class TestRuntime : IDisposable
        {
            private readonly string _Root;
            public SqliteDatabaseDriver Driver { get; }
            public LocalFilesystemArtifactBlobStore BlobStore { get; }
            public RuntimeSettings RuntimeSettings { get; }
            public ExternalRuntimeCapacityManager Capacity { get; }
            public string? LastRunId { get; private set; }

            private TestRuntime(string root, SqliteDatabaseDriver driver, LocalFilesystemArtifactBlobStore blobStore, RuntimeSettings runtimeSettings, ExternalRuntimeCapacityManager capacity)
            {
                _Root = root;
                Driver = driver;
                BlobStore = blobStore;
                RuntimeSettings = runtimeSettings;
                Capacity = capacity;
            }

            public static async Task<TestRuntime> CreateAsync(CancellationToken token, int maxRuntimeMs = 5000)
            {
                string root = TempDirectory("tempo-artifact-runtime-");
                SqliteDatabaseDriver driver = await TempTestStore.CreateAsync(token);
                LocalFilesystemArtifactBlobStore blobStore = new LocalFilesystemArtifactBlobStore(new ArtifactSettings { RootPath = Path.Combine(root, "artifacts"), MaxUploadBytes = 100 * 1024 * 1024, MaxBytesPerTenant = 100 * 1024 * 1024 });
                RuntimeSettings runtimeSettings = new RuntimeSettings();
                runtimeSettings.ExternalExecution.CacheRoot = Path.Combine(root, "cache");
                runtimeSettings.ExternalExecution.ScratchRoot = Path.Combine(root, "scratch");
                runtimeSettings.ExternalExecution.DefaultMaxRuntimeMs = maxRuntimeMs;
                runtimeSettings.ExternalExecution.MaxConcurrentProcessesServerWide = 2;
                runtimeSettings.ExternalExecution.MaxConcurrentProcessesPerTenant = 1;
                ExternalRuntimeCapacityManager capacity = new ExternalRuntimeCapacityManager(runtimeSettings.ExternalExecution);
                return new TestRuntime(root, driver, blobStore, runtimeSettings, capacity);
            }

            public async Task<ArtifactVersionRecord> CreateProcessArtifactAsync(string tenantId, string artifactName, string versionLabel, string mode, CancellationToken token, IEnumerable<string>? envAllow = null, string? artifactId = null)
            {
                ArtifactRecord artifact = string.IsNullOrWhiteSpace(artifactId)
                    ? await Driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantId, Name = artifactName }, token)
                    : (await Driver.Artifacts.ReadAsync(tenantId, artifactId!, token))!;
                ArtifactManifest manifest = ProcessManifest(mode, envAllow);
                return await CreateVersionAsync(tenantId, artifact.Id, versionLabel, BuildProcessPackage(manifest), manifest, token);
            }

            public async Task<ArtifactVersionRecord> CreateDotnetArtifactAsync(string tenantId, string artifactName, string versionLabel, string mode, CancellationToken token)
            {
                ArtifactRecord artifact = await Driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantId, Name = artifactName }, token);
                ArtifactManifest manifest = DotnetManifest(mode);
                return await CreateVersionAsync(tenantId, artifact.Id, versionLabel, BuildProcessPackage(manifest), manifest, token);
            }

            public async Task<ArtifactVersionRecord> CreatePythonArtifactAsync(string tenantId, string artifactName, string versionLabel, CancellationToken token, bool includeRequirements = false)
            {
                ArtifactRecord artifact = await Driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantId, Name = artifactName }, token);
                ArtifactManifest manifest = PythonManifest();
                Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
                {
                    ["tempo.step.json"] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                    ["handler.py"] = Encoding.UTF8.GetBytes("def run(req):\n    return {\"python\": True, \"flowRunId\": req.get(\"flowRunId\")}\n")
                };
                if (includeRequirements) files["requirements.txt"] = Encoding.UTF8.GetBytes("definitely-not-installed-tempo-test-package==0.0.1\n");
                return await CreateVersionAsync(tenantId, artifact.Id, versionLabel, ZipWithEntries(files), manifest, token);
            }

            public async Task<ArtifactVersionRecord> CreateJavaScriptArtifactAsync(string tenantId, string artifactName, string versionLabel, CancellationToken token)
            {
                ArtifactRecord artifact = await Driver.Artifacts.CreateAsync(new ArtifactRecord { TenantId = tenantId, Name = artifactName }, token);
                ArtifactManifest manifest = JavaScriptManifest();
                Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
                {
                    ["tempo.step.json"] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                    ["handler.js"] = Encoding.UTF8.GetBytes("exports.run = async function(req) { return { javascript: true, flowRunId: req.flowRunId }; };\n")
                };
                return await CreateVersionAsync(tenantId, artifact.Id, versionLabel, ZipWithEntries(files), manifest, token);
            }

            public async Task<StepResult> RunArtifactProcessStepAsync(string tenantId, string artifactId, string version, string executionKey, CancellationToken token, IEnumerable<string>? envRefs = null)
            {
                (FlowRun run, DataFlowRecord flow) = await CreateArtifactFlowRunAsync(tenantId, artifactId, version, executionKey, token, envRefs);
                FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(Driver, run, flow, token);
                return await RunExistingAsync(tenantId, flow, run, snapshot, token);
            }

            public async Task<StepResult> RunArtifactPythonStepAsync(string tenantId, string artifactId, string version, string executionKey, CancellationToken token)
            {
                DataFlowRecord flow = await Driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenantId,
                    Name = "flow-" + executionKey,
                    StartStepId = executionKey,
                    Transitions = new Dictionary<string, StepTransition> { [executionKey] = new StepTransition() }
                }, token);
                await Driver.Steps.CreateAsync(new StepRecord
                {
                    TenantId = tenantId,
                    ExecutionKey = executionKey,
                    Name = executionKey,
                    RuntimeConfig = new ArtifactPythonRuntimeConfig { ArtifactId = artifactId, ArtifactVersion = version }
                }, token);
                FlowRun run = await Driver.FlowRuns.CreateAsync(new FlowRun { TenantId = tenantId, DataFlowId = flow.Id }, token);
                FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(Driver, run, flow, token);
                return await RunExistingAsync(tenantId, flow, run, snapshot, token);
            }

            public async Task<StepResult> RunArtifactJavaScriptStepAsync(string tenantId, string artifactId, string version, string executionKey, CancellationToken token)
            {
                DataFlowRecord flow = await Driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenantId,
                    Name = "flow-" + executionKey,
                    StartStepId = executionKey,
                    Transitions = new Dictionary<string, StepTransition> { [executionKey] = new StepTransition() }
                }, token);
                await Driver.Steps.CreateAsync(new StepRecord
                {
                    TenantId = tenantId,
                    ExecutionKey = executionKey,
                    Name = executionKey,
                    RuntimeConfig = new ArtifactJavaScriptRuntimeConfig { ArtifactId = artifactId, ArtifactVersion = version }
                }, token);
                FlowRun run = await Driver.FlowRuns.CreateAsync(new FlowRun { TenantId = tenantId, DataFlowId = flow.Id }, token);
                FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(Driver, run, flow, token);
                return await RunExistingAsync(tenantId, flow, run, snapshot, token);
            }

            public async Task<StepResult> RunArtifactDotnetProcessStepAsync(string tenantId, string artifactId, string version, string executionKey, CancellationToken token)
            {
                DataFlowRecord flow = await Driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenantId,
                    Name = "flow-" + executionKey,
                    StartStepId = executionKey,
                    Transitions = new Dictionary<string, StepTransition> { [executionKey] = new StepTransition() }
                }, token);
                await Driver.Steps.CreateAsync(new StepRecord
                {
                    TenantId = tenantId,
                    ExecutionKey = executionKey,
                    Name = executionKey,
                    RuntimeConfig = new ArtifactDotnetProcessRuntimeConfig { ArtifactId = artifactId, ArtifactVersion = version }
                }, token);
                FlowRun run = await Driver.FlowRuns.CreateAsync(new FlowRun { TenantId = tenantId, DataFlowId = flow.Id }, token);
                FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(Driver, run, flow, token);
                return await RunExistingAsync(tenantId, flow, run, snapshot, token);
            }

            public async Task<StepResult> RunExistingStepAsync(string tenantId, string executionKey, CancellationToken token)
            {
                DataFlowRecord flow = await Driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenantId,
                    Name = "flow-" + executionKey,
                    StartStepId = executionKey,
                    Transitions = new Dictionary<string, StepTransition> { [executionKey] = new StepTransition() }
                }, token);
                FlowRun run = await Driver.FlowRuns.CreateAsync(new FlowRun { TenantId = tenantId, DataFlowId = flow.Id }, token);
                FlowRunExecutionSnapshot snapshot = await FlowRunSnapshotBuilder.BuildAsync(Driver, run, flow, token);
                return await RunExistingAsync(tenantId, flow, run, snapshot, token);
            }

            public async Task<(FlowRun Run, DataFlowRecord Flow)> CreateArtifactFlowRunAsync(string tenantId, string artifactId, string version, string executionKey, CancellationToken token, IEnumerable<string>? envRefs = null)
            {
                DataFlowRecord flow = await Driver.DataFlows.CreateAsync(new DataFlowRecord
                {
                    TenantId = tenantId,
                    Name = "flow-" + executionKey,
                    StartStepId = executionKey,
                    Transitions = new Dictionary<string, StepTransition> { [executionKey] = new StepTransition() }
                }, token);
                await Driver.Steps.CreateAsync(new StepRecord
                {
                    TenantId = tenantId,
                    ExecutionKey = executionKey,
                    Name = executionKey,
                    RuntimeConfig = new ArtifactProcessRuntimeConfig { ArtifactId = artifactId, ArtifactVersion = version, EnvironmentReferences = (envRefs ?? Array.Empty<string>()).ToList() }
                }, token);
                FlowRun run = await Driver.FlowRuns.CreateAsync(new FlowRun { TenantId = tenantId, DataFlowId = flow.Id }, token);
                LastRunId = run.Id;
                return (run, flow);
            }

            public async Task<StepResult> RunExistingAsync(string tenantId, DataFlowRecord flow, FlowRun run, FlowRunExecutionSnapshot snapshot, CancellationToken token)
            {
                StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(new StepManager(), runtimes: RuntimeSettings, database: Driver, artifactBlobStore: BlobStore, externalCapacity: Capacity);
                RegistryDataFlowRunner runner = new RegistryDataFlowRunner(new DatabaseStepExecutionResolver(Driver), registry)
                {
                    MetricsStore = new FlowMetricsBridge(Driver, run.Id, tenantId)
                };
                StepResult result = await runner.Run(FlowDispatchService.Hydrate(flow), new StepRequest
                {
                    TenantId = tenantId,
                    DataFlowId = flow.Id,
                    FlowRunId = run.Id,
                    RequestId = run.Id,
                    Data = new Dictionary<string, object> { ["value"] = 123 }
                }, snapshot, token);
                LastRunId = run.Id;
                return result;
            }

            private async Task<ArtifactVersionRecord> CreateVersionAsync(string tenantId, string artifactId, string versionLabel, byte[] package, ArtifactManifest manifest, CancellationToken token)
            {
                string sha = Sha256Hex(package);
                using MemoryStream ms = new MemoryStream(package);
                ArtifactBlobWriteResult write = await BlobStore.PutAsync(tenantId, sha, ms, package.Length, token);
                return await Driver.ArtifactVersions.CreateAsync(new ArtifactVersionRecord
                {
                    TenantId = tenantId,
                    ArtifactId = artifactId,
                    Version = versionLabel,
                    Sha256 = sha,
                    ByteLength = package.Length,
                    StorageKey = write.StorageKey,
                    ManifestJson = ArtifactManifestService.Serialize(manifest)
                }, token);
            }

            public void Dispose()
            {
                try { TempTestStore.DisposeAsync(Driver).GetAwaiter().GetResult(); } catch { }
                DeleteDirectory(_Root);
            }
        }

        private static ArtifactManifest ProcessManifest(string mode, IEnumerable<string>? envAllow = null)
        {
            ArtifactManifest manifest = new ArtifactManifest
            {
                ManifestVersion = "1",
                RuntimeKey = StepRuntimeKeys.ArtifactProcess.ToString(),
                SupportedProtocolVersions = new List<string> { "1.0" },
                DefaultEntrypoint = "main",
                InputSchema = "{}",
                OutputSchema = "{}",
                EnvironmentAllowList = (envAllow ?? Array.Empty<string>()).ToList()
            };
            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Command = "fixture/Test.ArtifactFixture.dll",
                Args = mode == "success" ? new List<string>() : new List<string> { mode },
                EnvironmentAllowList = (envAllow ?? Array.Empty<string>()).ToList(),
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };
            return manifest;
        }

        private static ArtifactManifest DotnetManifest(string mode, IEnumerable<string>? envAllow = null)
        {
            ArtifactManifest manifest = new ArtifactManifest
            {
                ManifestVersion = "1",
                RuntimeKey = StepRuntimeKeys.ArtifactDotnetProcess.ToString(),
                SupportedProtocolVersions = new List<string> { "1.0" },
                DefaultEntrypoint = "main",
                InputSchema = "{}",
                OutputSchema = "{}",
                EnvironmentAllowList = (envAllow ?? Array.Empty<string>()).ToList()
            };
            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Command = "fixture/Test.ArtifactFixture.dll",
                HandlerType = "Test.ArtifactFixture.Program+FixtureHandler",
                Args = mode == "success" ? new List<string>() : new List<string> { mode },
                EnvironmentAllowList = (envAllow ?? Array.Empty<string>()).ToList(),
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };
            return manifest;
        }

        private static ArtifactManifest PythonManifest()
        {
            ArtifactManifest manifest = new ArtifactManifest
            {
                ManifestVersion = "1",
                RuntimeKey = StepRuntimeKeys.ArtifactPython.ToString(),
                SupportedProtocolVersions = new List<string> { "1.0" },
                DefaultEntrypoint = "main",
                InputSchema = "{}",
                OutputSchema = "{}"
            };
            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Module = "handler",
                Function = "run",
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };
            return manifest;
        }

        private static ArtifactManifest JavaScriptManifest()
        {
            ArtifactManifest manifest = new ArtifactManifest
            {
                ManifestVersion = "1",
                RuntimeKey = StepRuntimeKeys.ArtifactJavaScript.ToString(),
                SupportedProtocolVersions = new List<string> { "1.0" },
                DefaultEntrypoint = "main",
                InputSchema = "{}",
                OutputSchema = "{}"
            };
            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Module = "handler.js",
                Function = "run",
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };
            return manifest;
        }

        private static byte[] BuildProcessPackage(ArtifactManifest manifest)
        {
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
            {
                ["tempo.step.json"] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest))
            };
            string fixtureDir = FixtureDirectory();
            foreach (string file in Directory.EnumerateFiles(fixtureDir, "*", SearchOption.TopDirectoryOnly))
            {
                files["fixture/" + Path.GetFileName(file)] = File.ReadAllBytes(file);
            }
            return ZipWithEntries(files);
        }

        private static string FixtureDirectory()
        {
            const string tfm = "net10.0";
            string? dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++, dir = Directory.GetParent(dir)?.FullName)
            {
                string candidate = Path.Combine(dir, "src", "Test.ArtifactFixture", "bin", "Debug", tfm);
                if (File.Exists(Path.Combine(candidate, "Test.ArtifactFixture.dll"))) return candidate;
            }
            dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++, dir = Directory.GetParent(dir)?.FullName)
            {
                string candidate = Path.Combine(dir, "..", "..", "..", "..", "Test.ArtifactFixture", "bin", "Debug", tfm);
                candidate = Path.GetFullPath(candidate);
                if (File.Exists(Path.Combine(candidate, "Test.ArtifactFixture.dll"))) return candidate;
            }
            throw new FileNotFoundException("Test.ArtifactFixture.dll was not built for " + tfm + ".");
        }

        private static byte[] ZipWithEntries(Dictionary<string, byte[]> files)
        {
            using MemoryStream ms = new MemoryStream();
            using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (KeyValuePair<string, byte[]> file in files)
                {
                    ZipArchiveEntry entry = zip.CreateEntry(file.Key);
                    using Stream output = entry.Open();
                    output.Write(file.Value, 0, file.Value.Length);
                }
            }
            return ms.ToArray();
        }

        private static byte[] ZipWithSymlinkEntry(string name, string target)
        {
            using MemoryStream ms = new MemoryStream();
            using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry entry = zip.CreateEntry(name);
                entry.ExternalAttributes = unchecked((int)(0xA1FF << 16));
                byte[] data = Encoding.UTF8.GetBytes(target);
                using Stream output = entry.Open();
                output.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        private static async Task<bool> PythonAvailableAsync(CancellationToken token)
        {
            try
            {
                using System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.StartInfo.ArgumentList.Add("--version");
                if (!process.Start()) return false;
                await process.WaitForExitAsync(token);
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private static async Task<bool> NodeAvailableAsync(CancellationToken token)
        {
            try
            {
                using System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "node",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                process.StartInfo.ArgumentList.Add("--version");
                if (!process.Start()) return false;
                await process.WaitForExitAsync(token);
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private static ExternalExecutionSettings ExternalSettings(string root)
        {
            return new ExternalExecutionSettings
            {
                CacheRoot = Path.Combine(root, "cache"),
                ScratchRoot = Path.Combine(root, "scratch")
            };
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

        private static string TempDirectory(string prefix)
        {
            string path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

#if NET10_0
        private static Permission PermissionFor(string tenantId, string name, ResourceTypeEnum resource, OperationTypeEnum operation)
        {
            return new Permission
            {
                TenantId = tenantId,
                Name = name,
                ResourceTypes = new List<ResourceTypeEnum> { resource },
                OperationTypes = new List<OperationTypeEnum> { operation },
                PermissionType = PermissionTypeEnum.Permit
            };
        }

        private static string ArtifactStepCreateJson(string executionKey, string artifactId)
        {
            var body = new
            {
                executionKey,
                name = executionKey,
                runtimeKey = StepRuntimeKeys.ArtifactProcess.ToString(),
                runtimeConfig = new
                {
                    runtimeKey = StepRuntimeKeys.ArtifactProcess.ToString(),
                    artifactId,
                    artifactVersion = "latest"
                }
            };
            return JsonSerializer.Serialize(body);
        }

        private static string RuntimeValidationJson(string artifactId)
        {
            var body = new
            {
                runtimeKey = StepRuntimeKeys.ArtifactProcess.ToString(),
                config = new
                {
                    runtimeKey = StepRuntimeKeys.ArtifactProcess.ToString(),
                    artifactId,
                    artifactVersion = "latest"
                }
            };
            return JsonSerializer.Serialize(body);
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
#endif
    }
}
