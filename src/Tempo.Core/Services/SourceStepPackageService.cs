namespace Tempo.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Artifacts;
    using Tempo.Core.Database;
    using Tempo.Core.Enums;
    using Tempo.Core.Models;
    using Tempo.Core.Requests;
    using Tempo.Core.Responses;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;
    using Tempo.Protocol;

    /// <summary>Builds artifact packages and persisted steps from pasted source files.</summary>
    public class SourceStepPackageService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly IArtifactBlobStore _BlobStore;
        private readonly ExternalExecutionSettings _Settings;

        public SourceStepPackageService(DatabaseDriverBase database, IArtifactBlobStore blobStore, ExternalExecutionSettings? settings = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _BlobStore = blobStore ?? throw new ArgumentNullException(nameof(blobStore));
            _Settings = settings ?? new ExternalExecutionSettings();
        }

        public async Task<SourceStepCreateResponse> CreateAsync(string tenantId, SourceStepCreateRequest request, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (request == null) throw new ArgumentNullException(nameof(request));
            IReadOnlyList<string> errors = request.Validate();
            if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors), nameof(request));

            SourcePackage package = await BuildPackageAsync(request, token).ConfigureAwait(false);
            ArtifactRecord artifact = await _Database.Artifacts.CreateAsync(new ArtifactRecord
            {
                TenantId = tenantId,
                Name = string.IsNullOrWhiteSpace(request.ArtifactName) ? request.Name.Trim() + " source package" : request.ArtifactName!.Trim(),
                Description = "Generated from pasted " + request.NormalizedLanguage + " source for step '" + request.Name.Trim() + "'.",
                Active = true
            }, token).ConfigureAwait(false);

            ArtifactFileSnapshotService artifactFiles = new ArtifactFileSnapshotService(_Database, _BlobStore, _Settings);
            ArtifactVersionRecord version = await artifactFiles.ReplaceFilesAndSnapshotAsync(tenantId, artifact.Id, package.Files, token).ConfigureAwait(false);

            StepRuntimeConfig runtimeConfig = package.CreateRuntimeConfig(artifact.Id, version.Version);
            StepRecord step = new StepRecord
            {
                TenantId = tenantId,
                ExecutionKey = string.IsNullOrWhiteSpace(request.ExecutionKey) ? request.Name.Trim() : request.ExecutionKey!.Trim(),
                Name = request.Name.Trim(),
                Description = request.Description,
                RuntimeKey = package.RuntimeKey,
                RuntimeConfig = runtimeConfig,
                ContractType = request.ContractType,
                InputSchema = request.InputSchema,
                OutputSchema = request.OutputSchema,
                ValidateInput = request.ValidateInput,
                ValidateOutput = request.ValidateOutput,
                RuntimeBindingState = StepRuntimeBindingStateEnum.Resolved,
                MaxRuntimeMs = request.MaxRuntimeMs,
                Active = request.Active
            };
            StepRecord createdStep = await _Database.Steps.CreateAsync(step, token).ConfigureAwait(false);
            return new SourceStepCreateResponse
            {
                Step = StepResponse.FromRecord(createdStep),
                Artifact = artifact,
                ArtifactVersion = version
            };
        }

        private async Task<SourcePackage> BuildPackageAsync(SourceStepCreateRequest request, CancellationToken token)
        {
            switch (request.NormalizedLanguage)
            {
                case SourceStepLanguage.Python:
                    return BuildPythonPackage(request);
                case SourceStepLanguage.JavaScript:
                    return BuildJavaScriptPackage(request);
                case SourceStepLanguage.CSharp:
                    return await BuildCSharpPackageAsync(request, token).ConfigureAwait(false);
                default:
                    throw new ArgumentException("language must be Python, JavaScript, or CSharp.", nameof(request));
            }
        }

        private static SourcePackage BuildPythonPackage(SourceStepCreateRequest request)
        {
            string fileName = SafeFileName(request.FileName, "handler.py", ".py");
            string module = string.IsNullOrWhiteSpace(request.Module) ? Path.GetFileNameWithoutExtension(fileName) : request.Module!.Trim();
            string function = string.IsNullOrWhiteSpace(request.Function) ? "run" : request.Function.Trim();
            ArtifactManifest manifest = NewManifest(StepRuntimeKeys.ArtifactPython, request);
            manifest.Entrypoints[request.Entrypoint] = new ArtifactManifestEntrypoint
            {
                Module = module,
                Function = function,
                InputSchema = request.InputSchema,
                OutputSchema = request.OutputSchema,
                ArgumentSchema = "{}"
            };
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
            {
                [ArtifactManifestService.ManifestFileName] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                [fileName] = Encoding.UTF8.GetBytes(request.Code)
            };
            return new SourcePackage(StepRuntimeKeys.ArtifactPython, manifest, files, (artifactId, version) => new ArtifactPythonRuntimeConfig
            {
                ArtifactId = artifactId,
                ArtifactVersion = version,
                Entrypoint = request.Entrypoint,
                Module = module,
                Function = function
            });
        }

        private static SourcePackage BuildJavaScriptPackage(SourceStepCreateRequest request)
        {
            string fileName = SafeFileName(request.FileName, "handler.js", ".js");
            string module = string.IsNullOrWhiteSpace(request.Module) ? fileName : request.Module!.Trim();
            string function = string.IsNullOrWhiteSpace(request.Function) ? "run" : request.Function.Trim();
            ArtifactManifest manifest = NewManifest(StepRuntimeKeys.ArtifactJavaScript, request);
            manifest.Entrypoints[request.Entrypoint] = new ArtifactManifestEntrypoint
            {
                Module = module,
                Function = function,
                InputSchema = request.InputSchema,
                OutputSchema = request.OutputSchema,
                ArgumentSchema = "{}"
            };
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
            {
                [ArtifactManifestService.ManifestFileName] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                [fileName] = Encoding.UTF8.GetBytes(request.Code)
            };
            return new SourcePackage(StepRuntimeKeys.ArtifactJavaScript, manifest, files, (artifactId, version) => new ArtifactJavaScriptRuntimeConfig
            {
                ArtifactId = artifactId,
                ArtifactVersion = version,
                Entrypoint = request.Entrypoint,
                Module = module,
                Function = function
            });
        }

        private async Task<SourcePackage> BuildCSharpPackageAsync(SourceStepCreateRequest request, CancellationToken token)
        {
            RuntimeCommandProbeResult sdkProbe = RuntimeCommandProbe.ProbeDotnetSdk(_Settings);
            if (!sdkProbe.Available) throw new InvalidOperationException("CSharp source steps require a .NET SDK. " + sdkProbe.Message);

            string root = Path.Combine(Path.GetTempPath(), "tempo-source-step-" + Guid.NewGuid().ToString("N"));
            string sourceDir = Path.Combine(root, "src");
            string outputDir = Path.Combine(root, "out");
            Directory.CreateDirectory(sourceDir);
            try
            {
                string assemblyName = "Tempo.SourceStep." + Guid.NewGuid().ToString("N");
                string fileName = SafeFileName(request.FileName, "UserStep.cs", ".cs");
                string handlerType = string.IsNullOrWhiteSpace(request.HandlerType) ? "Tempo.UserSteps.Handler" : request.HandlerType.Trim();
                string tempoAssembly = typeof(Tempo.StepRequest).Assembly.Location;
                string targetFramework = CurrentTargetFramework();

                File.WriteAllText(Path.Combine(sourceDir, fileName), request.Code, Encoding.UTF8);
                File.WriteAllText(Path.Combine(sourceDir, "Program.cs"), CSharpHostSource(handlerType), Encoding.UTF8);
                File.WriteAllText(Path.Combine(sourceDir, assemblyName + ".csproj"), CSharpProjectSource(targetFramework, assemblyName, tempoAssembly), Encoding.UTF8);

                await RunDotnetAsync(_Settings.DotnetExecutable, sourceDir, outputDir, token).ConfigureAwait(false);

                ArtifactManifest manifest = NewManifest(StepRuntimeKeys.ArtifactDotnetProcess, request);
                manifest.Entrypoints[request.Entrypoint] = new ArtifactManifestEntrypoint
                {
                    Command = "dotnet/" + assemblyName + ".dll",
                    HandlerType = handlerType,
                    Args = new List<string> { handlerType },
                    InputSchema = request.InputSchema,
                    OutputSchema = request.OutputSchema,
                    ArgumentSchema = "{}"
                };

                Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
                {
                    [ArtifactManifestService.ManifestFileName] = Encoding.UTF8.GetBytes(ArtifactManifestService.Serialize(manifest)),
                    ["source/" + fileName] = Encoding.UTF8.GetBytes(request.Code),
                    ["source/Program.cs"] = Encoding.UTF8.GetBytes(CSharpHostSource(handlerType)),
                    ["source/" + assemblyName + ".csproj"] = Encoding.UTF8.GetBytes(CSharpProjectSource(targetFramework, assemblyName, tempoAssembly))
                };
                foreach (string file in Directory.EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly))
                {
                    files["dotnet/" + Path.GetFileName(file)] = File.ReadAllBytes(file);
                }

                return new SourcePackage(StepRuntimeKeys.ArtifactDotnetProcess, manifest, files, (artifactId, version) => new ArtifactDotnetProcessRuntimeConfig
                {
                    ArtifactId = artifactId,
                    ArtifactVersion = version,
                    Entrypoint = request.Entrypoint
                });
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
            }
        }

        private static async Task RunDotnetAsync(string dotnetExecutable, string sourceDir, string outputDir, CancellationToken token)
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(dotnetExecutable) ? "dotnet" : dotnetExecutable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = sourceDir
            };
            process.StartInfo.ArgumentList.Add("publish");
            process.StartInfo.ArgumentList.Add("--configuration");
            process.StartInfo.ArgumentList.Add("Release");
            process.StartInfo.ArgumentList.Add("--output");
            process.StartInfo.ArgumentList.Add(outputDir);
            process.StartInfo.ArgumentList.Add("/p:RestoreIgnoreFailedSources=true");

            if (!process.Start()) throw new InvalidOperationException("dotnet publish failed to start.");
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            string stdout = await stdoutTask.ConfigureAwait(false);
            string stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                string detail = (stdout + Environment.NewLine + stderr).Trim();
                if (detail.Length > 8000) detail = detail.Substring(detail.Length - 8000);
                throw new InvalidOperationException("CSharp source step failed to compile: " + detail);
            }
        }

        private static ArtifactManifest NewManifest(RuntimeKey runtimeKey, SourceStepCreateRequest request)
        {
            return new ArtifactManifest
            {
                ManifestVersion = "1",
                RuntimeKey = runtimeKey.ToString(),
                SupportedProtocolVersions = new List<string> { ProtocolVersions.Current },
                DefaultEntrypoint = request.Entrypoint,
                InputSchema = request.InputSchema,
                OutputSchema = request.OutputSchema,
                Metadata = new Dictionary<string, string>
                {
                    ["sourceLanguage"] = request.NormalizedLanguage.ToString(),
                    ["generatedBy"] = "Tempo.SourceStepPackageService"
                }
            };
        }

        private static string CSharpProjectSource(string targetFramework, string assemblyName, string tempoAssembly)
        {
            return """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>__TFM__</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>__ASSEMBLY__</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Tempo">
      <HintPath>__TEMPO_ASSEMBLY__</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>
</Project>
""".Replace("__TFM__", XmlEscape(targetFramework))
                .Replace("__ASSEMBLY__", XmlEscape(assemblyName))
                .Replace("__TEMPO_ASSEMBLY__", XmlEscape(tempoAssembly));
        }

        private static string CSharpHostSource(string handlerType)
        {
            return """
using System.Reflection;
using Tempo.Protocol;

namespace Tempo.SourceStepHost;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string handlerTypeName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : "__HANDLER_TYPE__";
        Type? handlerType = ResolveType(handlerTypeName);
        if (handlerType == null) throw new InvalidOperationException("Handler type not found: " + handlerTypeName);
        if (Activator.CreateInstance(handlerType) is not ITempoStepHandler handler)
            throw new InvalidOperationException("Handler type must implement Tempo.Protocol.ITempoStepHandler: " + handlerTypeName);
        return await TempoStepHost.RunAsync(handler);
    }

    private static Type? ResolveType(string typeName)
    {
        Type? direct = Type.GetType(typeName, throwOnError: false);
        if (direct != null) return direct;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(typeName, throwOnError: false);
            if (type != null) return type;
        }
        return null;
    }
}
""".Replace("__HANDLER_TYPE__", CSharpEscape(handlerType));
        }

        private static string CurrentTargetFramework()
        {
            TargetFrameworkAttribute? attr = typeof(Tempo.StepRequest).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            string frameworkName = attr?.FrameworkName ?? string.Empty;
            if (frameworkName.StartsWith(".NETCoreApp,Version=v", StringComparison.OrdinalIgnoreCase))
            {
                string version = frameworkName.Substring(".NETCoreApp,Version=v".Length);
                return "net" + version;
            }
            return "net10.0";
        }

        private static string SafeFileName(string? value, string defaultName, string requiredExtension)
        {
            string fileName = string.IsNullOrWhiteSpace(value) ? defaultName : Path.GetFileName(value.Trim());
            if (string.IsNullOrWhiteSpace(fileName)) fileName = defaultName;
            fileName = SafeSegment(fileName);
            if (!fileName.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase)) fileName += requiredExtension;
            return fileName;
        }

        private static string SafeSegment(string value)
        {
            char[] chars = (value ?? string.Empty).ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok) chars[i] = '_';
            }
            string result = new string(chars).Trim('.');
            return string.IsNullOrWhiteSpace(result) ? "source" : result;
        }

        private static string XmlEscape(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
        }

        private static string CSharpEscape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private sealed class SourcePackage
        {
            private readonly Func<string, string, StepRuntimeConfig> _Factory;

            public SourcePackage(RuntimeKey runtimeKey, ArtifactManifest manifest, Dictionary<string, byte[]> files, Func<string, string, StepRuntimeConfig> factory)
            {
                RuntimeKey = runtimeKey;
                Manifest = manifest;
                Files = files;
                _Factory = factory;
            }

            public RuntimeKey RuntimeKey { get; }
            public ArtifactManifest Manifest { get; }
            public Dictionary<string, byte[]> Files { get; }
            public StepRuntimeConfig CreateRuntimeConfig(string artifactId, string version) => _Factory(artifactId, version);
        }
    }
}
