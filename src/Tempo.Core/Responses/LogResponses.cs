namespace Tempo.Core.Responses
{
    using System;

    /// <summary>
    /// Summary of an available log source.
    /// </summary>
    public class LogSourceSummaryResponse
    {
        /// <summary>
        /// Kind of the log source (for example, the source category or type).
        /// </summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier of the log source.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable display name for the log source.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the log source is currently available.
        /// Default: false.
        /// </summary>
        public bool Available { get; set; } = false;

        /// <summary>
        /// Indicates whether the log source has any files.
        /// Default: false.
        /// </summary>
        public bool HasFiles { get; set; } = false;

        /// <summary>
        /// Number of files contained in the log source.
        /// Default: 0.
        /// </summary>
        public int FileCount { get; set; } = 0;

        /// <summary>
        /// Indicates whether the log source is enabled.
        /// Default: true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Indicates whether the log source is currently active.
        /// Default: false.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Current state of the log source.
        /// May be null if no state is available.
        /// </summary>
        public string? State { get; set; } = null;

        /// <summary>
        /// Host name associated with the log source.
        /// May be null if not applicable.
        /// </summary>
        public string? HostName { get; set; } = null;

        /// <summary>
        /// UTC timestamp of the last modification to the log source.
        /// May be null if unknown.
        /// </summary>
        public DateTime? LastModifiedUtc { get; set; } = null;
    }

    /// <summary>
    /// Summary of one log file within a source.
    /// </summary>
    public class LogFileSummaryResponse
    {
        /// <summary>
        /// Kind of the log source that contains this file.
        /// </summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier of the log source that contains this file.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// Full path of the log file.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Name of the log file.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Length of the log file in bytes.
        /// Default: 0.
        /// </summary>
        public long ByteLength { get; set; } = 0;

        /// <summary>
        /// UTC timestamp of the last modification to the log file.
        /// </summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>
        /// Indicates whether this is the current (active) log file.
        /// Default: false.
        /// </summary>
        public bool IsCurrent { get; set; } = false;

        /// <summary>
        /// Indicates whether the source of this log file is currently active.
        /// Default: false.
        /// </summary>
        public bool SourceActive { get; set; } = false;

        /// <summary>
        /// Indicates whether deletion of this log file is allowed.
        /// Default: true.
        /// </summary>
        public bool DeleteAllowed { get; set; } = true;

        /// <summary>
        /// Indicates whether downloading of this log file is allowed.
        /// Default: true.
        /// </summary>
        public bool DownloadAllowed { get; set; } = true;

        /// <summary>
        /// Mode used when deleting the log file (for example, "Delete" or "Truncate").
        /// Default: "Delete".
        /// </summary>
        public string DeleteMode { get; set; } = "Delete";
    }

    /// <summary>
    /// Bounded log file read response.
    /// </summary>
    public class LogFileReadResponse : LogFileSummaryResponse
    {
        /// <summary>
        /// Content type of the returned log content.
        /// Default: "text/plain; charset=utf-8".
        /// </summary>
        public string ContentType { get; set; } = "text/plain; charset=utf-8";

        /// <summary>
        /// The log file content that was read.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the returned content was truncated.
        /// Default: false.
        /// </summary>
        public bool Truncated { get; set; } = false;

        /// <summary>
        /// Number of trailing lines requested from the end of the file.
        /// Default: 0.
        /// </summary>
        public int TailLines { get; set; } = 0;

        /// <summary>
        /// Maximum number of bytes requested for the read.
        /// Default: 0.
        /// </summary>
        public long MaxBytes { get; set; } = 0;

        /// <summary>
        /// Number of bytes actually returned in the content.
        /// Default: 0.
        /// </summary>
        public long ReturnedByteLength { get; set; } = 0;
    }

    /// <summary>
    /// Log file delete or truncate response.
    /// </summary>
    public class LogFileDeleteResponse
    {
        /// <summary>
        /// Kind of the log source that contained the file.
        /// </summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier of the log source that contained the file.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// Full path of the log file that was deleted or truncated.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Action that was performed (for example, "Delete" or "Truncate").
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the action succeeded.
        /// Default: true.
        /// </summary>
        public bool Success { get; set; } = true;
    }
}
