namespace Tempo.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>Runtime provider settings.</summary>
    public class RuntimeSettings
    {
        /// <summary>Settings for process-backed external execution.</summary>
        public ExternalExecutionSettings ExternalExecution
        {
            get => _ExternalExecution;
            set => _ExternalExecution = value ?? throw new ArgumentNullException(nameof(ExternalExecution));
        }

        /// <summary>Operator allowlist for host executable runtimes.</summary>
        public HostExecutableSettings HostExecutables
        {
            get => _HostExecutables;
            set => _HostExecutables = value ?? throw new ArgumentNullException(nameof(HostExecutables));
        }

        private ExternalExecutionSettings _ExternalExecution = new ExternalExecutionSettings();
        private HostExecutableSettings _HostExecutables = new HostExecutableSettings();
    }

    /// <summary>Limits and paths for process-backed external execution.</summary>
    public class ExternalExecutionSettings
    {
        /// <summary>Maximum concurrent external processes server-wide. Default: 4. Range: 1 to 1024.</summary>
        public int MaxConcurrentProcessesServerWide
        {
            get => _MaxConcurrentProcessesServerWide;
            set => _MaxConcurrentProcessesServerWide = Math.Clamp(value, 1, 1024);
        }

        /// <summary>Maximum concurrent external processes per tenant. Default: 2. Range: 1 to 256.</summary>
        public int MaxConcurrentProcessesPerTenant
        {
            get => _MaxConcurrentProcessesPerTenant;
            set => _MaxConcurrentProcessesPerTenant = Math.Clamp(value, 1, 256);
        }

        /// <summary>Default maximum runtime for process-backed steps. Default: 30000ms. Range: 100 to 86400000.</summary>
        public int DefaultMaxRuntimeMs
        {
            get => _DefaultMaxRuntimeMs;
            set => _DefaultMaxRuntimeMs = Math.Clamp(value, 100, 24 * 60 * 60 * 1000);
        }

        /// <summary>Maximum stdout bytes captured from a process. Default: 1 MiB.</summary>
        public long MaxStdoutBytes
        {
            get => _MaxStdoutBytes;
            set => _MaxStdoutBytes = Math.Clamp(value, 1, 1024L * 1024L * 1024L);
        }

        /// <summary>Maximum stderr bytes captured from a process. Default: 1 MiB.</summary>
        public long MaxStderrBytes
        {
            get => _MaxStderrBytes;
            set => _MaxStderrBytes = Math.Clamp(value, 1, 1024L * 1024L * 1024L);
        }

        /// <summary>Maximum JSON input bytes written to a process. Default: 1 MiB.</summary>
        public long MaxInputBytes
        {
            get => _MaxInputBytes;
            set => _MaxInputBytes = Math.Clamp(value, 1, 1024L * 1024L * 1024L);
        }

        /// <summary>Maximum JSON output bytes read from a process. Default: 1 MiB.</summary>
        public long MaxOutputBytes
        {
            get => _MaxOutputBytes;
            set => _MaxOutputBytes = Math.Clamp(value, 1, 1024L * 1024L * 1024L);
        }

        /// <summary>Tenant-isolated scratch root for external processes.</summary>
        public string ScratchRoot
        {
            get => _ScratchRoot;
            set => _ScratchRoot = !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentNullException(nameof(ScratchRoot));
        }

        /// <summary>Tenant-isolated cache root for reusable runtime assets.</summary>
        public string CacheRoot
        {
            get => _CacheRoot;
            set => _CacheRoot = !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentNullException(nameof(CacheRoot));
        }

        /// <summary>Environment variable names allowed to flow into external processes.</summary>
        public List<string> EnvironmentAllowList { get; set; } = new List<string>();

        /// <summary>Placeholder network policy mode for future sandboxing. Default: disabled.</summary>
        public string NetworkPolicyMode
        {
            get => _NetworkPolicyMode;
            set => _NetworkPolicyMode = string.IsNullOrWhiteSpace(value) ? "disabled" : value.Trim();
        }

        /// <summary>Whether cancellation should attempt to kill the process tree. Default: true.</summary>
        public bool KillProcessTreeOnCancel { get; set; } = true;

        /// <summary>Python executable used by Artifact.Python when no version-specific executable is provided.</summary>
        public string PythonExecutable
        {
            get => _PythonExecutable;
            set => _PythonExecutable = string.IsNullOrWhiteSpace(value) ? "python" : value.Trim();
        }

        /// <summary>Node.js executable used by Artifact.JavaScript.</summary>
        public string NodeExecutable
        {
            get => _NodeExecutable;
            set => _NodeExecutable = string.IsNullOrWhiteSpace(value) ? "node" : value.Trim();
        }

        /// <summary>.NET executable used by Artifact.DotnetProcess and source C# packaging.</summary>
        public string DotnetExecutable
        {
            get => _DotnetExecutable;
            set => _DotnetExecutable = string.IsNullOrWhiteSpace(value) ? "dotnet" : value.Trim();
        }

        /// <summary>Whether Artifact.Python may install package dependencies into a cached virtual environment.</summary>
        public bool AllowPythonDependencyInstall { get; set; } = false;

        private int _MaxConcurrentProcessesServerWide = 4;
        private int _MaxConcurrentProcessesPerTenant = 2;
        private int _DefaultMaxRuntimeMs = 30000;
        private long _MaxStdoutBytes = 1024L * 1024L;
        private long _MaxStderrBytes = 1024L * 1024L;
        private long _MaxInputBytes = 1024L * 1024L;
        private long _MaxOutputBytes = 1024L * 1024L;
        private string _ScratchRoot = "./scratch";
        private string _CacheRoot = "./runtime-cache";
        private string _NetworkPolicyMode = "disabled";
        private string _PythonExecutable = "python";
        private string _NodeExecutable = "node";
        private string _DotnetExecutable = "dotnet";
    }

    /// <summary>Settings for operator-provisioned host executable runtime entries.</summary>
    public class HostExecutableSettings
    {
        /// <summary>Whether Host.Executable runtime entries are available. Default: false.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Operator-owned executable allowlist. Paths are visible only through admin settings.</summary>
        public List<HostExecutableAllowListEntry> AllowList { get; set; } = new List<HostExecutableAllowListEntry>();

        /// <summary>Find an enabled allowlist entry by key.</summary>
        public HostExecutableAllowListEntry? Find(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            foreach (HostExecutableAllowListEntry entry in AllowList)
            {
                if (entry.Enabled && string.Equals(entry.Key, key, StringComparison.Ordinal)) return entry;
            }

            return null;
        }
    }

    /// <summary>One operator-approved host executable.</summary>
    public class HostExecutableAllowListEntry
    {
        /// <summary>Tenant-facing stable key. This is the only executable selector tenant config may reference.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Display name shown to operators.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Operator-owned absolute executable path. Never supplied by tenant config.</summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>Optional operator-owned working directory. Defaults to the executable directory.</summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>Fixed operator-supplied arguments prepended before tenant arguments.</summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>Environment variable names that this allowlist entry may read from the host process environment.</summary>
        public List<string> EnvironmentAllowList { get; set; } = new List<string>();

        /// <summary>Maximum runtime override for this executable. Zero uses the global external execution default or step override.</summary>
        public int MaxRuntimeMs
        {
            get => _MaxRuntimeMs;
            set => _MaxRuntimeMs = Math.Clamp(value, 0, 24 * 60 * 60 * 1000);
        }

        /// <summary>Whether this entry can be selected.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Tenant argument policy for this executable.</summary>
        public HostExecutableArgumentPolicy ArgumentPolicy
        {
            get => _ArgumentPolicy;
            set => _ArgumentPolicy = value ?? new HostExecutableArgumentPolicy();
        }

        private int _MaxRuntimeMs = 0;
        private HostExecutableArgumentPolicy _ArgumentPolicy = new HostExecutableArgumentPolicy();
    }

    /// <summary>Simple tenant argument policy for Host.Executable entries.</summary>
    public class HostExecutableArgumentPolicy
    {
        /// <summary>Whether tenant config may provide extra arguments. Default: false.</summary>
        public bool AllowAdditionalArguments { get; set; } = false;

        /// <summary>Maximum tenant-provided argument count. Applies when additional arguments are enabled.</summary>
        public int MaxArguments
        {
            get => _MaxArguments;
            set => _MaxArguments = Math.Clamp(value, 0, 256);
        }

        /// <summary>Exact argument values allowed when non-empty.</summary>
        public List<string> AllowedValues { get; set; } = new List<string>();

        /// <summary>Allowed argument prefixes when non-empty.</summary>
        public List<string> AllowedPrefixes { get; set; } = new List<string>();

        private int _MaxArguments = 32;
    }
}
