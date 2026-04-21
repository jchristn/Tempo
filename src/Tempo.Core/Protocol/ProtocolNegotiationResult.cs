namespace Tempo.Core.Protocol
{
    /// <summary>Protocol negotiation outcome.</summary>
    public class ProtocolNegotiationResult
    {
        /// <summary>Requested protocol version, or null when caller omitted it.</summary>
        public string? RequestedVersion { get; set; } = null;

        /// <summary>Negotiated protocol version when supported.</summary>
        public string? NegotiatedVersion { get; set; } = null;

        /// <summary>True when negotiation succeeded.</summary>
        public bool Supported { get; set; } = false;

        /// <summary>Human-readable outcome.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
