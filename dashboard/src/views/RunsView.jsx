import { useEffect, useState } from 'react';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import TenantPicker from '../components/TenantPicker';
import CopyableId from '../components/CopyableId';
import JsonViewerModal from '../components/JsonViewerModal';
import RowActions from '../components/RowActions';
import ConfirmModal from '../components/ConfirmModal';
import { formatDuration, formatTime } from '../utils/formatters';

const STATE_PILL = {
  Queued: 'pill-neutral',
  Running: 'pill-info',
  Succeeded: 'pill-success',
  Failed: 'pill-danger',
  Exception: 'pill-danger',
  Cancelled: 'pill-warning'
};

function pick(obj, camel, pascal, fallback = undefined) {
  if (!obj) return fallback;
  if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
  if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
  return fallback;
}

function artifactSnapshotsForRun(run) {
  const raw = pick(run, 'executionSnapshotJson', 'ExecutionSnapshotJson', '');
  if (!raw) return [];
  let snapshot = null;
  try {
    snapshot = typeof raw === 'string' ? JSON.parse(raw) : raw;
  } catch {
    return [];
  }

  const versions = pick(snapshot, 'artifactVersions', 'ArtifactVersions', {});
  return Object.entries(versions || {}).map(([key, value]) => ({
    key,
    artifactId: pick(value, 'artifactId', 'ArtifactId', ''),
    requestedVersion: pick(value, 'requestedVersion', 'RequestedVersion', ''),
    versionId: pick(value, 'versionId', 'VersionId', ''),
    version: pick(value, 'version', 'Version', ''),
    sha256: pick(value, 'sha256', 'Sha256', ''),
    manifestEntrypoint: pick(value, 'manifestEntrypoint', 'ManifestEntrypoint', '')
  }));
}

