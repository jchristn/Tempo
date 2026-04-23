import { useEffect, useMemo, useState } from 'react';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import TenantPicker from '../components/TenantPicker';
import CopyableId from '../components/CopyableId';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ModalRecordId from '../components/ModalRecordId';
import RowActions from '../components/RowActions';
import { formatTime } from '../utils/formatters';

const REST = 'External.Rest';
const ARTIFACT_PROCESS = 'Artifact.Process';
const ARTIFACT_PYTHON = 'Artifact.Python';
const ARTIFACT_JAVASCRIPT = 'Artifact.JavaScript';
const ARTIFACT_DOTNET_PROCESS = 'Artifact.DotnetProcess';
const BUILTIN_UNKNOWN = 'Builtin.Unknown';

function defaultConfig(runtimeKey) {
  switch (runtimeKey) {
    case REST:
      return { runtimeKey, method: 'GET', url: '', headers: {}, timeoutMs: 30000 };
    case ARTIFACT_PROCESS:
    case ARTIFACT_DOTNET_PROCESS:
      return { runtimeKey, artifactId: '', artifactVersion: 'current', entrypoint: '', arguments: [], environmentReferences: [] };
    case ARTIFACT_PYTHON:
      return { runtimeKey, artifactId: '', artifactVersion: 'current', entrypoint: '', module: '', function: 'run', pythonVersion: '', arguments: [], environmentReferences: [] };
    case ARTIFACT_JAVASCRIPT:
      return { runtimeKey, artifactId: '', artifactVersion: 'current', entrypoint: '', module: '', function: 'run', arguments: [], environmentReferences: [] };
    case BUILTIN_UNKNOWN:
      return { runtimeKey, identifier: '' };
    default:
      return { runtimeKey };
  }
}

function runtimeConfigForStep(step) {
  if (step.runtimeConfig) return step.runtimeConfig;
  if (step.runtimeKey === REST && step.rest) {
    return {
      runtimeKey: REST,
      method: step.rest.method || 'GET',
      url: step.rest.url || '',
      headers: step.rest.headers || {},
      timeoutMs: step.rest.timeoutMs || 30000
    };
  }
  return defaultConfig(step.runtimeKey || REST);
}

function newStep() {
  return {
    executionKey: '',
    name: '',
    description: '',
    runtimeKey: REST,
    runtimeConfig: defaultConfig(REST),
    contractType: 'Loose',
    inputSchema: '',
    outputSchema: '',
    validateInput: false,
    validateOutput: false,
    maxRuntimeMs: 0,
    active: true
  };
}

function sourceTemplate(language) {
  if (language === 'CSharp') {
    return {
      language,
      fileName: 'Handler.cs',
      function: 'run',
      handlerType: 'Tempo.UserSteps.Handler',
      code: 'using System.Threading;\nusing System.Threading.Tasks;\nusing Tempo;\nusing Tempo.Protocol;\n\nnamespace Tempo.UserSteps;\n\npublic sealed class Handler : TempoStepHandlerBase\n{\n    public override Task<StepResult> RunAsync(StepRequest request, CancellationToken token = default)\n    {\n        LogInfo("Echo step received input: " + request.Data);\n        return Task.FromResult(Success(request, new { ok = true, input = request.Data }));\n    }\n}\n'
    };
  }
  if (language === 'Python') {
    return {
      language,
      fileName: 'handler.py',
      function: 'run',
      handlerType: 'Tempo.UserSteps.Handler',
      code: 'def run(req):\n    return {\"ok\": True, \"input\": req.get(\"data\")}\n'
    };
  }
  return {
    language: 'JavaScript',
    fileName: 'handler.js',
    function: 'run',
    handlerType: 'Tempo.UserSteps.Handler',
    code: 'exports.run = async function(req) {\n  return { ok: true, input: req.data };\n};\n'
  };
}

function newSourceStep() {
  return {
    executionKey: '',
    name: '',
    description: '',
    artifactName: '',
    version: '',
    entrypoint: 'main',
    contractType: 'Loose',
    inputSchema: '',
    outputSchema: '',
    validateInput: false,
    validateOutput: false,
    maxRuntimeMs: 0,
    active: true,
    ...sourceTemplate('JavaScript')
  };
}

function editStep(step) {
  const runtimeKey = step.runtimeKey || (step.stepType === 'Rest' ? REST : 'Builtin.Unknown');
  return {
    ...step,
    runtimeKey,
    runtimeConfig: runtimeConfigForStep({ ...step, runtimeKey }),
    inputSchema: step.inputSchema || '',
    outputSchema: step.outputSchema || '',
    contractType: step.contractType || 'Loose'
  };
}

