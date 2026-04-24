import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import ConfirmModal from '../components/ConfirmModal';
import CopyableId from '../components/CopyableId';
import JsonViewerModal from '../components/JsonViewerModal';
import Modal from '../components/Modal';
import ModalRecordId from '../components/ModalRecordId';
import PageHeader from '../components/PageHeader';
import RowActions from '../components/RowActions';
import TableFrame from '../components/TableFrame';
import TenantPicker from '../components/TenantPicker';
import { formatBytes, formatDuration, formatTime } from '../utils/formatters';
import { normalizeApiError, translateLiteral } from '../utils/i18n';

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
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [stateFilter, setStateFilter] = useState('');
  const [flowIdFilter, setFlowIdFilter] = useState('');
  const [workerIdFilter, setWorkerIdFilter] = useState('');
  const [sourceIpFilter, setSourceIpFilter] = useState('');
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
      dataFlowId: flowIdFilter || undefined,
      workerId: workerIdFilter || undefined,
      sourceIp: sourceIpFilter || undefined
    }).then((d) => {
      if (!cancelled) setData(d);
    }).catch(() => {
      if (!cancelled) setData({ items: [], totalCount: 0 });
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, stateFilter, flowIdFilter, workerIdFilter, sourceIpFilter, refreshKey]);

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
      setRunError(normalizeApiError(err, t));
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
        setRunError(normalizeApiError(err, t));
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
      alert(normalizeApiError(err, t));
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
      setRunError(normalizeApiError(err, t));
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
      setRunError(normalizeApiError(err, t));
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
      setRunError(normalizeApiError(err, t));
    }
  };

  const columns = [
    { key: 'createdUtc', label: 'Queued', tip: 'When this run was enqueued', render: (r) => formatTime(r.createdUtc) },
    {
      key: 'state',
      label: 'State',
      tip: 'Lifecycle state from queued to terminal',
      render: (r) => <span className={'pill ' + (STATE_PILL[r.state] || 'pill-neutral')}>{tl(r.state)}</span>
    },
    {
      key: 'dispatchState',
      label: 'Dispatch',
      tip: 'Fine-grained dispatch and recovery state',
      render: (r) => <span className="pill pill-neutral">{tl(pick(r, 'dispatchState', 'DispatchState', '-'))}</span>
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
        if (!workerId) return t('common.placeholders.dash');
        return (
          <div>
            <div><CopyableId value={workerId} max={16} /></div>
            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>{nodeKind ? tl(nodeKind) : tl('Worker')}</div>
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
          extra={r.state === 'Queued' ? [{ label: tl('Cancel'), onClick: () => cancel(r), title: tl('Cancel this queued run before assignment') }] : []}
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
        title={tl('Runs')}
        subtitle={tl('Track queued, running, and completed flow executions with assignment history and durable per-run logs. {{count}} runs in selected tenant.', { count: data?.totalCount ?? 0 })}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }} title={tl('Refresh the run list automatically every few seconds')}>
              <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} style={{ width: 'auto' }} />
              {tl('Auto-refresh')}
            </label>
          </>
        }
      />

      <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
        <div className="field">
          <label title={tl('Filter to runs in a specific lifecycle state')}>{tl('State')}</label>
          <select
            value={stateFilter}
            onChange={(e) => { setStateFilter(e.target.value); setPageNumber(1); }}
            title={tl('Filter to runs in a specific lifecycle state')}
          >
            <option value="">{t('common.generic.any')}</option>
            {Object.keys(STATE_PILL).map((s) => <option key={s} value={s}>{tl(s)}</option>)}
          </select>
        </div>
        <div className="field">
          <label title={tl('Show only runs of a single flow by identifier')}>{tl('Flow ID')}</label>
          <input
            value={flowIdFilter}
            onChange={(e) => { setFlowIdFilter(e.target.value); setPageNumber(1); }}
            placeholder="flow_..."
            title={tl('Show only runs of a single flow by identifier')}
          />
        </div>
        <div className="field">
          <label title={tl('Show only runs assigned to a specific worker identifier')}>{tl('Worker ID')}</label>
          <input
            value={workerIdFilter}
            onChange={(e) => { setWorkerIdFilter(e.target.value); setPageNumber(1); }}
            placeholder="wrk_..."
            title={tl('Show only runs assigned to a specific worker identifier')}
          />
        </div>
        <div className="field">
          <label title={tl('Show only runs observed from a specific client source IP')}>{tl('Source IP')}</label>
          <input
            value={sourceIpFilter}
            onChange={(e) => { setSourceIpFilter(e.target.value); setPageNumber(1); }}
            placeholder="198.51.100.10"
            title={tl('Show only runs observed from a specific client source IP')}
          />
        </div>
        <div style={{ display: 'flex', alignItems: 'end' }}>
          <button
            className="button-secondary"
            onClick={() => {
              setStateFilter('');
              setFlowIdFilter('');
              setWorkerIdFilter('');
              setSourceIpFilter('');
              setPageNumber(1);
            }}
            style={{ width: '100%' }}
            title={tl('Clear all run filters')}
          >
            {t('common.actions.clear')}
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
          title={tl('Run - {{id}}', { id: viewing.id.slice(0, 16) })}
          size="xlarge"
          headerMeta={<ModalRecordId label={tl('Run ID')} value={viewing.id} />}
        >
          {runError && <div className="login-error">{runError}</div>}
          {runDetailLoading && <div className="callout callout-info">{tl('Refreshing run details and log catalog.')}</div>}

          <div className="summary-tiles">
            <div className="summary-tile" title={tl('Current lifecycle state for this run')}>
              <div className="label">{tl('State')}</div>
              <div className="value" style={{ fontSize: '1.25rem' }}>{tl(pick(viewing, 'state', 'State', '-'))}</div>
            </div>
            <div className="summary-tile" title={tl('The flow executed by this run')}>
              <div className="label">{tl('Flow')}</div>
              <div className="value" style={{ fontSize: '1rem' }}>{pick(viewing, 'dataFlowId', 'DataFlowId') ? <CopyableId value={pick(viewing, 'dataFlowId', 'DataFlowId')} max={18} /> : t('common.placeholders.dash')}</div>
            </div>
            <div className="summary-tile" title={tl('Worker currently or most recently assigned to this run')}>
              <div className="label">{tl('Worker')}</div>
              <div className="value" style={{ fontSize: '1rem' }}>{pick(viewing, 'assignedWorkerId', 'AssignedWorkerId') ? <CopyableId value={pick(viewing, 'assignedWorkerId', 'AssignedWorkerId')} max={18} /> : t('common.placeholders.dash')}</div>
            </div>
            <div className="summary-tile" title={tl('Observed client source IP when the run was enqueued')}>
              <div className="label">{tl('Source IP')}</div>
              <div className="value" style={{ fontSize: '1rem' }}>{pick(viewing, 'sourceIp', 'SourceIp', '-')}</div>
            </div>
            <div className="summary-tile" title={tl('How many assignment attempts were recorded for this run')}>
              <div className="label">{tl('Attempts')}</div>
              <div className="value">{runActivity?.assignments?.length ?? pick(viewing, 'dispatchAttempt', 'DispatchAttempt', 0)}</div>
            </div>
            <div className="summary-tile" title={tl('Elapsed runtime from start to completion when available')}>
              <div className="label">{tl('Runtime')}</div>
              <div className="value">{runDuration(viewing)}</div>
            </div>
          </div>

          <div className="drawer-actions">
            <button className="button-secondary" onClick={() => loadRun(viewing.id)} title={tl('Refresh this run, its assignment history, and its log file catalog')}>{tl('Refresh run')}</button>
            {selectedRunLog && (
              <button className="button-secondary" onClick={() => downloadRunLog(selectedRunLog)} title={tl('Download the complete selected run log file')}>{tl('Download selected log')}</button>
            )}
            {selectedRunLog && selectedRunLog.deleteAllowed && (
              <button className="button-secondary" onClick={() => setConfirmDeleteRunLog(selectedRunLog)} title={tl('Delete the selected archived run log file')}>{tl('Delete selected log')}</button>
            )}
            {!runIsActive && runLogs.length > 0 && (
              <button className="button-danger" onClick={() => setConfirmDeleteAllRunLogs(true)} title={tl('Delete all archived log files for this completed run')}>{tl('Delete all run logs')}</button>
            )}
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">{tl('Summary')}</div>
            <div className="run-summary-grid">
              <div className="summary-panel">
                <div className="summary-panel-title">{tl('Execution')}</div>
                <dl className="details-kv run-summary-kv">
                  <dt title={tl('Lifecycle state for this run')}>{tl('State')}</dt><dd><span className={'pill ' + (STATE_PILL[pick(viewing, 'state', 'State', '')] || 'pill-neutral')}>{tl(pick(viewing, 'state', 'State', '-'))}</span></dd>
                  <dt title={tl('Flow identifier executed by this run')}>{tl('Flow')}</dt><dd>{pick(viewing, 'dataFlowId', 'DataFlowId') ? <CopyableId value={pick(viewing, 'dataFlowId', 'DataFlowId')} /> : t('common.placeholders.dash')}</dd>
                  <dt title={tl('Client IP observed when the run was enqueued')}>{tl('Source IP')}</dt><dd><span className="monospace">{pick(viewing, 'sourceIp', 'SourceIp', '-')}</span></dd>
                  <dt title={tl('Fine-grained dispatch and recovery state')}>{tl('Dispatch')}</dt><dd><span className="pill pill-neutral">{tl(pick(viewing, 'dispatchState', 'DispatchState', '-'))}</span></dd>
                  <dt title={tl('Worker assigned to this run')}>{tl('Worker')}</dt><dd>{pick(viewing, 'assignedWorkerId', 'AssignedWorkerId') ? <CopyableId value={pick(viewing, 'assignedWorkerId', 'AssignedWorkerId')} /> : t('common.placeholders.dash')}</dd>
                  <dt title={tl('Execution node type that ran the workload')}>{tl('Node kind')}</dt><dd>{tl(pick(viewing, 'executionNodeKind', 'ExecutionNodeKind', '-'))}</dd>
                  <dt title={tl('Current or last assignment record id')}>{tl('Assignment')}</dt><dd>{pick(viewing, 'runAssignmentId', 'RunAssignmentId') ? <CopyableId value={pick(viewing, 'runAssignmentId', 'RunAssignmentId')} /> : t('common.placeholders.dash')}</dd>
                  <dt title={tl('Queued timestamp')}>{tl('Queued')}</dt><dd>{formatTime(pick(viewing, 'createdUtc', 'CreatedUtc'))}</dd>
                  <dt title={tl('Assigned timestamp')}>{tl('Assigned')}</dt><dd>{formatTime(pick(viewing, 'assignedUtc', 'AssignedUtc'))}</dd>
                  <dt title={tl('Started timestamp')}>{tl('Started')}</dt><dd>{formatTime(pick(viewing, 'startedUtc', 'StartedUtc'))}</dd>
                  <dt title={tl('Completed timestamp')}>{tl('Completed')}</dt><dd>{formatTime(pick(viewing, 'completedUtc', 'CompletedUtc'))}</dd>
                  <dt title={tl('Lease expiry for the current assignment')}>{tl('Lease expiry')}</dt><dd>{formatTime(pick(viewing, 'leaseExpiresUtc', 'LeaseExpiresUtc'))}</dd>
                </dl>
              </div>

              <div className="summary-panel">
                <div className="summary-panel-title">{tl('Payloads')}</div>
                <div className="summary-panel-stack">
                  <div className="summary-code-section">
                    <div className="summary-code-label" title={tl('Original run input payload')}>{tl('Input')}</div>
                    <pre className="code-block">{pick(viewing, 'inputData', 'InputData', '') || t('common.generic.empty')}</pre>
                  </div>
                  <div className="summary-code-section">
                    <div className="summary-code-label" title={tl('Final run output payload')}>{tl('Output')}</div>
                    <pre className="code-block">{pick(viewing, 'outputData', 'OutputData', '') || t('common.generic.empty')}</pre>
                  </div>
                  {pick(viewing, 'errorMessage', 'ErrorMessage') && (
                    <div className="summary-code-section">
                      <div className="summary-code-label" title={tl('Terminal error message for this run')}>{tl('Error')}</div>
                      <div className="summary-error-block">{pick(viewing, 'errorMessage', 'ErrorMessage')}</div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">{tl('Assignment History')}</div>
            <table className="data-table">
              <thead>
                <tr>
                  <th title={tl('Attempt number for this run assignment')}>{tl('Attempt')}</th>
                  <th title={tl('Run assignment identifier')}>{tl('Assignment')}</th>
                  <th title={tl('Worker that owned the assignment')}>{tl('Worker')}</th>
                  <th title={tl('Assignment state')}>{tl('State')}</th>
                  <th title={tl('Lease expiry for the assignment')}>{tl('Lease')}</th>
                  <th title={tl('Assignment start time')}>{tl('Assigned')}</th>
                  <th title={tl('Assignment completion time')}>{tl('Completed')}</th>
                  <th title={tl('Elapsed time from assignment to completion')}>{tl('Duration')}</th>
                </tr>
              </thead>
              <tbody>
                {(runActivity?.assignments || []).length < 1 ? (
                  <tr><td colSpan={8} className="empty-state">{tl('No assignment history recorded yet.')}</td></tr>
                ) : (runActivity?.assignments || []).map((assignment) => (
                  <tr key={pick(assignment, 'id', 'Id', Math.random().toString())}>
                    <td title={tl('Attempt number')}>{pick(assignment, 'attemptNumber', 'AttemptNumber', '-')}</td>
                    <td title={tl('Run assignment identifier')}>{pick(assignment, 'id', 'Id') ? <CopyableId value={pick(assignment, 'id', 'Id')} max={18} /> : t('common.placeholders.dash')}</td>
                    <td title={tl('Worker assigned')}>{pick(assignment, 'workerId', 'WorkerId') ? <CopyableId value={pick(assignment, 'workerId', 'WorkerId')} max={18} /> : t('common.placeholders.dash')}</td>
                    <td title={tl('Assignment state')}><span className="pill pill-neutral">{tl(pick(assignment, 'state', 'State', '-'))}</span></td>
                    <td title={tl('Assignment lease expiry')}>{formatTime(pick(assignment, 'leaseExpiresUtc', 'LeaseExpiresUtc'))}</td>
                    <td title={tl('Assignment start time')}>{formatTime(pick(assignment, 'assignedUtc', 'AssignedUtc'))}</td>
                    <td title={tl('Assignment completion time')}>{formatTime(pick(assignment, 'completedUtc', 'CompletedUtc'))}</td>
                    <td title={tl('Elapsed time from assignment to completion')}>{assignmentDuration(assignment)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">{tl('Worker Activity')}</div>
            <table className="data-table">
              <thead>
                <tr>
                  <th title={tl('Event timestamp')}>{tl('When')}</th>
                  <th title={tl('Worker activity event type')}>{tl('Event')}</th>
                  <th title={tl('Event severity')}>{tl('Severity')}</th>
                  <th title={tl('Worker identifier')}>{tl('Worker')}</th>
                  <th title={tl('Assignment identifier')}>{tl('Assignment')}</th>
                  <th title={tl('Message recorded for this event')}>{tl('Message')}</th>
                </tr>
              </thead>
              <tbody>
                {(runActivity?.activity || []).length < 1 ? (
                  <tr><td colSpan={6} className="empty-state">{tl('No worker activity recorded yet.')}</td></tr>
                ) : (runActivity?.activity || []).map((activity) => (
                  <tr key={pick(activity, 'id', 'Id', Math.random().toString())}>
                    <td title={tl('Event timestamp')}>{formatTime(pick(activity, 'createdUtc', 'CreatedUtc'))}</td>
                    <td title={tl('Worker activity event type')}><span className="pill pill-neutral">{tl(pick(activity, 'eventType', 'EventType', '-'))}</span></td>
                    <td title={tl('Event severity')}>{tl(pick(activity, 'severity', 'Severity', '-'))}</td>
                    <td title={tl('Worker identifier')}>{pick(activity, 'workerId', 'WorkerId') ? <CopyableId value={pick(activity, 'workerId', 'WorkerId')} max={16} /> : t('common.placeholders.dash')}</td>
                    <td title={tl('Run assignment identifier')}>{pick(activity, 'runAssignmentId', 'RunAssignmentId') ? <CopyableId value={pick(activity, 'runAssignmentId', 'RunAssignmentId')} max={16} /> : t('common.placeholders.dash')}</td>
                    <td title={tl('Worker activity message')}>{pick(activity, 'message', 'Message', '-')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">{tl('Run Logs')}</div>
            <div className="logs-workspace">
              <div className="logs-sidebar-panel">
                <div className="logs-panel-header">
                  <div>
                    <div className="drawer-section-title">{tl('Files')}</div>
                    <div className="view-subtitle">{tl('Select one log file produced for this run.')}</div>
                  </div>
                </div>
                <table className="data-table">
                  <thead>
                    <tr>
                      <th title={tl('Log file type')}>{tl('Kind')}</th>
                      <th title={tl('Log file name and relative path')}>{tl('File')}</th>
                      <th title={tl('File size')}>{tl('Size')}</th>
                      <th title={tl('Last modification time')}>{tl('Modified')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {runLogs.length < 1 ? (
                      <tr><td colSpan={4} className="empty-state">{tl('No run log files are visible yet.')}</td></tr>
                    ) : runLogs.map((file) => (
                      <tr
                        key={file.path}
                        className={selectedRunLogPath === file.path ? 'clickable' : 'clickable'}
                        onClick={() => setSelectedRunLogPath(file.path)}
                        title={tl('Load this run log file in the viewer')}
                        style={selectedRunLogPath === file.path ? { background: 'color-mix(in srgb, var(--color-primary) 8%, transparent)' } : undefined}
                      >
                        <td title={tl('Log file type')}><span className="pill pill-neutral">{tl(logKindLabel(file))}</span></td>
                        <td title={file.path}>
                          <div style={{ minWidth: 0 }}>
                            <div style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}><code className="monospace">{file.fileName}</code></div>
                            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                              <span title={file.path}><code className="monospace">{file.path}</code></span>
                            </div>
                            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>
                              {file.attemptNumber ? tl('Attempt {{count}}', { count: file.attemptNumber }) : tl('Run-level')}
                              {file.stepId ? tl(' | Step {{id}}', { id: file.stepId }) : ''}
                              {file.workerId ? tl(' | Worker {{id}}', { id: file.workerId }) : ''}
                            </div>
                          </div>
                        </td>
                        <td title={tl('File size')}>{formatBytes(file.byteLength)}</td>
                        <td title={tl('Last modification time')}>{formatTime(file.lastModifiedUtc)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="log-viewer-panel">
                <div className="logs-panel-header">
                  <div>
                    <div className="drawer-section-title">{tl('Viewer')}</div>
                    <div className="view-subtitle">{selectedRunLog ? tl('Reading {{fileName}}', { fileName: selectedRunLog.fileName }) : tl('Select a run log file to read a bounded tail.')}</div>
                  </div>
                  {selectedRunLog && (
                    <div className="log-viewer-toolbar">
                      <button className="button-secondary" onClick={() => downloadRunLog(selectedRunLog)} title={tl('Download the complete selected run log file')}>{t('common.actions.download')}</button>
                      <button
                        className={selectedRunLog.deleteAllowed ? 'button-secondary' : 'button-secondary'}
                        disabled={!selectedRunLog.deleteAllowed}
                        onClick={() => setConfirmDeleteRunLog(selectedRunLog)}
                        title={selectedRunLog.deleteAllowed ? tl('Delete this archived run log file') : tl('Active run log files cannot be deleted while the run is still active')}
                      >
                        {tl('Delete file')}
                      </button>
                    </div>
                  )}
                </div>

                <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
                  <div className="field">
                    <label title={tl('Maximum number of lines returned from the end of the selected run log file')}>{tl('Tail lines')}</label>
                    <input type="number" min="1" value={runLogTailLines} onChange={(e) => setRunLogTailLines(e.target.value)} title={tl('Maximum number of lines returned from the end of the selected run log file')} />
                  </div>
                  <div className="field">
                    <label title={tl('Maximum number of UTF-8 bytes returned in the viewer')}>{tl('Max bytes')}</label>
                    <input type="number" min="1" value={runLogMaxBytes} onChange={(e) => setRunLogMaxBytes(e.target.value)} title={tl('Maximum number of UTF-8 bytes returned in the viewer')} />
                  </div>
                  <div style={{ display: 'flex', alignItems: 'end' }}>
                    <button className="button-secondary" onClick={() => loadRun(viewing.id, { preferredPath: selectedRunLogPath })} style={{ width: '100%' }} title={tl('Refresh the log catalog while keeping the current file selection when possible')}>{tl('Refresh files')}</button>
                  </div>
                </div>

                {!selectedRunLog && <div className="empty-state">{tl('Select a run log file from the list to read it.')}</div>}
                {selectedRunLog && (
                  <>
                    <div className="logs-meta-strip">
                      <span title={tl('Selected file path')}><code className="monospace">{selectedRunLog.path}</code></span>
                      <span title={tl('Log file kind')}>{tl(selectedRunLog.kind)}</span>
                      <span title={tl('File size')}>{formatBytes(selectedRunLog.byteLength)}</span>
                      <span title={tl('Last modification time')}>{formatTime(selectedRunLog.lastModifiedUtc)}</span>
                    </div>
                    {runLogData?.truncated && (
                      <div className="callout callout-warning">
                        {tl('Viewer output is truncated to the last {{lines}} lines and {{bytes}}.', { lines: runLogData.tailLines, bytes: formatBytes(runLogData.maxBytes) })}
                      </div>
                    )}
                    <div className="log-viewer-content" title={tl('Bounded tail text from the selected run log file')}>
                      {runLogLoading ? t('common.generic.loading') : (runLogData?.content || '')}
                    </div>
                  </>
                )}
              </div>
            </div>
          </div>

          {artifactSnapshots.length > 0 && (
            <div className="drawer-section">
              <div className="drawer-section-title">{tl('Artifact Snapshot')}</div>
              <table className="data-table">
                <thead>
                  <tr>
                    <th title={tl('Artifact snapshot key within the execution plan')}>{tl('Key')}</th>
                    <th title={tl('Artifact identifier')}>{tl('Artifact')}</th>
                    <th title={tl('Requested artifact version')}>{tl('Requested')}</th>
                    <th title={tl('Resolved artifact version')}>{tl('Resolved')}</th>
                    <th title={tl('Artifact version identifier')}>{tl('Version ID')}</th>
                    <th title={tl('Artifact SHA-256')}>{tl('SHA-256')}</th>
                    <th title={tl('Manifest entrypoint')}>{tl('Entrypoint')}</th>
                  </tr>
                </thead>
                <tbody>
                  {artifactSnapshots.map((snapshot) => (
                    <tr key={snapshot.key}>
                      <td className="monospace" title={tl('Artifact snapshot key')}>{snapshot.key}</td>
                      <td title={tl('Artifact identifier')}>{snapshot.artifactId ? <CopyableId value={snapshot.artifactId} max={18} /> : t('common.placeholders.dash')}</td>
                      <td className="monospace" title={tl('Requested version')}>{snapshot.requestedVersion || t('common.placeholders.dash')}</td>
                      <td className="monospace" title={tl('Resolved version')}>{snapshot.version || t('common.placeholders.dash')}</td>
                      <td title={tl('Artifact version identifier')}>{snapshot.versionId ? <CopyableId value={snapshot.versionId} max={18} /> : t('common.placeholders.dash')}</td>
                      <td title={tl('Artifact SHA-256')}>{snapshot.sha256 ? <CopyableId value={snapshot.sha256} max={18} /> : t('common.placeholders.dash')}</td>
                      <td className="monospace" title={tl('Manifest entrypoint')}>{snapshot.manifestEntrypoint || t('common.placeholders.dash')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="drawer-section">
            <div className="drawer-section-title">{tl('Step Timeline')}</div>
            <table className="data-table">
              <thead>
                <tr>
                  <th title={tl('Step sequence number')}>#</th>
                  <th title={tl('Step identifier')}>{tl('Step')}</th>
                  <th title={tl('Step result')}>{tl('Result')}</th>
                  <th title={tl('Artifact identifier')}>{tl('Artifact')}</th>
                  <th title={tl('Artifact version or version id')}>{tl('Version')}</th>
                  <th title={tl('Next step selected after this result')}>{tl('Next')}</th>
                  <th title={tl('Step start time')}>{tl('Started')}</th>
                  <th title={tl('Elapsed step runtime')}>{tl('Duration')}</th>
                </tr>
              </thead>
              <tbody>
                {steps.length === 0 ? (
                  <tr><td colSpan={8} className="empty-state">{tl('No step runs recorded yet.')}</td></tr>
                ) : steps.map((s) => (
                  <tr key={s.id}>
                    <td title={tl('Step sequence number')}>{s.sequence}</td>
                    <td className="monospace" title={tl('Step identifier')}>{s.stepId}</td>
                    <td title={tl('Step result')}><span className={'pill ' + (s.result === 'Success' ? 'pill-success' : s.result === 'Error' ? 'pill-warning' : 'pill-danger')}>{tl(s.result)}</span></td>
                    <td title={tl('Artifact identifier')}>{s.artifactId ? <CopyableId value={s.artifactId} max={18} /> : t('common.placeholders.dash')}</td>
                    <td title={tl('Artifact version')}>{s.artifactVersionId ? <CopyableId value={s.artifactVersionId} max={18} /> : (s.artifactVersion || t('common.placeholders.dash'))}</td>
                    <td className="monospace" title={tl('Next step')}>{s.nextStepId || tl('(end)')}</td>
                    <td title={tl('Step start time')}>{formatTime(s.startedUtc)}</td>
                    <td title={tl('Elapsed step runtime')}>{s.completedUtc ? formatDuration(new Date(s.completedUtc) - new Date(s.startedUtc)) : t('common.placeholders.dash')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title={tl('Run JSON')} />

      <ConfirmModal
        open={!!confirmDelete}
        danger
        title={tl('Delete run')}
        recordId={confirmDelete?.id || ''}
        recordIdLabel={tl('Run ID')}
        message={tl('Delete this run? Step runs and per-run logs will also be removed.')}
        confirmLabel={t('common.actions.delete')}
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
        title={tl('Delete run log')}
        recordId={confirmDeleteRunLog?.path || ''}
        recordIdLabel={tl('Path')}
        message={tl('Delete this archived run log file from disk? This cannot be undone.')}
        confirmLabel={t('common.actions.delete')}
        onConfirm={deleteRunLog}
        onCancel={() => setConfirmDeleteRunLog(null)}
      />

      <ConfirmModal
        open={confirmDeleteAllRunLogs}
        danger
        title={tl('Delete all run logs')}
        recordId={viewing?.id || ''}
        recordIdLabel={tl('Run ID')}
        message={tl('Delete every archived log file for this run? This cannot be undone.')}
        confirmLabel={tl('Delete all')}
        onConfirm={deleteAllRunLogs}
        onCancel={() => setConfirmDeleteAllRunLogs(false)}
      />
    </div>
  );
}

export default RunsView;
