import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import PageHeader from '../components/PageHeader';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';
import ConfirmModal from '../components/ConfirmModal';
import { normalizeApiError, translateLiteral } from '../utils/i18n';

const DB_TYPES = ['Sqlite', 'MySql', 'Postgresql', 'SqlServer'];

const SECTION_LABELS = {
  rest: 'REST listener',
  database: 'Database',
  logging: 'Logging',
  auth: 'Authentication',
  requestHistory: 'Request history',
  engine: 'Workflow engine',
  hydration: 'Hydration / seeding'
};

function humanizeSection(name) {
  if (!name) return '';
  const key = name.charAt(0).toLowerCase() + name.slice(1);
  return SECTION_LABELS[key] || name;
}

function deepClone(o) { return JSON.parse(JSON.stringify(o || {})); }

function SettingsView({ apiClient, principal }) {
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
  const { serverUrl, token, theme, toggleTheme, logout } = useAuth();
  const isAdmin = !!principal?.isAdmin || principal?.type === 'administrator';

  const [serverPath, setServerPath] = useState('');
  const [settings, setSettings] = useState(null);
  const [original, setOriginal] = useState(null);
  const [rawText, setRawText] = useState('');
  const [originalRaw, setOriginalRaw] = useState('');
  const [showRaw, setShowRaw] = useState(false);
  const [rebootSections, setRebootSections] = useState([]);
  const [lastRebootChanges, setLastRebootChanges] = useState(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [confirmSave, setConfirmSave] = useState(false);

  const load = useCallback(async () => {
    if (!apiClient || !isAdmin) return;
    setLoading(true);
    setError(null);
    try {
      const result = await apiClient.getSettings();
      const s = result.settings || {};
      const pretty = JSON.stringify(s, null, 2);
      setServerPath(result.path || '');
      setSettings(deepClone(s));
      setOriginal(deepClone(s));
      setRawText(pretty);
      setOriginalRaw(pretty);
      setRebootSections(result.rebootRequiredSections || []);
    } catch (err) {
      setError(normalizeApiError(err, t));
    } finally {
      setLoading(false);
    }
  }, [apiClient, isAdmin, t]);

  useEffect(() => { load(); }, [load]);

  const updateSection = (section, updater) => {
    setSettings((s) => ({ ...s, [section]: { ...(s?.[section] || {}), ...updater } }));
  };

  const dirty = showRaw
    ? rawText !== originalRaw
    : JSON.stringify(settings) !== JSON.stringify(original);

  const parsedRaw = (() => {
    try { return JSON.parse(rawText); } catch { return null; }
  })();
  const rawInvalid = showRaw && parsedRaw === null;

  const handleSave = async () => {
    const payload = showRaw ? parsedRaw : settings;
    if (!payload) return;
    setSaving(true);
    setError(null);
    try {
      const result = await apiClient.saveSettings(payload);
      setLastRebootChanges(result?.rebootRequired || []);
      await load();
    } catch (err) {
      setError(normalizeApiError(err, t));
    } finally {
      setSaving(false);
    }
  };

  const reset = () => {
    setSettings(deepClone(original));
    setRawText(originalRaw);
    setLastRebootChanges(null);
  };

  const rest = settings?.rest || settings?.Rest || {};
  const db = settings?.database || settings?.Database || {};
  const log = settings?.logging || settings?.Logging || {};
  const auth = settings?.auth || settings?.Auth || {};
  const rh = settings?.requestHistory || settings?.RequestHistory || {};
  const eng = settings?.engine || settings?.Engine || {};
  const hyd = settings?.hydration || settings?.Hydration || {};

  const restKey = settings?.rest !== undefined ? 'rest' : 'Rest';
  const dbKey = settings?.database !== undefined ? 'database' : 'Database';
  const logKey = settings?.logging !== undefined ? 'logging' : 'Logging';
  const authKey = settings?.auth !== undefined ? 'auth' : 'Auth';
  const rhKey = settings?.requestHistory !== undefined ? 'requestHistory' : 'RequestHistory';
  const engKey = settings?.engine !== undefined ? 'engine' : 'Engine';
  const hydKey = settings?.hydration !== undefined ? 'hydration' : 'Hydration';

  const numeric = (v, dflt = 0) => {
    const n = parseInt(v, 10);
    return Number.isFinite(n) ? n : dflt;
  };

  return (
    <div>
      <PageHeader
        title={tl('Settings')}
        subtitle={tl('Edit dashboard preferences and server configuration sections exposed to this session.')}
        actions={isAdmin && (
          <>
            <button className="button-secondary" onClick={load} disabled={loading} title={tl('Reload settings from the server')}>{t('common.actions.reload')}</button>
            <button className="button-secondary" onClick={reset} disabled={!dirty || loading} title={tl('Discard unsaved edits')}>{t('common.actions.reset')}</button>
            <button className="button-primary" disabled={!dirty || rawInvalid || saving} onClick={() => setConfirmSave(true)} title={tl('Persist edits to tempo.json and reload the in-memory copy')}>{saving ? tl('Saving...') : t('common.actions.saveAll')}</button>
          </>
        )}
      />

      <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
        <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('Information about the current dashboard session')}>{tl('Connection')}</div>
        <dl className="details-kv">
          <dt title={tl('Base URL the dashboard talks to')}>{tl('Server URL')}</dt><dd className="monospace">{serverUrl}</dd>
          <dt title={tl('Bearer token used for API calls (Authorization header)')}>{tl('Token')}</dt><dd><CopyableId value={token} max={40} /></dd>
          <dt title={tl('Currently signed-in identity')}>{tl('Principal')}</dt><dd>{principal?.email || principal?.id || tl('anonymous')}</dd>
          <dt title={tl('Tenant the principal is scoped to')}>{tl('Tenant')}</dt><dd><CopyableId value={principal?.tenantId} /></dd>
          <dt title={tl('Effective role of the principal')}>{tl('Role')}</dt><dd>{isAdmin ? tl('Global admin') : (principal?.isTenantAdmin ? tl('Tenant admin') : tl('User'))}</dd>
        </dl>
      </div>

      <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
        <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }}>{tl('Appearance')}</div>
        <p style={{ fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)', marginBottom: 'var(--spacing-md)' }}>
          {tl('Current theme')}: <strong>{tl(theme)}</strong>
        </p>
        <button className="button-secondary" onClick={toggleTheme} title={tl('Toggle between light and dark UI')}>
          {t('common.theme.switchMode', { theme: t(theme === 'light' ? 'common.theme.dark' : 'common.theme.light') })}
        </button>
      </div>

      {isAdmin && settings && !showRaw && (
        <>
          {error && <div className="login-error">{error}</div>}
          {lastRebootChanges && lastRebootChanges.length > 0 && (
            <div className="callout callout-warning">
              {tl('Saved. Sections requiring a reboot to take effect')}: <strong>{lastRebootChanges.map((section) => tl(humanizeSection(section))).join(', ')}</strong>
            </div>
          )}
          {lastRebootChanges && lastRebootChanges.length === 0 && (
            <div className="callout callout-success">{tl('Saved. Changes take effect immediately.')}</div>
          )}
          {rebootSections.length > 0 && (
            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', marginBottom: 'var(--spacing-sm)' }}>
              {tl('Sections marked')} <span title={tl('Server restart required to apply changes')} style={{ color: 'var(--color-warning)' }}>*</span> {tl('require a server restart to take effect')}:{' '}
              {rebootSections.map((s, i) => (
                <span key={s}>{i > 0 ? ', ' : ''}<strong>{tl(humanizeSection(s))}</strong></span>
              ))}
            </div>
          )}

          {/* REST */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('HTTP listener configuration. Changes here require a server restart')}>
              {tl('REST listener')} {rebootSections.includes('rest') && <span style={{ color: 'var(--color-warning)' }} title={tl('Restart required')}>*</span>}
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Hostname or IP the server binds to. Use 0.0.0.0 to bind all interfaces, 127.0.0.1 for localhost only')}>{tl('Hostname')}</label>
                <input value={rest.Hostname || rest.hostname || ''} placeholder="127.0.0.1" onChange={(e) => updateSection(restKey, { [Object.keys(rest).includes('hostname') ? 'hostname' : 'Hostname']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title={tl('TCP port the server listens on')}>{tl('Port')}</label>
                <input type="number" min={1} max={65535} value={rest.Port ?? rest.port ?? 8901} placeholder="8901" onChange={(e) => updateSection(restKey, { [Object.keys(rest).includes('port') ? 'port' : 'Port']: numeric(e.target.value, 8901) })} />
              </div>
            </div>
            <div className="form-row">
              <label title={tl('When checked, server expects to be terminated by a TLS proxy (or use TLS itself). Currently informational')}><input type="checkbox" checked={!!(rest.Ssl ?? rest.ssl)} onChange={(e) => updateSection(restKey, { [Object.keys(rest).includes('ssl') ? 'ssl' : 'Ssl']: e.target.checked })} style={{ width: 'auto' }} /> {tl('SSL / TLS')}</label>
            </div>
          </div>

          {/* Database */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('Persistence layer for tenants, users, flows, runs, etc. Changes require a server restart')}>
              {tl('Database')} {rebootSections.includes('database') && <span style={{ color: 'var(--color-warning)' }} title={tl('Restart required')}>*</span>}
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Database provider')}>{tl('Type')}</label>
                <select value={db.Type || db.type || 'Sqlite'} onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('type') ? 'type' : 'Type']: e.target.value })}>
                  {DB_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
              <div className="form-row">
                <label title={tl('Command timeout in seconds (1-3600)')}>{tl('Command timeout (s)')}</label>
                <input type="number" min={1} max={3600} value={db.CommandTimeoutSeconds ?? db.commandTimeoutSeconds ?? 30} placeholder="30" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('commandTimeoutSeconds') ? 'commandTimeoutSeconds' : 'CommandTimeoutSeconds']: numeric(e.target.value, 30) })} />
              </div>
            </div>
            {(db.Type || db.type || 'Sqlite') === 'Sqlite' ? (
              <div className="form-row">
                <label title={tl('Path to the SQLite database file (relative to the working directory)')}>{tl('SQLite filename')}</label>
                <input value={db.Filename || db.filename || ''} placeholder="./tempo.db" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('filename') ? 'filename' : 'Filename']: e.target.value })} />
              </div>
            ) : (
              <>
                <div className="grid-2">
                  <div className="form-row">
                    <label title={tl('Database server hostname')}>{tl('Server')}</label>
                    <input value={db.Server || db.server || ''} placeholder="db.example.com" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('server') ? 'server' : 'Server']: e.target.value })} />
                  </div>
                  <div className="form-row">
                    <label title={tl('Database server port (0 = provider default)')}>{tl('Port')}</label>
                    <input type="number" min={0} max={65535} value={db.Port ?? db.port ?? 0} placeholder="0" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('port') ? 'port' : 'Port']: numeric(e.target.value, 0) })} />
                  </div>
                </div>
                <div className="form-row">
                  <label title={tl('Logical database name on the server')}>{tl('Database name')}</label>
                  <input value={db.DatabaseName || db.databaseName || ''} placeholder="tempo" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('databaseName') ? 'databaseName' : 'DatabaseName']: e.target.value })} />
                </div>
                <div className="grid-2">
                  <div className="form-row">
                    <label title={tl('Database user with access to the configured database')}>{tl('Username')}</label>
                    <input value={db.Username || db.username || ''} placeholder="tempo_user" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('username') ? 'username' : 'Username']: e.target.value })} />
                  </div>
                  <div className="form-row">
                    <label title={tl('Password for the configured database user')}>{tl('Password')}</label>
                    <input type="password" value={db.Password || db.password || ''} placeholder="********" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('password') ? 'password' : 'Password']: e.target.value })} />
                  </div>
                </div>
              </>
            )}
          </div>

          {/* Logging */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('SyslogLogging configuration')}>{tl('Logging')}</div>
            <div className="grid-2">
              <div className="form-row"><label title={tl('Mirror log entries to stdout')}><input type="checkbox" checked={!!(log.ConsoleLogging ?? log.consoleLogging)} onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('consoleLogging') ? 'consoleLogging' : 'ConsoleLogging']: e.target.checked })} style={{ width: 'auto' }} /> {tl('Console logging')}</label></div>
              <div className="form-row"><label title={tl('Write log entries to disk in the configured directory')}><input type="checkbox" checked={!!(log.FileLogging ?? log.fileLogging)} onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('fileLogging') ? 'fileLogging' : 'FileLogging']: e.target.checked })} style={{ width: 'auto' }} /> {tl('File logging')}</label></div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Directory where log files are written; created if missing')}>{tl('Log directory')}</label>
                <input value={log.LogDirectory || log.logDirectory || ''} placeholder="./logs" onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('logDirectory') ? 'logDirectory' : 'LogDirectory']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title={tl('Base filename for log files (date-stamped suffixes are added automatically)')}>{tl('Log filename')}</label>
                <input value={log.LogFilename || log.logFilename || ''} placeholder="tempo.log" onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('logFilename') ? 'logFilename' : 'LogFilename']: e.target.value })} />
              </div>
            </div>
          </div>

          {/* Auth */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('Token issuance and signing')}>{tl('Authentication')}</div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Issuer name embedded in tokens (informational)')}>{tl('Issuer')}</label>
                <input value={auth.Issuer || auth.issuer || ''} placeholder="tempo" onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('issuer') ? 'issuer' : 'Issuer']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title={tl('Token lifetime in minutes. Range: 5 to 525600 (1 year)')}>{tl('Token expiration (min)')}</label>
                <input type="number" min={5} max={525600} value={auth.TokenExpirationMinutes ?? auth.tokenExpirationMinutes ?? 1440} placeholder="1440" onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('tokenExpirationMinutes') ? 'tokenExpirationMinutes' : 'TokenExpirationMinutes']: numeric(e.target.value, 1440) })} />
              </div>
            </div>
            <div className="form-row">
              <label title={tl('AES-256 signing key. Strings shorter than 32 bytes are SHA-256 hashed. Override via TEMPO_AUTH_SIGNING_KEY environment variable')}>{tl('Signing key')}</label>
              <input type="password" value={auth.SigningKey || auth.signingKey || ''} placeholder={tl('32+ character secret')} onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('signingKey') ? 'signingKey' : 'SigningKey']: e.target.value })} />
              <div className="form-help">{tl('Stored on disk as plaintext. Prefer the TEMPO_AUTH_SIGNING_KEY environment variable for production.')}</div>
            </div>
            <div className="form-row">
              <label title={tl('If set, the x-api-key header bypasses normal auth and authenticates as a global admin. Empty disables the bypass')}>{tl('Admin API key (bypass)')}</label>
              <input type="password" value={auth.AdminApiKey || auth.adminApiKey || ''} placeholder={tl('leave blank to disable')} onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('adminApiKey') ? 'adminApiKey' : 'AdminApiKey']: e.target.value })} />
            </div>
          </div>

          {/* Request history */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('Request capture, retention, and pruning')}>{tl('Request history')}</div>
            <div className="form-row"><label title={tl('Master toggle for request capture middleware')}><input type="checkbox" checked={!!(rh.Enabled ?? rh.enabled)} onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('enabled') ? 'enabled' : 'Enabled']: e.target.checked })} style={{ width: 'auto' }} /> {tl('Capture enabled')}</label></div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Maximum request body bytes captured before truncation. Range: 0 to 1048576')}>{tl('Max request body bytes')}</label>
                <input type="number" min={0} max={1048576} value={rh.MaxRequestBodyBytes ?? rh.maxRequestBodyBytes ?? 65536} placeholder="65536" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('maxRequestBodyBytes') ? 'maxRequestBodyBytes' : 'MaxRequestBodyBytes']: numeric(e.target.value, 65536) })} />
              </div>
              <div className="form-row">
                <label title={tl('Maximum response body bytes captured before truncation. Range: 0 to 1048576')}>{tl('Max response body bytes')}</label>
                <input type="number" min={0} max={1048576} value={rh.MaxResponseBodyBytes ?? rh.maxResponseBodyBytes ?? 65536} placeholder="65536" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('maxResponseBodyBytes') ? 'maxResponseBodyBytes' : 'MaxResponseBodyBytes']: numeric(e.target.value, 65536) })} />
              </div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl("Days to retain captured rows before they're eligible for pruning. Range: 1 to 3650")}>{tl('Retention (days)')}</label>
                <input type="number" min={1} max={3650} value={rh.RetentionDays ?? rh.retentionDays ?? 30} placeholder="30" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('retentionDays') ? 'retentionDays' : 'RetentionDays']: numeric(e.target.value, 30) })} />
              </div>
              <div className="form-row">
                <label title={tl('Minutes between retention prune passes. Range: 1 to 1440')}>{tl('Prune interval (min)')}</label>
                <input type="number" min={1} max={1440} value={rh.PruneIntervalMinutes ?? rh.pruneIntervalMinutes ?? 60} placeholder="60" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('pruneIntervalMinutes') ? 'pruneIntervalMinutes' : 'PruneIntervalMinutes']: numeric(e.target.value, 60) })} />
              </div>
            </div>
          </div>

          {/* Engine */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('Workflow execution engine and queue worker')}>{tl('Workflow engine')}</div>
            <div className="form-row"><label title={tl('When disabled, queued runs accumulate but are not dispatched')}><input type="checkbox" checked={!!(eng.QueueEnabled ?? eng.queueEnabled)} onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('queueEnabled') ? 'queueEnabled' : 'QueueEnabled']: e.target.checked })} style={{ width: 'auto' }} /> {tl('Queue worker enabled')}</label></div>
            <div className="form-row"><label title={tl('When enabled, Tempo.Server can execute runs itself as a pseudo-worker. Disable this to run Tempo.Server as control plane only')}><input type="checkbox" checked={!!(eng.ServerCanExecuteWorkload ?? eng.serverCanExecuteWorkload)} onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('serverCanExecuteWorkload') ? 'serverCanExecuteWorkload' : 'ServerCanExecuteWorkload']: e.target.checked })} style={{ width: 'auto' }} /> {tl('Server can execute workload')}</label></div>
            <div className="form-row"><label title={tl('Unsupported override that allows multiple active schedulers to dispatch simultaneously')}><input type="checkbox" checked={!!(eng.AllowDuplicateScheduler ?? eng.allowDuplicateScheduler)} onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('allowDuplicateScheduler') ? 'allowDuplicateScheduler' : 'AllowDuplicateScheduler']: e.target.checked })} style={{ width: 'auto' }} /> {tl('Allow duplicate scheduler')}</label></div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Upper bound on concurrent flow runs in this process. Range: 1 to 1024')}>{tl('Max concurrent runs')}</label>
                <input type="number" min={1} max={1024} value={eng.MaxConcurrentRuns ?? eng.maxConcurrentRuns ?? 4} placeholder="4" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('maxConcurrentRuns') ? 'maxConcurrentRuns' : 'MaxConcurrentRuns']: numeric(e.target.value, 4) })} />
              </div>
              <div className="form-row">
                <label title={tl('Polling interval (ms) when no runs are queued. Range: 100 to 60000')}>{tl('Poll interval (ms)')}</label>
                <input type="number" min={100} max={60000} value={eng.PollIntervalMs ?? eng.pollIntervalMs ?? 1000} placeholder="1000" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('pollIntervalMs') ? 'pollIntervalMs' : 'PollIntervalMs']: numeric(e.target.value, 1000) })} />
              </div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Scheduler placement strategy. LabelPinned prefers workers whose labels match the flow routing hint')}>{tl('Load-balancing strategy')}</label>
                <select value={eng.LoadBalancingStrategy ?? eng.loadBalancingStrategy ?? 'LeastLoaded'} onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('loadBalancingStrategy') ? 'loadBalancingStrategy' : 'LoadBalancingStrategy']: e.target.value })}>
                  <option value="LeastLoaded">{tl('LeastLoaded')}</option>
                  <option value="LabelPinned">{tl('LabelPinned')}</option>
                </select>
              </div>
              <div className="form-row">
                <label title={tl('How long an assignment lease remains valid before recovery can re-queue the run. Range: 1000 to 86400000')}>{tl('Lease duration (ms)')}</label>
                <input type="number" min={1000} max={86400000} value={eng.LeaseDurationMs ?? eng.leaseDurationMs ?? 300000} placeholder="300000" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('leaseDurationMs') ? 'leaseDurationMs' : 'LeaseDurationMs']: numeric(e.target.value, 300000) })} />
              </div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('How long a worker can miss heartbeats before it is treated as stale. Range: 1000 to 86400000')}>{tl('Worker heartbeat timeout (ms)')}</label>
                <input type="number" min={1000} max={86400000} value={eng.WorkerHeartbeatTimeoutMs ?? eng.workerHeartbeatTimeoutMs ?? 30000} placeholder="30000" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('workerHeartbeatTimeoutMs') ? 'workerHeartbeatTimeoutMs' : 'WorkerHeartbeatTimeoutMs']: numeric(e.target.value, 30000) })} />
              </div>
              <div className="form-row">
                <label title={tl('Maximum assignment attempts before a run is failed instead of re-queued. Range: 1 to 1024')}>{tl('Max assignment attempts')}</label>
                <input type="number" min={1} max={1024} value={eng.MaxAssignmentAttempts ?? eng.maxAssignmentAttempts ?? 3} placeholder="3" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('maxAssignmentAttempts') ? 'maxAssignmentAttempts' : 'MaxAssignmentAttempts']: numeric(e.target.value, 3) })} />
              </div>
            </div>
            <div className="form-row">
              <label title={tl('Comma-separated list of assembly paths to scan for [StepMethod] attributes at startup')}>{tl('Step assembly paths')}</label>
              <input value={eng.StepAssemblyPaths || eng.stepAssemblyPaths || ''} placeholder="./MySteps.dll,./MoreSteps.dll" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('stepAssemblyPaths') ? 'stepAssemblyPaths' : 'StepAssemblyPaths']: e.target.value })} />
            </div>
            <div className="form-help">{tl('Distributed scheduling settings in this section are reboot-required. Disable server execution to run Tempo.Server as control plane only and use Tempo.Worker nodes for actual execution.')}</div>
          </div>

          {/* Hydration */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('First-boot defaults seeded into an empty database')}>{tl('Hydration / seeding')}</div>
            <div className="form-row"><label title={tl('When checked, an empty database is seeded with default tenant/admin/user/credentials and four protected roles')}><input type="checkbox" checked={!!(hyd.SeedDefaults ?? hyd.seedDefaults)} onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('seedDefaults') ? 'seedDefaults' : 'SeedDefaults']: e.target.checked })} style={{ width: 'auto' }} /> {tl('Seed defaults on empty database')}</label></div>
            <div className="form-row">
              <label title={tl('Display name of the default tenant created on first boot')}>{tl('Default tenant name')}</label>
              <input value={hyd.DefaultTenantName || hyd.defaultTenantName || ''} placeholder={tl('Default Tenant')} onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultTenantName') ? 'defaultTenantName' : 'DefaultTenantName']: e.target.value })} />
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Email of the default global administrator')}>{tl('Default admin email')}</label>
                <input value={hyd.DefaultAdminEmail || hyd.defaultAdminEmail || ''} placeholder="admin@tempo.local" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultAdminEmail') ? 'defaultAdminEmail' : 'DefaultAdminEmail']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title={tl('Plaintext password for the default admin (SHA-256 hashed at insert time)')}>{tl('Default admin password')}</label>
                <input type="password" value={hyd.DefaultAdminPassword || hyd.defaultAdminPassword || ''} placeholder={tl('password')} onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultAdminPassword') ? 'defaultAdminPassword' : 'DefaultAdminPassword']: e.target.value })} />
              </div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title={tl('Email of the default tenant user')}>{tl('Default user email')}</label>
                <input value={hyd.DefaultUserEmail || hyd.defaultUserEmail || ''} placeholder="user@tempo.local" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultUserEmail') ? 'defaultUserEmail' : 'DefaultUserEmail']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title={tl('Plaintext password for the default user (SHA-256 hashed at insert time)')}>{tl('Default user password')}</label>
                <input type="password" value={hyd.DefaultUserPassword || hyd.defaultUserPassword || ''} placeholder={tl('password')} onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultUserPassword') ? 'defaultUserPassword' : 'DefaultUserPassword']: e.target.value })} />
              </div>
            </div>
            <div className="form-row">
              <label title={tl('Optional path to a hydration JSON file containing flows, steps, and triggers to load on startup')}>{tl('Hydration file')}</label>
              <input value={hyd.HydrationFile || hyd.hydrationFile || ''} placeholder="./hydration.json" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('hydrationFile') ? 'hydrationFile' : 'HydrationFile']: e.target.value })} />
            </div>
          </div>

          <div style={{ marginBottom: 'var(--spacing-md)', display: 'flex', gap: 'var(--spacing-sm)', alignItems: 'center' }}>
            <button className="button-secondary" onClick={() => setShowRaw(true)} title={tl('Switch to raw JSON editor for fields not surfaced above')}>{tl('Edit raw JSON...')}</button>
            <span style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>{tl('File')}: <code className="monospace">{serverPath || 'tempo.json'}</code></span>
          </div>
        </>
      )}

      {isAdmin && settings && showRaw && (
        <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
          <div className="card-header">
            <div className="card-title" title={tl('Raw JSON view of the entire settings object')}>{tl('Raw JSON')} ({serverPath || 'tempo.json'})</div>
            <div style={{ display: 'flex', gap: 'var(--spacing-sm)', alignItems: 'center' }}>
              <CopyButton value={rawText} title={tl('Copy JSON')} />
              <button className="button-secondary" onClick={() => { setShowRaw(false); load(); }} title={tl('Switch back to sectioned form view')}>{tl('Back to form')}</button>
            </div>
          </div>
          {rawInvalid && <div className="login-error">{tl('Invalid JSON - fix syntax errors before saving.')}</div>}
          <textarea
            value={rawText}
            onChange={(e) => { setRawText(e.target.value); setLastRebootChanges(null); }}
            rows={28}
            spellCheck={false}
            style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }}
            disabled={loading || saving}
          />
        </div>
      )}

      <div className="card">
        <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('End the current dashboard session and return to the login page')}>{tl('Session')}</div>
        <button className="button-danger" onClick={logout} title={tl('Sign out of the dashboard')}>{t('common.actions.signOut')}</button>
      </div>

      <ConfirmModal
        open={confirmSave}
        title={tl('Save server settings')}
        message={tl('Replace the server settings on disk and reload the in-memory copy?') + (showRaw && !parsedRaw ? ' ' + tl('(Invalid JSON; save will fail.)') : '')}
        confirmLabel={t('common.actions.save')}
        onConfirm={() => { setConfirmSave(false); handleSave(); }}
        onCancel={() => setConfirmSave(false)}
      />
    </div>
  );
}

export default SettingsView;
