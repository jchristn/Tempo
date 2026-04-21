namespace Tempo.Core.Runtime
{
    using System;
    using System.Threading.Tasks;

    /// <summary>Lease for one external runtime execution slot.</summary>
    public sealed class ExternalRuntimeCapacityLease : IDisposable, IAsyncDisposable
    {
        private readonly ExternalRuntimeCapacityManager _Owner;
        private int _Disposed = 0;

        internal ExternalRuntimeCapacityLease(ExternalRuntimeCapacityManager owner, string tenantId, string stepRunId, DateTime requestedUtc, DateTime acquiredUtc)
        {
            _Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            TenantId = tenantId;
            StepRunId = stepRunId;
            RequestedUtc = requestedUtc;
            AcquiredUtc = acquiredUtc;
            CapacityWaitMs = Math.Max(0, (long)(acquiredUtc - requestedUtc).TotalMilliseconds);
        }

        /// <summary>Tenant that owns the slot.</summary>
        public string TenantId { get; }

        /// <summary>Optional step run identifier waiting for capacity.</summary>
        public string StepRunId { get; }

        /// <summary>UTC time the slot was requested.</summary>
        public DateTime RequestedUtc { get; }

        /// <summary>UTC time the slot was acquired.</summary>
        public DateTime AcquiredUtc { get; }

        /// <summary>Capacity wait duration in milliseconds.</summary>
        public long CapacityWaitMs { get; }

        /// <summary>Release the slot.</summary>
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _Disposed, 1) == 0)
                _Owner.Release(this, DateTime.UtcNow);
        }

        /// <summary>Release the slot.</summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
