import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import PageHeader from '../components/PageHeader';
import CopyButton from '../components/CopyButton';
import ConfirmModal from '../components/ConfirmModal';
import { flattenOpenApiSpec, groupByTag, substitutePathParams, buildCurlSnippet, buildFetchSnippet } from '../utils/openApi';
import { formatDuration } from '../utils/formatters';

function KeyValueEditor({ entries, onChange, keyPlaceholder = 'key', valuePlaceholder = 'value' }) {
  const rows = Object.entries(entries || {});
  const update = (i, key, value) => {
    const next = rows.map(([k, v], idx) => idx === i ? [key, value] : [k, v]);
    onChange(Object.fromEntries(next.filter(([k]) => k)));
  };
  const remove = (i) => onChange(Object.fromEntries(rows.filter((_, idx) => idx !== i)));
  const add = () => onChange({ ...entries, '': '' });

  return (
    <div>
      {rows.map(([k, v], i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '200px 1fr auto', gap: 'var(--spacing-sm)', marginBottom: 'var(--spacing-sm)' }}>
          <input value={k} placeholder={keyPlaceholder} onChange={(e) => update(i, e.target.value, v)} />
          <input value={v} placeholder={valuePlaceholder} onChange={(e) => update(i, k, e.target.value)} />
          <button className="button-ghost" onClick={() => remove(i)} aria-label="Remove" title="Remove this row">×</button>
        </div>
      ))}
      <button type="button" className="button-secondary" onClick={add} title="Add a new row">+ Add</button>
    </div>
  );
}

function safePretty(text) {
  if (!text) return '';
  try { return JSON.stringify(JSON.parse(text), null, 2); }
  catch { return text; }
}

const LIVE_TRIGGER_TAG = 'Live HTTP triggers';

function parseAllowedMethods(configurationJson) {
  if (!configurationJson) return ['POST'];
  try {
    const c = JSON.parse(configurationJson);
    const m = c.allowedMethods || c.AllowedMethods;
    return Array.isArray(m) && m.length > 0 ? m : ['POST'];
  } catch {
    return ['POST'];
  }
}