function RunsView({ apiClient, principal }) {
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [stateFilter, setStateFilter] = useState('');
  const [flowIdFilter, setFlowIdFilter] = useState('');
  const [viewing, setViewing] = useState(null);
  const [steps, setSteps] = useState([]);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listRuns(tenantId, {
      pageNumber, pageSize,
      state: stateFilter || undefined,
      dataFlowId: flowIdFilter || undefined
    }).then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, stateFilter, flowIdFilter, refreshKey]);

  useEffect(() => {
    if (!autoRefresh) return;
    const id = setInterval(refresh, 3000);
    return () => clearInterval(id);
  }, [autoRefresh]);

  const openRun = async (run) => {
    setViewing(run);
    setSteps([]);
    const [details, list] = await Promise.all([
      apiClient.readRun(tenantId, run.id).catch(() => run),
      apiClient.readRunSteps(tenantId, run.id).catch(() => [])
    ]);
    setViewing(details || run);
    setSteps(list || []);
  };

  const cancel = async (run) => {
    try { await apiClient.cancelRun(tenantId, run.id); refresh(); }
    catch (err) { alert(err.message); }
  };

  const columns = [
    { key: 'createdUtc', label: 'Queued', tip: 'When this run was enqueued', render: (r) => formatTime(r.createdUtc) },
    { key: 'state', label: 'State', tip: 'Lifecycle state: Queued → Running → Succeeded / Failed / Exception / Cancelled', render: (r) => <span className={'pill ' + (STATE_PILL[r.state] || 'pill-neutral')}>{r.state}</span> },
    { key: 'dataFlowId', label: 'Flow', tip: 'The data flow being executed', render: (r) => <CopyableId value={r.dataFlowId} /> },
    { key: 'id', label: 'Run', tip: 'Globally unique run id (prefix run_)', render: (r) => <CopyableId value={r.id} /> },
    { key: 'duration', label: 'Duration', tip: 'Time from start to completion (started → completed)', cellClass: 'right', render: (r) => r.startedUtc && r.completedUtc ? formatDuration(new Date(r.completedUtc) - new Date(r.startedUtc)) : '-' },
    { key: 'actions', label: '', style: { width: 48 }, render: (r) => (
      <RowActions
        onView={() => openRun(r)}
        onViewJson={() => setJsonRow(r)}
        onDelete={() => setConfirmDelete(r)}
        extra={r.state === 'Queued' ? [{ label: 'Cancel', onClick: () => cancel(r) }] : []}
      />
    )}
  ];

  const artifactSnapshots = artifactSnapshotsForRun(viewing);

  return (
    <div>
      <PageHeader
        title="Runs"
        subtitle={'Track queued, running, and completed flow executions with per-step output. ' + (data?.totalCount ?? '-') + ' runs in selected tenant.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }}>
              <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} style={{ width: 'auto' }} />
              Auto-refresh
            </label>
          </>
        }
      />

      <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
        <div className="field">
          <label title="Filter to runs in a specific lifecycle state">State</label>
          <select value={stateFilter} onChange={(e) => setStateFilter(e.target.value)}>
            <option value="">Any</option>
            {Object.keys(STATE_PILL).map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
        <div className="field">
          <label title="Show only runs of a single flow (paste its identifier)">Flow ID</label>
          <input value={flowIdFilter} onChange={(e) => setFlowIdFilter(e.target.value)} placeholder="flow_…" />
        </div>
        <div style={{ display: 'flex', alignItems: 'end' }}>
          <button className="button-secondary" onClick={() => { setStateFilter(''); setFlowIdFilter(''); }} style={{ width: '100%' }}>Clear</button>
        </div>
      </div>

      <TableFrame
        columns={columns}
        items={data?.items || []}
        totalRecords={data?.totalCount ?? 0}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(s) => { setPageSize(s); setPageNumber(1); }}
        onRefresh={refresh}
        loading={loading}
        selectable
        onBulkDelete={tenantId ? (ids) => apiClient.bulkDeleteRuns(tenantId, ids).then(refresh) : null}
        onRowClick={openRun}
      />

      {viewing && (
        <Modal open onClose={() => setViewing(null)} title={'Run · ' + viewing.id.slice(0, 16)} size="large">
          <dl className="details-kv">
            <dt>State</dt><dd><span className={'pill ' + (STATE_PILL[viewing.state] || 'pill-neutral')}>{viewing.state}</span></dd>
            <dt>Flow</dt><dd><CopyableId value={viewing.dataFlowId} /></dd>
            <dt>Queued</dt><dd>{formatTime(viewing.createdUtc)}</dd>
            <dt>Started</dt><dd>{formatTime(viewing.startedUtc)}</dd>
            <dt>Completed</dt><dd>{formatTime(viewing.completedUtc)}</dd>
            <dt>Input</dt><dd><pre className="code-block">{viewing.inputData || '(empty)'}</pre></dd>
            <dt>Output</dt><dd><pre className="code-block">{viewing.outputData || '(empty)'}</pre></dd>
            {viewing.errorMessage && (<><dt>Error</dt><dd style={{ color: 'var(--color-danger)' }}>{viewing.errorMessage}</dd></>)}
          </dl>
          {artifactSnapshots.length > 0 && (
            <>
              <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-sm)' }}>Artifact snapshot</h4>
              <table className="data-table">
                <thead><tr><th>Key</th><th>Artifact</th><th>Requested</th><th>Resolved</th><th>Version ID</th><th>SHA-256</th><th>Entrypoint</th></tr></thead>
                <tbody>
                  {artifactSnapshots.map((snapshot) => (
                    <tr key={snapshot.key}>
                      <td className="monospace">{snapshot.key}</td>
                      <td>{snapshot.artifactId ? <CopyableId value={snapshot.artifactId} max={18} /> : '-'}</td>
                      <td className="monospace">{snapshot.requestedVersion || '-'}</td>
                      <td className="monospace">{snapshot.version || '-'}</td>
                      <td>{snapshot.versionId ? <CopyableId value={snapshot.versionId} max={18} /> : '-'}</td>
                      <td>{snapshot.sha256 ? <CopyableId value={snapshot.sha256} max={18} /> : '-'}</td>
                      <td className="monospace">{snapshot.manifestEntrypoint || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
          <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-sm)' }}>Step timeline</h4>
          <table className="data-table">
            <thead><tr><th>#</th><th>Step</th><th>Result</th><th>Artifact</th><th>Version</th><th>Next</th><th>Started</th><th>Duration</th></tr></thead>
            <tbody>
              {steps.length === 0 ? (
                <tr><td colSpan={8} className="empty-state">No step runs recorded yet.</td></tr>
              ) : steps.map((s) => (
                <tr key={s.id}>
                  <td>{s.sequence}</td>
                  <td className="monospace">{s.stepId}</td>
                  <td><span className={'pill ' + (s.result === 'Success' ? 'pill-success' : s.result === 'Error' ? 'pill-warning' : 'pill-danger')}>{s.result}</span></td>
                  <td>{s.artifactId ? <CopyableId value={s.artifactId} max={18} /> : '-'}</td>
                  <td>{s.artifactVersionId ? <CopyableId value={s.artifactVersionId} max={18} /> : (s.artifactVersion || '-')}</td>
                  <td className="monospace">{s.nextStepId || '(end)'}</td>
                  <td>{formatTime(s.startedUtc)}</td>
                  <td>{s.completedUtc ? formatDuration(new Date(s.completedUtc) - new Date(s.startedUtc)) : '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Run JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete run"
        message={'Delete this run? Step runs will also be removed.'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteRun(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default RunsView;
