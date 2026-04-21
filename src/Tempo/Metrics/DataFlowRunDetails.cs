namespace Tempo.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tempo.Enums;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

    /// <summary>
    /// Data flow run details.
    /// </summary>
    public class DataFlowRunDetails
    {
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
        /// Request identifier.
        /// </summary>
        public string RequestId
        {
            get => _RequestId;
            set => _RequestId = !string.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(RequestId));
        }

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

        private string _TenantId = Tempo.TempoIds.GenerateTenantId();
        private string _DataFlowId = Tempo.TempoIds.GenerateDataFlowId();
        private string _RequestId = Tempo.TempoIds.GenerateRequestId();

        /// <summary>
        /// Data flow run details.
        /// </summary>
        public DataFlowRunDetails()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
