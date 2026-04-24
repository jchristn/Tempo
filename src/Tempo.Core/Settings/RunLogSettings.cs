namespace Tempo.Core.Settings
{
    using System;

    /// <summary>
    /// Settings controlling per-run log capture and retention.
    /// </summary>
    public class RunLogSettings
    {
        /// <summary>Whether run-log capture is enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Root directory containing per-run log directories.</summary>
        public string RootPath
        {
            get
            {
                return _RootPath;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(RootPath));
                _RootPath = value;
            }
        }

        /// <summary>Retention period in days for completed run-log directories.</summary>
        public int RetentionDays
        {
            get
            {
                return _RetentionDays;
            }
            set
            {
                _RetentionDays = Math.Clamp(value, 1, 3650);
            }
        }

        /// <summary>Prune cadence in minutes.</summary>
        public int PruneIntervalMinutes
        {
            get
            {
                return _PruneIntervalMinutes;
            }
            set
            {
                _PruneIntervalMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        /// <summary>Default tail line count used for run-log reads.</summary>
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

        /// <summary>Default maximum byte count returned in one bounded run-log read.</summary>
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

        /// <summary>Maximum tail line count allowed in one run-log read.</summary>
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

        /// <summary>Maximum byte count allowed in one run-log read.</summary>
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

        private string _RootPath = "./run-logs";
        private int _RetentionDays = 7;
        private int _PruneIntervalMinutes = 60;
        private int _DefaultTailLines = 400;
        private long _DefaultMaxBytes = 262144;
        private int _MaxTailLines = 5000;
        private long _MaxReadBytes = 1048576;
    }
}
