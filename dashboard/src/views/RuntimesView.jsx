import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import TenantPicker from '../components/TenantPicker';
import JsonViewerModal from '../components/JsonViewerModal';
import { formatNumber } from '../utils/formatters';
import { translateLiteral } from '../utils/i18n';

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
  const { t, i18n } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
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
    { key: 'runtimeKey', label: tl('Runtime'), tip: tl('Runtime provider key'), render: (r) => <code>{String(r.runtimeKey)}</code> },
    { key: 'displayName', label: tl('Name'), tip: tl('Runtime display name') },
    { key: 'availability', label: tl('Availability'), tip: tl('Whether this runtime can be selected now'), render: (r) => <span className={'pill ' + availabilityPill(r.availability)}>{tl(r.availability || '-')}</span> },
    { key: 'packagingType', label: tl('Packaging'), tip: tl('Where runtime code comes from'), render: (r) => <span className={'pill ' + packagingPill(r.packagingType)}>{tl(r.packagingType || '-')}</span> },
    { key: 'supportsArtifacts', label: tl('Artifacts'), tip: tl('Whether the runtime references uploaded artifacts'), render: (r) => r.supportsArtifacts ? t('common.boolean.yes') : t('common.boolean.no') },
    { key: 'configTypeName', label: tl('Config DTO'), tip: tl('Concrete runtime config type used by the API') },
    { key: 'configProperties', label: tl('Config fields'), tip: tl('Runtime-specific config fields'), render: (r) => (r.configProperties || []).map((p) => p.name + (p.required ? '*' : '')).join(', ') || '-' },
    { key: 'securityNotes', label: tl('Security'), tip: tl('Runtime security notes'), render: (r) => r.securityNotes || '-' }
  ];

  return (
    <div>
      <PageHeader
        title={tl('Runtimes')}
        subtitle={tl('Review step runtime providers, config fields, and execution capacity. {{total}} providers | {{available}} available | {{disabled}} disabled.', {
          total: formatNumber(runtimeCounts.total, undefined, i18n.resolvedLanguage),
          available: formatNumber(runtimeCounts.available, undefined, i18n.resolvedLanguage),
          disabled: formatNumber(runtimeCounts.disabled, undefined, i18n.resolvedLanguage)
        })}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-secondary" onClick={refresh}>{t('common.actions.refresh')}</button>
          </>
        }
      />

      <div className="summary-tiles">
        <div className="summary-tile">
          <div className="label">{tl('Artifact execution')}</div>
          <div className="value">{status ? tl('Available') : '-'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{tl('Tenant active')}</div>
          <div className="value">{formatNumber(activeTenant, undefined, i18n.resolvedLanguage)}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{tl('Tenant queued')}</div>
          <div className="value">{formatNumber(queuedTenant, undefined, i18n.resolvedLanguage)}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{tl('Server pressure')}</div>
          <div className="value">{formatNumber(activeServer, undefined, i18n.resolvedLanguage)} / {formatNumber(queuedServer, undefined, i18n.resolvedLanguage)}</div>
        </div>
      </div>

      {status && (
        <div className="callout callout-info">
          {tl('Runtime commands:')} Python <code>{pick(status, 'pythonExecutable', 'PythonExecutable', 'python')}</code>, Node.js <code>{pick(status, 'nodeExecutable', 'NodeExecutable', 'node')}</code>, .NET <code>{pick(status, 'dotnetExecutable', 'DotnetExecutable', 'dotnet')}</code>.
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
        emptyMessage={tl('No runtime providers returned by the server.')}
        onRowClick={(runtime) => setJsonRow(runtime)}
        rightSlot={<span className="pill pill-info">{formatNumber(runtimeCounts.artifact, undefined, i18n.resolvedLanguage)} {tl('artifact-capable')}</span>}
      />

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title={tl('Runtime JSON')} />
    </div>
  );
}

export default RuntimesView;
