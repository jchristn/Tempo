import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import DataFlowGraphEditor from '../components/DataFlowGraphEditor';
import { formatTime } from '../utils/formatters';
import { authTokenPlaceholder, buildCurlCommand } from '../utils/curl';
import { normalizeApiError, translateLiteral } from '../utils/i18n';

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

function authModeLabel(value, t) {
  return value === 'ApiAuthenticated'
    ? translateLiteral(t, 'API auth')
    : translateLiteral(t, 'Public');
}

function authModePillClass(value) {
  return value === 'ApiAuthenticated' ? 'pill-warning' : 'pill-info';
}

function AuthModePill({ value, t }) {
  const mode = value || 'Public';
  return (
    <span
      className={'pill ' + authModePillClass(mode)}
      title={mode === 'ApiAuthenticated'
        ? translateLiteral(t, 'HTTP trigger invocation requires normal Tempo API authentication')
        : translateLiteral(t, 'HTTP trigger invocation is allowed for anyone with the trigger URL')}
    >
      {authModeLabel(mode, t)}
    </span>
  );
}

function InvocationAuthSelector({ value, onChange, t }) {
  const selected = value || 'Public';
  const options = [
    {
      value: 'Public',
      title: translateLiteral(t, 'Public trigger'),
      badge: translateLiteral(t, 'Public'),
      description: translateLiteral(t, 'Anyone with the trigger URL can invoke this flow')
    },
    {
      value: 'ApiAuthenticated',
      title: translateLiteral(t, 'API-authenticated trigger'),
      badge: translateLiteral(t, 'API auth'),
      description: translateLiteral(t, 'Callers must authenticate to Tempo and have tenant access')
    }
  ];

  return (
    <>
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
    </>
  );
}

function readAllowedMethods(configuration) {
  if (!configuration) return ['POST'];
  try {
    const parsed = typeof configuration === 'string' ? JSON.parse(configuration) : configuration;
    const methods = parsed?.allowedMethods || parsed?.AllowedMethods;
    if (!Array.isArray(methods)) return ['POST'];
    const normalized = methods
      .filter((value) => typeof value === 'string' && value.trim().length > 0)
      .map((value) => value.trim().toUpperCase());
    return normalized.length > 0 ? normalized : ['POST'];
  } catch {
    return ['POST'];
  }
}

function selectHttpTrigger(triggers, flowId) {
  const matches = (Array.isArray(triggers) ? triggers : [])
    .filter((trigger) => (trigger.dataFlowId || trigger.DataFlowId) === flowId)
    .filter((trigger) => String(trigger.triggerType || trigger.TriggerType || '').toLowerCase() === 'http');
  if (matches.length < 1) return null;
  return [...matches].sort((left, right) => {
    const leftActive = !!(left.active ?? left.Active);
    const rightActive = !!(right.active ?? right.Active);
    if (leftActive !== rightActive) return leftActive ? -1 : 1;
    const leftKey = String(left.name || left.Name || left.id || left.Id || '');
    const rightKey = String(right.name || right.Name || right.id || right.Id || '');
    return leftKey.localeCompare(rightKey);
  })[0];
}

function triggerCurlCommand(apiClient, trigger, invocationAuthMode) {
  const triggerId = trigger?.id || trigger?.Id || 'trg_your_id';
  const allowedMethods = readAllowedMethods(trigger?.configuration || trigger?.Configuration);
  const method = allowedMethods.includes('POST')
    ? 'POST'
    : (allowedMethods.includes('GET') ? 'GET' : (allowedMethods[0] || 'POST'));
  const baseUrl = (apiClient?.baseUrl || window.location.origin || '').replace(/\/$/, '');
  const headers = {};
  if (invocationAuthMode === 'ApiAuthenticated') headers.Authorization = 'Bearer ' + authTokenPlaceholder();
  if (method !== 'GET') headers['Content-Type'] = 'application/json';
  return {
    method,
    ...buildCurlCommand({
    url: baseUrl + '/v1.0/triggers/http/' + encodeURIComponent(triggerId),
    method,
    headers,
    body: method === 'GET' ? null : { value: 'hello from curl' },
    shellExpandableHeaders: ['Authorization']
    })
  };
}

