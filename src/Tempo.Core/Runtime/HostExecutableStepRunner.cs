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

        /// <summary>
        /// Initializes a new instance of the <see cref="HostExecutableStepRunner"/> class.
        /// </summary>
        /// <param name="tenantId">The tenant identifier that owns the execution.</param>
        /// <param name="allowListKey">The allowlist key identifying the approved host executable. Cannot be null.</param>
        /// <param name="executablePath">The path to the host executable to run.</param>
        /// <param name="workingDirectory">The working directory for the process.</param>
        /// <param name="arguments">Additional command-line arguments passed to the executable.</param>
        /// <param name="environmentReferences">Names of environment variables to forward to the process.</param>
        /// <param name="settings">The external execution settings.</param>
        /// <param name="capacity">The capacity manager controlling concurrent external executions.</param>
        /// <param name="runLogs">Optional run-log session for capturing output.</param>
        /// <param name="runLogStep">Optional run-log step scope for capturing output.</param>
        /// <param name="maxRuntimeMs">Maximum runtime in milliseconds (0 for no timeout).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="allowListKey"/> is null.</exception>
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

        /// <summary>
        /// Builds the <see cref="ProcessStartInfo"/> that launches the allow-listed host executable from its artifact root.
        /// </summary>
        /// <param name="scratch">Per-run scratch directory exposed to the process.</param>
        /// <returns>A configured <see cref="ProcessStartInfo"/> ready to start.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the host executable does not exist.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the working directory does not exist.</exception>
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
