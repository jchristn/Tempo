import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import PageHeader from '../components/PageHeader';
import CopyButton from '../components/CopyButton';
import ConfirmModal from '../components/ConfirmModal';
import { flattenOpenApiSpec, groupByTag, substitutePathParams, buildCurlSnippet, buildFetchSnippet } from '../utils/openApi';
import { formatDuration } from '../utils/formatters';
import { normalizeApiError, translateLiteral } from '../utils/i18n';

function KeyValueEditor({ entries, onChange, keyPlaceholder = 'key', valuePlaceholder = 'value', addLabel = '+ Add' }) {
  const { t } = useTranslation();
  const resolvedKeyPlaceholder = translateLiteral(t, keyPlaceholder);
  const resolvedValuePlaceholder = translateLiteral(t, valuePlaceholder);
  const resolvedAddLabel = translateLiteral(t, addLabel);
  const rows = Object.entries(entries || {});

  const update = (index, key, value) => {
    const next = rows.map(([existingKey, existingValue], rowIndex) => rowIndex === index ? [key, value] : [existingKey, existingValue]);
    onChange(Object.fromEntries(next.filter(([currentKey]) => currentKey)));
  };

  const remove = (index) => onChange(Object.fromEntries(rows.filter((_, rowIndex) => rowIndex !== index)));
  const add = () => onChange({ ...entries, '': '' });

  return (
    <div>
      {rows.map(([key, value], index) => (
        <div key={index} style={{ display: 'grid', gridTemplateColumns: '200px 1fr auto', gap: 'var(--spacing-sm)', marginBottom: 'var(--spacing-sm)' }}>
          <input value={key} placeholder={resolvedKeyPlaceholder} onChange={(e) => update(index, e.target.value, value)} />
          <input value={value} placeholder={resolvedValuePlaceholder} onChange={(e) => update(index, key, e.target.value)} />
          <button className="button-ghost" onClick={() => remove(index)} aria-label={t('common.actions.remove')} title={t('views.apiExplorer.removeRow', { defaultValue: 'Remove this row' })}>x</button>
        </div>
      ))}
      <button type="button" className="button-secondary" onClick={add} title={t('views.apiExplorer.addRowTitle', { defaultValue: 'Add a new row' })}>{resolvedAddLabel}</button>
    </div>
  );
}

