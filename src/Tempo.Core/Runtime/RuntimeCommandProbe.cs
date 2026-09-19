namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Tempo.Core.Settings;

    /// <summary>Checks host command availability for optional external runtimes.</summary>
    public static class RuntimeCommandProbe
    {
        private const int DefaultTimeoutMs = 3000;
        private static readonly ConcurrentDictionary<string, RuntimeCommandProbeResult> _Cache = new ConcurrentDictionary<string, RuntimeCommandProbeResult>(StringComparer.Ordinal);

        /// <summary>Probes availability of the configured Python executable.</summary>
        /// <param name="settings">External execution settings supplying the Python executable path.</param>
        /// <returns>The probe result describing availability.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static RuntimeCommandProbeResult ProbePython(ExternalExecutionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Probe(settings.PythonExecutable, new[] { "--version" }, "Python");
        }

        /// <summary>Probes availability of the configured Node.js executable.</summary>
        /// <param name="settings">External execution settings supplying the Node.js executable path.</param>
        /// <returns>The probe result describing availability.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static RuntimeCommandProbeResult ProbeNode(ExternalExecutionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Probe(settings.NodeExecutable, new[] { "--version" }, "Node.js");
        }

        /// <summary>Probes availability of the configured .NET runtime executable.</summary>
        /// <param name="settings">External execution settings supplying the dotnet executable path.</param>
        /// <returns>The probe result describing availability.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static RuntimeCommandProbeResult ProbeDotnetRuntime(ExternalExecutionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Probe(settings.DotnetExecutable, new[] { "--info" }, ".NET");
        }

        /// <summary>Probes availability of the .NET SDK and verifies at least one SDK is installed.</summary>
        /// <param name="settings">External execution settings supplying the dotnet executable path.</param>
        /// <returns>The probe result describing availability, marked missing when no SDKs are reported.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static RuntimeCommandProbeResult ProbeDotnetSdk(ExternalExecutionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            RuntimeCommandProbeResult result = Probe(settings.DotnetExecutable, new[] { "--list-sdks" }, ".NET SDK");
            if (!result.Available) return result;
            if (string.IsNullOrWhiteSpace(result.Output))
            {
                return RuntimeCommandProbeResult.Missing(settings.DotnetExecutable, ".NET SDK command was found, but no installed SDKs were reported.");
            }

            return result;
        }

        /// <summary>Probes availability of an arbitrary executable, caching the result by command and arguments.</summary>
        /// <param name="executable">Executable path or command to run.</param>
        /// <param name="arguments">Arguments passed to the executable.</param>
        /// <param name="displayName">Human-readable name used in result messages.</param>
        /// <returns>The probe result describing availability.</returns>
        public static RuntimeCommandProbeResult Probe(string executable, string[] arguments, string displayName)
        {
            if (string.IsNullOrWhiteSpace(executable)) return RuntimeCommandProbeResult.Missing(executable, displayName + " executable is not configured.");
            string command = executable.Trim();
            string[] args = arguments ?? Array.Empty<string>();
            string cacheKey = command + "\u001f" + string.Join("\u001e", args);
            return _Cache.GetOrAdd(cacheKey, _ => ProbeUncached(command, args, displayName));
        }

        private static RuntimeCommandProbeResult ProbeUncached(string command, string[] arguments, string displayName)
        {
            try
            {
                if (Path.IsPathFullyQualified(command) && !File.Exists(command))
                    return RuntimeCommandProbeResult.Missing(command, displayName + " executable path does not exist.");

                using Process process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                foreach (string argument in arguments ?? Array.Empty<string>()) process.StartInfo.ArgumentList.Add(argument);
                if (!process.Start()) return RuntimeCommandProbeResult.Missing(command, displayName + " command did not start.");
                if (!process.WaitForExit(DefaultTimeoutMs))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return RuntimeCommandProbeResult.Missing(command, displayName + " command did not respond within " + DefaultTimeoutMs + "ms.");
                }

                string output = (process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd()).Trim();
                if (process.ExitCode != 0)
                {
                    string detail = string.IsNullOrWhiteSpace(output) ? "exit code " + process.ExitCode : output.Split('\n').Last().Trim();
                    return RuntimeCommandProbeResult.Missing(command, displayName + " command returned " + detail + ".");
                }

                return RuntimeCommandProbeResult.Found(command, output);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is IOException || ex is UnauthorizedAccessException)
            {
                return RuntimeCommandProbeResult.Missing(command, displayName + " command could not be started: " + ex.Message);
            }
        }
    }

    /// <summary>Result from probing a host runtime command.</summary>
    public sealed class RuntimeCommandProbeResult
    {
        /// <summary>True when the probed command is available.</summary>
        public bool Available { get; private set; }
        /// <summary>The command that was probed. Default: empty.</summary>
        public string Command { get; private set; } = string.Empty;
        /// <summary>Human-readable message describing the outcome. Default: empty.</summary>
        public string Message { get; private set; } = string.Empty;
        /// <summary>Captured command output. Default: empty.</summary>
        public string Output { get; private set; } = string.Empty;

        /// <summary>Creates a result indicating the command was found and available.</summary>
        /// <param name="command">The command that was probed.</param>
        /// <param name="output">Captured command output.</param>
        /// <returns>An available probe result.</returns>
        public static RuntimeCommandProbeResult Found(string command, string output)
        {
            return new RuntimeCommandProbeResult
            {
                Available = true,
                Command = command ?? string.Empty,
                Message = "Command is available.",
                Output = output ?? string.Empty
            };
        }

        /// <summary>Creates a result indicating the command was unavailable.</summary>
        /// <param name="command">The command that was probed, if known.</param>
        /// <param name="message">Message describing why the command is unavailable.</param>
        /// <returns>An unavailable probe result.</returns>
        public static RuntimeCommandProbeResult Missing(string? command, string message)
        {
            return new RuntimeCommandProbeResult
            {
                Available = false,
                Command = command ?? string.Empty,
                Message = message ?? "Command is unavailable.",
                Output = string.Empty
            };
        }
    }
}
