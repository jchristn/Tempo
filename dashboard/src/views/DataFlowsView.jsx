import { useEffect, useState } from 'react';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import TenantPicker from '../components/TenantPicker';
import CopyableId from '../components/CopyableId';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import RowActions from '../components/RowActions';
import DataFlowGraphEditor from '../components/DataFlowGraphEditor';
import { formatTime } from '../utils/formatters';

function emptyFlow() {
  return {
    name: '',
    description: '',
    startStepId: 'start',
    maxRuntimeMs: 0,
    transitions: {
      start: { OnSuccess: null, OnFailure: null, OnException: null }
    },
    active: true
  };
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
        <Modal open size="large" onClose={() => setEditing(null)} title={editing.id ? 'Edit flow' : 'Create flow'}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}>
          <div className="grid-2">
            <div className="form-row"><label title="Flow name; shown when wiring up triggers and runs">Name</label><input value={editing.name || ''} placeholder="Order Fulfillment" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
            <div className="form-row"><label title="Identifier (or name) of the step that runs first">Start step</label><input value={editing.startStepId || ''} placeholder="start" onChange={(e) => setEditing({ ...editing, startStepId: e.target.value })} /></div>
          </div>
          <div className="form-row"><label title="Optional human-readable description of the flow's purpose">Description</label><input value={editing.description || ''} placeholder="Validates the order, charges payment, and sends confirmation" onChange={(e) => setEditing({ ...editing, description: e.target.value })} /></div>

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

          <div className="form-row" style={{ marginTop: 'var(--spacing-sm)' }}><label title="Inactive flows reject new runs (existing runs continue to completion)"><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> Active</label></div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Flow JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete flow"
        message={'Delete flow "' + (confirmDelete?.name || '') + '"?'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteFlow(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default DataFlowsView;
