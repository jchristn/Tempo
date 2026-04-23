import { useEffect, useState } from 'react';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import TenantPicker from '../components/TenantPicker';
import CopyableId from '../components/CopyableId';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ModalRecordId from '../components/ModalRecordId';
import RowActions from '../components/RowActions';
import DataFlowGraphEditor from '../components/DataFlowGraphEditor';
import { formatTime } from '../utils/formatters';

function emptyFlow() {
  return {
    name: '',
    description: '',
    startStepId: 'start',
    maxRuntimeMs: 0,
    invocationAuthMode: 'Public',
    transitions: {
      start: { OnSuccess: null, OnFailure: null, OnException: null }
    },
    active: true
  };
}

function authModeLabel(value) {
  return value === 'ApiAuthenticated' ? 'API auth' : 'Public';
}

function authModePillClass(value) {
  return value === 'ApiAuthenticated' ? 'pill-warning' : 'pill-info';
}

function AuthModePill({ value }) {
  const mode = value || 'Public';
  return <span className={'pill ' + authModePillClass(mode)} title={mode === 'ApiAuthenticated' ? 'HTTP trigger invocation requires normal Tempo API authentication' : 'HTTP trigger invocation is allowed for anyone with the trigger URL'}>{authModeLabel(mode)}</span>;
}

function InvocationAuthSelector({ value, onChange }) {
  const selected = value || 'Public';
  const options = [
    {
      value: 'Public',
      title: 'Public trigger URL',
      badge: 'Public',
      description: 'Anyone with the trigger ID can invoke this flow. Use for demos, webhooks, or gateway-protected routes.'
    },
    {
      value: 'ApiAuthenticated',
      title: 'Require API authentication',
      badge: 'API auth',
      description: 'Callers must send normal Tempo API credentials and have access to this tenant.'
    }
  ];

  return (
    <div className="auth-mode-options">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          className={'auth-mode-card' + (selected === option.value ? ' selected' : '')}
          title={option.description}
          onClick={() => onChange(option.value)}
        >
          <span className={'pill ' + authModePillClass(option.value)}>{option.badge}</span>
          <span className="auth-mode-title">{option.title}</span>
          <span className="auth-mode-description">{option.description}</span>
        </button>
      ))}
    </div>
  );
}

