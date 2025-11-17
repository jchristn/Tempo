namespace Tempo.Enums
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Step types.
    /// </summary>
    public enum StepTypeEnum
    {
        /// <summary>
        /// Code-based step (looked up from StepManager).
        /// </summary>
        Code,
        /// <summary>
        /// REST API step.
        /// </summary>
        Rest
    }
}
