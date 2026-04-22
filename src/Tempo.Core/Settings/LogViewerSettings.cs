namespace Tempo.Core.Settings
{
    using System;

    /// <summary>
    /// Settings controlling the admin log-viewer surface.
    /// </summary>
    public class LogViewerSettings
    {
        /// <summary>Root directory containing per-worker log subdirectories.</summary>
        public string WorkerRootPath
        {
            get
            {
                return _WorkerRootPath;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(WorkerRootPath));
                _WorkerRootPath = value;
            }
        }

        /// <summary>Current worker log filename written within each worker directory.</summary>
        public string WorkerLogFilename
        {
            get
            {
                return _WorkerLogFilename;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(WorkerLogFilename));
                _WorkerLogFilename = value;
            }
        }

        /// <summary>Default tail line count used when reading logs.</summary>
        public int DefaultTailLines
        {
            get
            {
                return _DefaultTailLines;
            }
            set
            {
                _DefaultTailLines = Math.Clamp(value, 1, 100000);
            }
        }

        /// <summary>Maximum tail line count allowed in one read request.</summary>
        public int MaxTailLines
        {
            get
            {
                return _MaxTailLines;
            }
            set
            {
                _MaxTailLines = Math.Clamp(value, 1, 100000);
            }
        }

        /// <summary>Default maximum bytes returned in one bounded read request.</summary>
        public long DefaultMaxBytes
        {
            get
            {
                return _DefaultMaxBytes;
            }
            set
            {
                _DefaultMaxBytes = Math.Clamp(value, 1, 64L * 1024L * 1024L);
            }
        }

        /// <summary>Maximum bytes allowed in one bounded read request.</summary>
        public long MaxReadBytes
        {
            get
            {
                return _MaxReadBytes;
            }
            set
            {
                _MaxReadBytes = Math.Clamp(value, 1, 64L * 1024L * 1024L);
            }
        }

        private string _WorkerRootPath = "./worker-logs";
        private string _WorkerLogFilename = "tempo-worker.log";
        private int _DefaultTailLines = 200;
        private int _MaxTailLines = 5000;
        private long _DefaultMaxBytes = 131072;
        private long _MaxReadBytes = 1048576;
    }
}
