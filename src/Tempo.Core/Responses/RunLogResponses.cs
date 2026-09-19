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
        /// <summary>
        /// The flow run this activity describes.
        /// </summary>
        public FlowRun Run { get; set; } = new FlowRun();

        /// <summary>
        /// Worker-assignment records associated with the run.
        /// </summary>
        public List<RunAssignmentRecord> Assignments { get; set; } = new List<RunAssignmentRecord>();

        /// <summary>
        /// Worker activity records associated with the run.
        /// </summary>
        public List<WorkerActivityRecord> Activity { get; set; } = new List<WorkerActivityRecord>();
    }

    /// <summary>
    /// Summary of one file within a run-log directory.
    /// </summary>
    public class RunLogFileSummaryResponse
    {
        /// <summary>
        /// Identifier of the flow run this file belongs to.
        /// </summary>
        public string FlowRunId { get; set; } = string.Empty;

        /// <summary>
        /// Full path of the run-log file.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Name of the run-log file.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Kind of the run-log file (for example, "Run").
        /// Default: "Run".
        /// </summary>
        public string Kind { get; set; } = "Run";

        /// <summary>
        /// Attempt number associated with the file.
        /// May be null if not applicable.
        /// </summary>
        public int? AttemptNumber { get; set; } = null;

        /// <summary>
        /// Identifier of the run assignment associated with the file.
        /// May be null if not applicable.
        /// </summary>
        public string? RunAssignmentId { get; set; } = null;

        /// <summary>
        /// Identifier of the worker associated with the file.
        /// May be null if not applicable.
        /// </summary>
        public string? WorkerId { get; set; } = null;

        /// <summary>
        /// Identifier of the step associated with the file.
        /// May be null if not applicable.
        /// </summary>
        public string? StepId { get; set; } = null;

        /// <summary>
        /// Identifier of the step run associated with the file.
        /// May be null if not applicable.
        /// </summary>
        public string? StepRunId { get; set; } = null;

        /// <summary>
        /// Length of the run-log file in bytes.
        /// Default: 0.
        /// </summary>
        public long ByteLength { get; set; } = 0;

        /// <summary>
        /// UTC timestamp of the last modification to the run-log file.
        /// </summary>
        public DateTime LastModifiedUtc { get; set; }

        /// <summary>
        /// Indicates whether the run-log file is currently active.
        /// Default: false.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Indicates whether deletion of this run-log file is allowed.
        /// Default: true.
        /// </summary>
        public bool DeleteAllowed { get; set; } = true;

        /// <summary>
        /// Indicates whether downloading of this run-log file is allowed.
        /// Default: true.
        /// </summary>
        public bool DownloadAllowed { get; set; } = true;

        /// <summary>
        /// Mode used when deleting the run-log file (for example, "Delete" or "Truncate").
        /// Default: "Delete".
        /// </summary>
        public string DeleteMode { get; set; } = "Delete";
    }

    /// <summary>
    /// Bounded read response for one run-log file.
    /// </summary>
    public class RunLogFileReadResponse : RunLogFileSummaryResponse
    {
        /// <summary>
        /// Content type of the returned run-log content.
        /// Default: "text/plain; charset=utf-8".
        /// </summary>
        public string ContentType { get; set; } = "text/plain; charset=utf-8";

        /// <summary>
        /// The run-log file content that was read.
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
    /// Run-log file delete or truncate response.
    /// </summary>
    public class RunLogDeleteResponse
    {
        /// <summary>
        /// Identifier of the flow run whose file was deleted or truncated.
        /// </summary>
        public string FlowRunId { get; set; } = string.Empty;

        /// <summary>
        /// Full path of the run-log file that was deleted or truncated.
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
