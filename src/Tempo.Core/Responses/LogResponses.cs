namespace Tempo.Core.Responses
{
    using System;

    /// <summary>
    /// Summary of an available log source.
    /// </summary>
    public class LogSourceSummaryResponse
    {
        public string SourceKind { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Available { get; set; } = false;
        public bool HasFiles { get; set; } = false;
        public int FileCount { get; set; } = 0;
        public bool Enabled { get; set; } = true;
        public bool Active { get; set; } = false;
        public string? State { get; set; } = null;
        public string? HostName { get; set; } = null;
        public DateTime? LastModifiedUtc { get; set; } = null;
    }

    /// <summary>
    /// Summary of one log file within a source.
    /// </summary>
    public class LogFileSummaryResponse
    {
        public string SourceKind { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long ByteLength { get; set; } = 0;
        public DateTime LastModifiedUtc { get; set; }
        public bool IsCurrent { get; set; } = false;
        public bool SourceActive { get; set; } = false;
        public bool DeleteAllowed { get; set; } = true;
        public bool DownloadAllowed { get; set; } = true;
        public string DeleteMode { get; set; } = "Delete";
    }

    /// <summary>
    /// Bounded log file read response.
    /// </summary>
    public class LogFileReadResponse : LogFileSummaryResponse
    {
        public string ContentType { get; set; } = "text/plain; charset=utf-8";
        public string Content { get; set; } = string.Empty;
        public bool Truncated { get; set; } = false;
        public int TailLines { get; set; } = 0;
        public long MaxBytes { get; set; } = 0;
        public long ReturnedByteLength { get; set; } = 0;
    }

    /// <summary>
    /// Log file delete or truncate response.
    /// </summary>
    public class LogFileDeleteResponse
    {
        public string SourceKind { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
    }
}