function DataFlowsView({ apiClient, principal }) {
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
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
  const [httpTrigger, setHttpTrigger] = useState(null);
  const [httpTriggerLoading, setHttpTriggerLoading] = useState(false);

  const refresh = () => setRefreshKey((k) => k + 1);
  const curlExample = useMemo(
    () => httpTrigger && editing ? triggerCurlCommand(apiClient, httpTrigger, editing.invocationAuthMode) : null,
    [apiClient, editing, httpTrigger]
  );

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listFlows(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  useEffect(() => {
    if (!editing?.id || !tenantId || !apiClient) {
      setHttpTrigger(null);
      setHttpTriggerLoading(false);
      return;
    }

    let cancelled = false;
    setHttpTriggerLoading(true);
    apiClient.listTriggers(tenantId, { pageSize: 500, includeInactive: true })
      .then((result) => {
        if (cancelled) return;
        setHttpTrigger(selectHttpTrigger(result?.items || [], editing.id));
      })
      .catch(() => {
        if (!cancelled) setHttpTrigger(null);
      })
      .finally(() => {
        if (!cancelled) setHttpTriggerLoading(false);
      });

    return () => { cancelled = true; };
  }, [apiClient, tenantId, editing?.id]);

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
      catch { setTransitionsError(tl('Transitions JSON is invalid.')); return; }
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
    catch (err) { alert(normalizeApiError(err, t)); }
  };

  const columns = [
    { key: 'name', label: tl('Name'), tip: tl('Flow name; can be referenced by triggers and from the Run page') },
    { key: 'startStepId', label: tl('Start step'), tip: tl('The step identifier executed first when the flow runs'), cellClass: 'monospace' },
    { key: 'invocationAuthMode', label: tl('Invocation'), tip: tl('Authentication required when this flow is invoked through an HTTP trigger'), render: (f) => <AuthModePill value={f.invocationAuthMode} t={t} /> },
    { key: 'steps', label: tl('Steps'), tip: tl('Number of step nodes defined in the transition graph'), render: (f) => Object.keys(f.transitions || {}).length },
    { key: 'id', label: tl('Identifier'), tip: tl('Globally unique flow id (prefix flow_)'), render: (f) => <CopyableId value={f.id} /> },
    { key: 'createdUtc', label: tl('Created'), tip: tl('When the flow was created'), render: (f) => formatTime(f.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (f) => (
      <RowActions
        onEdit={() => startEdit(f)}
        onViewJson={() => setJsonRow(f)}
        onDelete={() => setConfirmDelete(f)}
        deleteDisabled={!!f.isProtected}
        extra={[
          { label: tl('Run'), onClick: () => runFlow(f) }
        ]}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title={tl('Data Flows')}
        subtitle={tl('Connect steps into executable graphs, then run them directly or attach triggers. {{count}} flows in selected tenant.', { count: data?.totalCount ?? '-' })}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => startEdit(null)}>{tl('+ New flow')}</button>
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
          title={editing.id ? tl('Edit flow') : tl('Create flow')}
          headerMeta={<ModalRecordId label={tl('Flow ID')} value={editing.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>{t('common.actions.cancel')}</button>
            <button className="button-primary" onClick={save}>{t('common.actions.save')}</button>
          </>}
        >
          <div className="grid-2">
            <div className="form-row"><label title={tl('Flow name; shown when wiring up triggers and runs')}>{tl('Name')}</label><input value={editing.name || ''} placeholder={tl('Order Fulfillment')} onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
            <div className="form-row"><label title={tl('Identifier (or name) of the step that runs first')}>{tl('Start step')}</label><input value={editing.startStepId || ''} placeholder="start" onChange={(e) => setEditing({ ...editing, startStepId: e.target.value })} /></div>
          </div>
          <div className="form-row"><label title={tl("Optional human-readable description of the flow's purpose")}>{tl('Description')}</label><input value={editing.description || ''} placeholder={tl('Validates the order, charges payment, and sends confirmation')} onChange={(e) => setEditing({ ...editing, description: e.target.value })} /></div>
          <div className="flow-settings-section">
            <div className="flow-settings-heading">
              <label title={tl('Controls who can invoke this flow through its HTTP trigger and the runtime guardrails applied to it')}>{tl('Run policy')}</label>
              <div className="form-help">{tl('Choose who can invoke the trigger and define the flow-level runtime guardrails')}</div>
            </div>
            <div className="flow-settings-grid">
              <InvocationAuthSelector value={editing.invocationAuthMode} onChange={(invocationAuthMode) => setEditing({ ...editing, invocationAuthMode })} t={t} />
              <div className="flow-runtime-card" title={tl('Flow-level runtime ceiling and active state')}>
                <div className="flow-runtime-header">
                  <span className="pill pill-neutral">{tl('Runtime')}</span>
                  <div className="auth-mode-title">{tl('Flow runtime guardrails')}</div>
                  <div className="auth-mode-description">{tl('Set the maximum synchronous wait budget and whether the flow accepts new runs')}</div>
                </div>
                <div className="form-row flow-runtime-input">
                  <label title={tl('Flow-level runtime ceiling in milliseconds; 0 disables the flow timeout')}>{tl('Timeout (ms)')}</label>
                  <input type="number" min="0" value={editing.maxRuntimeMs || 0} placeholder="0" onChange={(e) => setEditing({ ...editing, maxRuntimeMs: parseInt(e.target.value || '0', 10) })} />
                  <div className="form-help">{tl('HTTP trigger calls wait up to the flow timeout plus a small server buffer')}</div>
                </div>
                <label className="flow-active-toggle" title={tl('Inactive flows reject new runs while existing runs continue to completion')}>
                  <input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} />
                  <span>
                    <strong>{t('common.generic.active')}</strong>
                    <small>{tl('Accept new direct runs and trigger invocations')}</small>
                  </span>
                </label>
              </div>
            </div>
          </div>
          <div className="flow-command-section">
            <div className="flow-settings-heading">
              <label title={tl("Example HTTP trigger invocation that matches this flow's authentication policy")}>{tl('Example cURL')}</label>
              <div className="form-help">{tl('Uses the first attached HTTP trigger for this flow. Direct /flows/{id}/runs API calls always require Tempo API authentication.')}</div>
            </div>
            {!editing.id ? (
              <div className="flow-command-empty">
                {tl('Save the flow and attach an HTTP trigger to generate an invocation cURL example')}
              </div>
            ) : httpTriggerLoading ? (
              <div className="flow-command-empty">{tl('Loading HTTP trigger details...')}</div>
            ) : !httpTrigger ? (
              <div className="flow-command-empty">
                {tl('No HTTP trigger is attached to this flow yet. Create one on the Triggers page to generate a matching cURL example.')}
              </div>
            ) : (
              <>
                <div className="flow-command-meta">
                  <div className="flow-command-pills">
                    <span className="pill pill-neutral" title={tl('Detected from your current browser platform')}>{curlExample?.label ? tl(curlExample.label) : ''}</span>
                    <span className="pill pill-info" title={tl('HTTP method used by the selected trigger example')}>{curlExample?.method}</span>
                    <span className="pill pill-neutral" title={tl('HTTP trigger identifier used for this example')}>{httpTrigger.id || httpTrigger.Id}</span>
                  </div>
                  <div className="form-help">{tl('Uses {{lineSeparator}} line continuations for your shell', { lineSeparator: curlExample?.lineSeparator || '' })}</div>
                </div>
                <div className="command-copy-row">
                  <pre className="code-block">{curlExample?.command}</pre>
                  <CopyButton value={curlExample?.command || ''} title={tl('Copy example cURL command')} />
                </div>
                {!(httpTrigger.active ?? httpTrigger.Active) && (
                  <div className="form-help flow-command-note">
                    {tl('The attached trigger is currently inactive, so this command will fail until the trigger is re-enabled.')}
                  </div>
                )}
              </>
            )}
          </div>

          <div style={{ display: 'flex', gap: 4, marginBottom: 'var(--spacing-sm)' }}>
            <div className="range-selector">
              <button type="button" className={editMode === 'graph' ? 'active' : ''} title={tl('Visual editor for step transitions')} onClick={() => {
                if (editMode === 'json') {
                  try { setEditing({ ...editing, transitions: JSON.parse(transitionsText) }); setTransitionsError(null); }
                  catch { setTransitionsError(tl('Transitions JSON is invalid.')); return; }
                }
                setEditMode('graph');
              }}>{tl('Graph')}</button>
              <button type="button" className={editMode === 'json' ? 'active' : ''} title={tl('Raw JSON editor for the transitions object')} onClick={() => {
                setTransitionsText(JSON.stringify(editing.transitions || {}, null, 2));
                setEditMode('json');
              }}>{t('components.modal.json')}</button>
            </div>
            <div style={{ marginLeft: 'auto', fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', alignSelf: 'center' }}>
              {tl('Referenced step ids that do not yet exist are auto-created when you save.')}
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
              <label title={tl('JSON object keyed by step id, each value has OnSuccess/OnFailure/OnException routing targets')}>{tl('Transitions (JSON)')}</label>
              <textarea rows={14} value={transitionsText} placeholder='{\n  "start": { "OnSuccess": "validate", "OnFailure": null, "OnException": null },\n  "validate": { "OnSuccess": "charge", "OnFailure": "notify", "OnException": "notify" }\n}' onChange={(e) => { setTransitionsText(e.target.value); setTransitionsError(null); }} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
              {transitionsError && <div className="form-help" style={{ color: 'var(--color-danger)' }}>{transitionsError}</div>}
            </div>
          )}
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title={tl('Flow JSON')} />
      <ConfirmModal open={!!confirmDelete} danger title={tl('Delete flow')}
        recordId={confirmDelete?.id || ''}
        recordIdLabel={tl('Flow ID')}
        message={tl('Delete flow "{{name}}"?', { name: confirmDelete?.name || '' })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={async () => { await apiClient.deleteFlow(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default DataFlowsView;
