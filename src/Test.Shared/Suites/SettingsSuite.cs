namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Tempo.Core.Enums;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Touchstone.Core;

    /// <summary>Settings loader tests.</summary>
    public static class SettingsSuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Settings",
                displayName: "Settings loader and environment overrides",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Settings", "DefaultsWhenMissing", "Loader returns defaults when file does not exist", async _ =>
                    {
                        await Task.CompletedTask;
                        Settings s = SettingsLoader.Load(Path.Combine(Path.GetTempPath(), "tempo-nonexistent-" + Guid.NewGuid().ToString("N") + ".json"));
                        Assert2.Equal(DatabaseTypeEnum.Sqlite, s.Database.Type, "default db type");
                        Assert2.Equal(8901, s.Rest.Port, "default port");
                        Assert2.True(s.Hydration.SeedDefaults, "default seed");
                    }),
                    new TestCaseDescriptor("Settings", "LoadFromFile", "Loader reads values from JSON file", async _ =>
                    {
                        await Task.CompletedTask;
                        string file = Path.Combine(Path.GetTempPath(), "tempo-settings-" + Guid.NewGuid().ToString("N") + ".json");
                        File.WriteAllText(file, "{ \"rest\": { \"port\": 9090 }, \"database\": { \"type\": \"Sqlite\", \"filename\": \"./custom.db\" } }");
                        try
                        {
                            Settings s = SettingsLoader.Load(file);
                            Assert2.Equal(9090, s.Rest.Port, "port loaded");
                            Assert2.Equal("./custom.db", s.Database.Filename!, "filename loaded");
                        }
                        finally { try { File.Delete(file); } catch { } }
                    }),
                    new TestCaseDescriptor("Settings", "EnvOverride", "Environment variables override file values", async _ =>
                    {
                        await Task.CompletedTask;
                        string file = Path.Combine(Path.GetTempPath(), "tempo-settings-" + Guid.NewGuid().ToString("N") + ".json");
                        File.WriteAllText(file, "{ \"auth\": { \"signingKey\": \"from-file\" } }");
                        Environment.SetEnvironmentVariable(SettingsLoader.EnvAuthSigningKey, "from-env-signing-key-123456");
                        try
                        {
                            Settings s = SettingsLoader.Load(file);
                            Assert2.Equal("from-env-signing-key-123456", s.Auth.SigningKey, "env wins");
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable(SettingsLoader.EnvAuthSigningKey, null);
                            try { File.Delete(file); } catch { }
                        }
                    }),
                    new TestCaseDescriptor("Settings", "SaveRoundtrip", "Save then load returns equivalent settings", async _ =>
                    {
                        await Task.CompletedTask;
                        string file = Path.Combine(Path.GetTempPath(), "tempo-settings-rt-" + Guid.NewGuid().ToString("N") + ".json");
                        try
                        {
                            Settings s = new Settings();
                            s.Rest.Port = 7777;
                            s.Database.Filename = "./rt.db";
                            SettingsLoader.Save(s, file);
                            Settings loaded = SettingsLoader.Load(file);
                            Assert2.Equal(7777, loaded.Rest.Port, "port roundtrip");
                            Assert2.Equal("./rt.db", loaded.Database.Filename!, "filename roundtrip");
                        }
                        finally { try { File.Delete(file); } catch { } }
                    }),
                    new TestCaseDescriptor("Settings", "PortClamp", "REST port clamps to valid range", async _ =>
                    {
                        await Task.CompletedTask;
                        RestSettings r = new RestSettings();
                        r.Port = -5;
                        Assert2.Equal(1, r.Port, "clamp low");
                        r.Port = 999999;
                        Assert2.Equal(65535, r.Port, "clamp high");
                    }),
                    new TestCaseDescriptor("Settings", "RetentionClamp", "Retention days clamp", async _ =>
                    {
                        await Task.CompletedTask;
                        RequestHistorySettings r = new RequestHistorySettings();
                        r.RetentionDays = 0;
                        Assert2.Equal(1, r.RetentionDays, "clamp low");
                        r.RetentionDays = 10000;
                        Assert2.Equal(3650, r.RetentionDays, "clamp high");
                    }),
                    new TestCaseDescriptor("Settings", "MaxConcurrentClamp", "Engine concurrency clamp", async _ =>
                    {
                        await Task.CompletedTask;
                        EngineSettings e = new EngineSettings();
                        e.MaxConcurrentRuns = 0;
                        Assert2.Equal(1, e.MaxConcurrentRuns, "clamp low");
                        e.MaxConcurrentRuns = 9999;
                        Assert2.Equal(1024, e.MaxConcurrentRuns, "clamp high");
                    }),
                    new TestCaseDescriptor("Settings", "ArtifactSettings", "Artifact storage settings clamp and load environment root path", async _ =>
                    {
                        await Task.CompletedTask;
                        ArtifactSettings a = new ArtifactSettings();
                        a.MaxUploadBytes = 0;
                        Assert2.Equal(1L, a.MaxUploadBytes, "upload clamp low");
                        a.MaxBytesPerTenant = 0;
                        Assert2.Equal(1L, a.MaxBytesPerTenant, "tenant quota clamp low");
                        a.VersionGracePeriodDays = -1;
                        Assert2.Equal(0, a.VersionGracePeriodDays, "version grace clamp low");
                        a.FlowRunReplayRetentionDays = 0;
                        Assert2.Equal(1, a.FlowRunReplayRetentionDays, "flow replay clamp low");
                        a.MaxVersionsPerArtifact = -1;
                        Assert2.Equal(0, a.MaxVersionsPerArtifact, "max versions clamp low");
                        a.GcBatchSize = 0;
                        Assert2.Equal(1, a.GcBatchSize, "gc batch clamp low");
                        a.GcIntervalMinutes = 0;
                        Assert2.Equal(1, a.GcIntervalMinutes, "gc interval clamp low");

                        string root = Path.Combine(Path.GetTempPath(), "tempo-artifact-root-" + Guid.NewGuid().ToString("N"));
                        Environment.SetEnvironmentVariable(SettingsLoader.EnvArtifactRootPath, root);
                        try
                        {
                            Settings s = SettingsLoader.Load(Path.Combine(Path.GetTempPath(), "tempo-nonexistent-" + Guid.NewGuid().ToString("N") + ".json"));
                            Assert2.Equal(root, s.Artifacts.RootPath, "artifact root env override");
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable(SettingsLoader.EnvArtifactRootPath, null);
                        }
                    }),
                    new TestCaseDescriptor("Settings", "ExternalExecutionSettings", "External execution settings clamp limits and load JSON/env overrides", async _ =>
                    {
                        await Task.CompletedTask;
                        ExternalExecutionSettings external = new ExternalExecutionSettings();
                        external.MaxConcurrentProcessesServerWide = 0;
                        Assert2.Equal(1, external.MaxConcurrentProcessesServerWide, "server cap clamp low");
                        external.MaxConcurrentProcessesPerTenant = 0;
                        Assert2.Equal(1, external.MaxConcurrentProcessesPerTenant, "tenant cap clamp low");
                        external.DefaultMaxRuntimeMs = 1;
                        Assert2.Equal(100, external.DefaultMaxRuntimeMs, "runtime clamp low");
                        external.MaxStdoutBytes = 0;
                        Assert2.Equal(1L, external.MaxStdoutBytes, "stdout clamp low");
                        external.MaxStderrBytes = 0;
                        Assert2.Equal(1L, external.MaxStderrBytes, "stderr clamp low");
                        external.MaxInputBytes = 0;
                        Assert2.Equal(1L, external.MaxInputBytes, "input clamp low");
                        external.MaxOutputBytes = 0;
                        Assert2.Equal(1L, external.MaxOutputBytes, "output clamp low");

                        string file = Path.Combine(Path.GetTempPath(), "tempo-settings-external-" + Guid.NewGuid().ToString("N") + ".json");
                        File.WriteAllText(file, "{ \"runtimes\": { \"externalExecution\": { \"maxConcurrentProcessesServerWide\": 3, \"maxConcurrentProcessesPerTenant\": 2, \"scratchRoot\": \"./scratch-json\", \"pythonExecutable\": \"python-test\", \"nodeExecutable\": \"node-test\", \"dotnetExecutable\": \"dotnet-test\" } } }");
                        Environment.SetEnvironmentVariable(SettingsLoader.EnvExternalExecutionCacheRoot, "./cache-env");
                        Environment.SetEnvironmentVariable(SettingsLoader.EnvExternalExecutionNodeExecutable, "node-env");
                        try
                        {
                            Settings s = SettingsLoader.Load(file);
                            Assert2.Equal(3, s.Runtimes.ExternalExecution.MaxConcurrentProcessesServerWide, "server cap loaded");
                            Assert2.Equal(2, s.Runtimes.ExternalExecution.MaxConcurrentProcessesPerTenant, "tenant cap loaded");
                            Assert2.Equal("./scratch-json", s.Runtimes.ExternalExecution.ScratchRoot, "scratch loaded");
                            Assert2.Equal("./cache-env", s.Runtimes.ExternalExecution.CacheRoot, "cache env override");
                            Assert2.Equal("python-test", s.Runtimes.ExternalExecution.PythonExecutable, "python executable loaded");
                            Assert2.Equal("node-env", s.Runtimes.ExternalExecution.NodeExecutable, "node executable env override");
                            Assert2.Equal("dotnet-test", s.Runtimes.ExternalExecution.DotnetExecutable, "dotnet executable loaded");
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable(SettingsLoader.EnvExternalExecutionCacheRoot, null);
                            Environment.SetEnvironmentVariable(SettingsLoader.EnvExternalExecutionNodeExecutable, null);
                            try { File.Delete(file); } catch { }
                        }
                    }),
                    new TestCaseDescriptor("Settings", "HostExecutableSettings", "Host executable allowlist settings are disabled by default and load from JSON", async _ =>
                    {
                        await Task.CompletedTask;
                        HostExecutableSettings host = new HostExecutableSettings();
                        Assert2.False(host.Enabled, "disabled by default");
                        Assert2.Equal(0, host.AllowList.Count, "empty allowlist by default");
                        HostExecutableAllowListEntry entry = new HostExecutableAllowListEntry();
                        entry.MaxRuntimeMs = -1;
                        Assert2.Equal(0, entry.MaxRuntimeMs, "entry timeout clamp low");
                        entry.MaxRuntimeMs = int.MaxValue;
                        Assert2.Equal(24 * 60 * 60 * 1000, entry.MaxRuntimeMs, "entry timeout clamp high");
                        entry.ArgumentPolicy.MaxArguments = -1;
                        Assert2.Equal(0, entry.ArgumentPolicy.MaxArguments, "argument max clamp low");

                        string executable = Path.Combine(Path.GetTempPath(), "tempo-host-tool-" + Guid.NewGuid().ToString("N") + ".exe");
                        string escaped = executable.Replace("\\", "\\\\");
                        string file = Path.Combine(Path.GetTempPath(), "tempo-settings-host-" + Guid.NewGuid().ToString("N") + ".json");
                        File.WriteAllText(file, "{ \"runtimes\": { \"hostExecutables\": { \"enabled\": true, \"allowList\": [ { \"key\": \"tool\", \"displayName\": \"Tool\", \"executablePath\": \"" + escaped + "\", \"argumentPolicy\": { \"allowAdditionalArguments\": true, \"allowedPrefixes\": [\"--safe=\"] } } ] } } }");
                        try
                        {
                            Settings s = SettingsLoader.Load(file);
                            Assert2.True(s.Runtimes.HostExecutables.Enabled, "host enabled");
                            Assert2.Equal("tool", s.Runtimes.HostExecutables.AllowList[0].Key, "key loaded");
                            Assert2.Equal(executable, s.Runtimes.HostExecutables.AllowList[0].ExecutablePath, "path loaded");
                            Assert2.True(s.Runtimes.HostExecutables.AllowList[0].ArgumentPolicy.AllowAdditionalArguments, "argument policy loaded");
                            Assert2.Equal("--safe=", s.Runtimes.HostExecutables.AllowList[0].ArgumentPolicy.AllowedPrefixes[0], "prefix loaded");
                        }
                        finally
                        {
                            try { File.Delete(file); } catch { }
                        }
                    })
                });
        }
    }
}
