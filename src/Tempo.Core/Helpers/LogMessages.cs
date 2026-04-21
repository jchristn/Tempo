namespace Tempo.Core.Helpers
{
    /// <summary>Formatting helpers for emitted log messages.</summary>
    public static class LogMessages
    {
        /// <summary>Remove trailing whitespace and terminal periods from a log message.</summary>
        public static string WithoutTerminalPeriod(string? message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            return message.TrimEnd().TrimEnd('.');
        }
    }
}
