namespace Tempo.Core.Protocol
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Tempo.Core.Models;
    using Tempo.Protocol;

    /// <summary>Negotiates Tempo step protocol versions for runtime artifacts and runners.</summary>
    public class ProtocolNegotiator
    {
        /// <summary>Versions supported by this host.</summary>
        public IReadOnlyList<string> SupportedVersions => ProtocolVersions.Supported;

        /// <summary>Negotiate a requested protocol version. Empty requests default to current.</summary>
        public ProtocolNegotiationResult Negotiate(string? requestedVersion)
        {
            string requested = string.IsNullOrWhiteSpace(requestedVersion) ? ProtocolVersions.Current : requestedVersion.Trim();
            if (ProtocolVersions.IsSupported(requested))
            {
                return new ProtocolNegotiationResult
                {
                    RequestedVersion = requestedVersion,
                    NegotiatedVersion = ProtocolVersions.Normalize(requested),
                    Supported = true,
                    Message = "Protocol version " + ProtocolVersions.Normalize(requested) + " is supported."
                };
            }

            return new ProtocolNegotiationResult
            {
                RequestedVersion = requestedVersion,
                NegotiatedVersion = null,
                Supported = false,
                Message = "Protocol version " + requested + " is not supported by this host."
            };
        }

        /// <summary>Negotiate a set of declared protocol versions. Multiple distinct declarations are rejected as ambiguous.</summary>
        public ProtocolNegotiationResult Negotiate(IEnumerable<string?> requestedVersions)
        {
            if (requestedVersions == null) return Negotiate((string?)null);
            List<string> versions = requestedVersions
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (versions.Count == 0) return Negotiate((string?)null);
            if (versions.Count > 1)
            {
                return new ProtocolNegotiationResult
                {
                    RequestedVersion = string.Join(",", versions),
                    NegotiatedVersion = null,
                    Supported = false,
                    Message = "Multiple protocol versions were requested; declaration is ambiguous."
                };
            }

            return Negotiate(versions[0]);
        }

        /// <summary>Ensure a requested protocol version is supported and return its canonical form.</summary>
        public string EnsureSupported(string? requestedVersion)
        {
            ProtocolNegotiationResult result = Negotiate(requestedVersion);
            if (!result.Supported || string.IsNullOrWhiteSpace(result.NegotiatedVersion))
                throw new NotSupportedException(result.Message);
            return result.NegotiatedVersion;
        }

        /// <summary>Ensure a set of declared protocol versions negotiates to exactly one supported version.</summary>
        public string EnsureSupported(IEnumerable<string?> requestedVersions)
        {
            ProtocolNegotiationResult result = Negotiate(requestedVersions);
            if (!result.Supported || string.IsNullOrWhiteSpace(result.NegotiatedVersion))
                throw new NotSupportedException(result.Message);
            return result.NegotiatedVersion;
        }

        /// <summary>Negotiate the protocol version declared by an artifact manifest.</summary>
        public ProtocolNegotiationResult Negotiate(ArtifactManifest? manifest)
        {
            return Negotiate(manifest?.ProtocolVersion);
        }

        /// <summary>Ensure an artifact manifest's protocol version is supported.</summary>
        public string EnsureSupported(ArtifactManifest? manifest)
        {
            return EnsureSupported(manifest?.ProtocolVersion);
        }
    }
}
