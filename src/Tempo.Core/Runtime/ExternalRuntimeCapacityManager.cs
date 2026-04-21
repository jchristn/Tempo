namespace Tempo.Core.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tempo.Core.Settings;

    /// <summary>Coordinates server-wide and per-tenant slots for process-backed runtimes.</summary>
    public class ExternalRuntimeCapacityManager
    {
        private readonly object _Lock = new object();
        private readonly SemaphoreSlim _ServerSlots;
        private readonly Dictionary<string, TenantCapacity> _Tenants = new Dictionary<string, TenantCapacity>(StringComparer.Ordinal);
        private readonly int _MaxServerWide;
        private readonly int _MaxPerTenant;
        private int _ActiveServerWide = 0;
        private int _QueuedServerWide = 0;
        private long _TotalCapacityWaitMs = 0;
        private long _TotalProcessRuntimeMs = 0;
        private int _ProcessKillCount = 0;

        /// <summary>Instantiate.</summary>
        public ExternalRuntimeCapacityManager(ExternalExecutionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _MaxServerWide = settings.MaxConcurrentProcessesServerWide;
            _MaxPerTenant = settings.MaxConcurrentProcessesPerTenant;
            _ServerSlots = new SemaphoreSlim(_MaxServerWide, _MaxServerWide);
        }

        /// <summary>Acquire one external runtime execution slot, waiting until both tenant and server capacity are available.</summary>
        public async Task<ExternalRuntimeCapacityLease> AcquireAsync(string tenantId, string? stepRunId = null, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            string normalizedTenantId = tenantId.Trim();
            string normalizedStepRunId = string.IsNullOrWhiteSpace(stepRunId) ? string.Empty : stepRunId.Trim();
            DateTime requestedUtc = DateTime.UtcNow;

            TenantCapacity tenant = GetTenant(normalizedTenantId);
            bool tenantQueued = false;
            bool tenantAcquired = false;
            bool serverQueued = false;
            bool serverAcquired = false;

            try
            {
                lock (_Lock)
                {
                    if (tenant.Active >= _MaxPerTenant)
                    {
                        tenant.Queued++;
                        tenantQueued = true;
                    }
                }

                await tenant.Slots.WaitAsync(token).ConfigureAwait(false);
                tenantAcquired = true;

                lock (_Lock)
                {
                    if (tenantQueued) tenant.Queued--;
                    tenant.Active++;
                }

                lock (_Lock)
                {
                    if (_ActiveServerWide >= _MaxServerWide)
                    {
                        _QueuedServerWide++;
                        serverQueued = true;
                    }
                }

                await _ServerSlots.WaitAsync(token).ConfigureAwait(false);
                serverAcquired = true;

                DateTime acquiredUtc = DateTime.UtcNow;
                ExternalRuntimeCapacityLease lease = new ExternalRuntimeCapacityLease(this, normalizedTenantId, normalizedStepRunId, requestedUtc, acquiredUtc);
                lock (_Lock)
                {
                    if (serverQueued) _QueuedServerWide--;
                    _ActiveServerWide++;
                    _TotalCapacityWaitMs += lease.CapacityWaitMs;
                }

                return lease;
            }
            catch
            {
                if (serverAcquired)
                {
                    _ServerSlots.Release();
                    lock (_Lock) { _ActiveServerWide = Math.Max(0, _ActiveServerWide - 1); }
                }

                if (serverQueued)
                {
                    lock (_Lock) { _QueuedServerWide = Math.Max(0, _QueuedServerWide - 1); }
                }

                if (tenantAcquired)
                {
                    tenant.Slots.Release();
                    lock (_Lock) { tenant.Active = Math.Max(0, tenant.Active - 1); }
                }
                else if (tenantQueued)
                {
                    lock (_Lock) { tenant.Queued = Math.Max(0, tenant.Queued - 1); }
                }

                throw;
            }
        }

        /// <summary>Record that an external process was killed.</summary>
        public void RecordProcessKilled()
        {
            lock (_Lock) { _ProcessKillCount++; }
        }

        /// <summary>Read current counters.</summary>
        public ExternalRuntimeCapacitySnapshot Snapshot()
        {
            lock (_Lock)
            {
                ExternalRuntimeCapacitySnapshot snapshot = new ExternalRuntimeCapacitySnapshot
                {
                    MaxServerWide = _MaxServerWide,
                    MaxPerTenant = _MaxPerTenant,
                    ActiveServerWide = _ActiveServerWide,
                    QueuedServerWide = _QueuedServerWide,
                    TotalCapacityWaitMs = _TotalCapacityWaitMs,
                    TotalProcessRuntimeMs = _TotalProcessRuntimeMs,
                    ProcessKillCount = _ProcessKillCount
                };

                foreach (KeyValuePair<string, TenantCapacity> tenant in _Tenants)
                {
                    snapshot.ActiveByTenant[tenant.Key] = tenant.Value.Active;
                    snapshot.QueuedByTenant[tenant.Key] = tenant.Value.Queued;
                }

                return snapshot;
            }
        }

        internal void Release(ExternalRuntimeCapacityLease lease, DateTime completedUtc)
        {
            TenantCapacity tenant = GetTenant(lease.TenantId);
            long runtimeMs = Math.Max(0, (long)(completedUtc - lease.AcquiredUtc).TotalMilliseconds);

            lock (_Lock)
            {
                tenant.Active = Math.Max(0, tenant.Active - 1);
                _ActiveServerWide = Math.Max(0, _ActiveServerWide - 1);
                _TotalProcessRuntimeMs += runtimeMs;
            }

            _ServerSlots.Release();
            tenant.Slots.Release();
        }

        private TenantCapacity GetTenant(string tenantId)
        {
            lock (_Lock)
            {
                if (!_Tenants.TryGetValue(tenantId, out TenantCapacity? tenant))
                {
                    tenant = new TenantCapacity(_MaxPerTenant);
                    _Tenants[tenantId] = tenant;
                }

                return tenant;
            }
        }

        private sealed class TenantCapacity
        {
            public TenantCapacity(int max)
            {
                Slots = new SemaphoreSlim(max, max);
            }

            public SemaphoreSlim Slots { get; }
            public int Active { get; set; }
            public int Queued { get; set; }
        }
    }
}