function safePretty(text) {
  if (!text) return '';
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

function parseAllowedMethods(configurationJson) {
  if (!configurationJson) return ['POST'];
  try {
    const config = JSON.parse(configurationJson);
    const methods = config.allowedMethods || config.AllowedMethods;
    return Array.isArray(methods) && methods.length > 0 ? methods : ['POST'];
  } catch {
    return ['POST'];
  }
}

function ApiExplorerView({ apiClient, principal }) {
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
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
    apiClient.getOpenApiSpec().then(setSpec).catch((err) => setError(normalizeApiError(err, t)));
  }, [apiClient, t]);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    const isAdmin = !!principal?.isAdmin || principal?.type === 'administrator';

    const loadTriggers = async () => {
      try {
        let tenantIds = [];
        if (isAdmin) {
          const tenants = await apiClient.listTenants({ pageSize: 500 });
          tenantIds = (tenants?.items || []).map((tenant) => tenant.id);
        } else if (principal?.tenantId) {
          tenantIds = [principal.tenantId];
        }

        const all = [];
        for (const tenantId of tenantIds) {
          try {
            const list = await apiClient.listTriggers(tenantId, { pageSize: 500 });
            for (const trigger of (list?.items || [])) {
              if (trigger.triggerType !== 'Http') continue;
              const methods = parseAllowedMethods(trigger.configuration);
              const path = '/v1.0/triggers/http/' + trigger.id;
              for (const method of methods) {
                all.push({
                  id: 'trigger:' + trigger.id + ':' + method,
                  method: method.toUpperCase(),
                  path,
                  tag: t('views.apiExplorer.liveHttpTriggers', { defaultValue: 'Live HTTP triggers' }),
                  summary: trigger.name + (trigger.description ? ' - ' + trigger.description : ''),
                  description: t('views.apiExplorer.liveTriggerDescription', {
                    defaultValue: 'Fires data flow {{dataFlowId}}. Trigger id: {{triggerId}}. Tenant: {{tenantId}}. Body is forwarded as the StepRequest data.',
                    dataFlowId: trigger.dataFlowId || t('common.generic.none'),
                    triggerId: trigger.id,
                    tenantId
                  }),
                  parameters: [],
                  requestBody: { content: { 'application/json': { schema: { type: 'object' } } } },
                  responses: {}
                });
              }
            }
          } catch {
            // Tenant inaccessible; skip it.
          }
        }

        if (!cancelled) setTriggerOps(all);
      } catch {
        if (!cancelled) setTriggerOps([]);
      }
    };

    loadTriggers();
    return () => { cancelled = true; };
  }, [apiClient, principal, t]);

  const operations = useMemo(() => {
    const base = flattenOpenApiSpec(spec);
    return [...base, ...triggerOps].sort((a, b) => (a.tag + a.path).localeCompare(b.tag + b.path));
  }, [spec, triggerOps]);

  const grouped = useMemo(() => groupByTag(operations), [operations]);
  const tags = useMemo(() => Object.keys(grouped).sort(), [grouped]);
  const op = useMemo(() => operations.find((item) => item.id === opId), [operations, opId]);

  useEffect(() => {
    if (!activeTag && tags.length > 0) setActiveTag(tags[0]);
  }, [activeTag, tags]);

  useEffect(() => {
    if (!op) return;
    const next = {};
    for (const param of op.parameters || []) {
      if (param.in === 'path') next[param.name] = '';
    }
    setPathParams(next);
    setQueryParams({});
    setBody(op.method !== 'GET' && op.method !== 'HEAD' ? '{\n  \n}' : '');
    setResponse(null);
  }, [op]);

  const filteredTagOps = useMemo(() => {
    if (!activeTag) return [];
    const tagOps = grouped[activeTag] || [];
    if (!filterText) return tagOps;
    const needle = filterText.toLowerCase();
    return tagOps.filter((item) =>
      item.path.toLowerCase().includes(needle) ||
      (item.summary || '').toLowerCase().includes(needle) ||
      item.method.toLowerCase().includes(needle)
    );
  }, [activeTag, filterText, grouped]);

  const needsConfirm = op && (op.method === 'DELETE' || (op.path || '').toLowerCase().includes('bulk'));

  const execute = async () => {
    if (!op) return;
    setBusy(true);
    setResponse(null);

    try {
      const resolvedPath = substitutePathParams(op.path, pathParams);
      const start = performance.now();
      const result = await apiClient.executeExplorer({
        method: op.method,
        path: resolvedPath,
        query: queryParams,
        headers,
        body: op.method !== 'GET' && op.method !== 'HEAD' ? (body || null) : null
      });
      const text = await result.text();
      const responseHeaders = {};
      result.headers.forEach((value, key) => { responseHeaders[key] = value; });
      setResponse({ status: result.status, headers: responseHeaders, body: text, durationMs: performance.now() - start });
    } catch (err) {
      setResponse({ status: 0, headers: {}, body: normalizeApiError(err, t), durationMs: 0 });
    } finally {
      setBusy(false);
    }
  };

  const handleExecute = () => {
    if (needsConfirm) setConfirmExecute(true);
    else void execute();
  };

  const pathParamList = op ? (op.parameters || []).filter((param) => param.in === 'path') : [];
  const queryParamList = op ? (op.parameters || []).filter((param) => param.in === 'query') : [];
  const resolvedPath = op ? substitutePathParams(op.path, pathParams) : '';
  const resolvedBody = op && op.method !== 'GET' && op.method !== 'HEAD' ? (body || null) : null;
  const curl = op ? buildCurlSnippet(serverUrl, op.method, resolvedPath, queryParams, headers, resolvedBody) : '';
  const fetchSnippet = op ? buildFetchSnippet(serverUrl, op.method, resolvedPath, queryParams, headers, resolvedBody) : '';

  return (
    <div>
      <PageHeader
        title={t('views.apiExplorer.title')}
        subtitle={
          operations.length
            ? t('views.apiExplorer.loadedSubtitle', {
              defaultValue: 'Browse and execute {{operations}} documented API operations across {{resources}} resources.',
              operations: operations.length,
              resources: tags.length
            })
            : t('views.apiExplorer.loadingSubtitle', { defaultValue: 'Loading the generated OpenAPI document.' })
        }
      />
      {error && <div className="login-error">{error}</div>}

      <div className="explorer-shell-v2">
        <aside className="explorer-resources">
          <div className="explorer-resources-header" title={t('views.apiExplorer.resourcesTitle', { defaultValue: "Resources are derived from the 'tags' field in /openapi.json" })}>
            {tl(t('views.apiExplorer.resources', { defaultValue: 'Resources' }))}
          </div>
          {tags.map((tag) => {
            const count = (grouped[tag] || []).length;
            const translatedTag = tl(tag);
            return (
              <button
                key={tag}
                className={'explorer-resource' + (activeTag === tag ? ' active' : '')}
                onClick={() => { setActiveTag(tag); setOpId(null); setFilterText(''); }}
                title={t('views.apiExplorer.resourceSummary', { defaultValue: '{{tag}} - {{count}} operation', tag: translatedTag, count })}
              >
                <span className="explorer-resource-name">{translatedTag}</span>
                <span className="explorer-resource-count">{count}</span>
              </button>
            );
          })}
        </aside>

        <main className="explorer-main">
          {!op && activeTag && (
            <div className="explorer-card">
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 'var(--spacing-md)' }}>
                <h3 style={{ margin: 0 }} title={t('views.apiExplorer.operationsTitle', { defaultValue: 'Operations exposed by this resource' })}>{tl(activeTag)}</h3>
                <input
                  value={filterText}
                  onChange={(e) => setFilterText(e.target.value)}
                  placeholder={t('views.apiExplorer.filterPlaceholder', { defaultValue: 'Filter by path, method, summary...' })}
                  title={t('views.apiExplorer.filterTitle', { defaultValue: 'Filter the operation list below' })}
                  style={{ maxWidth: 300 }}
                />
              </div>
              <div className="explorer-op-grid">
                {filteredTagOps.length === 0 && <div className="empty-state">{t('views.apiExplorer.noFilteredOperations', { defaultValue: 'No operations match the filter.' })}</div>}
                {filteredTagOps.map((item) => (
                  <button
                    key={item.id}
                    className="explorer-op-card"
                    onClick={() => setOpId(item.id)}
                    title={item.method + ' ' + item.path + (item.summary ? ' - ' + item.summary : '')}
                  >
                    <span className={'explorer-method ' + item.method.toLowerCase()}>{item.method}</span>
                    <code className="explorer-op-path">{item.path}</code>
                    {item.summary && <div className="explorer-op-summary">{item.summary}</div>}
                  </button>
                ))}
              </div>
            </div>
          )}

          {op && (
            <>
              <div className="explorer-card">
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 'var(--spacing-sm)' }}>
                  <button className="button-ghost" onClick={() => setOpId(null)} title={t('common.actions.back')}>{t('common.actions.back')}</button>
                  <span className={'explorer-method ' + op.method.toLowerCase()}>{op.method}</span>
                  <code className="monospace" style={{ fontSize: 'var(--font-size-base)' }}>{op.path}</code>
                </div>
                {op.summary && <p style={{ color: 'var(--color-text-secondary)', marginBottom: 'var(--spacing-md)' }}>{op.summary}</p>}

                {pathParamList.length > 0 && (
                  <>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: '0 0 var(--spacing-xs)' }} title={t('views.apiExplorer.pathParametersTitle', { defaultValue: 'URL placeholders like {tenantId}; substituted before the request is sent' })}>
                      {t('views.apiExplorer.pathParameters', { defaultValue: 'Path parameters' })}
                    </h4>
                    {pathParamList.map((param) => (
                      <div key={param.name} className="form-row">
                        <label title={(param.description || '') + (param.required ? ' (' + t('views.apiExplorer.required', { defaultValue: 'required' }) + ')' : ' (' + t('common.generic.optional') + ')')}>
                          {param.name}{param.required ? ' *' : ''}
                        </label>
                        <input value={pathParams[param.name] || ''} onChange={(e) => setPathParams((current) => ({ ...current, [param.name]: e.target.value }))} placeholder={param.example || param.schema?.type || 'string'} />
                      </div>
                    ))}
                  </>
                )}

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)' }} title={t('views.apiExplorer.queryParametersTitle', { defaultValue: 'Query string parameters appended to the URL' })}>
                  {t('views.apiExplorer.queryParameters', { defaultValue: 'Query parameters' })}
                </h4>
                {queryParamList.length > 0 && queryParamList.map((param) => (
                  <div key={param.name} className="form-row">
                    <label title={(param.description || '') + (param.required ? ' (' + t('views.apiExplorer.required', { defaultValue: 'required' }) + ')' : ' (' + t('common.generic.optional') + ')')}>
                      {param.name}{param.required ? ' *' : ''}
                    </label>
                    <input value={queryParams[param.name] || ''} onChange={(e) => setQueryParams((current) => ({ ...current, [param.name]: e.target.value }))} placeholder={param.example || param.schema?.type || 'string'} />
                  </div>
                ))}
                <div className="form-row">
                  <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }} title={t('views.apiExplorer.additionalQueryTitle', { defaultValue: 'Add ad-hoc query parameters not declared in the OpenAPI spec' })}>
                    {t('views.apiExplorer.additionalQuery', { defaultValue: 'Additional query params' })}
                  </label>
                  <KeyValueEditor
                    entries={queryParams}
                    onChange={setQueryParams}
                    keyPlaceholder={t('views.apiExplorer.paramPlaceholder', { defaultValue: 'param' })}
                    valuePlaceholder={t('views.apiExplorer.valuePlaceholder', { defaultValue: 'value' })}
                    addLabel={t('common.actions.add')}
                  />
                </div>

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)' }} title={t('views.apiExplorer.headersTitle', { defaultValue: 'HTTP headers sent with the request. Auth headers are added automatically by the dashboard' })}>
                  {t('views.apiExplorer.headers', { defaultValue: 'Headers' })}
                </h4>
                <KeyValueEditor
                  entries={headers}
                  onChange={setHeaders}
                  keyPlaceholder={t('views.apiExplorer.headerPlaceholder', { defaultValue: 'Header' })}
                  valuePlaceholder={t('views.apiExplorer.valuePlaceholder', { defaultValue: 'value' })}
                  addLabel={t('common.actions.addHeader')}
                />

                {op.method !== 'GET' && op.method !== 'HEAD' && (
                  <>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)' }} title={t('views.apiExplorer.bodyTitle', { defaultValue: 'JSON request body. Must be valid JSON for application/json endpoints' })}>
                      {t('views.apiExplorer.body', { defaultValue: 'Body (JSON)' })}
                    </h4>
                    <textarea rows={10} value={body} onChange={(e) => setBody(e.target.value)} placeholder={'{\n  "name": "example"\n}'} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
                  </>
                )}

                <div style={{ marginTop: 'var(--spacing-md)', display: 'flex', gap: 'var(--spacing-sm)' }}>
                  <button className="button-primary" disabled={busy} onClick={handleExecute} title={needsConfirm ? t('views.apiExplorer.executeDanger', { defaultValue: 'Destructive: confirm before execution' }) : t('views.apiExplorer.executeTitle', { defaultValue: 'Send the request' })}>
                    {busy ? t('views.apiExplorer.running', { defaultValue: 'Running...' }) : t('common.actions.execute')}
                  </button>
                </div>
              </div>

              <div className="explorer-card">
                <h3>{t('views.apiExplorer.response', { defaultValue: 'Response' })}</h3>
                {!response && <div className="empty-state">{t('views.apiExplorer.responseEmpty', { defaultValue: 'Execute to see the response.' })}</div>}
                {response && (
                  <>
                    <div style={{ marginBottom: 'var(--spacing-sm)', display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span className={response.status >= 200 && response.status < 400 ? 'pill pill-success' : 'pill pill-danger'}>
                        {response.status || t('views.apiExplorer.networkError', { defaultValue: 'network error' })}
                      </span>
                      <span style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--font-size-sm)' }}>{formatDuration(response.durationMs)}</span>
                    </div>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                      {t('views.apiExplorer.responseHeaders', { defaultValue: 'Headers' })} <CopyButton value={JSON.stringify(response.headers, null, 2)} />
                    </h4>
                    <pre className="code-block">{JSON.stringify(response.headers, null, 2)}</pre>
                    <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                      {t('views.apiExplorer.responseBody', { defaultValue: 'Body' })} <CopyButton value={response.body} />
                    </h4>
                    <pre className="code-block">{safePretty(response.body)}</pre>
                  </>
                )}

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }} title={t('views.apiExplorer.curlTitle', { defaultValue: 'Equivalent curl command for the configured request' })}>
                  curl <CopyButton value={curl} />
                </h4>
                <pre className="code-block">{curl}</pre>

                <h4 style={{ fontSize: 'var(--font-size-sm)', margin: 'var(--spacing-md) 0 var(--spacing-xs)', display: 'inline-flex', alignItems: 'center', gap: 6 }} title={t('views.apiExplorer.fetchTitle', { defaultValue: 'Equivalent JavaScript fetch() call for the configured request' })}>
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
        title={t('views.apiExplorer.destructiveOperation', { defaultValue: 'Destructive operation' })}
        message={op ? t('views.apiExplorer.destructiveMessage', { defaultValue: 'Execute {{method}} {{path}}? This operation may delete data.', method: op.method, path: op.path }) : ''}
        confirmLabel={t('common.actions.execute')}
        onConfirm={() => { setConfirmExecute(false); void execute(); }}
        onCancel={() => setConfirmExecute(false)}
      />
    </div>
  );
}

export default ApiExplorerView;
