namespace Tempo.Core.Protocol
{
    using System.Collections.Generic;

    /// <summary>Protocol conformance validation result.</summary>
    public class ProtocolConformanceResult
    {
        /// <summary>Protocol version found in the envelope.</summary>
        public string? ProtocolVersion { get; set; } = null;

        /// <summary>Data flow identifier found in the envelope.</summary>
        public string? DataFlowId { get; set; } = null;

        /// <summary>Request identifier found in the envelope.</summary>
        public string? RequestId { get; set; } = null;

        /// <summary>Step result value found in a result envelope.</summary>
        public string? Result { get; set; } = null;

        /// <summary>Validation errors.</summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>True when there are no validation errors.</summary>
        public bool Valid => Errors.Count == 0;
    }
}