function ApiExplorerView({ apiClient, principal }) {
  const { serverUrl } = useAuth();
  const [spec, setSpec] = useState(null);
  const [triggerOps, setTriggerOps] = useState([]);
  const [error, setError] = useState(null);
  const [opId, setOpId] = useState(null);
  const [activeTag, setActiveTag] = useState(null);
  const [filterText, setFilterText] = useState('');
  const [pathParams, setPathParams] = useState({});
  const [queryParams, setQueryParams] = useState({});
  const [headers, setHeaders] = useState({ 'Content-Type': 'application/json' });
  const [body, setBody] = useState('');
  const [response, setResponse] = useState(null);
  const [busy, setBusy] = useState(false);
  const [confirmExecute, setConfirmExecute] = useState(false);

  useEffect(() => {
    if (!apiClient) return;
    apiClient.getOpenApiSpec().then(setSpec).catch((err) => setError(err.message));
  }, [apiClient]);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    const isAdmin = !!principal?.isAdmin || principal?.type === 'administrator';
    const loadTriggers = async () => {
      try {
        let tenantIds = [];
        if (isAdmin) {
          const tenants = await apiClient.listTenants({ pageSize: 500 });
          tenantIds = (tenants?.items || []).map((t) => t.id);
        } else if (principal?.tenantId) {
          tenantIds = [principal.tenantId];
        }
        const all = [];
        for (const tid of tenantIds) {
          try {
            const list = await apiClient.listTriggers(tid, { pageSize: 500 });
            for (const t of (list?.items || [])) {
              if (t.triggerType !== 'Http') continue;
              const methods = parseAllowedMethods(t.configuration);
              const path = '/v1.0/triggers/http/' + t.id;
              for (const m of methods) {
                all.push({
                  id: 'trigger:' + t.id + ':' + m,
                  method: m.toUpperCase(),
                  path,
                  tag: LIVE_TRIGGER_TAG,
                  summary: t.name + (t.description ? ' — ' + t.description : ''),
                  description: 'Fires data flow ' + (t.dataFlowId || '(none)') + '. Trigger id: ' + t.id + '. Tenant: ' + tid + '. Body is forwarded as the StepRequest data.',
                  parameters: [],
                  requestBody: { content: { 'application/json': { schema: { type: 'object' } } } },
                  responses: {}
                });
              }
            }
          } catch { /* tenant inaccessible — skip */ }
        }
        if (!cancelled) setTriggerOps(all);
      } catch { if (!cancelled) setTriggerOps([]); }
    };
    loadTriggers();
    return () => { cancelled = true; };
  }, [apiClient, principal]);

  const operations = useMemo(() => {
    const base = flattenOpenApiSpec(spec);
    return [...base, ...triggerOps].sort((a, b) => (a.tag + a.path).localeCompare(b.tag + b.path));
  }, [spec, triggerOps]);
  const grouped = useMemo(() => groupByTag(operations), [operations]);
  const tags = useMemo(() => Object.keys(grouped).sort(), [grouped]);
  const op = useMemo(() => operations.find((o) => o.id === opId), [operations, opId]);

  useEffect(() => {
    if (!activeTag && tags.length > 0) setActiveTag(tags[0]);
  }, [tags, activeTag]);

  useEffect(() => {
    if (!op) return;
    const next = {};
    for (const p of op.parameters || []) if (p.in === 'path') next[p.name] = '';
    setPathParams(next);
    setQueryParams({});
    setBody(op.method !== 'GET' && op.method !== 'HEAD' ? '{\n  \n}' : '');
    setResponse(null);
  }, [opId]);

  const filteredTagOps = useMemo(() => {
    if (!activeTag) return [];
    const ops = grouped[activeTag] || [];
    if (!filterText) return ops;
    const q = filterText.toLowerCase();
    return ops.filter((o) => o.path.toLowerCase().includes(q) || (o.summary || '').toLowerCase().includes(q) || o.method.toLowerCase().includes(q));
  }, [grouped, activeTag, filterText]);

  const needsConfirm = op && (op.method === 'DELETE' || (op.path || '').toLowerCase().includes('bulk'));

  const execute = async () => {
    if (!op) return;
    setBusy(true);
    setResponse(null);
    try {
      const resolvedPath = substitutePathParams(op.path, pathParams);
      const start = performance.now();
      const res = await apiClient.executeExplorer({
        method: op.method,
        path: resolvedPath,
        query: queryParams,
        headers,
        body: op.method !== 'GET' && op.method !== 'HEAD' ? (body || null) : null
      });
      const text = await res.text();
      const h = {};
      res.headers.forEach((v, k) => { h[k] = v; });
      setResponse({ status: res.status, headers: h, body: text, durationMs: performance.now() - start });
    } catch (err) {
      setResponse({ status: 0, headers: {}, body: err.message, durationMs: 0 });
    } finally {
      setBusy(false);
    }
  };

  const handleExecute = () => { if (needsConfirm) setConfirmExecute(true); else execute(); };

  const pathParamList = op ? (op.parameters || []).filter((p) => p.in === 'path') : [];
  const queryParamList = op ? (op.parameters || []).filter((p) => p.in === 'query') : [];

  const resolvedPath = op ? substitutePathParams(op.path, pathParams) : '';
  const curl = op ? buildCurlSnippet(serverUrl, op.method, resolvedPath, queryParams, headers, op.method !== 'GET' && op.method !== 'HEAD' ? (body || null) : null) : '';
  const fetchSnippet = op ? buildFetchSnippet(serverUrl, op.method, resolvedPath, queryParams, headers, op.method !== 'GET' && op.method !== 'HEAD' ? (body || null) : null) : '';

  return (
    <div>
      <PageHeader title="API Explorer" subtitle={operations.length ? 'Browse and execute ' + operations.length + ' documented API operations across ' + tags.length + ' resources.' : 'Loading the generated OpenAPI document.'} />
      {error && <div className="login-error">{error}</div>}

      <div className="explorer-shell-v2">
        <aside className="explorer-resources">
          <div className="explorer-resources-header" title="Resources are derived from the 'tags' field in /openapi.json">Resources</div>
          {tags.map((tag) => {
            const count = (grouped[tag] || []).length;
            return (
              <button
                key={tag}
                className={'explorer-resource' + (activeTag === tag ? ' active' : '')}
                onClick={() => { setActiveTag(tag); setOpId(null); setFilterText(''); }}
                title={tag + ' — ' + count + ' operation' + (count === 1 ? '' : 's')}
              >
                <span className="explorer-resource-name">{tag}</span>
                <span className="explorer-resource-count">{count}</span>
              </button>
            );
          })}
        </aside>

        <main className="explorer-main">
          {!op && activeTag && (
            <div className="explorer-card">
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 'var(--spacing-md)' }}>
                <h3 style={{ margin: 0 }} title="Operations exposed by this resource">{activeTag}</h3>
                <input
                  value={filterText}
                  onChange={(e) => setFilterText(e.target.value)}
                  placeholder="Filter by path, method, summary…"
                  title="Filter the operation list below"
                  style={{ maxWidth: 300 }}
                />
              </div>
              <div className="explorer-op-grid">
                {filteredTagOps.length === 0 && <div className="empty-state">No operations match the filter.</div>}
                {filteredTagOps.map((o) => (
                  <button
                    key={o.id}
                    className="explorer-op-card"
                    onClick={() => setOpId(o.id)}
                    title={o.method + ' ' + o.path + (o.summary ? ' — ' + o.summary : '')}
                  >
                    <span className={'explorer-method ' + o.method.toLowerCase()}>{o.method}</span>
                    <code className="explorer-op-path">{o.path}</code>
                    {o.summary && <div className="explorer-op-summary">{o.summary}</div>}
                  </button>
                ))}
              </div>
            </div>
          )}

          {op && (
            <>
              <div className="explorer-card">
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 'var(--spacing-sm)' }}>
                  <button className="button-ghost" onClick={() => setOpId(null)} title="Back to operation list">← Back</button>
                  <span className={'explorer-method ' + op.method.toLowerCase()}>{op.method}</span>
                  <code className="monospace" style={{ fontSize: 'var(--font-size-base)' }}>{op.path}</code>
                </div>
                {op.summary && <p style={{ color: 'var(--color-text-secondary)', marginBottom: 'var(--spacing-md)' }}>{op.summary}</p>}

                {pathParamList.length > 0 && (
                  <>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: '0 0 var(--spacing-xs)' }} title="URL placeholders like {tenantId}; substituted before the request is sent">Path parameters</h4>
                    {pathParamList.map((p) => (
                      <div key={p.name} className="form-row">
                        <label title={(p.description || '') + (p.required ? ' (required)' : ' (optional)')}>{p.name}{p.required ? ' *' : ''}</label>
                        <input value={pathParams[p.name] || ''} onChange={(e) => setPathParams((s) => ({ ...s, [p.name]: e.target.value }))} placeholder={p.example || p.schema?.type || 'string'} />
                      </div>
                    ))}
                  </>
                )}

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)' }} title="Query string parameters appended to the URL">Query parameters</h4>
                {queryParamList.length > 0 && queryParamList.map((p) => (
                  <div key={p.name} className="form-row">
                    <label title={(p.description || '') + (p.required ? ' (required)' : ' (optional)')}>{p.name}{p.required ? ' *' : ''}</label>
                    <input value={queryParams[p.name] || ''} onChange={(e) => setQueryParams((s) => ({ ...s, [p.name]: e.target.value }))} placeholder={p.example || p.schema?.type || 'string'} />
                  </div>
                ))}
                <div className="form-row">
                  <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }} title="Add ad-hoc query parameters not declared in the OpenAPI spec">Additional query params</label>
                  <KeyValueEditor entries={queryParams} onChange={setQueryParams} keyPlaceholder="param" valuePlaceholder="value" />
                </div>

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)' }} title="HTTP headers sent with the request. Auth headers are added automatically by the dashboard">Headers</h4>
                <KeyValueEditor entries={headers} onChange={setHeaders} keyPlaceholder="Header" valuePlaceholder="value" />

                {op.method !== 'GET' && op.method !== 'HEAD' && (
                  <>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)' }} title="JSON request body. Must be valid JSON for application/json endpoints">Body (JSON)</h4>
                    <textarea rows={10} value={body} onChange={(e) => setBody(e.target.value)} placeholder='{\n  "name": "example"\n}' style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
                  </>
                )}

                <div style={{ marginTop: 'var(--spacing-md)', display: 'flex', gap: 'var(--spacing-sm)' }}>
                  <button className="button-primary" disabled={busy} onClick={handleExecute} title={needsConfirm ? 'Destructive: confirm before execution' : 'Send the request'}>{busy ? 'Running…' : 'Execute'}</button>
                </div>
              </div>

              <div className="explorer-card">
                <h3>Response</h3>
                {!response && <div className="empty-state">Execute to see the response.</div>}
                {response && (
                  <>
                    <div style={{ marginBottom: 'var(--spacing-sm)', display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span className={response.status >= 200 && response.status < 400 ? 'pill pill-success' : 'pill pill-danger'}>{response.status || 'network error'}</span>
                      <span style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--font-size-sm)' }}>{formatDuration(response.durationMs)}</span>
                    </div>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                      Headers <CopyButton value={JSON.stringify(response.headers, null, 2)} />
                    </h4>
                    <pre className="code-block">{JSON.stringify(response.headers, null, 2)}</pre>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                      Body <CopyButton value={response.body} />
                    </h4>
                    <pre className="code-block">{safePretty(response.body)}</pre>
                  </>
                )}

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }} title="Equivalent curl command for the configured request">
                  curl <CopyButton value={curl} />
                </h4>
                <pre className="code-block">{curl}</pre>

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }} title="Equivalent JavaScript fetch() call for the configured request">
                  fetch <CopyButton value={fetchSnippet} />
                </h4>
                <pre className="code-block">{fetchSnippet}</pre>
              </div>
            </>
          )}
        </main>
      </div>

      <ConfirmModal
        open={confirmExecute}
        danger
        title="Destructive operation"
        message={op ? `Execute ${op.method} ${op.path}? This operation may delete data.` : ''}
        confirmLabel="Execute"
        onConfirm={() => { setConfirmExecute(false); execute(); }}
        onCancel={() => setConfirmExecute(false)}
      />
    </div>
  );
}

export default ApiExplorerView;
