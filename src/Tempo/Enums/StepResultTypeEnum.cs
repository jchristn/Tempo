namespace Tempo.Enums
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Step result types.
    /// </summary>
    public enum StepResultTypeEnum
    {
        /// <summary>
        /// Successs.
        /// </summary>
        Success,
        /// <summary>
        /// Timeout.
        /// </summary>
        Timeout,
        /// <summary>
        /// Error.
        /// </summary>
        Error,
        /// <summary>
        /// Exception.
        /// </summary>
        Exception,
        /// <summary>
        /// MaxIterationsExceeded.
        /// </summary>
        MaxIterationsExceeded
    }
}
