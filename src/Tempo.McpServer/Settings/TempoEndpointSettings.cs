namespace Tempo.McpServer.Settings
{
    /// <summary>
    /// Tempo API endpoint and authentication settings.
    /// </summary>
    public class TempoEndpointSettings
    {
        /// <summary>Tempo API endpoint.</summary>
        public string Endpoint { get; set; } = Constants.DefaultTempoEndpoint;

        /// <summary>Request timeout in milliseconds.</summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>Default tenant identifier used by tenant-scoped tools when omitted.</summary>
        public string? DefaultTenantId { get; set; } = null;

        /// <summary>Tempo x-token value.</summary>
        public string? Token { get; set; } = null;

        /// <summary>Tempo x-api-key value.</summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>Tempo x-access-key value.</summary>
        public string? AccessKey { get; set; } = null;

        /// <summary>Legacy unsupported setting retained for config compatibility. Ignored by the client.</summary>
        public string? SecretKey { get; set; } = null;
    }
}