function parseJsonObject(text, label) {
  if (!text || !text.trim()) return {};
  const value = JSON.parse(text);
  if (!value || Array.isArray(value) || typeof value !== 'object') throw new Error(label + ' must be a JSON object.');
  return value;
}

function listText(values) {
  return (values || []).join('\n');
}

function parseList(text) {
  return (text || '')
    .split(/\r?\n|,/)
    .map((v) => v.trim())
    .filter(Boolean);
}

function runtimePill(runtimeKey) {
  if (runtimeKey === REST) return 'pill-info';
  if (runtimeKey === ARTIFACT_PROCESS || runtimeKey === ARTIFACT_PYTHON || runtimeKey === ARTIFACT_JAVASCRIPT || runtimeKey === ARTIFACT_DOTNET_PROCESS) return 'pill-warning';
  if ((runtimeKey || '').startsWith('Builtin.')) return 'pill-success';
  return 'pill-neutral';
}

function availabilityPill(availability) {
  return availability === 'Available' ? 'pill-success' : availability === 'DisabledBySettings' ? 'pill-warning' : 'pill-neutral';
}

function normalizeError(err) {
  if (!err) return 'Request failed.';
  if (err.body) {
    try {
      const parsed = JSON.parse(err.body);
      return parsed.message || parsed.details || err.message;
    } catch { return err.body; }
  }
  return err.message || String(err);
}

function registeredIdentifierSet(registered) {
  return new Set((registered || []).map((s) => s.identifier).filter(Boolean));
}

function isOrphanedBuiltin(step, registeredIds) {
  const runtimeKey = String(step.runtimeKey || '');
  if (step.runtimeBindingState === 'Orphaned') return true;
  if (runtimeKey === BUILTIN_UNKNOWN) return true;
  if (!runtimeKey.startsWith('Builtin.')) return false;
  const cfg = step.runtimeConfig || {};
  const identifier = cfg.identifier || step.executionKey || step.name;
  return !!identifier && registeredIds.size > 0 && !registeredIds.has(identifier);
}

function coerceDescriptorValue(value, descriptor) {
  const type = (descriptor?.type || 'string').toLowerCase();
  if (type === 'integer') return parseInt(value || '0', 10);
  if (type === 'number') return parseFloat(value || '0');
  if (type === 'boolean') return value === true || value === 'true';
  if (type === 'array') return Array.isArray(value) ? value : parseList(value || '');
  if (type === 'object') return typeof value === 'string' ? parseJsonObject(value, descriptor.name || 'Object value') : (value || {});
  return value == null ? '' : value;
}

function normalizeDescriptorConfig(config, descriptor) {
  if (!descriptor || !descriptor.configProperties || descriptor.configProperties.length === 0) return config;
  const normalized = { ...config };
  for (const prop of descriptor.configProperties) {
    if (normalized[prop.name] !== undefined && normalized[prop.name] !== null) {
      normalized[prop.name] = coerceDescriptorValue(normalized[prop.name], prop);
    }
  }
  return normalized;
}

