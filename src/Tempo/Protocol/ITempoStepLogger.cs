namespace Tempo.Protocol
{
    /// <summary>Minimal logging surface exposed to step handlers during execution.</summary>
    public interface ITempoStepLogger
    {
        /// <summary>Write a debug log line.</summary>
        void Debug(string message);

        /// <summary>Write an informational log line.</summary>
        void Info(string message);

        /// <summary>Write a warning log line.</summary>
        void Warn(string message);

        /// <summary>Write an error log line.</summary>
        void Error(string message);
    }
}
