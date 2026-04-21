namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;

    /// <summary>Ensures a fresh tenant has one representative step for each startup runtime type.</summary>
    public class DefaultRuntimeStepSeeder
    {
        public const string BuiltinClassExecutionKey = "tempo.sample.builtin.class";
        public const string BuiltinMethodExecutionKey = "tempo.sample.builtin.method";
        public const string BuiltinUnknownExecutionKey = "tempo.sample.builtin.unknown";
        public const string ExternalRestExecutionKey = "tempo.sample.external.rest";
        public const string LegacyInlineRestExecutionKey = "tempo.sample.legacy.inline_rest";
        public const string ArtifactProcessExecutionKey = "tempo.sample.artifact.process";
        public const string ArtifactPythonExecutionKey = "tempo.sample.artifact.python";
        public const string ArtifactJavaScriptExecutionKey = "tempo.sample.artifact.javascript";
        public const string ArtifactDotnetProcessExecutionKey = "tempo.sample.artifact.dotnet_process";
        public const string HostExecutableExecutionKey = "tempo.sample.host.executable";

        private readonly DatabaseDriverBase _Database;
        private readonly ArtifactSettings _ArtifactSettings;
        private readonly RuntimeSettings _RuntimeSettings;
        private readonly StepManager? _StepManager;
        private readonly IArtifactBlobStore _BlobStore;
        private readonly RestSettings? _RestSettings;

        /// <summary>Instantiate.</summary>
        public DefaultRuntimeStepSeeder(
            DatabaseDriverBase database,
            ArtifactSettings artifactSettings,
            RuntimeSettings runtimeSettings,
            StepManager? stepManager = null,
            IArtifactBlobStore? blobStore = null,
            RestSettings? restSettings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _ArtifactSettings = artifactSettings ?? throw new ArgumentNullException(nameof(artifactSettings));
            _RuntimeSettings = runtimeSettings ?? throw new ArgumentNullException(nameof(runtimeSettings));
            _StepManager = stepManager;
            _BlobStore = blobStore ?? new LocalFilesystemArtifactBlobStore(_ArtifactSettings);
            _RestSettings = restSettings;
        }

        /// <summary>Idempotently seed startup runtime examples for a tenant.</summary>
        public async Task<DefaultRuntimeStepSeedResult> EnsureAsync(string tenantId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            DefaultRuntimeStepSeedResult result = new DefaultRuntimeStepSeedResult();
            StepRuntimeRegistry registry = StepRuntimeRegistry.CreateDefault(_StepManager, runtimes: _RuntimeSettings, database: _Database, artifactBlobStore: _BlobStore);
            HashSet<RuntimeKey> enabled = registry.DescribeAll()
                .Where(d => d.Availability == StepRuntimeAvailabilityStateEnum.Available)
                .Select(d => d.RuntimeKey)
                .ToHashSet();

            if (enabled.Contains(StepRuntimeKeys.BuiltinClass))
                await EnsureBuiltinClassStepAsync(tenantId, result, token).ConfigureAwait(false);
            if (enabled.Contains(StepRuntimeKeys.BuiltinMethod))
                await EnsureBuiltinMethodStepAsync(tenantId, result, token).ConfigureAwait(false);
            if (enabled.Contains(StepRuntimeKeys.ExternalRest))
                await EnsureStepAsync(tenantId, ExternalRestExecutionKey, ExternalRestStep(tenantId), result, token).ConfigureAwait(false);
            if (enabled.Contains(StepRuntimeKeys.ArtifactProcess))
                await EnsureArtifactProcessStepAsync(tenantId, result, token).ConfigureAwait(false);
            if (enabled.Contains(StepRuntimeKeys.ArtifactPython))
                await EnsureArtifactPythonStepAsync(tenantId, result, token).ConfigureAwait(false);
            if (enabled.Contains(StepRuntimeKeys.ArtifactJavaScript))
                await EnsureArtifactJavaScriptStepAsync(tenantId, result, token).ConfigureAwait(false);
            if (enabled.Contains(StepRuntimeKeys.ArtifactDotnetProcess))
                await EnsureArtifactDotnetProcessStepAsync(tenantId, result, token).ConfigureAwait(false);
            if (enabled.Contains(StepRuntimeKeys.HostExecutable))
                await EnsureHostExecutableStepAsync(tenantId, result, token).ConfigureAwait(false);

            await RemoveRetiredCompatibilityTemplateAsync(tenantId, BuiltinUnknownExecutionKey, result, token).ConfigureAwait(false);
            await RemoveRetiredCompatibilityTemplateAsync(tenantId, LegacyInlineRestExecutionKey, result, token).ConfigureAwait(false);

            return result;
        }

        private async Task EnsureBuiltinClassStepAsync(string tenantId, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            BuiltinStepRegistration? registration = SelectRegistration(BuiltinStepSourceKind.Class, tenantId, BuiltinClassExecutionKey);
            if (registration == null)
            {
                result.Notes.Add("Builtin.Class sample was skipped because no class step is registered in the startup StepManager");
                return;
            }

            StepRecord step = new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = BuiltinClassExecutionKey,
                Name = "Sample built-in class",
                Description = "Startup sample for the Builtin.Class runtime.",
                RuntimeKey = StepRuntimeKeys.BuiltinClass,
                RuntimeConfig = new BuiltinClassRuntimeConfig
                {
                    Identifier = registration.ExecutionKey,
                    TypeName = registration.DeclaringType,
                    AssemblyName = registration.AssemblyName,
                    AssemblyVersion = registration.AssemblyVersion,
                    SignatureHash = registration.SignatureHash
                },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                MaxRuntimeMs = registration.MaxRuntimeMs,
                Active = true,
                IsProtected = true
            };
            await EnsureStepAsync(tenantId, BuiltinClassExecutionKey, step, result, token).ConfigureAwait(false);
        }

        private async Task EnsureBuiltinMethodStepAsync(string tenantId, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            BuiltinStepRegistration? registration = SelectRegistration(BuiltinStepSourceKind.Method, tenantId, BuiltinMethodExecutionKey);
            if (registration == null)
            {
                result.Notes.Add("Builtin.Method sample was skipped because no StepMethod registration is available in the startup StepManager");
                return;
            }

            StepRecord step = new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = BuiltinMethodExecutionKey,
                Name = "Sample built-in method",
                Description = "Startup sample for the Builtin.Method runtime.",
                RuntimeKey = StepRuntimeKeys.BuiltinMethod,
                RuntimeConfig = new BuiltinMethodRuntimeConfig
                {
                    Identifier = registration.ExecutionKey,
                    DeclaringType = registration.DeclaringType,
                    MethodName = registration.MethodName,
                    AssemblyName = registration.AssemblyName,
                    AssemblyVersion = registration.AssemblyVersion,
                    SignatureHash = registration.SignatureHash
                },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                MaxRuntimeMs = registration.MaxRuntimeMs,
                Active = true,
                IsProtected = true
            };
            await EnsureStepAsync(tenantId, BuiltinMethodExecutionKey, step, result, token).ConfigureAwait(false);
        }

        private BuiltinStepRegistration? SelectRegistration(BuiltinStepSourceKind sourceKind, string tenantId, string preferredExecutionKey)
        {
            if (_StepManager == null) return null;
            List<BuiltinStepRegistration> registrations = _StepManager.Registrations(tenantId: tenantId)
                .Where(r => r.SourceKind == sourceKind)
                .ToList();
            return registrations.FirstOrDefault(r => string.Equals(r.ExecutionKey, preferredExecutionKey, StringComparison.Ordinal)) ??
                registrations.FirstOrDefault(r => r.IsGlobal) ??
                registrations.FirstOrDefault();
        }

        private async Task EnsureArtifactProcessStepAsync(string tenantId, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            ArtifactManifest manifest = NewManifest(StepRuntimeKeys.ArtifactProcess);
            string command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tempo-sample.cmd" : "tempo-sample.sh";
            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Command = command,
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };

            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
            {
                [ArtifactManifestService.ManifestFileName] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                ["tempo-sample.cmd"] = Encoding.UTF8.GetBytes("@echo off\r\necho {\"protocolVersion\":\"1.0\",\"result\":\"Success\",\"data\":{\"sample\":\"artifact-process\"}}\r\n"),
                ["tempo-sample.sh"] = Encoding.UTF8.GetBytes("#!/bin/sh\nprintf '%s\\n' '{\"protocolVersion\":\"1.0\",\"result\":\"Success\",\"data\":{\"sample\":\"artifact-process\"}}'\n")
            };
            ArtifactRecord artifact = await EnsureArtifactAsync(tenantId, "Sample Artifact.Process", "Startup sample package for Artifact.Process.", result, token).ConfigureAwait(false);
            ArtifactVersionRecord version = await EnsureArtifactVersionAsync(tenantId, artifact.Id, files, result, token).ConfigureAwait(false);
            await EnsureStepAsync(tenantId, ArtifactProcessExecutionKey, new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = ArtifactProcessExecutionKey,
                Name = "Sample artifact process",
                Description = "Runs a tiny artifact-rooted script that emits a StepResult.",
                RuntimeKey = StepRuntimeKeys.ArtifactProcess,
                RuntimeConfig = new ArtifactProcessRuntimeConfig { ArtifactId = artifact.Id, ArtifactVersion = version.Version, Entrypoint = "main" },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                Active = true,
                IsProtected = true
            }, result, token).ConfigureAwait(false);
        }

        private async Task EnsureArtifactPythonStepAsync(string tenantId, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            ArtifactManifest manifest = NewManifest(StepRuntimeKeys.ArtifactPython);
            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Module = "handler",
                Function = "run",
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
            {
                [ArtifactManifestService.ManifestFileName] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                ["handler.py"] = Encoding.UTF8.GetBytes("def run(req):\n    return {\"sample\": \"artifact-python\", \"requestId\": req.get(\"requestId\")}\n")
            };
            ArtifactRecord artifact = await EnsureArtifactAsync(tenantId, "Sample Artifact.Python", "Startup sample package for Artifact.Python.", result, token).ConfigureAwait(false);
            ArtifactVersionRecord version = await EnsureArtifactVersionAsync(tenantId, artifact.Id, files, result, token).ConfigureAwait(false);
            await EnsureStepAsync(tenantId, ArtifactPythonExecutionKey, new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = ArtifactPythonExecutionKey,
                Name = "Sample artifact Python",
                Description = "Runs a tiny Python handler through the Tempo protocol shim.",
                RuntimeKey = StepRuntimeKeys.ArtifactPython,
                RuntimeConfig = new ArtifactPythonRuntimeConfig { ArtifactId = artifact.Id, ArtifactVersion = version.Version, Entrypoint = "main", Module = "handler", Function = "run" },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                Active = true,
                IsProtected = true
            }, result, token).ConfigureAwait(false);
        }

        private async Task EnsureArtifactJavaScriptStepAsync(string tenantId, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            ArtifactManifest manifest = NewManifest(StepRuntimeKeys.ArtifactJavaScript);
            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Module = "handler.js",
                Function = "run",
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
            {
                [ArtifactManifestService.ManifestFileName] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                ["handler.js"] = Encoding.UTF8.GetBytes("exports.run = async function(req) {\n  return { sample: \"artifact-javascript\", requestId: req.requestId };\n};\n")
            };
            ArtifactRecord artifact = await EnsureArtifactAsync(tenantId, "Sample Artifact.JavaScript", "Startup sample package for Artifact.JavaScript.", result, token).ConfigureAwait(false);
            ArtifactVersionRecord version = await EnsureArtifactVersionAsync(tenantId, artifact.Id, files, result, token).ConfigureAwait(false);
            await EnsureStepAsync(tenantId, ArtifactJavaScriptExecutionKey, new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = ArtifactJavaScriptExecutionKey,
                Name = "Sample artifact JavaScript",
                Description = "Runs a tiny JavaScript handler through the Tempo protocol shim.",
                RuntimeKey = StepRuntimeKeys.ArtifactJavaScript,
                RuntimeConfig = new ArtifactJavaScriptRuntimeConfig { ArtifactId = artifact.Id, ArtifactVersion = version.Version, Entrypoint = "main", Module = "handler.js", Function = "run" },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                Active = true,
                IsProtected = true
            }, result, token).ConfigureAwait(false);
        }

        private async Task EnsureArtifactDotnetProcessStepAsync(string tenantId, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            ArtifactManifest manifest = NewManifest(StepRuntimeKeys.ArtifactDotnetProcess);
            DotnetSamplePackage? sample = FindDotnetSamplePackage();
            if (sample == null)
            {
                result.Notes.Add("Artifact.DotnetProcess sample was skipped because the packaged Tempo.Sample.DotnetProcess assembly was not found");
                return;
            }

            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            foreach (string file in Directory.EnumerateFiles(sample.Directory, "*", SearchOption.TopDirectoryOnly))
            {
                files[sample.PackageDirectory + "/" + Path.GetFileName(file)] = File.ReadAllBytes(file);
            }

            manifest.Entrypoints["main"] = new ArtifactManifestEntrypoint
            {
                Command = sample.PackageDirectory + "/" + sample.CommandFile,
                HandlerType = sample.HandlerType,
                InputSchema = "{}",
                OutputSchema = "{}",
                ArgumentSchema = "{}"
            };
            files[ArtifactManifestService.ManifestFileName] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest));

            ArtifactRecord artifact = await EnsureArtifactAsync(tenantId, "Sample Artifact.DotnetProcess", "Startup sample package for Artifact.DotnetProcess.", result, token).ConfigureAwait(false);
            ArtifactVersionRecord version = await EnsureArtifactVersionAsync(tenantId, artifact.Id, files, result, token).ConfigureAwait(false);
            await EnsureStepAsync(tenantId, ArtifactDotnetProcessExecutionKey, new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = ArtifactDotnetProcessExecutionKey,
                Name = "Sample artifact .NET process",
                Description = "Runs a tiny .NET artifact through the Tempo SDK handler contract.",
                RuntimeKey = StepRuntimeKeys.ArtifactDotnetProcess,
                RuntimeConfig = new ArtifactDotnetProcessRuntimeConfig { ArtifactId = artifact.Id, ArtifactVersion = version.Version, Entrypoint = "main" },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                Active = true,
                IsProtected = true
            }, result, token).ConfigureAwait(false);
        }

        private async Task EnsureHostExecutableStepAsync(string tenantId, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            HostExecutableAllowListEntry? entry = _RuntimeSettings.HostExecutables.AllowList.FirstOrDefault(e => e.Enabled);
            if (entry == null)
            {
                result.Notes.Add("Host.Executable sample was skipped because host executables are enabled but no enabled allowlist entry exists");
                return;
            }

            await EnsureStepAsync(tenantId, HostExecutableExecutionKey, new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = HostExecutableExecutionKey,
                Name = "Sample host executable",
                Description = "Startup sample for the first enabled Host.Executable allowlist entry.",
                RuntimeKey = StepRuntimeKeys.HostExecutable,
                RuntimeConfig = new HostExecutableRuntimeConfig { AllowListKey = entry.Key },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                Active = true,
                IsProtected = true
            }, result, token).ConfigureAwait(false);
        }

        private static StepRecord BuiltinUnknownStep(string tenantId)
        {
            return new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = BuiltinUnknownExecutionKey,
                Name = "Sample unresolved built-in",
                Description = "Compatibility sample for legacy built-in rows before reconciliation.",
                RuntimeKey = StepRuntimeKeys.BuiltinUnknown,
                RuntimeConfig = new BuiltinUnknownRuntimeConfig { Identifier = BuiltinUnknownExecutionKey },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Unresolved,
                Active = false,
                IsProtected = true
            };
        }

        private StepRecord ExternalRestStep(string tenantId)
        {
            return new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = ExternalRestExecutionKey,
                Name = "Sample external REST",
                Description = "Startup sample for the External.Rest runtime.",
                RuntimeKey = StepRuntimeKeys.ExternalRest,
                RuntimeConfig = new ExternalRestRuntimeConfig
                {
                    Method = "GET",
                    Url = ExternalRestSampleUrl(),
                    TimeoutMs = 5000
                },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                Active = true,
                IsProtected = true
            };
        }

        private static StepRecord LegacyInlineRestStep(string tenantId)
        {
            return new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = LegacyInlineRestExecutionKey,
                Name = "Sample legacy inline REST",
                Description = "Compatibility sample for Legacy.InlineRest; new steps should use External.Rest.",
                RuntimeKey = StepRuntimeKeys.LegacyInlineRest,
                RuntimeConfig = new LegacyInlineRestRuntimeConfig
                {
                    Method = "GET",
                    Url = "https://postman-echo.com/get?tempo=legacy-sample",
                    TimeoutMs = 5000
                },
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                Active = false,
                IsProtected = true
            };
        }

        private async Task EnsureStepAsync(string tenantId, string executionKey, StepRecord record, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            StepRecord? existing = await _Database.Steps.ReadByExecutionKeyAsync(tenantId, executionKey, token).ConfigureAwait(false);
            if (existing != null)
            {
                if (!existing.IsProtected)
                {
                    result.Notes.Add("Runtime sample step '" + executionKey + "' already exists and is not protected; leaving it unchanged");
                    return;
                }

                record.Id = existing.Id;
                record.CreatedUtc = existing.CreatedUtc;
                await _Database.Steps.UpdateAsync(record, token).ConfigureAwait(false);
                return;
            }

            await _Database.Steps.CreateAsync(record, token).ConfigureAwait(false);
            result.StepsCreated.Add(executionKey);
        }

        private async Task RemoveRetiredCompatibilityTemplateAsync(string tenantId, string executionKey, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            StepRecord? existing = await _Database.Steps.ReadByExecutionKeyAsync(tenantId, executionKey, token).ConfigureAwait(false);
            if (existing == null || !existing.IsProtected) return;

            DeletionDependencyResult dependencies = await new DeletionDependencyService(_Database).FindStepReferencesAsync(tenantId, executionKey, token).ConfigureAwait(false);
            if (dependencies.IsBlocked)
            {
                existing.Active = false;
                await _Database.Steps.UpdateAsync(existing, token).ConfigureAwait(false);
                result.Notes.Add("Retired compatibility template '" + executionKey + "' is still referenced and was left inactive");
                return;
            }

            await _Database.Steps.DeleteAsync(tenantId, existing.Id, token).ConfigureAwait(false);
        }

        private async Task<ArtifactRecord> EnsureArtifactAsync(string tenantId, string name, string description, DefaultRuntimeStepSeedResult result, CancellationToken token)
        {
            ArtifactRecord? existing = await _Database.Artifacts.ReadByNameAsync(tenantId, name, token).ConfigureAwait(false);
            if (existing != null)
            {
                if (existing.IsProtected && (!existing.Active || existing.Description != description))
                {
                    existing.Description = description;
                    existing.Active = true;
                    await _Database.Artifacts.UpdateAsync(existing, token).ConfigureAwait(false);
                }
                return existing;
            }
            ArtifactRecord created = await _Database.Artifacts.CreateAsync(new ArtifactRecord
            {
                TenantId = tenantId,
                Name = name,
                Description = description,
                Active = true,
                IsProtected = true
            }, token).ConfigureAwait(false);
            result.ArtifactsCreated.Add(created.Id);
            return created;
        }

        private async Task<ArtifactVersionRecord> EnsureArtifactVersionAsync(
            string tenantId,
            string artifactId,
            Dictionary<string, byte[]> files,
            DefaultRuntimeStepSeedResult result,
            CancellationToken token)
        {
            ArtifactVersionRecord? existing = await _Database.ArtifactVersions.ReadByVersionAsync(tenantId, artifactId, Constants.MutableArtifactVersion, token).ConfigureAwait(false);
            ArtifactFileSnapshotService artifactFiles = new ArtifactFileSnapshotService(_Database, _BlobStore, _RuntimeSettings.ExternalExecution);
            ArtifactVersionRecord version = await artifactFiles.ReplaceFilesAndSnapshotAsync(tenantId, artifactId, files, token).ConfigureAwait(false);
            version.IsProtected = true;
            version = await _Database.ArtifactVersions.UpdateAsync(version, token).ConfigureAwait(false);
            if (existing == null) result.ArtifactVersionsCreated.Add(version.Id);
            return version;
        }

        private static ArtifactManifest NewManifest(RuntimeKey runtimeKey)
        {
            return new ArtifactManifest
            {
                ManifestVersion = "1",
                RuntimeKey = runtimeKey.ToString(),
                SupportedProtocolVersions = new List<string> { "1.0" },
                DefaultEntrypoint = "main",
                InputSchema = "{}",
                OutputSchema = "{}"
            };
        }

        private string ExternalRestSampleUrl()
        {
            RestSettings rest = _RestSettings ?? new RestSettings();
            string host = rest.Hostname.Trim();
            if (host == "*" || host == "+" || host == "0.0.0.0" || host == "::") host = "127.0.0.1";
            string scheme = rest.Ssl ? "https" : "http";
            return scheme + "://" + host + ":" + rest.Port + "/v1.0/samples/external-rest";
        }

        private static DotnetSamplePackage? FindDotnetSamplePackage()
        {
            const string tfm = "net10.0";
            string[] outputCandidates =
            {
                Path.Combine(AppContext.BaseDirectory, "Samples", "DotnetProcess"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Samples", "DotnetProcess"))
            };
            foreach (string candidate in outputCandidates)
            {
                string dll = Path.Combine(candidate, "Tempo.Sample.DotnetProcess.dll");
                if (File.Exists(dll))
                {
                    return new DotnetSamplePackage(candidate, "dotnet-sample", "Tempo.Sample.DotnetProcess.dll", "Tempo.Sample.DotnetProcess.Program+SampleHandler");
                }
            }

            string? sampleDir = FindProjectOutputDirectory("Tempo.Sample.DotnetProcess", "Tempo.Sample.DotnetProcess.dll", tfm);
            if (!string.IsNullOrWhiteSpace(sampleDir))
            {
                return new DotnetSamplePackage(sampleDir, "dotnet-sample", "Tempo.Sample.DotnetProcess.dll", "Tempo.Sample.DotnetProcess.Program+SampleHandler");
            }

            string? fixtureDir = FindProjectOutputDirectory("Test.ArtifactFixture", "Test.ArtifactFixture.dll", tfm);
            if (!string.IsNullOrWhiteSpace(fixtureDir))
            {
                return new DotnetSamplePackage(fixtureDir, "dotnet-sample", "Test.ArtifactFixture.dll", "Test.ArtifactFixture.Program+FixtureHandler");
            }

            return null;
        }

        private static string? FindProjectOutputDirectory(string projectName, string assemblyName, string tfm)
        {
            string? dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++, dir = Directory.GetParent(dir)?.FullName)
            {
                string candidate = Path.Combine(dir, "src", projectName, "bin", "Debug", tfm);
                if (File.Exists(Path.Combine(candidate, assemblyName))) return candidate;
            }

            dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++, dir = Directory.GetParent(dir)?.FullName)
            {
                string candidate = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", projectName, "bin", "Debug", tfm));
                if (File.Exists(Path.Combine(candidate, assemblyName))) return candidate;
            }

            return null;
        }

        private sealed class DotnetSamplePackage
        {
            public DotnetSamplePackage(string directory, string packageDirectory, string commandFile, string handlerType)
            {
                Directory = directory;
                PackageDirectory = packageDirectory;
                CommandFile = commandFile;
                HandlerType = handlerType;
            }

            public string Directory { get; }
            public string PackageDirectory { get; }
            public string CommandFile { get; }
            public string HandlerType { get; }
        }
    }
}
