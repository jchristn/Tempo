import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import ActivityChart from '../components/ActivityChart';
import ConfirmModal from '../components/ConfirmModal';
import CopyableId from '../components/CopyableId';
import JsonViewerModal from '../components/JsonViewerModal';
import Modal from '../components/Modal';
import ModalRecordId from '../components/ModalRecordId';
import PageHeader from '../components/PageHeader';
import RowActions from '../components/RowActions';
import TableFrame from '../components/TableFrame';
import { formatDuration, formatRelative, formatTime } from '../utils/formatters';

const WORKER_STATE_CLASS = {
  Online: 'pill-success',
  Offline: 'pill-neutral',
  Draining: 'pill-warning',
  Stale: 'pill-warning'
};

function pick(obj, camel, pascal, fallback = undefined) {
  if (!obj) return fallback;
  if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
  if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
  return fallback;
}

function stateClass(state) {
  return WORKER_STATE_CLASS[state] || 'pill-neutral';
}

function bucketKey(stepMs, utcString) {
  const parsed = utcString ? new Date(utcString).getTime() : Number.NaN;
  if (Number.isNaN(parsed)) return null;
  return Math.floor(parsed / stepMs) * stepMs;
}

function buildWorkerSummary(workers, rangeId) {
  const ranges = {
    hour: { hours: 1, stepMs: 60_000 },
    day: { hours: 24, stepMs: 900_000 },
    week: { hours: 168, stepMs: 3_600_000 },
    month: { hours: 720, stepMs: 21_600_000 }
  };
  const range = ranges[rangeId] || ranges.day;
  const endMs = Date.now();
  const startMs = endMs - range.hours * 3_600_000;
  const buckets = new Map();

  for (let cursor = Math.floor(startMs / range.stepMs) * range.stepMs; cursor < endMs; cursor += range.stepMs) {
    buckets.set(cursor, {
      bucketStartUtc: new Date(cursor).toISOString(),
      bucketEndUtc: new Date(cursor + range.stepMs).toISOString(),
      successCount: 0,
      failureCount: 0,
      averageDurationMs: 0
    });
  }

  let online = 0;
  for (const worker of workers || []) {
    const state = pick(worker, 'state', 'State', 'Offline');
    const key = bucketKey(range.stepMs, pick(worker, 'lastHeartbeatUtc', 'LastHeartbeatUtc') || pick(worker, 'createdUtc', 'CreatedUtc'));
    if (key !== null && buckets.has(key)) {
      const bucket = buckets.get(key);
      if (state === 'Online') {
        bucket.successCount += 1;
        online += 1;
      } else {
        bucket.failureCount += 1;
      }
    } else if (state === 'Online') {
      online += 1;
    }
  }

  return {
    totalCount: (workers || []).length,
    totalSuccess: online,
    totalFailure: Math.max(0, (workers || []).length - online),
    averageDurationMs: 0,
    buckets: Array.from(buckets.values())
  };
}

function capabilitySummary(capabilities) {
  const normalized = (capabilities || []).map((item) => {
    const runtimeKey = pick(item, 'runtimeKey', 'RuntimeKey', '');
    const sourceKind = pick(item, 'sourceKind', 'SourceKind', '');
    return [runtimeKey, sourceKind].filter(Boolean).join(' | ');
  }).filter(Boolean);
  return normalized.length > 0 ? normalized.join(', ') : 'No advertised capabilities';
}

function labelSummary(labels) {
  return (labels || []).length > 0 ? labels.join(', ') : 'None';
}

function formatTaskTimeout(timeoutMs) {
  const numeric = Number(timeoutMs || 0);
  return numeric > 0 ? formatDuration(numeric) : 'Unlimited';
}

function enabledLabel(enabled) {
  return enabled ? 'Enabled' : 'Blocked';
}

function enabledClass(enabled) {
  return enabled ? 'pill-success' : 'pill-danger';
}

