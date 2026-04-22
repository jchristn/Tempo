namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Tempo.Core.Settings;

    /// <summary>Executes an operator allowlisted host executable through the external process protocol.</summary>
    public class HostExecutableStepRunner : ArtifactProcessStepRunner
    {
        private readonly string _AllowListKey;

        public HostExecutableStepRunner(
            string tenantId,
            string allowListKey,
            string executablePath,
            string workingDirectory,
            IEnumerable<string> arguments,
            IEnumerable<string> environmentReferences,
            ExternalExecutionSettings settings,
            ExternalRuntimeCapacityManager capacity,
            RunLogSession? runLogs = null,
            RunLogStepScope? runLogStep = null,
            int maxRuntimeMs = 0)
            : base(
                tenantId,
                new ArtifactVersionSnapshot { ArtifactId = string.Empty, VersionId = string.Empty, Version = string.Empty, Sha256 = string.Empty },
                workingDirectory,
                allowListKey,
                executablePath,
                arguments,
                environmentReferences,
                settings,
                capacity,
                runLogs,
                runLogStep,
                maxRuntimeMs)
        {
            _AllowListKey = allowListKey ?? throw new ArgumentNullException(nameof(allowListKey));
        }

        protected override ProcessStartInfo BuildStartInfo(string scratch)
        {
            string commandPath = Path.GetFullPath(_Command);
            if (!File.Exists(commandPath)) throw new FileNotFoundException("Host executable was not found.", commandPath);
            if (!Directory.Exists(_ArtifactRoot)) throw new DirectoryNotFoundException("Host executable working directory was not found: " + _ArtifactRoot);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _ArtifactRoot
            };

            AddCommand(psi, commandPath);
            foreach (string arg in _Arguments) psi.ArgumentList.Add(arg);

            psi.Environment["TEMPO_HOST_EXECUTABLE_KEY"] = _AllowListKey;
            psi.Environment["TEMPO_SCRATCH_DIR"] = scratch;
            foreach (string name in _EnvironmentReferences.Distinct(StringComparer.Ordinal))
            {
                string? value = Environment.GetEnvironmentVariable(name);
                if (value != null) psi.Environment[name] = value;
            }

            WrapWithLinuxProcessGroup(psi);
            return psi;
        }
    }
}
