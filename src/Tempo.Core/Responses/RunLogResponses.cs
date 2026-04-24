namespace Tempo.Core.Responses
{
    using System;
    using System.Collections.Generic;
    using Tempo.Core.Models;

    /// <summary>
    /// Run history plus worker-assignment activity.
    /// </summary>
    public class RunActivityResponse
    {
        public FlowRun Run { get; set; } = new FlowRun();
        public List<RunAssignmentRecord> Assignments { get; set; } = new List<RunAssignmentRecord>();
        public List<WorkerActivityRecord> Activity { get; set; } = new List<WorkerActivityRecord>();
    }

    /// <summary>
    /// Summary of one file within a run-log directory.
    /// </summary>
    public class RunLogFileSummaryResponse
    {
        public string FlowRunId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Kind { get; set; } = "Run";
        public int? AttemptNumber { get; set; } = null;
        public string? RunAssignmentId { get; set; } = null;
        public string? WorkerId { get; set; } = null;
        public string? StepId { get; set; } = null;
        public string? StepRunId { get; set; } = null;
        public long ByteLength { get; set; } = 0;
        public DateTime LastModifiedUtc { get; set; }
        public bool Active { get; set; } = false;
        public bool DeleteAllowed { get; set; } = true;
        public bool DownloadAllowed { get; set; } = true;
        public string DeleteMode { get; set; } = "Delete";
    }

    /// <summary>
    /// Bounded read response for one run-log file.
    /// </summary>
    public class RunLogFileReadResponse : RunLogFileSummaryResponse
    {
        public string ContentType { get; set; } = "text/plain; charset=utf-8";
        public string Content { get; set; } = string.Empty;
        public bool Truncated { get; set; } = false;
        public int TailLines { get; set; } = 0;
        public long MaxBytes { get; set; } = 0;
        public long ReturnedByteLength { get; set; } = 0;
    }

    /// <summary>
    /// Run-log file delete or truncate response.
    /// </summary>
    public class RunLogDeleteResponse
    {
        public string FlowRunId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
    }
}
