namespace Tempo.Metrics
{
    using PrettyId;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Enums;

    /// <summary>
    /// Step run details.
    /// </summary>
    public class StepRunDetails
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Row identifier.
        /// </summary>
        public string RowId { get; set; } = null;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get => _TenantId;
            set => _TenantId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(TenantId));
        }

        /// <summary>
        /// Data flow identifier.
        /// </summary>
        public string DataFlowId
        {
            get => _DataFlowId;
            set => _DataFlowId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(DataFlowId));
        }

        /// <summary>
        /// Step identifier.
        /// </summary>
        public string StepId
        {
            get => _StepId;
            set => _StepId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(StepId));
        }

        /// <summary>
        /// Request identifier.
        /// </summary>
        public string RequestId
        {
            get => _RequestId;
            set => _RequestId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(RequestId));
        }

        /// <summary>
        /// Next step identifier.
        /// </summary>
        public string NextStepId { get; set; } = null;

        /// <summary>
        /// Start time, in UTC time.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End time, in UTC time.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total runtime in milliseconds.
        /// </summary>
        public double TotalMs
        {
            get => (EndUtc - StartUtc).TotalMilliseconds;
        }

        /// <summary>
        /// Result.
        /// </summary>
        public StepResultTypeEnum Result { get; set; } = StepResultTypeEnum.Success;

        private string _TenantId = new IdGenerator().Generate("tenant_", 64);
        private string _DataFlowId = new IdGenerator().Generate("dataflow_", 64);
        private string _StepId = new IdGenerator().Generate("step_", 64);
        private string _RequestId = new IdGenerator().Generate("request_", 64);

        /// <summary>
        /// Step run details.
        /// </summary>
        public StepRunDetails()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