function StepsView({ apiClient, principal }) {
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [registered, setRegistered] = useState([]);
  const [runtimes, setRuntimes] = useState([]);
  const [artifacts, setArtifacts] = useState([]);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(null);
  const [sourceEditing, setSourceEditing] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [formError, setFormError] = useState('');
  const [sourceError, setSourceError] = useState('');
  const [sourceSaving, setSourceSaving] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const refresh = () => setRefreshKey((k) => k + 1);

  const runtimeMap = useMemo(() => {
    const map = new Map();
    for (const runtime of runtimes || []) map.set(String(runtime.runtimeKey), runtime);
    return map;
  }, [runtimes]);
  const registeredIds = useMemo(() => registeredIdentifierSet(registered), [registered]);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listSteps(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  useEffect(() => { if (apiClient) apiClient.listRegisteredSteps().then(setRegistered).catch(() => {}); }, [apiClient]);

  useEffect(() => {
    if (!apiClient || !tenantId) return;
    let cancelled = false;
    Promise.all([
      apiClient.listTenantRuntimes(tenantId).catch(() => []),
      apiClient.listArtifacts(tenantId, { pageSize: 500 }).then((d) => d.items || []).catch(() => [])
    ]).then(([runtimeList, artifactList]) => {
      if (cancelled) return;
      setRuntimes(runtimeList || []);
      setArtifacts(artifactList || []);
    });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, refreshKey]);

  const setRuntimeKey = (runtimeKey) => {
    setEditing((current) => ({ ...current, runtimeKey, runtimeConfig: defaultConfig(runtimeKey) }));
    setFormError('');
  };

  const updateConfig = (patch) => {
    setEditing((current) => ({
      ...current,
      runtimeConfig: { ...(current.runtimeConfig || defaultConfig(current.runtimeKey)), ...patch }
    }));
  };

  const buildPayload = () => {
    let cfg = { ...(editing.runtimeConfig || defaultConfig(editing.runtimeKey)), runtimeKey: editing.runtimeKey };
    if (editing.runtimeKey === REST) {
      cfg.headers = typeof cfg.headers === 'string' ? parseJsonObject(cfg.headers, 'Headers') : (cfg.headers || {});
      cfg.timeoutMs = parseInt(cfg.timeoutMs || '0', 10);
    }
    if (editing.runtimeKey === ARTIFACT_PROCESS || editing.runtimeKey === ARTIFACT_PYTHON || editing.runtimeKey === ARTIFACT_JAVASCRIPT || editing.runtimeKey === ARTIFACT_DOTNET_PROCESS) {
      cfg.arguments = Array.isArray(cfg.arguments) ? cfg.arguments : parseList(cfg.argumentsText || '');
      cfg.environmentReferences = Array.isArray(cfg.environmentReferences) ? cfg.environmentReferences : parseList(cfg.environmentReferencesText || '');
      if (!cfg.artifactVersion) cfg.artifactVersion = 'current';
    }
    if (editing.runtimeKey === ARTIFACT_PYTHON && !cfg.function) cfg.function = 'run';
    if (editing.runtimeKey === ARTIFACT_JAVASCRIPT && !cfg.function) cfg.function = 'run';
    if (![REST, ARTIFACT_PROCESS, ARTIFACT_PYTHON, ARTIFACT_JAVASCRIPT, ARTIFACT_DOTNET_PROCESS].includes(editing.runtimeKey)) {
      cfg = normalizeDescriptorConfig(cfg, runtimeMap.get(editing.runtimeKey));
    }

    return {
      executionKey: editing.executionKey || editing.name,
      name: editing.name || '',
      description: editing.description || null,
      runtimeKey: editing.runtimeKey,
      runtimeConfig: cfg,
      contractType: editing.contractType || 'Loose',
      inputSchema: editing.inputSchema || null,
      outputSchema: editing.outputSchema || null,
      validateInput: !!editing.validateInput,
      validateOutput: !!editing.validateOutput,
      maxRuntimeMs: parseInt(editing.maxRuntimeMs || '0', 10),
      active: editing.active !== false
    };
  };

  const save = async () => {
    setFormError('');
    try {
      const payload = buildPayload();
      await apiClient.validateRuntime(tenantId, { runtimeKey: payload.runtimeKey, config: payload.runtimeConfig });
      if (editing.id) await apiClient.updateStep(tenantId, editing.id, payload);
      else await apiClient.createStep(tenantId, payload);
      setEditing(null);
      refresh();
    } catch (err) {
      setFormError(normalizeError(err));
    }
  };

  const setSourceLanguage = (language) => {
    setSourceEditing((current) => ({ ...current, ...sourceTemplate(language) }));
    setSourceError('');
  };

  const saveSource = async () => {
    setSourceError('');
    setSourceSaving(true);
    try {
      const payload = {
        executionKey: sourceEditing.executionKey || sourceEditing.name,
        name: sourceEditing.name || '',
        description: sourceEditing.description || null,
        language: sourceEditing.language,
        code: sourceEditing.code || '',
        fileName: sourceEditing.fileName || null,
        artifactName: sourceEditing.artifactName || null,
        entrypoint: sourceEditing.entrypoint || 'main',
        module: sourceEditing.module || null,
        function: sourceEditing.function || 'run',
        handlerType: sourceEditing.handlerType || 'Tempo.UserSteps.Handler',
        contractType: sourceEditing.contractType || 'Loose',
        inputSchema: sourceEditing.inputSchema || null,
        outputSchema: sourceEditing.outputSchema || null,
        validateInput: !!sourceEditing.validateInput,
        validateOutput: !!sourceEditing.validateOutput,
        maxRuntimeMs: parseInt(sourceEditing.maxRuntimeMs || '0', 10),
        active: sourceEditing.active !== false
      };
      await apiClient.createSourceStep(tenantId, payload);
      setSourceEditing(null);
      refresh();
    } catch (err) {
      setSourceError(normalizeError(err));
    } finally {
      setSourceSaving(false);
    }
  };

  const columns = [
    { key: 'name', label: 'Name', tip: 'Display name for this step' },
    { key: 'executionKey', label: 'Execution key', tip: 'Stable key used by flow transitions', render: (s) => s.executionKey || s.name },
    { key: 'runtimeKey', label: 'Runtime', tip: 'Runtime provider used to execute this step', render: (s) => (
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
        <span className={'pill ' + runtimePill(String(s.runtimeKey))}>{String(s.runtimeKey || s.stepType)}</span>
        {isOrphanedBuiltin(s, registeredIds) && <span className="pill pill-danger" title="No matching in-process registration is available">Orphaned</span>}
        {s.runtimeBindingState === 'Ambiguous' && <span className="pill pill-warning" title="More than one built-in registration matched this step">Ambiguous</span>}
      </div>
    ) },
    { key: 'artifactId', label: 'Artifact', tip: 'Artifact referenced by artifact-backed runtimes', render: (s) => s.artifactId ? <CopyableId value={s.artifactId} /> : '-' },
    { key: 'maxRuntimeMs', label: 'Timeout', tip: 'Per-step timeout in milliseconds', render: (s) => s.maxRuntimeMs ? s.maxRuntimeMs + ' ms' : '-' },
    { key: 'id', label: 'Identifier', tip: 'Globally unique step id', render: (s) => <CopyableId value={s.id} /> },
    { key: 'createdUtc', label: 'Created', tip: 'When the step was created', render: (s) => formatTime(s.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (s) => (
      <RowActions
        onEdit={() => { setEditing(editStep(s)); setFormError(''); }}
        onViewJson={() => setJsonRow(s)}
        onDelete={() => setConfirmDelete(s)}
        deleteDisabled={!!s.isProtected}
      />
    )}
  ];

  const selectedRuntime = runtimeMap.get(editing?.runtimeKey);

  return (
    <div>
      <PageHeader
        title="Steps"
        subtitle={'Create reusable work units first, then wire them into data flows. ' + (data?.totalCount ?? '-') + ' persisted | ' + registered.length + ' registered in-process.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-secondary" onClick={() => { setEditing(newStep()); setFormError(''); }}>+ Config step</button>
            <button className="button-primary" onClick={() => { setSourceEditing(newSourceStep()); setSourceError(''); }}>+ Step from code</button>
          </>
        }
      />

      {registered.length > 0 && (
        <div className="card" style={{ marginBottom: 'var(--spacing-md)' }} title="Code-based steps registered at server startup">
          <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }}>Registered steps</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 'var(--spacing-sm)' }}>
            {registered.map((s) => <div key={s.identifier} className="pill pill-info" title="Registered in-process">{s.identifier}</div>)}
          </div>
        </div>
      )}

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
        onBulkDelete={tenantId ? (ids) => apiClient.bulkDeleteSteps(tenantId, ids).then(refresh) : null}
        onRowClick={(s) => { setEditing(editStep(s)); setFormError(''); }}
      />

      {editing && (
        <Modal
          open
          onClose={() => setEditing(null)}
          title={editing.id ? 'Edit step' : 'Create step'}
          headerMeta={<ModalRecordId label="Step ID" value={editing.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}
        >
          {formError && <div className="login-error">{formError}</div>}

          <div className="grid-2">
            <div className="form-row"><label title="Stable key referenced by flow transitions">Execution key</label><input value={editing.executionKey || ''} placeholder="validate_order" onChange={(e) => setEditing({ ...editing, executionKey: e.target.value })} /></div>
            <div className="form-row"><label title="Display name">Name</label><input value={editing.name || ''} placeholder="Validate Order" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          </div>
          <div className="form-row"><label title="Optional description">Description</label><input value={editing.description || ''} placeholder="Validate order payload" onChange={(e) => setEditing({ ...editing, description: e.target.value })} /></div>

          <div className="grid-2">
            <div className="form-row">
              <label title="Runtime provider">Runtime</label>
              <select value={editing.runtimeKey || REST} onChange={(e) => setRuntimeKey(e.target.value)}>
                {(runtimes.length ? runtimes : [{ runtimeKey: REST, displayName: 'External REST', availability: 'Available' }]).map((runtime) => (
                  <option key={String(runtime.runtimeKey)} value={String(runtime.runtimeKey)}>
                    {runtime.displayName || String(runtime.runtimeKey)} ({runtime.availability || 'Available'})
                  </option>
                ))}
              </select>
              {selectedRuntime && (
                <div className="form-help">
                  <span className={'pill ' + availabilityPill(selectedRuntime.availability)}>{selectedRuntime.availability}</span>
                  {' '}{selectedRuntime.securityNotes || selectedRuntime.description}
                </div>
              )}
            </div>
            <div className="form-row"><label title="Per-step runtime ceiling in milliseconds">Timeout (ms)</label><input type="number" value={editing.maxRuntimeMs || 0} placeholder="0" onChange={(e) => setEditing({ ...editing, maxRuntimeMs: parseInt(e.target.value || '0', 10) })} /></div>
          </div>

          {editing.runtimeKey === REST && <RestFields config={editing.runtimeConfig || defaultConfig(REST)} updateConfig={updateConfig} />}
          {editing.runtimeKey === ARTIFACT_PROCESS && <ArtifactProcessFields config={editing.runtimeConfig || defaultConfig(ARTIFACT_PROCESS)} updateConfig={updateConfig} artifacts={artifacts} />}
          {editing.runtimeKey === ARTIFACT_PYTHON && <ArtifactPythonFields config={editing.runtimeConfig || defaultConfig(ARTIFACT_PYTHON)} updateConfig={updateConfig} artifacts={artifacts} />}
          {editing.runtimeKey === ARTIFACT_JAVASCRIPT && <ArtifactJavaScriptFields config={editing.runtimeConfig || defaultConfig(ARTIFACT_JAVASCRIPT)} updateConfig={updateConfig} artifacts={artifacts} />}
          {editing.runtimeKey === ARTIFACT_DOTNET_PROCESS && <ArtifactDotnetProcessFields config={editing.runtimeConfig || defaultConfig(ARTIFACT_DOTNET_PROCESS)} updateConfig={updateConfig} artifacts={artifacts} />}
          {![REST, ARTIFACT_PROCESS, ARTIFACT_PYTHON, ARTIFACT_JAVASCRIPT, ARTIFACT_DOTNET_PROCESS].includes(editing.runtimeKey) && (
            <GenericRuntimeFields
              runtime={selectedRuntime}
              runtimeKey={editing.runtimeKey}
              config={editing.runtimeConfig || defaultConfig(editing.runtimeKey)}
              updateConfig={updateConfig}
              replaceConfig={(runtimeConfig) => setEditing({ ...editing, runtimeConfig })}
              setFormError={setFormError}
            />
          )}

          <div className="grid-2">
            <div className="form-row">
              <label title="Contract validation mode">Contract</label>
              <select value={editing.contractType || 'Loose'} onChange={(e) => setEditing({ ...editing, contractType: e.target.value })}>
                <option value="Loose">Loose</option>
                <option value="Schema">Schema</option>
              </select>
            </div>
            <div className="form-row">
              <label title="Inactive steps do not run normally">Active</label>
              <select value={editing.active === false ? 'false' : 'true'} onChange={(e) => setEditing({ ...editing, active: e.target.value === 'true' })}>
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </div>
          </div>

          <div className="grid-2">
            <div className="form-row">
              <label title="Optional JSON schema for input">Input schema</label>
              <textarea rows={4} value={editing.inputSchema || ''} placeholder='{"type":"object"}' onChange={(e) => setEditing({ ...editing, inputSchema: e.target.value })} style={{ fontFamily: 'var(--font-mono)' }} />
              <label className="form-help"><input type="checkbox" checked={!!editing.validateInput} onChange={(e) => setEditing({ ...editing, validateInput: e.target.checked })} style={{ width: 'auto' }} /> Validate input</label>
            </div>
            <div className="form-row">
              <label title="Optional JSON schema for output">Output schema</label>
              <textarea rows={4} value={editing.outputSchema || ''} placeholder='{"type":"object"}' onChange={(e) => setEditing({ ...editing, outputSchema: e.target.value })} style={{ fontFamily: 'var(--font-mono)' }} />
              <label className="form-help"><input type="checkbox" checked={!!editing.validateOutput} onChange={(e) => setEditing({ ...editing, validateOutput: e.target.checked })} style={{ width: 'auto' }} /> Validate output</label>
            </div>
          </div>
        </Modal>
      )}

      {sourceEditing && (
        <Modal open onClose={() => setSourceEditing(null)} title="Create step from code"
          footer={<>
            <button className="button-secondary" onClick={() => setSourceEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={saveSource} disabled={sourceSaving}>{sourceSaving ? 'Creating...' : 'Create source step'}</button>
          </>}>
          {sourceError && <div className="login-error">{sourceError}</div>}

          <div className="grid-2">
            <div className="form-row"><label title="Stable key referenced by flow transitions">Execution key</label><input value={sourceEditing.executionKey || ''} placeholder="transform_order" onChange={(e) => setSourceEditing({ ...sourceEditing, executionKey: e.target.value })} /></div>
            <div className="form-row"><label title="Display name">Name</label><input value={sourceEditing.name || ''} placeholder="Transform Order" onChange={(e) => setSourceEditing({ ...sourceEditing, name: e.target.value })} /></div>
          </div>
          <div className="form-row"><label title="Optional description">Description</label><input value={sourceEditing.description || ''} placeholder="Transforms the incoming payload" onChange={(e) => setSourceEditing({ ...sourceEditing, description: e.target.value })} /></div>

          <div className="grid-2">
            <div className="form-row">
              <label title="Source language">Language</label>
              <select value={sourceEditing.language} onChange={(e) => setSourceLanguage(e.target.value)}>
                <option value="JavaScript">JavaScript</option>
                <option value="Python">Python</option>
                <option value="CSharp">C#</option>
              </select>
            </div>
            <div className="form-row"><label title="Generated artifact package name">Artifact name</label><input value={sourceEditing.artifactName || ''} placeholder="Transform Order source package" onChange={(e) => setSourceEditing({ ...sourceEditing, artifactName: e.target.value })} /></div>
          </div>

          <div className="grid-2">
            <div className="form-row"><label title="Source file name in the generated artifact">File name</label><input value={sourceEditing.fileName || ''} onChange={(e) => setSourceEditing({ ...sourceEditing, fileName: e.target.value })} /></div>
          </div>

          <div className="grid-2">
            <div className="form-row"><label title="Manifest entrypoint name">Entrypoint</label><input value={sourceEditing.entrypoint || 'main'} onChange={(e) => setSourceEditing({ ...sourceEditing, entrypoint: e.target.value })} /></div>
            {sourceEditing.language === 'CSharp' ? (
              <div className="form-row"><label title="C# handler type implementing Tempo.Protocol.ITempoStepHandler or inheriting Tempo.Protocol.TempoStepHandlerBase">Handler type</label><input value={sourceEditing.handlerType || ''} onChange={(e) => setSourceEditing({ ...sourceEditing, handlerType: e.target.value })} /></div>
            ) : (
              <div className="form-row"><label title="Function/export called when the step runs">Function</label><input value={sourceEditing.function || 'run'} onChange={(e) => setSourceEditing({ ...sourceEditing, function: e.target.value })} /></div>
            )}
          </div>

          {(sourceEditing.language === 'Python' || sourceEditing.language === 'JavaScript') && (
            <div className="form-row"><label title="Optional module override; defaults from file name">Module</label><input value={sourceEditing.module || ''} placeholder={sourceEditing.language === 'Python' ? 'handler' : 'handler.js'} onChange={(e) => setSourceEditing({ ...sourceEditing, module: e.target.value })} /></div>
          )}

          <div className="form-row">
            <label title="Complete source file contents">Source code</label>
            <textarea rows={14} value={sourceEditing.code || ''} onChange={(e) => setSourceEditing({ ...sourceEditing, code: e.target.value })} spellCheck={false} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
            <div className="form-help">Python and JavaScript functions receive the StepRequest object. C# handlers should inherit <code>TempoStepHandlerBase</code> or implement <code>ITempoStepHandler</code>.</div>
          </div>

          <div className="grid-2">
            <div className="form-row">
              <label title="Contract validation mode">Contract</label>
              <select value={sourceEditing.contractType || 'Loose'} onChange={(e) => setSourceEditing({ ...sourceEditing, contractType: e.target.value })}>
                <option value="Loose">Loose</option>
                <option value="Schema">Schema</option>
              </select>
            </div>
            <div className="form-row"><label title="Per-step runtime ceiling in milliseconds">Timeout (ms)</label><input type="number" value={sourceEditing.maxRuntimeMs || 0} placeholder="0" onChange={(e) => setSourceEditing({ ...sourceEditing, maxRuntimeMs: parseInt(e.target.value || '0', 10) })} /></div>
          </div>

          <div className="grid-2">
            <div className="form-row">
              <label title="Optional JSON schema for input">Input schema</label>
              <textarea rows={4} value={sourceEditing.inputSchema || ''} placeholder='{"type":"object"}' onChange={(e) => setSourceEditing({ ...sourceEditing, inputSchema: e.target.value })} style={{ fontFamily: 'var(--font-mono)' }} />
              <label className="form-help"><input type="checkbox" checked={!!sourceEditing.validateInput} onChange={(e) => setSourceEditing({ ...sourceEditing, validateInput: e.target.checked })} style={{ width: 'auto' }} /> Validate input</label>
            </div>
            <div className="form-row">
              <label title="Optional JSON schema for output">Output schema</label>
              <textarea rows={4} value={sourceEditing.outputSchema || ''} placeholder='{"type":"object"}' onChange={(e) => setSourceEditing({ ...sourceEditing, outputSchema: e.target.value })} style={{ fontFamily: 'var(--font-mono)' }} />
              <label className="form-help"><input type="checkbox" checked={!!sourceEditing.validateOutput} onChange={(e) => setSourceEditing({ ...sourceEditing, validateOutput: e.target.checked })} style={{ width: 'auto' }} /> Validate output</label>
            </div>
          </div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Step JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete step"
        recordId={confirmDelete?.id || ''}
        recordIdLabel="Step ID"
        message={'Delete step "' + (confirmDelete?.name || '') + '"?'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteStep(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

function descriptorTextValue(config, prop) {
  const value = config?.[prop.name];
  if (value === undefined || value === null) return '';
  const type = (prop.type || 'string').toLowerCase();
  if (type === 'array') return Array.isArray(value) ? listText(value) : String(value);
  if (type === 'object') return typeof value === 'string' ? value : JSON.stringify(value, null, 2);
  return String(value);
}

function GenericRuntimeFields({ runtime, runtimeKey, config, updateConfig, replaceConfig, setFormError }) {
  const props = (runtime?.configProperties || []).filter((prop) => prop.name !== 'runtimeKey');
  if (props.length === 0) {
    return (
      <div className="form-row">
        <label title="Runtime configuration JSON">Runtime config JSON</label>
        <textarea rows={8} value={JSON.stringify(config || defaultConfig(runtimeKey), null, 2)} onChange={(e) => {
          try { replaceConfig(JSON.parse(e.target.value)); setFormError(''); }
          catch { setFormError('Runtime config JSON is invalid.'); }
        }} style={{ fontFamily: 'var(--font-mono)' }} />
      </div>
    );
  }

  return (
    <div className="grid-2">
      {props.map((prop) => (
        <DescriptorRuntimeField
          key={prop.name}
          prop={prop}
          config={config}
          updateConfig={updateConfig}
          setFormError={setFormError}
        />
      ))}
    </div>
  );
}

function DescriptorRuntimeField({ prop, config, updateConfig, setFormError }) {
  const type = (prop.type || 'string').toLowerCase();
  const value = config?.[prop.name];
  const label = prop.name + (prop.required ? ' *' : '');
  const title = prop.description || (prop.required ? 'Required runtime config field' : 'Runtime config field');
  const onChange = (next) => {
    setFormError('');
    updateConfig({ [prop.name]: next });
  };

  if (type === 'boolean') {
    return (
      <div className="form-row">
        <label title={title}>{label}</label>
        <select value={value === true || value === 'true' ? 'true' : 'false'} onChange={(e) => onChange(e.target.value === 'true')}>
          <option value="true">True</option>
          <option value="false">False</option>
        </select>
      </div>
    );
  }

  if (type === 'array') {
    return (
      <div className="form-row">
        <label title={title}>{label}</label>
        <textarea rows={4} value={descriptorTextValue(config, prop)} onChange={(e) => onChange(e.target.value)} />
        <div className="form-help">One item per line, or comma-separated.</div>
      </div>
    );
  }

  if (type === 'object') {
    return (
      <div className="form-row">
        <label title={title}>{label}</label>
        <textarea rows={4} value={descriptorTextValue(config, prop)} onChange={(e) => onChange(e.target.value)} style={{ fontFamily: 'var(--font-mono)' }} />
      </div>
    );
  }

  return (
    <div className="form-row">
      <label title={title}>{label}</label>
      <input
        type={type === 'integer' || type === 'number' ? 'number' : 'text'}
        value={descriptorTextValue(config, prop)}
        onChange={(e) => onChange(e.target.value)}
      />
    </div>
  );
}

function RestFields({ config, updateConfig }) {
  return (
    <>
      <div className="grid-2">
        <div className="form-row">
          <label title="HTTP method">Method</label>
          <select value={config.method || 'GET'} onChange={(e) => updateConfig({ method: e.target.value })}>
            {['GET','POST','PUT','DELETE','PATCH'].map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </div>
        <div className="form-row"><label title="HTTP request timeout">REST timeout (ms)</label><input type="number" value={config.timeoutMs || 30000} onChange={(e) => updateConfig({ timeoutMs: parseInt(e.target.value || '0', 10) })} /></div>
      </div>
      <div className="form-row">
        <label title="Outbound URL called when the step runs">URL template</label>
        <input value={config.url || ''} placeholder="https://api.example.com/orders/{orderId}" onChange={(e) => updateConfig({ url: e.target.value })} />
        <div className="form-help">Use {'{tokenName}'} placeholders to read values from StepRequest.Data.</div>
      </div>
      <div className="form-row">
        <label title="HTTP headers sent with each request">Headers JSON</label>
        <textarea rows={4} value={typeof config.headers === 'string' ? config.headers : JSON.stringify(config.headers || {}, null, 2)} onChange={(e) => updateConfig({ headers: e.target.value })} style={{ fontFamily: 'var(--font-mono)' }} />
      </div>
    </>
  );
}

function ArtifactSelector({ artifacts, value, onChange }) {
  return (
    <select value={value || ''} onChange={(e) => onChange(e.target.value)}>
      <option value="">Select artifact</option>
      {artifacts.map((artifact) => <option key={artifact.id} value={artifact.id}>{artifact.name} ({artifact.id})</option>)}
    </select>
  );
}

function ArtifactProcessFields({ config, updateConfig, artifacts }) {
  return (
    <>
      <div className="grid-2">
        <div className="form-row"><label title="Uploaded artifact package">Artifact</label><ArtifactSelector artifacts={artifacts} value={config.artifactId} onChange={(artifactId) => updateConfig({ artifactId })} /></div>
        <div className="form-row"><label title="Artifact version label or current">Artifact version</label><input value={config.artifactVersion || 'current'} onChange={(e) => updateConfig({ artifactVersion: e.target.value })} /></div>
      </div>
      <div className="form-row"><label title="Manifest entrypoint name">Entrypoint</label><input value={config.entrypoint || ''} placeholder="main" onChange={(e) => updateConfig({ entrypoint: e.target.value })} /></div>
      <div className="grid-2">
        <div className="form-row"><label title="Arguments passed to the manifest command">Arguments</label><textarea rows={4} value={listText(config.arguments)} onChange={(e) => updateConfig({ arguments: parseList(e.target.value) })} /></div>
        <div className="form-row"><label title="Allowed environment variable names, never values">Environment names</label><textarea rows={4} value={listText(config.environmentReferences)} onChange={(e) => updateConfig({ environmentReferences: parseList(e.target.value) })} /></div>
      </div>
    </>
  );
}

function ArtifactPythonFields({ config, updateConfig, artifacts }) {
  return (
    <>
      <ArtifactProcessFields config={config} updateConfig={updateConfig} artifacts={artifacts} />
      <div className="grid-2">
        <div className="form-row"><label title="Python module inside the artifact">Module</label><input value={config.module || ''} placeholder="handler" onChange={(e) => updateConfig({ module: e.target.value })} /></div>
        <div className="form-row"><label title="Function called by the SDK envelope">Function</label><input value={config.function || 'run'} placeholder="run" onChange={(e) => updateConfig({ function: e.target.value })} /></div>
      </div>
      <div className="form-row"><label title="Optional Python executable/version selector">Python version</label><input value={config.pythonVersion || ''} placeholder="3.11" onChange={(e) => updateConfig({ pythonVersion: e.target.value })} /></div>
    </>
  );
}

function ArtifactJavaScriptFields({ config, updateConfig, artifacts }) {
  return (
    <>
      <ArtifactProcessFields config={config} updateConfig={updateConfig} artifacts={artifacts} />
      <div className="grid-2">
        <div className="form-row"><label title="JavaScript module path inside the artifact">Module</label><input value={config.module || ''} placeholder="handler.js" onChange={(e) => updateConfig({ module: e.target.value })} /></div>
        <div className="form-row"><label title="Exported function called by the SDK envelope">Function</label><input value={config.function || 'run'} placeholder="run" onChange={(e) => updateConfig({ function: e.target.value })} /></div>
      </div>
    </>
  );
}

function ArtifactDotnetProcessFields({ config, updateConfig, artifacts }) {
  return (
    <>
      <ArtifactProcessFields config={config} updateConfig={updateConfig} artifacts={artifacts} />
      <div className="form-help">The selected manifest entrypoint must point to a .dll with a Tempo SDK handler type.</div>
    </>
  );
}

export default StepsView;
