import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import TenantPicker from '../components/TenantPicker';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ModalRecordId from '../components/ModalRecordId';
import RowActions from '../components/RowActions';
import { formatTime } from '../utils/formatters';
import { HTTP_METHODS } from '../utils/constants';

function parseHttpConfig(s) {
  if (!s) return { allowedMethods: ['POST'], headers: {}, bodySchema: null };
  try {
    const obj = JSON.parse(s);
    return {
      allowedMethods: obj.allowedMethods || obj.AllowedMethods || ['POST'],
      headers: obj.headers || obj.Headers || {},
      bodySchema: obj.bodySchema || obj.BodySchema || null
    };
  } catch {
    return { allowedMethods: ['POST'], headers: {}, bodySchema: null };
  }
}

function TriggersView({ apiClient, principal }) {
  const { serverUrl } = useAuth();
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [flows, setFlows] = useState([]);
  const [editing, setEditing] = useState(null);
  const [httpConfig, setHttpConfig] = useState({ allowedMethods: ['POST'], headers: {}, bodySchema: null });
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listTriggers(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  useEffect(() => {
    if (!apiClient || !tenantId) return;
    apiClient.listFlows(tenantId, { pageSize: 500 }).then((d) => setFlows(d.items || [])).catch(() => {});
  }, [apiClient, tenantId]);

  const startEdit = (t) => {
    const base = t ? { ...t } : { name: '', triggerType: 'Http', active: true };
    setEditing(base);
    setHttpConfig(parseHttpConfig(base.configuration));
  };

  const save = async () => {
    const body = { ...editing };
    if (body.triggerType === 'Http') body.configuration = JSON.stringify(httpConfig, null, 2);
    if (body.id) await apiClient.updateTrigger(tenantId, body.id, body);
    else await apiClient.createTrigger(tenantId, body);
    setEditing(null);
    refresh();
  };

  const toggleMethod = (m) => {
    setHttpConfig((c) => {
      const set = new Set(c.allowedMethods);
      if (set.has(m)) set.delete(m); else set.add(m);
      return { ...c, allowedMethods: Array.from(set) };
    });
  };

  const updateHeader = (idx, key, value) => {
    setHttpConfig((c) => {
      const entries = Object.entries(c.headers || {});
      const next = entries.map(([k, v], i) => i === idx ? [key, value] : [k, v]);
      return { ...c, headers: Object.fromEntries(next.filter(([k]) => k)) };
    });
  };
  const removeHeader = (idx) => {
    setHttpConfig((c) => {
      const entries = Object.entries(c.headers || {}).filter((_, i) => i !== idx);
      return { ...c, headers: Object.fromEntries(entries) };
    });
  };
  const addHeader = () => setHttpConfig((c) => ({ ...c, headers: { ...(c.headers || {}), '': '' } }));

  const triggerPublicUrl = (t) => t.triggerType === 'Http' && t.id ? serverUrl + '/v1.0/triggers/http/' + t.id : null;

  const openJsonRow = (t) => {
    const url = triggerPublicUrl(t);
    setJsonRow(url ? { ...t, publicUrl: url } : t);
  };

  const columns = [
    { key: 'name', label: 'Name', tip: 'Trigger name; appears in run history when this trigger fires a flow' },
    { key: 'triggerType', label: 'Type', tip: 'How the trigger fires: Http (public POST URL), Native (in-process API call), or RabbitMq (queue subscription)', render: (t) => <span className="pill pill-info">{t.triggerType}</span> },
    { key: 'dataFlowId', label: 'Data Flow', tip: 'The data flow that runs when this trigger fires', render: (t) => {
      if (!t.dataFlowId) return <span style={{ color: 'var(--color-text-muted)' }}>(none)</span>;
      const flow = flows.find((f) => f.id === t.dataFlowId);
      return <span title={t.dataFlowId}>{flow ? flow.name : t.dataFlowId}</span>;
    } },
    { key: 'url', label: 'Public URL', tip: 'POST to this URL to fire an HTTP trigger; body is passed as the StepRequest data', render: (t) => (
      t.triggerType === 'Http' ? (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          <code className="monospace" title={triggerPublicUrl(t)}>{'/v1.0/triggers/http/' + t.id.slice(0, 12) + '…'}</code>
          <CopyButton value={triggerPublicUrl(t)} />
        </span>
      ) : '-'
    )},
    { key: 'createdUtc', label: 'Created', tip: 'When the trigger was created', render: (t) => formatTime(t.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (t) => (
      <RowActions
        onEdit={() => startEdit(t)}
        onViewJson={() => openJsonRow(t)}
        onDelete={() => setConfirmDelete(t)}
        deleteDisabled={!!t.isProtected}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title="Triggers"
        subtitle={'Create inbound entry points that start a data flow. ' + (data?.totalCount ?? '-') + ' triggers in selected tenant.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => startEdit(null)}>+ New trigger</button>
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
        onBulkDelete={tenantId ? (ids) => apiClient.bulkDeleteTriggers(tenantId, ids).then(refresh) : null}
        onRowClick={(t) => startEdit(t)}
      />

      {editing && (
        <Modal
          open
          onClose={() => setEditing(null)}
          title={editing.id ? 'Edit trigger' : 'Create trigger'}
          headerMeta={<ModalRecordId label="Trigger ID" value={editing.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}
        >
          <div className="grid-2">
            <div className="form-row"><label title="Trigger name; visible on run records">Name</label><input value={editing.name || ''} placeholder="Order webhook" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
            <div className="form-row">
              <label title="Http: exposes a POST URL. Native: invoked from in-process code. RabbitMq: subscribes to a queue">Type</label>
              <select value={editing.triggerType} onChange={(e) => setEditing({ ...editing, triggerType: e.target.value })}>
                <option value="Http">Http</option>
                <option value="Native">Native</option>
                <option value="RabbitMq">RabbitMq</option>
              </select>
            </div>
          </div>
          <div className="form-row">
            <label title="The flow that runs when this trigger fires. Each fire creates one Run">Associated flow</label>
            <select value={editing.dataFlowId || ''} onChange={(e) => setEditing({ ...editing, dataFlowId: e.target.value || null })}>
              <option value="">(none)</option>
              {flows.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
          </div>

          {editing.triggerType === 'Http' && (
            <div className="card" style={{ marginBottom: 'var(--spacing-sm)' }}>
              <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }}>HTTP request configuration</div>

              <div className="form-row">
                <label title="Public URL clients POST to in order to fire the trigger. Generated from the trigger id and not editable">Public URL (inbound, read-only)</label>
                {editing.id ? (
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <code className="monospace" style={{ flex: 1, padding: '0.5rem 0.625rem', background: 'var(--color-surface-alt)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)' }}>{serverUrl + '/v1.0/triggers/http/' + editing.id}</code>
                    <CopyButton value={serverUrl + '/v1.0/triggers/http/' + editing.id} />
                  </div>
                ) : (
                  <div className="form-help" style={{ padding: '0.5rem 0.625rem', background: 'var(--color-surface-alt)', border: '1px dashed var(--color-border)', borderRadius: 'var(--radius-sm)' }}>
                    Will be assigned after save. Pattern: <code className="monospace">{serverUrl}/v1.0/triggers/http/&lt;trigger-id&gt;</code>
                  </div>
                )}
                <div className="form-help">
                  This is the <strong>inbound</strong> URL — external clients (webhooks, your own apps, partners) POST
                  here to <em>start</em> the associated data flow. The request body becomes the initial
                  <code> StepRequest.Data</code> the flow receives. The URL is generated from the trigger's id and is
                  not editable.
                  <br /><br />
                  Don't confuse this with a step's <em>outbound</em> URL template (on the Steps page) — that's where the
                  flow's REST steps fire requests <em>out</em> to other services as part of doing their work.
                </div>
              </div>

              <div className="form-row">
                <label title="HTTP methods accepted on the public URL. Other methods are rejected with 405">Allowed HTTP methods</label>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {HTTP_METHODS.map((m) => (
                    <label key={m} title={m} style={{ display: 'inline-flex', gap: 4, alignItems: 'center', padding: '0.25rem 0.5rem', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', cursor: 'pointer' }}>
                      <input type="checkbox" checked={httpConfig.allowedMethods.includes(m)} onChange={() => toggleMethod(m)} style={{ width: 'auto' }} />
                      <span>{m}</span>
                    </label>
                  ))}
                </div>
              </div>

              <div className="form-row">
                <label title="Headers required on incoming requests. Empty value = require header presence only; non-empty value = require an exact match">Required headers</label>
                {Object.entries(httpConfig.headers || {}).map(([k, v], i) => (
                  <div key={i} style={{ display: 'grid', gridTemplateColumns: '200px 1fr auto', gap: 'var(--spacing-sm)', marginBottom: 'var(--spacing-sm)' }}>
                    <input value={k} placeholder="x-api-key" onChange={(e) => updateHeader(i, e.target.value, v)} />
                    <input value={v} placeholder="(empty = presence only)" onChange={(e) => updateHeader(i, k, e.target.value)} />
                    <button type="button" className="button-ghost" onClick={() => removeHeader(i)} aria-label="Remove" title="Remove this header requirement">×</button>
                  </div>
                ))}
                <button type="button" className="button-secondary" onClick={addHeader} title="Add a new required-header rule">+ Add header</button>
              </div>

              <div className="form-row">
                <label title="Optional JSON Schema; if set, request bodies are validated against it before the flow fires">Body JSON schema (optional)</label>
                <textarea rows={6} placeholder='{"type":"object","required":["orderId"],"properties":{"orderId":{"type":"string"}}}' value={httpConfig.bodySchema ? JSON.stringify(httpConfig.bodySchema, null, 2) : ''} onChange={(e) => {
                  if (!e.target.value.trim()) { setHttpConfig((c) => ({ ...c, bodySchema: null })); return; }
                  try { setHttpConfig((c) => ({ ...c, bodySchema: JSON.parse(e.target.value) })); }
                  catch { /* leave as-is; user will fix */ }
                }} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
                <div className="form-help">Parsed and saved into the trigger's <code>configuration</code> JSON blob. Enforcement happens in your flow/step logic.</div>
              </div>
            </div>
          )}

          {editing.triggerType !== 'Http' && (
            <div className="form-row"><label title="Free-form JSON configuration consumed by the trigger implementation">Configuration (JSON, optional)</label><textarea rows={4} value={editing.configuration || ''} placeholder='{"queueName": "orders", "exchange": "events"}' onChange={(e) => setEditing({ ...editing, configuration: e.target.value })} style={{ fontFamily: 'var(--font-mono)' }} /></div>
          )}

          <div className="form-row"><label title="Inactive triggers reject all firings"><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> Active</label></div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Trigger JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete trigger"
        recordId={confirmDelete?.id || ''}
        recordIdLabel="Trigger ID"
        message={'Delete trigger "' + (confirmDelete?.name || '') + '"?'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteTrigger(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default TriggersView;
