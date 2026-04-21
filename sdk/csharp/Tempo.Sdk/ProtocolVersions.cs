namespace Tempo.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Tempo step protocol version constants and compatibility helpers.</summary>
    public static class ProtocolVersions
    {
        /// <summary>Protocol version 1.0.</summary>
        public const string V1 = "1.0";

        /// <summary>Current protocol version emitted by this SDK.</summary>
        public const string Current = V1;

        /// <summary>Environment variable containing the negotiated protocol version.</summary>
        public const string ProtocolVersionEnvironmentVariable = "TEMPO_PROTOCOL_VERSION";

        /// <summary>Environment variable containing comma-separated server-supported protocol versions.</summary>
        public const string SupportedProtocolVersionsEnvironmentVariable = "TEMPO_SUPPORTED_PROTOCOL_VERSIONS";

        private static readonly string[] _Supported = new[] { V1 };

        /// <summary>Supported protocol versions.</summary>
        public static IReadOnlyList<string> Supported => _Supported;

        /// <summary>Return true when the supplied version is supported. Empty values default to current.</summary>
        public static bool IsSupported(string? version)
        {
            string normalized = string.IsNullOrWhiteSpace(version) ? Current : version.Trim();
            return _Supported.Any(v => string.Equals(v, normalized, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Return the canonical protocol version or throw for unsupported values.</summary>
        public static string Normalize(string? version)
        {
            string normalized = string.IsNullOrWhiteSpace(version) ? Current : version.Trim();
            string? match = _Supported.FirstOrDefault(v => string.Equals(v, normalized, StringComparison.OrdinalIgnoreCase));
            if (match == null) throw new NotSupportedException("Unsupported Tempo step protocol version '" + normalized + "'.");
            return match;
        }
    }
}
