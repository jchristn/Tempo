namespace Tempo.Enums
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Trigger types.
    /// </summary>
    public enum TriggerTypeEnum
    {
        /// <summary>
        /// HTTP.
        /// </summary>
        Http,
        /// <summary>
        /// RabbitMQ.
        /// </summary>
        RabbitMq,
        /// <summary>
        /// Native.
        /// </summary>
        Native
    }
}
