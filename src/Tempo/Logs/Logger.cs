namespace Tempo.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Logger.
    /// </summary>
    public abstract class Logger
    {
        /// <summary>
        /// Logger.
        /// </summary>
        public Logger()
        {

        }

        /// <summary>
        /// Emit a log message.
        /// </summary>
        /// <param name="sev">Severity.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Log(SeverityEnum sev, string msg);

        /// <summary>
        /// Emit a debug message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Debug(string msg);

        /// <summary>
        /// Emit an informational message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Info(string msg);

        /// <summary>
        /// Emit a warning message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Warn(string msg);

        /// <summary>
        /// Emit an error message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Error(string msg);

        /// <summary>
        /// Emit an alert message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Alert(string msg);

        /// <summary>
        /// Emit a critical message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Critical(string msg);

        /// <summary>
        /// Emit an emergency message.
        /// </summary>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Emergency(string msg);

        /// <summary>
        /// Emit a request-specific log message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="sev">Severity.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Log(string requestIdentifier, SeverityEnum sev, string msg);

        /// <summary>
        /// Emit a request-specific debug message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Debug(string requestIdentifier, string msg);

        /// <summary>
        /// Emit a request-specific informational message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Info(string requestIdentifier, string msg);

        /// <summary>
        /// Emit a request-specific warning message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Warn(string requestIdentifier, string msg);

        /// <summary>
        /// Emit a request-specific error message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Error(string requestIdentifier, string msg);

        /// <summary>
        /// Emit a request-specific alert message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Alert(string requestIdentifier, string msg);

        /// <summary>
        /// Emit a request-specific critical message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Critical(string requestIdentifier, string msg);

        /// <summary>
        /// Emit a request-specific emergency message.
        /// </summary>
        /// <param name="requestIdentifier">Request identifier.</param>
        /// <param name="msg">Message.</param>
        /// <returns>Task.</returns>
        public abstract Task Emergency(string requestIdentifier, string msg);
    }
}
