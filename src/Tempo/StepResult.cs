namespace Tempo
{
    using PrettyId;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Enums;

    /// <summary>
    /// Step result.
    /// </summary>
    public class StepResult
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Data flow identifier.
        /// </summary>
        public string DataFlowId
        {
            get => _DataFlowId;
            set => _DataFlowId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(DataFlowId)));
        }

        /// <summary>
        /// Request identifier.
        /// </summary>
        public string RequestId
        {
            get => _RequestId;
            set => _RequestId = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(RequestId)));
        }

        /// <summary>
        /// Step result.
        /// </summary>
        public StepResultTypeEnum Result { get; set; } = StepResultTypeEnum.Success;

        /// <summary>
        /// Request data.
        /// </summary>
        public object Data { get; set; } = null;

        /// <summary>
        /// Exception data.
        /// </summary>
        public Exception Exception { get; set; } = null;

        /// <summary>
        /// Metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        private string _DataFlowId = new IdGenerator().Generate("dataflow_", 64);
        private string _RequestId = new IdGenerator().Generate("request_", 64);

        /// <summary>
        /// Step result.
        /// </summary>
        public StepResult()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.-
    }
}
