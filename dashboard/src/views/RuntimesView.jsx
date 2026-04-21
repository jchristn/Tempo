import { useEffect, useMemo, useState } from 'react';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import TenantPicker from '../components/TenantPicker';
import JsonViewerModal from '../components/JsonViewerModal';

function availabilityPill(availability) {
  if (availability === 'Available') return 'pill-success';
  if (availability === 'DisabledBySettings') return 'pill-warning';
  if (availability === 'MissingDependency') return 'pill-warning';
  if (availability === 'Preview') return 'pill-info';
  return 'pill-neutral';
}

function packagingPill(packagingType) {
  if (packagingType === 'Builtin') return 'pill-success';
  if (packagingType === 'External') return 'pill-warning';
  if (packagingType === 'Host') return 'pill-info';
  return 'pill-neutral';
}

function pick(obj, camel, pascal, fallback) {
  if (!obj) return fallback;
  if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
  if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
  return fallback;
}

function RuntimesView({ apiClient, principal }) {
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [serverRuntimes, setServerRuntimes] = useState([]);
  const [tenantRuntimes, setTenantRuntimes] = useState([]);
  const [status, setStatus] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [loading, setLoading] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    apiClient.listRuntimes()
      .then((items) => { if (!cancelled) setServerRuntimes(items || []); })
      .catch(() => { if (!cancelled) setServerRuntimes([]); });
    return () => { cancelled = true; };
  }, [apiClient, refreshKey]);

  useEffect(() => {
    if (!apiClient || !tenantId) return;
    let cancelled = false;
    setLoading(true);
    Promise.all([
      apiClient.listTenantRuntimes(tenantId).catch(() => []),
      apiClient.getTenantExternalExecutionStatus(tenantId).catch(() => null)
    ]).then(([runtimeList, runtimeStatus]) => {
      if (cancelled) return;
      setTenantRuntimes(runtimeList || []);
      setStatus(runtimeStatus);
    }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, refreshKey]);

  const runtimeCounts = useMemo(() => {
    const source = tenantRuntimes.length ? tenantRuntimes : serverRuntimes;
    return {
      total: source.length,
      available: source.filter((r) => r.availability === 'Available').length,
      disabled: source.filter((r) => r.availability === 'DisabledBySettings').length,
      artifact: source.filter((r) => !!r.supportsArtifacts).length
    };
  }, [serverRuntimes, tenantRuntimes]);

  const capacity = pick(status, 'capacity', 'Capacity', {});
  const activeTenant = pick(capacity, 'activeForTenant', 'ActiveForTenant', 0);
  const queuedTenant = pick(capacity, 'queuedForTenant', 'QueuedForTenant', 0);
  const activeServer = pick(capacity, 'activeServerWide', 'ActiveServerWide', 0);
  const queuedServer = pick(capacity, 'queuedServerWide', 'QueuedServerWide', 0);

  const columns = [
    { key: 'runtimeKey', label: 'Runtime', tip: 'Runtime provider key', render: (r) => <code>{String(r.runtimeKey)}</code> },
    { key: 'displayName', label: 'Name', tip: 'Runtime display name' },
    { key: 'availability', label: 'Availability', tip: 'Whether this runtime can be selected now', render: (r) => <span className={'pill ' + availabilityPill(r.availability)}>{r.availability || '-'}</span> },
    { key: 'packagingType', label: 'Packaging', tip: 'Where runtime code comes from', render: (r) => <span className={'pill ' + packagingPill(r.packagingType)}>{r.packagingType || '-'}</span> },
    { key: 'supportsArtifacts', label: 'Artifacts', tip: 'Whether the runtime references uploaded artifacts', render: (r) => r.supportsArtifacts ? 'Yes' : 'No' },
    { key: 'configTypeName', label: 'Config DTO', tip: 'Concrete runtime config type used by the API' },
    { key: 'configProperties', label: 'Config fields', tip: 'Runtime-specific config fields', render: (r) => (r.configProperties || []).map((p) => p.name + (p.required ? '*' : '')).join(', ') || '-' },
    { key: 'securityNotes', label: 'Security', tip: 'Runtime security notes', render: (r) => r.securityNotes || '-' }
  ];

  return (
    <div>
      <PageHeader
        title="Runtimes"
        subtitle={'Review step runtime providers, config fields, and execution capacity. ' + runtimeCounts.total + ' providers | ' + runtimeCounts.available + ' available | ' + runtimeCounts.disabled + ' disabled.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-secondary" onClick={refresh}>Refresh</button>
          </>
        }
      />

      <div className="summary-tiles">
        <div className="summary-tile">
          <div className="label">Artifact execution</div>
          <div className="value">{status ? 'Available' : '-'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Tenant active</div>
          <div className="value">{Number(activeTenant).toLocaleString()}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Tenant queued</div>
          <div className="value">{Number(queuedTenant).toLocaleString()}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Server pressure</div>
          <div className="value">{Number(activeServer).toLocaleString()} / {Number(queuedServer).toLocaleString()}</div>
        </div>
      </div>

      {status && (
        <div className="callout callout-info">
          Runtime commands: Python <code>{pick(status, 'pythonExecutable', 'PythonExecutable', 'python')}</code>, Node.js <code>{pick(status, 'nodeExecutable', 'NodeExecutable', 'node')}</code>, .NET <code>{pick(status, 'dotnetExecutable', 'DotnetExecutable', 'dotnet')}</code>.
        </div>
      )}

      <TableFrame
        columns={columns}
        items={tenantRuntimes.length ? tenantRuntimes : serverRuntimes}
        totalRecords={tenantRuntimes.length || serverRuntimes.length}
        pageNumber={1}
        pageSize={Math.max(tenantRuntimes.length || serverRuntimes.length || 1, 1)}
        onPageChange={() => {}}
        onPageSizeChange={() => {}}
        onRefresh={refresh}
        loading={loading}
        emptyMessage="No runtime providers returned by the server."
        onRowClick={(runtime) => setJsonRow(runtime)}
        rightSlot={<span className="pill pill-info">{runtimeCounts.artifact} artifact-capable</span>}
      />

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Runtime JSON" />
    </div>
  );
}

export default RuntimesView;
