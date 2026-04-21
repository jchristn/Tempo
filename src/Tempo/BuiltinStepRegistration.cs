namespace Tempo
{
    using System;

    /// <summary>Metadata describing a registered in-process built-in step.</summary>
    public class BuiltinStepRegistration
    {
#pragma warning disable CS8625
        /// <summary>Stable execution key.</summary>
        public string ExecutionKey { get; set; } = string.Empty;

        /// <summary>Tenant identifier, or null for global registrations.</summary>
        public string TenantId { get; set; } = null;

        /// <summary>Registration source kind.</summary>
        public BuiltinStepSourceKind SourceKind { get; set; }

        /// <summary>User-facing display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Declaring type full name.</summary>
        public string DeclaringType { get; set; } = null;

        /// <summary>Method name when the source kind is <see cref="BuiltinStepSourceKind.Method"/>.</summary>
        public string MethodName { get; set; } = null;

        /// <summary>Assembly simple name.</summary>
        public string AssemblyName { get; set; } = null;

        /// <summary>Assembly version string.</summary>
        public string AssemblyVersion { get; set; } = null;

        /// <summary>Deterministic hash of the executable signature.</summary>
        public string SignatureHash { get; set; } = string.Empty;

        /// <summary>Maximum runtime in milliseconds.</summary>
        public int MaxRuntimeMs { get; set; }

        /// <summary>True when the registration is globally available.</summary>
        public bool IsGlobal => string.IsNullOrWhiteSpace(TenantId) || string.Equals(TenantId, "global", StringComparison.OrdinalIgnoreCase);

        /// <summary>Create a detached copy.</summary>
        public BuiltinStepRegistration Clone()
        {
            return (BuiltinStepRegistration)MemberwiseClone();
        }
#pragma warning restore CS8625
    }
}
