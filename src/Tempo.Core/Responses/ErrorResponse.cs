namespace Tempo.Core.Responses
{
    /// <summary>
    /// Structured error response body.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>Error code identifier.</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>Human-readable message.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Optional details for logging/troubleshooting.</summary>
        public string? Details { get; set; } = null;

        /// <summary>Default constructor.</summary>
        public ErrorResponse() { }

        /// <summary>
        /// Instantiate with a code and message.
        /// </summary>
        /// <param name="error">Error code.</param>
        /// <param name="message">Human-readable message.</param>
        /// <param name="details">Optional details.</param>
        public ErrorResponse(string error, string message, string? details = null)
        {
            Error = error;
            Message = message;
            Details = details;
        }
    }
}
