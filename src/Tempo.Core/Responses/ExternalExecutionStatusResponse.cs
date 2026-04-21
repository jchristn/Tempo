namespace Tempo.Core.Responses
{
    using System.Collections.Generic;
    using Tempo.Core.Runtime;
    using Tempo.Core.Settings;

    /// <summary>External execution status and capacity pressure snapshot.</summary>
    public class ExternalExecutionStatusResponse
    {
        /// <summary>Maximum concurrent external processes server-wide.</summary>
        public int MaxConcurrentProcessesServerWide { get; set; }

        /// <summary>Maximum concurrent external processes per tenant.</summary>
        public int MaxConcurrentProcessesPerTenant { get; set; }

        /// <summary>Default maximum runtime for process-backed steps.</summary>
        public int DefaultMaxRuntimeMs { get; set; }

        /// <summary>Maximum stdout bytes captured from a process.</summary>
        public long MaxStdoutBytes { get; set; }

        /// <summary>Maximum stderr bytes captured from a process.</summary>
        public long MaxStderrBytes { get; set; }

        /// <summary>Maximum JSON input bytes written to a process.</summary>
        public long MaxInputBytes { get; set; }

        /// <summary>Maximum JSON output bytes read from a process.</summary>
        public long MaxOutputBytes { get; set; }

        /// <summary>Tenant-isolated scratch root.</summary>
        public string ScratchRoot { get; set; } = string.Empty;

        /// <summary>Tenant-isolated runtime cache root.</summary>
        public string CacheRoot { get; set; } = string.Empty;

        /// <summary>Allowed environment variable names.</summary>
        public List<string> EnvironmentAllowList { get; set; } = new List<string>();

        /// <summary>Configured network policy mode placeholder.</summary>
        public string NetworkPolicyMode { get; set; } = string.Empty;

        /// <summary>Whether cancellation should attempt to kill the process tree.</summary>
        public bool KillProcessTreeOnCancel { get; set; }

        /// <summary>Configured Python executable.</summary>
        public string PythonExecutable { get; set; } = string.Empty;

        /// <summary>Configured Node.js executable.</summary>
        public string NodeExecutable { get; set; } = string.Empty;

        /// <summary>Configured .NET executable.</summary>
        public string DotnetExecutable { get; set; } = string.Empty;

        /// <summary>Capacity pressure counters.</summary>
        public ExternalRuntimeCapacitySnapshot Capacity { get; set; } = new ExternalRuntimeCapacitySnapshot();

        /// <summary>Optional tenant highlighted in this response.</summary>
        public string? TenantId { get; set; } = null;

        /// <summary>Active external processes for <see cref="TenantId"/>.</summary>
        public int TenantActiveProcesses { get; set; }

        /// <summary>Queued external steps for <see cref="TenantId"/>.</summary>
        public int TenantQueuedSteps { get; set; }

        /// <summary>Create a response from settings and capacity counters.</summary>
        public static ExternalExecutionStatusResponse From(Settings settings, ExternalRuntimeCapacitySnapshot snapshot, string? tenantId = null)
        {
            ExternalExecutionSettings external = settings.Runtimes.ExternalExecution;
            ExternalExecutionStatusResponse response = new ExternalExecutionStatusResponse
            {
                MaxConcurrentProcessesServerWide = external.MaxConcurrentProcessesServerWide,
                MaxConcurrentProcessesPerTenant = external.MaxConcurrentProcessesPerTenant,
                DefaultMaxRuntimeMs = external.DefaultMaxRuntimeMs,
                MaxStdoutBytes = external.MaxStdoutBytes,
                MaxStderrBytes = external.MaxStderrBytes,
                MaxInputBytes = external.MaxInputBytes,
                MaxOutputBytes = external.MaxOutputBytes,
                ScratchRoot = external.ScratchRoot,
                CacheRoot = external.CacheRoot,
                EnvironmentAllowList = new List<string>(external.EnvironmentAllowList),
                NetworkPolicyMode = external.NetworkPolicyMode,
                KillProcessTreeOnCancel = external.KillProcessTreeOnCancel,
                PythonExecutable = external.PythonExecutable,
                NodeExecutable = external.NodeExecutable,
                DotnetExecutable = external.DotnetExecutable,
                Capacity = snapshot,
                TenantId = tenantId
            };

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                response.TenantActiveProcesses = snapshot.ActiveByTenant.TryGetValue(tenantId, out int active) ? active : 0;
                response.TenantQueuedSteps = snapshot.QueuedByTenant.TryGetValue(tenantId, out int queued) ? queued : 0;
            }

            return response;
        }
    }
}
