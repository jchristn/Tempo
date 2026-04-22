import { useEffect, useMemo, useState } from 'react';
import ConfirmModal from '../components/ConfirmModal';
import CopyableId from '../components/CopyableId';
import JsonViewerModal from '../components/JsonViewerModal';
import Modal from '../components/Modal';
import ModalRecordId from '../components/ModalRecordId';
import PageHeader from '../components/PageHeader';
import RowActions from '../components/RowActions';
import TableFrame from '../components/TableFrame';
import TenantPicker from '../components/TenantPicker';
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

function normalizeError(err) {
  if (!err) return 'Request failed.';
  if (err.body) {
    try {
      const parsed = JSON.parse(err.body);
      return parsed.message || parsed.details || err.message;
    } catch {
      return err.body;
    }
  }
  return err.message || String(err);
}

function formatBytes(value) {
  const n = Number(value || 0);
  if (n < 1024) return n + ' B';
  if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB';
  if (n < 1024 * 1024 * 1024) return (n / (1024 * 1024)).toFixed(1) + ' MB';
  return (n / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
}

function parsePositiveNumber(value, fallback) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
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

function isRunActive(run) {
  const state = pick(run, 'state', 'State', '');
  return state === 'Queued' || state === 'Running';
}

function pickInitialRunLog(files) {
  if (!Array.isArray(files) || files.length < 1) return null;
  return files.find((file) => file.path === 'run.log')
    || files.find((file) => file.kind === 'Run')
    || files.find((file) => file.active)
    || files[0];
}

function assignmentDuration(assignment) {
  const assignedUtc = pick(assignment, 'assignedUtc', 'AssignedUtc');
  const completedUtc = pick(assignment, 'completedUtc', 'CompletedUtc');
  if (!assignedUtc || !completedUtc) return '-';
  return formatDuration(new Date(completedUtc) - new Date(assignedUtc));
}

function runDuration(run) {
  const startedUtc = pick(run, 'startedUtc', 'StartedUtc');
  const completedUtc = pick(run, 'completedUtc', 'CompletedUtc');
  if (!startedUtc || !completedUtc) return '-';
  return formatDuration(new Date(completedUtc) - new Date(startedUtc));
}

function logKindLabel(file) {
  return pick(file, 'kind', 'Kind', 'Log');
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
  const [runActivity, setRunActivity] = useState(null);
  const [runLogs, setRunLogs] = useState([]);
  const [selectedRunLogPath, setSelectedRunLogPath] = useState('');
  const [runLogData, setRunLogData] = useState(null);
  const [runDetailLoading, setRunDetailLoading] = useState(false);
  const [runLogLoading, setRunLogLoading] = useState(false);
  const [runError, setRunError] = useState('');
  const [runLogTailLines, setRunLogTailLines] = useState('400');
  const [runLogMaxBytes, setRunLogMaxBytes] = useState('262144');
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [confirmDeleteRunLog, setConfirmDeleteRunLog] = useState(null);
  const [confirmDeleteAllRunLogs, setConfirmDeleteAllRunLogs] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listRuns(tenantId, {
      pageNumber,
      pageSize,
      state: stateFilter || undefined,
      dataFlowId: flowIdFilter || undefined
    }).then((d) => {
      if (!cancelled) setData(d);
    }).catch(() => {
      if (!cancelled) setData({ items: [], totalCount: 0 });
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, stateFilter, flowIdFilter, refreshKey]);

  useEffect(() => {
    if (!autoRefresh) return;
    const id = setInterval(refresh, 3000);
    return () => clearInterval(id);
  }, [autoRefresh]);

  const loadRun = async (runOrId, options = {}) => {
    const runId = typeof runOrId === 'string' ? runOrId : runOrId?.id;
    if (!tenantId || !apiClient || !runId) return;
    const preferredPath = options.preferredPath === undefined ? selectedRunLogPath : options.preferredPath;

    setRunDetailLoading(true);
    setRunError('');

    try {
      const [details, stepList, activity, logs] = await Promise.all([
        apiClient.readRun(tenantId, runId).catch(() => (typeof runOrId === 'object' ? runOrId : viewing)),
        apiClient.readRunSteps(tenantId, runId).catch(() => []),
        apiClient.getRunActivity(tenantId, runId).catch(() => null),
        apiClient.listRunLogs(tenantId, runId).catch(() => [])
      ]);

      const nextViewing = details || (typeof runOrId === 'object' ? runOrId : viewing);
      const nextLogs = Array.isArray(logs) ? logs : [];
      const selectedLog = nextLogs.find((file) => file.path === preferredPath);
      const fallbackLog = selectedLog || pickInitialRunLog(nextLogs);

      setViewing(nextViewing);
      setSteps(Array.isArray(stepList) ? stepList : []);
      setRunActivity(activity);
      setRunLogs(nextLogs);
      setSelectedRunLogPath(fallbackLog?.path || '');
    } catch (err) {
      setRunError(normalizeError(err));
    } finally {
      setRunDetailLoading(false);
    }
  };

  const openRun = async (run) => {
    setViewing(run);
    setSteps([]);
    setRunActivity(null);
    setRunLogs([]);
    setSelectedRunLogPath('');
    setRunLogData(null);
    setRunError('');
    await loadRun(run, { preferredPath: '' });
  };

  useEffect(() => {
    if (!tenantId || !apiClient || !viewing?.id || !selectedRunLogPath) {
      setRunLogData(null);
      return;
    }

    let cancelled = false;
    setRunLogLoading(true);
    apiClient.readRunLog(tenantId, viewing.id, selectedRunLogPath, {
      tailLines: parsePositiveNumber(runLogTailLines, 400),
      maxBytes: parsePositiveNumber(runLogMaxBytes, 262144)
    }).then((result) => {
      if (!cancelled) setRunLogData(result);
    }).catch((err) => {
      if (!cancelled) {
        setRunLogData(null);
        setRunError(normalizeError(err));
      }
    }).finally(() => {
      if (!cancelled) setRunLogLoading(false);
    });

    return () => { cancelled = true; };
  }, [apiClient, tenantId, viewing?.id, selectedRunLogPath, runLogTailLines, runLogMaxBytes]);

  const cancel = async (run) => {
    try {
      await apiClient.cancelRun(tenantId, run.id);
      refresh();
      if (viewing?.id === run.id) await loadRun(run.id);
    } catch (err) {
      alert(normalizeError(err));
    }
  };

  const downloadRunLog = async (file) => {
    if (!viewing?.id || !file?.path) return;
    try {
      const result = await apiClient.downloadRunLog(tenantId, viewing.id, file.path);
      const url = URL.createObjectURL(result.blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = result.fileName || file.fileName || 'tempo-run.log';
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setRunError(normalizeError(err));
    }
  };

  const deleteRunLog = async () => {
    if (!viewing?.id || !confirmDeleteRunLog?.path) return;
    try {
      await apiClient.deleteRunLog(tenantId, viewing.id, confirmDeleteRunLog.path);
      setConfirmDeleteRunLog(null);
      setRunLogData(null);
      await loadRun(viewing.id, { preferredPath: '' });
    } catch (err) {
      setConfirmDeleteRunLog(null);
      setRunError(normalizeError(err));
    }
  };

  const deleteAllRunLogs = async () => {
    if (!viewing?.id) return;
    try {
      await apiClient.deleteRunLogs(tenantId, viewing.id);
      setConfirmDeleteAllRunLogs(false);
      setRunLogs([]);
      setSelectedRunLogPath('');
      setRunLogData(null);
      await loadRun(viewing.id, { preferredPath: '' });
    } catch (err) {
      setConfirmDeleteAllRunLogs(false);
      setRunError(normalizeError(err));
    }
  };

  const columns = [
    { key: 'createdUtc', label: 'Queued', tip: 'When this run was enqueued', render: (r) => formatTime(r.createdUtc) },
    {
      key: 'state',
      label: 'State',
      tip: 'Lifecycle state from queued to terminal',
      render: (r) => <span className={'pill ' + (STATE_PILL[r.state] || 'pill-neutral')}>{r.state}</span>
    },
    {
      key: 'dispatchState',
      label: 'Dispatch',
      tip: 'Fine-grained dispatch and recovery state',
      render: (r) => <span className="pill pill-neutral">{pick(r, 'dispatchState', 'DispatchState', '-')}</span>
    },
    { key: 'dataFlowId', label: 'Flow', tip: 'The data flow being executed', render: (r) => <CopyableId value={r.dataFlowId} /> },
    {
      key: 'sourceIp',
      label: 'Source IP',
      tip: 'Client IP observed by the server when this run was enqueued',
      render: (r) => <span className="monospace">{pick(r, 'sourceIp', 'SourceIp', '-')}</span>
    },
    {
      key: 'assignedWorkerId',
      label: 'Placement',
      tip: 'Assigned worker and execution node kind',
      render: (r) => {
        const workerId = pick(r, 'assignedWorkerId', 'AssignedWorkerId', '');
        const nodeKind = pick(r, 'executionNodeKind', 'ExecutionNodeKind', '');
        if (!workerId) return '-';
        return (
          <div>
            <div><CopyableId value={workerId} max={16} /></div>
            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>{nodeKind || 'Worker'}</div>
          </div>
        );
      }
    },
    { key: 'id', label: 'Run', tip: 'Globally unique run id', render: (r) => <CopyableId value={r.id} /> },
    {
      key: 'duration',
      label: 'Duration',
      tip: 'Elapsed runtime from started to completed',
      cellClass: 'right',
      render: (r) => runDuration(r)
    },
    {
      key: 'actions',
      label: '',
      style: { width: 48 },
      render: (r) => (
        <RowActions
          onView={() => openRun(r)}
          onViewJson={() => setJsonRow(r)}
          onDelete={() => setConfirmDelete(r)}
          extra={r.state === 'Queued' ? [{ label: 'Cancel', onClick: () => cancel(r), title: 'Cancel this queued run before assignment' }] : []}
        />
      )
    }
  ];

  const artifactSnapshots = artifactSnapshotsForRun(viewing);
  const selectedRunLog = useMemo(
    () => runLogs.find((file) => file.path === selectedRunLogPath) || null,
    [runLogs, selectedRunLogPath]
  );
  const runIsActive = isRunActive(viewing);

  return (
    <div>
      <PageHeader
        title="Runs"
        subtitle={'Track queued, running, and completed flow executions with assignment history and durable per-run logs. ' + (data?.totalCount ?? '-') + ' runs in selected tenant.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }} title="Refresh the run list automatically every few seconds">
              <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} style={{ width: 'auto' }} />
              Auto-refresh
            </label>
          </>
        }
      />

      <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
        <div className="field">
          <label title="Filter to runs in a specific lifecycle state">State</label>
          <select value={stateFilter} onChange={(e) => setStateFilter(e.target.value)} title="Filter to runs in a specific lifecycle state">
            <option value="">Any</option>
            {Object.keys(STATE_PILL).map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
        <div className="field">
          <label title="Show only runs of a single flow by identifier">Flow ID</label>
          <input value={flowIdFilter} onChange={(e) => setFlowIdFilter(e.target.value)} placeholder="flow_..." title="Show only runs of a single flow by identifier" />
        </div>
        <div style={{ display: 'flex', alignItems: 'end' }}>
          <button className="button-secondary" onClick={() => { setStateFilter(''); setFlowIdFilter(''); }} style={{ width: '100%' }} title="Clear all run filters">Clear</button>
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
        <Modal
          open
          onClose={() => {
            setViewing(null);
            setSteps([]);
            setRunActivity(null);
            setRunLogs([]);
            setSelectedRunLogPath('');
            setRunLogData(null);
            setRunError('');
          }}
          title={'Run - ' + viewing.id.slice(0, 16)}
          size="large"
          headerMeta={<ModalRecordId label="Run ID" value={viewing.id} />}
        >
          {runError && <div className="login-error">{runError}</div>}
          {runDetailLoading && <div className="callout callout-info">Refreshing run details and log catalog.</div>}

          <div className="summary-tiles">
            <div className="summary-tile" title="Current lifecycle state for this run">
              <div className="label">State</div>
              <div className="value" style={{ fontSize: '1.25rem' }}>{pick(viewing, 'state', 'State', '-')}</div>
            </div>
            <div className="summary-tile" title="The flow executed by this run">
              <div className="label">Flow</div>
              <div className="value" style={{ fontSize: '1rem' }}>{pick(viewing, 'dataFlowId', 'DataFlowId') ? <CopyableId value={pick(viewing, 'dataFlowId', 'DataFlowId')} max={18} /> : '-'}</div>
            </div>
            <div className="summary-tile" title="Worker currently or most recently assigned to this run">
              <div className="label">Worker</div>
              <div className="value" style={{ fontSize: '1rem' }}>{pick(viewing, 'assignedWorkerId', 'AssignedWorkerId') ? <CopyableId value={pick(viewing, 'assignedWorkerId', 'AssignedWorkerId')} max={18} /> : '-'}</div>
            </div>
            <div className="summary-tile" title="Observed client source IP when the run was enqueued">
              <div className="label">Source IP</div>
              <div className="value" style={{ fontSize: '1rem' }}>{pick(viewing, 'sourceIp', 'SourceIp', '-')}</div>
            </div>
            <div className="summary-tile" title="How many assignment attempts were recorded for this run">
              <div className="label">Attempts</div>
              <div className="value">{runActivity?.assignments?.length ?? pick(viewing, 'dispatchAttempt', 'DispatchAttempt', 0)}</div>
            </div>
            <div className="summary-tile" title="Elapsed runtime from start to completion when available">
              <div className="label">Runtime</div>
              <div className="value">{runDuration(viewing)}</div>
            </div>
          </div>

          <div className="drawer-actions">
            <button className="button-secondary" onClick={() => loadRun(viewing.id)} title="Refresh this run, its assignment history, and its log file catalog">Refresh run</button>
            {selectedRunLog && (
              <button className="button-secondary" onClick={() => downloadRunLog(selectedRunLog)} title="Download the complete selected run log file">Download selected log</button>
            )}
            {selectedRunLog && selectedRunLog.deleteAllowed && (
              <button className="button-secondary" onClick={() => setConfirmDeleteRunLog(selectedRunLog)} title="Delete the selected archived run log file">Delete selected log</button>
            )}
            {!runIsActive && runLogs.length > 0 && (
              <button className="button-danger" onClick={() => setConfirmDeleteAllRunLogs(true)} title="Delete all archived log files for this completed run">Delete all run logs</button>
            )}
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">Summary</div>
            <dl className="details-kv">
              <dt title="Lifecycle state for this run">State</dt><dd><span className={'pill ' + (STATE_PILL[pick(viewing, 'state', 'State', '')] || 'pill-neutral')}>{pick(viewing, 'state', 'State', '-')}</span></dd>
              <dt title="Flow identifier executed by this run">Flow</dt><dd>{pick(viewing, 'dataFlowId', 'DataFlowId') ? <CopyableId value={pick(viewing, 'dataFlowId', 'DataFlowId')} /> : '-'}</dd>
              <dt title="Client IP observed when the run was enqueued">Source IP</dt><dd><span className="monospace">{pick(viewing, 'sourceIp', 'SourceIp', '-')}</span></dd>
              <dt title="Fine-grained dispatch and recovery state">Dispatch</dt><dd><span className="pill pill-neutral">{pick(viewing, 'dispatchState', 'DispatchState', '-')}</span></dd>
              <dt title="Worker assigned to this run">Worker</dt><dd>{pick(viewing, 'assignedWorkerId', 'AssignedWorkerId') ? <CopyableId value={pick(viewing, 'assignedWorkerId', 'AssignedWorkerId')} /> : '-'}</dd>
              <dt title="Execution node type that ran the workload">Node kind</dt><dd>{pick(viewing, 'executionNodeKind', 'ExecutionNodeKind', '-')}</dd>
              <dt title="Current or last assignment record id">Assignment</dt><dd>{pick(viewing, 'runAssignmentId', 'RunAssignmentId') ? <CopyableId value={pick(viewing, 'runAssignmentId', 'RunAssignmentId')} /> : '-'}</dd>
              <dt title="Queued timestamp">Queued</dt><dd>{formatTime(pick(viewing, 'createdUtc', 'CreatedUtc'))}</dd>
              <dt title="Assigned timestamp">Assigned</dt><dd>{formatTime(pick(viewing, 'assignedUtc', 'AssignedUtc'))}</dd>
              <dt title="Started timestamp">Started</dt><dd>{formatTime(pick(viewing, 'startedUtc', 'StartedUtc'))}</dd>
              <dt title="Completed timestamp">Completed</dt><dd>{formatTime(pick(viewing, 'completedUtc', 'CompletedUtc'))}</dd>
              <dt title="Lease expiry for the current assignment">Lease expiry</dt><dd>{formatTime(pick(viewing, 'leaseExpiresUtc', 'LeaseExpiresUtc'))}</dd>
              <dt title="Original run input payload">Input</dt><dd><pre className="code-block">{pick(viewing, 'inputData', 'InputData', '') || '(empty)'}</pre></dd>
              <dt title="Final run output payload">Output</dt><dd><pre className="code-block">{pick(viewing, 'outputData', 'OutputData', '') || '(empty)'}</pre></dd>
              {pick(viewing, 'errorMessage', 'ErrorMessage') && (<><dt title="Terminal error message for this run">Error</dt><dd style={{ color: 'var(--color-danger)' }}>{pick(viewing, 'errorMessage', 'ErrorMessage')}</dd></>)}
            </dl>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">Assignment History</div>
            <table className="data-table">
              <thead>
                <tr>
                  <th title="Attempt number for this run assignment">Attempt</th>
                  <th title="Run assignment identifier">Assignment</th>
                  <th title="Worker that owned the assignment">Worker</th>
                  <th title="Assignment state">State</th>
                  <th title="Lease expiry for the assignment">Lease</th>
                  <th title="Assignment start time">Assigned</th>
                  <th title="Assignment completion time">Completed</th>
                  <th title="Elapsed time from assignment to completion">Duration</th>
                </tr>
              </thead>
              <tbody>
                {(runActivity?.assignments || []).length < 1 ? (
                  <tr><td colSpan={8} className="empty-state">No assignment history recorded yet.</td></tr>
                ) : (runActivity?.assignments || []).map((assignment) => (
                  <tr key={pick(assignment, 'id', 'Id', Math.random().toString())}>
                    <td title="Attempt number">{pick(assignment, 'attemptNumber', 'AttemptNumber', '-')}</td>
                    <td title="Run assignment identifier">{pick(assignment, 'id', 'Id') ? <CopyableId value={pick(assignment, 'id', 'Id')} max={18} /> : '-'}</td>
                    <td title="Worker assigned">{pick(assignment, 'workerId', 'WorkerId') ? <CopyableId value={pick(assignment, 'workerId', 'WorkerId')} max={18} /> : '-'}</td>
                    <td title="Assignment state"><span className="pill pill-neutral">{pick(assignment, 'state', 'State', '-')}</span></td>
                    <td title="Assignment lease expiry">{formatTime(pick(assignment, 'leaseExpiresUtc', 'LeaseExpiresUtc'))}</td>
                    <td title="Assignment start time">{formatTime(pick(assignment, 'assignedUtc', 'AssignedUtc'))}</td>
                    <td title="Assignment completion time">{formatTime(pick(assignment, 'completedUtc', 'CompletedUtc'))}</td>
                    <td title="Elapsed time from assignment to completion">{assignmentDuration(assignment)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">Worker Activity</div>
            <table className="data-table">
              <thead>
                <tr>
                  <th title="Event timestamp">When</th>
                  <th title="Worker activity event type">Event</th>
                  <th title="Event severity">Severity</th>
                  <th title="Worker identifier">Worker</th>
                  <th title="Assignment identifier">Assignment</th>
                  <th title="Message recorded for this event">Message</th>
                </tr>
              </thead>
              <tbody>
                {(runActivity?.activity || []).length < 1 ? (
                  <tr><td colSpan={6} className="empty-state">No worker activity recorded yet.</td></tr>
                ) : (runActivity?.activity || []).map((activity) => (
                  <tr key={pick(activity, 'id', 'Id', Math.random().toString())}>
                    <td title="Event timestamp">{formatTime(pick(activity, 'createdUtc', 'CreatedUtc'))}</td>
                    <td title="Worker activity event type"><span className="pill pill-neutral">{pick(activity, 'eventType', 'EventType', '-')}</span></td>
                    <td title="Event severity">{pick(activity, 'severity', 'Severity', '-')}</td>
                    <td title="Worker identifier">{pick(activity, 'workerId', 'WorkerId') ? <CopyableId value={pick(activity, 'workerId', 'WorkerId')} max={16} /> : '-'}</td>
                    <td title="Run assignment identifier">{pick(activity, 'runAssignmentId', 'RunAssignmentId') ? <CopyableId value={pick(activity, 'runAssignmentId', 'RunAssignmentId')} max={16} /> : '-'}</td>
                    <td title="Worker activity message">{pick(activity, 'message', 'Message', '-')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">Run Logs</div>
            <div className="logs-workspace">
              <div className="logs-sidebar-panel">
                <div className="logs-panel-header">
                  <div>
                    <div className="drawer-section-title">Files</div>
                    <div className="view-subtitle">Select one log file produced for this run.</div>
                  </div>
                </div>
                <table className="data-table">
                  <thead>
                    <tr>
                      <th title="Log file type">Kind</th>
                      <th title="Log file name and relative path">File</th>
                      <th title="File size">Size</th>
                      <th title="Last modification time">Modified</th>
                    </tr>
                  </thead>
                  <tbody>
                    {runLogs.length < 1 ? (
                      <tr><td colSpan={4} className="empty-state">No run log files are visible yet.</td></tr>
                    ) : runLogs.map((file) => (
                      <tr
                        key={file.path}
                        className={selectedRunLogPath === file.path ? 'clickable' : 'clickable'}
                        onClick={() => setSelectedRunLogPath(file.path)}
                        title="Load this run log file in the viewer"
                        style={selectedRunLogPath === file.path ? { background: 'color-mix(in srgb, var(--color-primary) 8%, transparent)' } : undefined}
                      >
                        <td title="Log file type"><span className="pill pill-neutral">{logKindLabel(file)}</span></td>
                        <td title={file.path}>
                          <div style={{ minWidth: 0 }}>
                            <div style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}><code className="monospace">{file.fileName}</code></div>
                            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                              <span title={file.path}><code className="monospace">{file.path}</code></span>
                            </div>
                            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>
                              {file.attemptNumber ? 'Attempt ' + file.attemptNumber : 'Run-level'}
                              {file.stepId ? ' | Step ' + file.stepId : ''}
                              {file.workerId ? ' | Worker ' + file.workerId : ''}
                            </div>
                          </div>
                        </td>
                        <td title="File size">{formatBytes(file.byteLength)}</td>
                        <td title="Last modification time">{formatTime(file.lastModifiedUtc)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="log-viewer-panel">
                <div className="logs-panel-header">
                  <div>
                    <div className="drawer-section-title">Viewer</div>
                    <div className="view-subtitle">{selectedRunLog ? 'Reading ' + selectedRunLog.fileName : 'Select a run log file to read a bounded tail.'}</div>
                  </div>
                  {selectedRunLog && (
                    <div className="log-viewer-toolbar">
                      <button className="button-secondary" onClick={() => downloadRunLog(selectedRunLog)} title="Download the complete selected run log file">Download</button>
                      <button
                        className={selectedRunLog.deleteAllowed ? 'button-secondary' : 'button-secondary'}
                        disabled={!selectedRunLog.deleteAllowed}
                        onClick={() => setConfirmDeleteRunLog(selectedRunLog)}
                        title={selectedRunLog.deleteAllowed ? 'Delete this archived run log file' : 'Active run log files cannot be deleted while the run is still active'}
                      >
                        Delete file
                      </button>
                    </div>
                  )}
                </div>

                <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
                  <div className="field">
                    <label title="Maximum number of lines returned from the end of the selected run log file">Tail lines</label>
                    <input type="number" min="1" value={runLogTailLines} onChange={(e) => setRunLogTailLines(e.target.value)} title="Maximum number of lines returned from the end of the selected run log file" />
                  </div>
                  <div className="field">
                    <label title="Maximum number of UTF-8 bytes returned in the viewer">Max bytes</label>
                    <input type="number" min="1" value={runLogMaxBytes} onChange={(e) => setRunLogMaxBytes(e.target.value)} title="Maximum number of UTF-8 bytes returned in the viewer" />
                  </div>
                  <div style={{ display: 'flex', alignItems: 'end' }}>
                    <button className="button-secondary" onClick={() => loadRun(viewing.id, { preferredPath: selectedRunLogPath })} style={{ width: '100%' }} title="Refresh the log catalog while keeping the current file selection when possible">Refresh files</button>
                  </div>
                </div>

                {!selectedRunLog && <div className="empty-state">Select a run log file from the list to read it.</div>}
                {selectedRunLog && (
                  <>
                    <div className="logs-meta-strip">
                      <span title="Selected file path"><code className="monospace">{selectedRunLog.path}</code></span>
                      <span title="Log file kind">{selectedRunLog.kind}</span>
                      <span title="File size">{formatBytes(selectedRunLog.byteLength)}</span>
                      <span title="Last modification time">{formatTime(selectedRunLog.lastModifiedUtc)}</span>
                    </div>
                    {runLogData?.truncated && (
                      <div className="callout callout-warning">
                        Viewer output is truncated to the last {runLogData.tailLines} lines and {formatBytes(runLogData.maxBytes)}.
                      </div>
                    )}
                    <div className="log-viewer-content" title="Bounded tail text from the selected run log file">
                      {runLogLoading ? 'Loading...' : (runLogData?.content || '')}
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>

          {artifactSnapshots.length > 0 && (
            <div className="drawer-section">
              <div className="drawer-section-title">Artifact Snapshot</div>
              <table className="data-table">
                <thead>
                  <tr>
                    <th title="Artifact snapshot key within the execution plan">Key</th>
                    <th title="Artifact identifier">Artifact</th>
                    <th title="Requested artifact version">Requested</th>
                    <th title="Resolved artifact version">Resolved</th>
                    <th title="Artifact version identifier">Version ID</th>
                    <th title="Artifact SHA-256">SHA-256</th>
                    <th title="Manifest entrypoint">Entrypoint</th>
                  </tr>
                </thead>
                <tbody>
                  {artifactSnapshots.map((snapshot) => (
                    <tr key={snapshot.key}>
                      <td className="monospace" title="Artifact snapshot key">{snapshot.key}</td>
                      <td title="Artifact identifier">{snapshot.artifactId ? <CopyableId value={snapshot.artifactId} max={18} /> : '-'}</td>
                      <td className="monospace" title="Requested version">{snapshot.requestedVersion || '-'}</td>
                      <td className="monospace" title="Resolved version">{snapshot.version || '-'}</td>
                      <td title="Artifact version identifier">{snapshot.versionId ? <CopyableId value={snapshot.versionId} max={18} /> : '-'}</td>
                      <td title="Artifact SHA-256">{snapshot.sha256 ? <CopyableId value={snapshot.sha256} max={18} /> : '-'}</td>
                      <td className="monospace" title="Manifest entrypoint">{snapshot.manifestEntrypoint || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="drawer-section">
            <div className="drawer-section-title">Step Timeline</div>
            <table className="data-table">
              <thead>
                <tr>
                  <th title="Step sequence number">#</th>
                  <th title="Step identifier">Step</th>
                  <th title="Step result">Result</th>
                  <th title="Artifact identifier">Artifact</th>
                  <th title="Artifact version or version id">Version</th>
                  <th title="Next step selected after this result">Next</th>
                  <th title="Step start time">Started</th>
                  <th title="Elapsed step runtime">Duration</th>
                </tr>
              </thead>
              <tbody>
                {steps.length === 0 ? (
                  <tr><td colSpan={8} className="empty-state">No step runs recorded yet.</td></tr>
                ) : steps.map((s) => (
                  <tr key={s.id}>
                    <td title="Step sequence number">{s.sequence}</td>
                    <td className="monospace" title="Step identifier">{s.stepId}</td>
                    <td title="Step result"><span className={'pill ' + (s.result === 'Success' ? 'pill-success' : s.result === 'Error' ? 'pill-warning' : 'pill-danger')}>{s.result}</span></td>
                    <td title="Artifact identifier">{s.artifactId ? <CopyableId value={s.artifactId} max={18} /> : '-'}</td>
                    <td title="Artifact version">{s.artifactVersionId ? <CopyableId value={s.artifactVersionId} max={18} /> : (s.artifactVersion || '-')}</td>
                    <td className="monospace" title="Next step">{s.nextStepId || '(end)'}</td>
                    <td title="Step start time">{formatTime(s.startedUtc)}</td>
                    <td title="Elapsed step runtime">{s.completedUtc ? formatDuration(new Date(s.completedUtc) - new Date(s.startedUtc)) : '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Run JSON" />

      <ConfirmModal
        open={!!confirmDelete}
        danger
        title="Delete run"
        recordId={confirmDelete?.id || ''}
        recordIdLabel="Run ID"
        message={'Delete this run? Step runs and per-run logs will also be removed.'}
        confirmLabel="Delete"
        onConfirm={async () => {
          await apiClient.deleteRun(tenantId, confirmDelete.id);
          if (viewing?.id === confirmDelete.id) {
            setViewing(null);
            setSteps([]);
            setRunActivity(null);
            setRunLogs([]);
            setSelectedRunLogPath('');
            setRunLogData(null);
          }
          setConfirmDelete(null);
          refresh();
        }}
        onCancel={() => setConfirmDelete(null)}
      />

      <ConfirmModal
        open={!!confirmDeleteRunLog}
        danger
        title="Delete run log"
        recordId={confirmDeleteRunLog?.path || ''}
        recordIdLabel="Path"
        message="Delete this archived run log file from disk? This cannot be undone."
        confirmLabel="Delete"
        onConfirm={deleteRunLog}
        onCancel={() => setConfirmDeleteRunLog(null)}
      />

      <ConfirmModal
        open={confirmDeleteAllRunLogs}
        danger
        title="Delete all run logs"
        recordId={viewing?.id || ''}
        recordIdLabel="Run ID"
        message="Delete every archived log file for this run? This cannot be undone."
        confirmLabel="Delete all"
        onConfirm={deleteAllRunLogs}
        onCancel={() => setConfirmDeleteAllRunLogs(false)}
      />
    </div>
  );
}

export default RunsView;
