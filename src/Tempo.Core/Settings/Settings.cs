namespace Tempo.Core.Settings
{
    using System;

    /// <summary>
    /// Root settings object loaded from <c>tempo.json</c>.
    /// </summary>
    public class Settings
    {
        /// <summary>Timestamp at which the settings were created, in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>REST settings.</summary>
        public RestSettings Rest
        {
            get
            {
                return _Rest;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Rest));
                _Rest = value;
            }
        }

        /// <summary>Database settings.</summary>
        public DatabaseSettings Database
        {
            get
            {
                return _Database;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Database));
                _Database = value;
            }
        }

        /// <summary>Logging settings.</summary>
        public LoggingSettings Logging
        {
            get
            {
                return _Logging;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Logging));
                _Logging = value;
            }
        }

        /// <summary>Authentication settings.</summary>
        public AuthSettings Auth
        {
            get
            {
                return _Auth;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Auth));
                _Auth = value;
            }
        }

        /// <summary>Request history capture settings.</summary>
        public RequestHistorySettings RequestHistory
        {
            get
            {
                return _RequestHistory;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(RequestHistory));
                _RequestHistory = value;
            }
        }

        /// <summary>Workflow engine settings.</summary>
        public EngineSettings Engine
        {
            get
            {
                return _Engine;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Engine));
                _Engine = value;
            }
        }

        /// <summary>Hydration/seeding settings.</summary>
        public HydrationSettings Hydration
        {
            get
            {
                return _Hydration;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Hydration));
                _Hydration = value;
            }
        }

        /// <summary>Artifact storage settings.</summary>
        public ArtifactSettings Artifacts
        {
            get
            {
                return _Artifacts;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Artifacts));
                _Artifacts = value;
            }
        }

        /// <summary>Runtime provider settings.</summary>
        public RuntimeSettings Runtimes
        {
            get
            {
                return _Runtimes;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Runtimes));
                _Runtimes = value;
            }
        }

        /// <summary>Log viewer settings.</summary>
        public LogViewerSettings LogViewer
        {
            get
            {
                return _LogViewer;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(LogViewer));
                _LogViewer = value;
            }
        }

        private RestSettings _Rest = new RestSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private AuthSettings _Auth = new AuthSettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private EngineSettings _Engine = new EngineSettings();
        private HydrationSettings _Hydration = new HydrationSettings();
        private ArtifactSettings _Artifacts = new ArtifactSettings();
        private RuntimeSettings _Runtimes = new RuntimeSettings();
        private LogViewerSettings _LogViewer = new LogViewerSettings();
    }
}
