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
    /// Step request.
    /// </summary>
    public class StepRequest
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
        /// Request data.
        /// </summary>
        public object Data { get; set; } = null;

        /// <summary>
        /// Metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        /// <summary>
        /// Result type from the previous step in the data flow.
        /// Null if this is the first step.
        /// </summary>
        public StepResultTypeEnum? PreviousResult { get; set; } = null;

        private string _DataFlowId = new IdGenerator().Generate("dataflow_", 64);
        private string _RequestId = new IdGenerator().Generate("request_", 64);

        /// <summary>
        /// Step request.
        /// </summary>
        public StepRequest()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
