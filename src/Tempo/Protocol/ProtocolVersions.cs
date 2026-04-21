namespace Tempo.Protocol
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Public step protocol version constants and compatibility checks.
    /// </summary>
    public static class ProtocolVersions
    {
        /// <summary>Version 1.0 protocol.</summary>
        public const string V1 = "1.0";

        /// <summary>Current protocol version emitted by this host.</summary>
        public const string Current = V1;

        /// <summary>Environment variable containing the negotiated protocol version for a launched external process.</summary>
        public const string ProtocolVersionEnvironmentVariable = "TEMPO_PROTOCOL_VERSION";

        /// <summary>Environment variable containing comma-separated protocol versions supported by the launching server.</summary>
        public const string SupportedProtocolVersionsEnvironmentVariable = "TEMPO_SUPPORTED_PROTOCOL_VERSIONS";

        private static readonly string[] _Supported = new[] { V1 };

        /// <summary>Supported protocol versions.</summary>
        public static IReadOnlyList<string> Supported => _Supported;

        /// <summary>Return true when <paramref name="version"/> is supported. Empty values default to current.</summary>
        public static bool IsSupported(string? version)
        {
            string normalized = string.IsNullOrWhiteSpace(version) ? Current : version.Trim();
            return _Supported.Any(v => StringComparer.OrdinalIgnoreCase.Equals(v, normalized));
        }

        /// <summary>Return the canonical version string or throw for unsupported versions.</summary>
        public static string Normalize(string? version)
        {
            string normalized = string.IsNullOrWhiteSpace(version) ? Current : version.Trim();
            string? match = _Supported.FirstOrDefault(v => StringComparer.OrdinalIgnoreCase.Equals(v, normalized));
            if (match == null) throw new NotSupportedException("Unsupported Tempo step protocol version '" + normalized + "'.");
            return match;
        }
    }
}