function DataFlowsView({ apiClient, principal }) {
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [transitionsText, setTransitionsText] = useState('');
  const [transitionsError, setTransitionsError] = useState(null);
  const [editMode, setEditMode] = useState('graph');
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listFlows(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  const startEdit = (flow) => {
    const body = flow ? { ...flow } : emptyFlow();
    setEditing(body);
    setTransitionsText(JSON.stringify(body.transitions || {}, null, 2));
    setTransitionsError(null);
    setEditMode('graph');
  };

  const save = async () => {
    let transitions;
    if (editMode === 'json') {
      try { transitions = JSON.parse(transitionsText); }
      catch (err) { setTransitionsError(err.message); return; }
    } else {
      transitions = editing.transitions || {};
    }
    const body = { ...editing, transitions };
    let saved;
    if (body.id) saved = await apiClient.updateFlow(tenantId, body.id, body);
    else saved = await apiClient.createFlow(tenantId, body);
    try { await apiClient.ensureFlowSteps(tenantId, saved.id); } catch { /* non-fatal */ }
    setEditing(null);
    refresh();
  };

  const runFlow = async (flow) => {
    try { await apiClient.runFlow(tenantId, flow.id, {}); }
    catch (err) { alert(err.message); }
  };

  const columns = [
    { key: 'name', label: 'Name', tip: 'Flow name; can be referenced by triggers and from the Run page' },
    { key: 'startStepId', label: 'Start step', tip: 'The step identifier executed first when the flow runs', cellClass: 'monospace' },
    { key: 'invocationAuthMode', label: 'Invocation', tip: 'Authentication required when this flow is invoked through an HTTP trigger', render: (f) => <AuthModePill value={f.invocationAuthMode} /> },
    { key: 'steps', label: 'Steps', tip: 'Number of step nodes defined in the transition graph', render: (f) => Object.keys(f.transitions || {}).length },
    { key: 'id', label: 'Identifier', tip: 'Globally unique flow id (prefix flow_)', render: (f) => <CopyableId value={f.id} /> },
    { key: 'createdUtc', label: 'Created', tip: 'When the flow was created', render: (f) => formatTime(f.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (f) => (
      <RowActions
        onEdit={() => startEdit(f)}
        onViewJson={() => setJsonRow(f)}
        onDelete={() => setConfirmDelete(f)}
        deleteDisabled={!!f.isProtected}
        extra={[
          { label: 'Run', onClick: () => runFlow(f) }
        ]}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title="Data Flows"
        subtitle={'Connect steps into executable graphs, then run them directly or attach triggers. ' + (data?.totalCount ?? '-') + ' flows in selected tenant.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => startEdit(null)}>+ New flow</button>
          </>
        }
      />

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
        onBulkDelete={tenantId ? (ids) => apiClient.bulkDeleteFlows(tenantId, ids).then(refresh) : null}
        onRowClick={(f) => startEdit(f)}
      />

      {editing && (
        <Modal
          open
          size="large"
          onClose={() => setEditing(null)}
          title={editing.id ? 'Edit flow' : 'Create flow'}
          headerMeta={<ModalRecordId label="Flow ID" value={editing.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}
        >
          <div className="grid-2">
            <div className="form-row"><label title="Flow name; shown when wiring up triggers and runs">Name</label><input value={editing.name || ''} placeholder="Order Fulfillment" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
            <div className="form-row"><label title="Identifier (or name) of the step that runs first">Start step</label><input value={editing.startStepId || ''} placeholder="start" onChange={(e) => setEditing({ ...editing, startStepId: e.target.value })} /></div>
          </div>
          <div className="form-row"><label title="Optional human-readable description of the flow's purpose">Description</label><input value={editing.description || ''} placeholder="Validates the order, charges payment, and sends confirmation" onChange={(e) => setEditing({ ...editing, description: e.target.value })} /></div>
          <div className="flow-settings-grid">
            <div className="form-row">
              <label title="Controls whether HTTP trigger invocation is public or requires normal Tempo API authentication">Invocation auth</label>
              <InvocationAuthSelector value={editing.invocationAuthMode} onChange={(invocationAuthMode) => setEditing({ ...editing, invocationAuthMode })} />
            </div>
            <div className="flow-runtime-card">
              <div className="form-row">
                <label title="Flow-level runtime ceiling in milliseconds; 0 disables the flow timeout">Timeout (ms)</label>
                <input type="number" min="0" value={editing.maxRuntimeMs || 0} placeholder="0" onChange={(e) => setEditing({ ...editing, maxRuntimeMs: parseInt(e.target.value || '0', 10) })} />
                <div className="form-help">HTTP trigger calls wait up to the flow timeout plus a small server buffer.</div>
              </div>
              <label className="flow-active-toggle" title="Inactive flows reject new runs while existing runs continue to completion">
                <input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} />
                <span>
                  <strong>Active</strong>
                  <small>Accept new direct runs and trigger invocations</small>
                </span>
              </label>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 4, marginBottom: 'var(--spacing-sm)' }}>
            <div className="range-selector">
              <button type="button" className={editMode === 'graph' ? 'active' : ''} title="Visual editor for step transitions" onClick={() => {
                if (editMode === 'json') {
                  try { setEditing({ ...editing, transitions: JSON.parse(transitionsText) }); setTransitionsError(null); }
                  catch (err) { setTransitionsError(err.message); return; }
                }
                setEditMode('graph');
              }}>Graph</button>
              <button type="button" className={editMode === 'json' ? 'active' : ''} title="Raw JSON editor for the transitions object" onClick={() => {
                setTransitionsText(JSON.stringify(editing.transitions || {}, null, 2));
                setEditMode('json');
              }}>JSON</button>
            </div>
            <div style={{ marginLeft: 'auto', fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', alignSelf: 'center' }}>
              Referenced step ids that do not yet exist are auto-created when you save.
            </div>
          </div>

          {editMode === 'graph' ? (
            <DataFlowGraphEditor
              transitions={editing.transitions || {}}
              startStepId={editing.startStepId}
              onChange={(next) => setEditing({ ...editing, transitions: next })}
            />
          ) : (
            <div className="form-row">
              <label title="JSON object keyed by step id, each value has OnSuccess/OnFailure/OnException routing targets">Transitions (JSON)</label>
              <textarea rows={14} value={transitionsText} placeholder='{\n  "start": { "OnSuccess": "validate", "OnFailure": null, "OnException": null },\n  "validate": { "OnSuccess": "charge", "OnFailure": "notify", "OnException": "notify" }\n}' onChange={(e) => { setTransitionsText(e.target.value); setTransitionsError(null); }} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
              {transitionsError && <div className="form-help" style={{ color: 'var(--color-danger)' }}>{transitionsError}</div>}
            </div>
          )}
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Flow JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete flow"
        recordId={confirmDelete?.id || ''}
        recordIdLabel="Flow ID"
        message={'Delete flow "' + (confirmDelete?.name || '') + '"?'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteFlow(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default DataFlowsView;