function WorkersView({ apiClient, principal }) {
  const navigate = useNavigate();
  const isAdmin = !!principal?.isAdmin || principal?.type === 'administrator';
  const [data, setData] = useState(null);
  const [allWorkers, setAllWorkers] = useState([]);
  const [loading, setLoading] = useState(false);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [rangeId, setRangeId] = useState('day');
  const [stateFilter, setStateFilter] = useState('');
  const [enabledFilter, setEnabledFilter] = useState('');
  const [drainFilter, setDrainFilter] = useState('');
  const [search, setSearch] = useState('');
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const [viewing, setViewing] = useState(null);
  const [tokenIssued, setTokenIssued] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null);
  const [error, setError] = useState('');
  const refresh = () => setRefreshKey((current) => current + 1);

  useEffect(() => {
    if (!apiClient || !isAdmin) return;
    let cancelled = false;
    const filters = {
      pageNumber,
      pageSize,
      state: stateFilter || undefined,
      search: search || undefined,
      enabled: enabledFilter || undefined,
      drainMode: drainFilter || undefined
    };

    setLoading(true);
    setError('');

    Promise.all([
      apiClient.listWorkers(filters),
      apiClient.listWorkers({
        pageNumber: 1,
        pageSize: 500,
        state: stateFilter || undefined,
        search: search || undefined,
        enabled: enabledFilter || undefined,
        drainMode: drainFilter || undefined
      }).catch(() => ({ items: [] }))
    ]).then(([paged, all]) => {
      if (cancelled) return;
      setData(paged);
      setAllWorkers(all?.items || []);
    }).catch((err) => {
      if (!cancelled) {
        setError(err.message);
        setData({ items: [], totalCount: 0 });
        setAllWorkers([]);
      }
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });

    return () => { cancelled = true; };
  }, [apiClient, isAdmin, pageNumber, pageSize, stateFilter, enabledFilter, drainFilter, search, refreshKey]);

  useEffect(() => {
    if (!autoRefresh) return;
    const timer = setInterval(refresh, 5000);
    return () => clearInterval(timer);
  }, [autoRefresh]);

  const activitySummary = useMemo(() => buildWorkerSummary(allWorkers, rangeId), [allWorkers, rangeId]);

  const openWorker = async (worker) => {
    setViewing(worker);
    setTokenIssued(null);
    try {
      const latest = await apiClient.readWorker(worker.id);
      setViewing(latest || worker);
    } catch {
      setViewing(worker);
    }
  };

  const runAction = async (action, worker) => {
    setError('');
    if (!worker?.id) return;
    try {
      setTokenIssued(null);
      if (action === 'drain') {
        const updated = await apiClient.drainWorker(worker.id);
        setViewing(updated);
      } else if (action === 'resume') {
        const updated = await apiClient.resumeWorker(worker.id);
        setViewing(updated);
      } else if (action === 'block') {
        const updated = await apiClient.blockWorker(worker.id);
        setViewing(updated);
      } else if (action === 'unblock') {
        const updated = await apiClient.unblockWorker(worker.id);
        setViewing(updated);
      } else if (action === 'rotate-token') {
        const issued = await apiClient.rotateWorkerToken(worker.id);
        setTokenIssued(issued);
      }
      refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setConfirmAction(null);
    }
  };

  const openLogs = (worker) => {
    if (!worker?.id) return;
    navigate('/dashboard/logs?sourceKind=worker&sourceId=' + encodeURIComponent(worker.id));
  };

  const columns = [
    {
      key: 'state',
      label: 'State',
      tip: 'Current worker liveness state',
      render: (worker) => <span className={'pill ' + stateClass(pick(worker, 'state', 'State', 'Offline'))}>{pick(worker, 'state', 'State', 'Offline')}</span>
    },
    {
      key: 'name',
      label: 'Worker',
      tip: 'Worker display name and identifier',
      render: (worker) => (
        <div>
          <div>{pick(worker, 'name', 'Name', '-')}</div>
          <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>
            <CopyableId value={pick(worker, 'id', 'Id', '')} max={18} />
          </div>
        </div>
      )
    },
    {
      key: 'kind',
      label: 'Kind',
      tip: 'Worker runtime classification',
      render: (worker) => pick(worker, 'kind', 'Kind', '-')
    },
    {
      key: 'labels',
      label: 'Labels',
      tip: 'Placement labels used by LabelPinned scheduling',
      render: (worker) => labelSummary(pick(worker, 'labels', 'Labels', []))
    },
    {
      key: 'enabled',
      label: 'Admission',
      tip: 'Whether this worker is allowed to connect and accept work',
      render: (worker) => {
        const enabled = !!pick(worker, 'enabled', 'Enabled', false);
        return <span className={'pill ' + enabledClass(enabled)}>{enabledLabel(enabled)}</span>;
      }
    },
    {
      key: 'activeAssignmentCount',
      label: 'Runs',
      tip: 'Current assigned run count',
      cellClass: 'right',
      render: (worker) => pick(worker, 'activeAssignmentCount', 'ActiveAssignmentCount', 0)
    },
    {
      key: 'maxConcurrentRuns',
      label: 'Max',
      tip: 'Maximum concurrent assignments this worker accepts',
      cellClass: 'right',
      render: (worker) => pick(worker, 'maxConcurrentRuns', 'MaxConcurrentRuns', 0)
    },
    {
      key: 'maxTaskTimeoutMs',
      label: 'Task timeout',
      tip: 'Worker-enforced assignment timeout. Zero means unlimited.',
      render: (worker) => formatTaskTimeout(pick(worker, 'maxTaskTimeoutMs', 'MaxTaskTimeoutMs', 0))
    },
    {
      key: 'lastHeartbeatUtc',
      label: 'Last heartbeat',
      tip: 'Most recent heartbeat from this worker',
      render: (worker) => (
        <div>
          <div>{formatRelative(pick(worker, 'lastHeartbeatUtc', 'LastHeartbeatUtc'))}</div>
          <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>{formatTime(pick(worker, 'lastHeartbeatUtc', 'LastHeartbeatUtc'))}</div>
        </div>
      )
    },
    {
      key: 'actions',
      label: '',
      style: { width: 48 },
      render: (worker) => (
        <RowActions
          onView={() => openWorker(worker)}
          onViewJson={() => setJsonRow(worker)}
          extra={[
            { label: 'View logs', onClick: () => openLogs(worker), title: 'Open the log viewer scoped to this worker' },
            pick(worker, 'enabled', 'Enabled', false)
              ? { label: 'Block', onClick: () => setConfirmAction({ type: 'block', worker }), title: 'Block this worker and disconnect any active session' }
              : { label: 'Unblock', onClick: () => setConfirmAction({ type: 'unblock', worker }), title: 'Allow this worker to connect and accept work again' },
            pick(worker, 'drainMode', 'DrainMode')
              ? { label: 'Resume', onClick: () => setConfirmAction({ type: 'resume', worker }), title: 'Allow this worker to admit new runs again' }
              : { label: 'Drain', onClick: () => setConfirmAction({ type: 'drain', worker }), title: 'Stop this worker from taking new runs while letting current runs finish' },
            { label: 'Rotate token', onClick: () => setConfirmAction({ type: 'rotate-token', worker }), title: 'Issue a new worker token and invalidate the previous one immediately' }
          ]}
        />
      )
    }
  ];

  if (!isAdmin) {
    return (
      <div>
        <PageHeader title="Workers" subtitle="Worker management requires an administrator account." />
        <div className="login-error">This view is only available to administrators.</div>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="Workers"
        subtitle={'Inspect worker state, block or unblock nodes, drain or resume admission, and verify placement availability. ' + (data?.totalCount ?? 0) + ' worker(s) match the current filters.'}
        actions={(
          <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }} title="Refresh the worker list automatically every five seconds">
            <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} style={{ width: 'auto' }} />
            Auto-refresh
          </label>
        )}
      />

      {error && <div className="login-error">{error}</div>}

      <ActivityChart
        summary={activitySummary}
        rangeId={rangeId}
        onRangeChange={setRangeId}
        onRefresh={refresh}
        title="Worker Heartbeat Recency"
        totalLabel="Workers"
        successLabel="Online"
        failureLabel="Non-online"
        successLegend="Workers currently online"
        failureLegend="Workers currently offline, draining, or stale"
        emptyMessage="No workers match the current filters."
      />

      <div className="summary-tiles">
        <div className="summary-tile"><div className="label">Workers</div><div className="value">{activitySummary.totalCount}</div></div>
        <div className="summary-tile success"><div className="label">Online</div><div className="value">{activitySummary.totalSuccess}</div></div>
        <div className="summary-tile danger"><div className="label">Blocked</div><div className="value">{allWorkers.filter((worker) => !pick(worker, 'enabled', 'Enabled', false)).length}</div></div>
        <div className="summary-tile warning"><div className="label">Draining</div><div className="value">{allWorkers.filter((worker) => !!pick(worker, 'drainMode', 'DrainMode')).length}</div></div>
        <div className="summary-tile"><div className="label">Active runs</div><div className="value">{allWorkers.reduce((sum, worker) => sum + Number(pick(worker, 'activeAssignmentCount', 'ActiveAssignmentCount', 0) || 0), 0)}</div></div>
      </div>

      <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
        <div className="field">
          <label title="Filter workers by current state">State</label>
          <select value={stateFilter} onChange={(e) => { setStateFilter(e.target.value); setPageNumber(1); }}>
            <option value="">Any</option>
            <option value="Online">Online</option>
            <option value="Offline">Offline</option>
            <option value="Draining">Draining</option>
            <option value="Stale">Stale</option>
          </select>
        </div>
        <div className="field">
          <label title="Filter workers by enabled flag">Enabled</label>
          <select value={enabledFilter} onChange={(e) => { setEnabledFilter(e.target.value); setPageNumber(1); }}>
            <option value="">Any</option>
            <option value="true">Enabled</option>
            <option value="false">Blocked</option>
          </select>
        </div>
        <div className="field">
          <label title="Filter workers by drain mode">Drain mode</label>
          <select value={drainFilter} onChange={(e) => { setDrainFilter(e.target.value); setPageNumber(1); }}>
            <option value="">Any</option>
            <option value="true">Draining</option>
            <option value="false">Admitting</option>
          </select>
        </div>
        <div className="field" style={{ minWidth: 220 }}>
          <label title="Search by worker id, name, or host name">Search</label>
          <input value={search} onChange={(e) => { setSearch(e.target.value); setPageNumber(1); }} placeholder="wrk_..., host, or name" />
        </div>
        <div style={{ display: 'flex', alignItems: 'end' }}>
          <button
            className="button-secondary"
            onClick={() => {
              setStateFilter('');
              setEnabledFilter('');
              setDrainFilter('');
              setSearch('');
              setPageNumber(1);
            }}
            style={{ width: '100%' }}
            title="Clear all worker filters"
          >
            Clear
          </button>
        </div>
      </div>

      <TableFrame
        columns={columns}
        items={data?.items || []}
        totalRecords={data?.totalCount ?? 0}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        onRefresh={refresh}
        loading={loading}
        onRowClick={openWorker}
      />

      {viewing && (
        <Modal
          open
          onClose={() => { setViewing(null); setTokenIssued(null); }}
          title={'Worker - ' + pick(viewing, 'name', 'Name', pick(viewing, 'id', 'Id', ''))}
          size="drawer"
          headerMeta={<ModalRecordId label="Worker ID" value={pick(viewing, 'id', 'Id', '')} />}
        >
          {tokenIssued && (
            <div className="callout callout-warning">
              Token rotated for <strong>{pick(tokenIssued, 'workerId', 'WorkerId', '')}</strong>. Copy the plaintext token now; it will not be returned again.
              <div style={{ marginTop: 'var(--spacing-sm)' }}>
                <CopyableId value={pick(tokenIssued, 'token', 'Token', '')} max={28} />
              </div>
            </div>
          )}

          <div className="summary-tiles">
            <div className="summary-tile">
              <div className="label">State</div>
              <div className="value" style={{ fontSize: '1.25rem' }}>{pick(viewing, 'state', 'State', '-')}</div>
            </div>
            <div className="summary-tile">
              <div className="label">Active runs</div>
              <div className="value">{pick(viewing, 'activeAssignmentCount', 'ActiveAssignmentCount', 0)}</div>
            </div>
            <div className="summary-tile">
              <div className="label">Max concurrency</div>
              <div className="value">{pick(viewing, 'maxConcurrentRuns', 'MaxConcurrentRuns', 0)}</div>
            </div>
            <div className="summary-tile">
              <div className="label">Task timeout</div>
              <div className="value">{formatTaskTimeout(pick(viewing, 'maxTaskTimeoutMs', 'MaxTaskTimeoutMs', 0))}</div>
            </div>
            <div className="summary-tile">
              <div className="label">Admission</div>
              <div className="value">{enabledLabel(!!pick(viewing, 'enabled', 'Enabled', false))}</div>
            </div>
            <div className="summary-tile">
              <div className="label">Drain mode</div>
              <div className="value">{pick(viewing, 'drainMode', 'DrainMode') ? 'On' : 'Off'}</div>
            </div>
          </div>

          <div className="drawer-actions">
            <button className="button-secondary" onClick={() => openLogs(viewing)} title="Open the dedicated log viewer for this worker">View logs</button>
            <button
              className={pick(viewing, 'enabled', 'Enabled', false) ? 'button-danger' : 'button-secondary'}
              onClick={() => setConfirmAction({ type: pick(viewing, 'enabled', 'Enabled', false) ? 'block' : 'unblock', worker: viewing })}
              title={pick(viewing, 'enabled', 'Enabled', false) ? 'Block this worker, disconnect it, and deny future connections' : 'Allow this worker to connect and accept work again'}
            >
              {pick(viewing, 'enabled', 'Enabled', false) ? 'Block worker' : 'Unblock worker'}
            </button>
            <button
              className="button-secondary"
              onClick={() => setConfirmAction({ type: pick(viewing, 'drainMode', 'DrainMode') ? 'resume' : 'drain', worker: viewing })}
              title={pick(viewing, 'drainMode', 'DrainMode') ? 'Allow this worker to admit new runs again' : 'Stop this worker from taking new runs while letting current runs finish'}
            >
              {pick(viewing, 'drainMode', 'DrainMode') ? 'Resume worker' : 'Drain worker'}
            </button>
            <button className="button-primary" onClick={() => setConfirmAction({ type: 'rotate-token', worker: viewing })} title="Issue a new worker token and invalidate the previous one immediately">Rotate token</button>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">Identity</div>
            <dl className="details-kv">
              <dt>Worker ID</dt><dd><CopyableId value={pick(viewing, 'id', 'Id', '')} /></dd>
              <dt>Name</dt><dd>{pick(viewing, 'name', 'Name', '-')}</dd>
              <dt>Kind</dt><dd>{pick(viewing, 'kind', 'Kind', '-')}</dd>
              <dt>Host</dt><dd>{pick(viewing, 'hostName', 'HostName', '-')}</dd>
              <dt>Version</dt><dd>{pick(viewing, 'version', 'Version', '-')}</dd>
              <dt>Enabled</dt><dd>{pick(viewing, 'enabled', 'Enabled') ? 'Yes' : 'No'}</dd>
              <dt>Created</dt><dd>{formatTime(pick(viewing, 'createdUtc', 'CreatedUtc'))}</dd>
              <dt>Last heartbeat</dt><dd>{formatTime(pick(viewing, 'lastHeartbeatUtc', 'LastHeartbeatUtc'))}</dd>
              <dt>Token rotated</dt><dd>{formatTime(pick(viewing, 'tokenLastRotatedUtc', 'TokenLastRotatedUtc'))}</dd>
            </dl>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">Placement</div>
            <dl className="details-kv">
              <dt>Max concurrency</dt><dd>{pick(viewing, 'maxConcurrentRuns', 'MaxConcurrentRuns', 0)}</dd>
              <dt>Max task timeout</dt><dd>{formatTaskTimeout(pick(viewing, 'maxTaskTimeoutMs', 'MaxTaskTimeoutMs', 0))}</dd>
              <dt>Labels</dt><dd>{labelSummary(pick(viewing, 'labels', 'Labels', []))}</dd>
              <dt>Capabilities</dt><dd>{capabilitySummary(pick(viewing, 'capabilities', 'Capabilities', []))}</dd>
            </dl>
          </div>

          {pick(viewing, 'latestSession', 'LatestSession') && (
            <div className="drawer-section">
              <div className="drawer-section-title">Latest session</div>
              <dl className="details-kv">
                <dt>Session ID</dt><dd><CopyableId value={pick(pick(viewing, 'latestSession', 'LatestSession', {}), 'id', 'Id', '')} /></dd>
                <dt>Connected</dt><dd>{formatTime(pick(pick(viewing, 'latestSession', 'LatestSession', {}), 'connectedUtc', 'ConnectedUtc'))}</dd>
                <dt>Disconnected</dt><dd>{formatTime(pick(pick(viewing, 'latestSession', 'LatestSession', {}), 'disconnectedUtc', 'DisconnectedUtc'))}</dd>
                <dt>Disconnect reason</dt><dd>{pick(pick(viewing, 'latestSession', 'LatestSession', {}), 'disconnectReason', 'DisconnectReason', '-')}</dd>
                <dt>Protocol</dt><dd>{pick(pick(viewing, 'latestSession', 'LatestSession', {}), 'protocolVersion', 'ProtocolVersion', '-')}</dd>
              </dl>
            </div>
          )}
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Worker JSON" />

      <ConfirmModal
        open={!!confirmAction}
        title={
          confirmAction?.type === 'rotate-token'
            ? 'Rotate worker token'
            : confirmAction?.type === 'drain'
              ? 'Drain worker'
              : confirmAction?.type === 'resume'
                ? 'Resume worker'
                : confirmAction?.type === 'block'
                  ? 'Block worker'
                  : 'Unblock worker'
        }
        recordId={confirmAction?.worker?.id || ''}
        recordIdLabel="Worker ID"
        message={
          confirmAction?.type === 'rotate-token'
            ? 'Issue a new worker token? The previous token stops working immediately.'
            : confirmAction?.type === 'drain'
              ? 'Set this worker to drain mode so it stops accepting new runs?'
              : confirmAction?.type === 'resume'
                ? 'Resume this worker so it can accept new runs again?'
                : confirmAction?.type === 'block'
                  ? 'Block this worker, disconnect any active session, and deny future connection attempts until it is unblocked?'
                  : 'Unblock this worker so it can reconnect and accept new runs again?'
        }
        confirmLabel={
          confirmAction?.type === 'rotate-token'
            ? 'Rotate token'
            : confirmAction?.type === 'drain'
              ? 'Drain'
              : confirmAction?.type === 'resume'
                ? 'Resume'
                : confirmAction?.type === 'block'
                  ? 'Block'
                  : 'Unblock'
        }
        onConfirm={() => runAction(confirmAction?.type, confirmAction?.worker)}
        onCancel={() => setConfirmAction(null)}
      />
    </div>
  );
}

export default WorkersView;
