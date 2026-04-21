import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import PageHeader from '../components/PageHeader';
import CopyableId from '../components/CopyableId';
import CopyButton from '../components/CopyButton';
import ConfirmModal from '../components/ConfirmModal';

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
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [apiClient, isAdmin]);

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
      setError(err.message);
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
        title="Settings"
        subtitle="Edit dashboard preferences and server configuration sections exposed to this session."
        actions={isAdmin && (
          <>
            <button className="button-secondary" onClick={load} disabled={loading} title="Reload settings from the server">Reload</button>
            <button className="button-secondary" onClick={reset} disabled={!dirty || loading} title="Discard unsaved edits">Reset</button>
            <button className="button-primary" disabled={!dirty || rawInvalid || saving} onClick={() => setConfirmSave(true)} title="Persist edits to tempo.json and reload the in-memory copy">{saving ? 'Saving…' : 'Save all'}</button>
          </>
        )}
      />

      <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
        <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="Information about the current dashboard session">Connection</div>
        <dl className="details-kv">
          <dt title="Base URL the dashboard talks to">Server URL</dt><dd className="monospace">{serverUrl}</dd>
          <dt title="Bearer token used for API calls (Authorization header)">Token</dt><dd><CopyableId value={token} max={40} /></dd>
          <dt title="Currently signed-in identity">Principal</dt><dd>{principal?.email || principal?.id || 'anonymous'}</dd>
          <dt title="Tenant the principal is scoped to">Tenant</dt><dd><CopyableId value={principal?.tenantId} /></dd>
          <dt title="Effective role of the principal">Role</dt><dd>{isAdmin ? 'Global admin' : (principal?.isTenantAdmin ? 'Tenant admin' : 'User')}</dd>
        </dl>
      </div>

      <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
        <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }}>Appearance</div>
        <p style={{ fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)', marginBottom: 'var(--spacing-md)' }}>
          Current theme: <strong>{theme}</strong>
        </p>
        <button className="button-secondary" onClick={toggleTheme} title="Toggle between light and dark UI">Switch to {theme === 'light' ? 'dark' : 'light'} mode</button>
      </div>

      {isAdmin && settings && !showRaw && (
        <>
          {error && <div className="login-error">{error}</div>}
          {lastRebootChanges && lastRebootChanges.length > 0 && (
            <div className="callout callout-warning">
              Saved. Sections requiring a reboot to take effect: <strong>{lastRebootChanges.map(humanizeSection).join(', ')}</strong>
            </div>
          )}
          {lastRebootChanges && lastRebootChanges.length === 0 && (
            <div className="callout callout-success">Saved. Changes take effect immediately.</div>
          )}
          {rebootSections.length > 0 && (
            <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', marginBottom: 'var(--spacing-sm)' }}>
              Sections marked <span title="Server restart required to apply changes" style={{ color: 'var(--color-warning)' }}>⟳</span> require a server restart to take effect:{' '}
              {rebootSections.map((s, i) => (
                <span key={s}>{i > 0 ? ', ' : ''}<strong>{humanizeSection(s)}</strong></span>
              ))}
            </div>
          )}

          {/* REST */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="HTTP listener configuration. Changes here require a server restart">
              REST listener {rebootSections.includes('rest') && <span style={{ color: 'var(--color-warning)' }} title="Restart required">⟳</span>}
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Hostname or IP the server binds to. Use 0.0.0.0 to bind all interfaces, 127.0.0.1 for localhost only">Hostname</label>
                <input value={rest.Hostname || rest.hostname || ''} placeholder="127.0.0.1" onChange={(e) => updateSection(restKey, { [Object.keys(rest).includes('hostname') ? 'hostname' : 'Hostname']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title="TCP port the server listens on">Port</label>
                <input type="number" min={1} max={65535} value={rest.Port ?? rest.port ?? 8901} placeholder="8901" onChange={(e) => updateSection(restKey, { [Object.keys(rest).includes('port') ? 'port' : 'Port']: numeric(e.target.value, 8901) })} />
              </div>
            </div>
            <div className="form-row">
              <label title="When checked, server expects to be terminated by a TLS proxy (or use TLS itself). Currently informational"><input type="checkbox" checked={!!(rest.Ssl ?? rest.ssl)} onChange={(e) => updateSection(restKey, { [Object.keys(rest).includes('ssl') ? 'ssl' : 'Ssl']: e.target.checked })} style={{ width: 'auto' }} /> SSL / TLS</label>
            </div>
          </div>

          {/* Database */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="Persistence layer for tenants, users, flows, runs, etc. Changes require a server restart">
              Database {rebootSections.includes('database') && <span style={{ color: 'var(--color-warning)' }} title="Restart required">⟳</span>}
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Database provider">Type</label>
                <select value={db.Type || db.type || 'Sqlite'} onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('type') ? 'type' : 'Type']: e.target.value })}>
                  {DB_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
              <div className="form-row">
                <label title="Command timeout in seconds (1-3600)">Command timeout (s)</label>
                <input type="number" min={1} max={3600} value={db.CommandTimeoutSeconds ?? db.commandTimeoutSeconds ?? 30} placeholder="30" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('commandTimeoutSeconds') ? 'commandTimeoutSeconds' : 'CommandTimeoutSeconds']: numeric(e.target.value, 30) })} />
              </div>
            </div>
            {(db.Type || db.type || 'Sqlite') === 'Sqlite' ? (
              <div className="form-row">
                <label title="Path to the SQLite database file (relative to the working directory)">SQLite filename</label>
                <input value={db.Filename || db.filename || ''} placeholder="./tempo.db" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('filename') ? 'filename' : 'Filename']: e.target.value })} />
              </div>
            ) : (
              <>
                <div className="grid-2">
                  <div className="form-row">
                    <label title="Database server hostname">Server</label>
                    <input value={db.Server || db.server || ''} placeholder="db.example.com" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('server') ? 'server' : 'Server']: e.target.value })} />
                  </div>
                  <div className="form-row">
                    <label title="Database server port (0 = provider default)">Port</label>
                    <input type="number" min={0} max={65535} value={db.Port ?? db.port ?? 0} placeholder="0" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('port') ? 'port' : 'Port']: numeric(e.target.value, 0) })} />
                  </div>
                </div>
                <div className="form-row">
                  <label title="Logical database name on the server">Database name</label>
                  <input value={db.DatabaseName || db.databaseName || ''} placeholder="tempo" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('databaseName') ? 'databaseName' : 'DatabaseName']: e.target.value })} />
                </div>
                <div className="grid-2">
                  <div className="form-row">
                    <label title="Database user with access to the configured database">Username</label>
                    <input value={db.Username || db.username || ''} placeholder="tempo_user" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('username') ? 'username' : 'Username']: e.target.value })} />
                  </div>
                  <div className="form-row">
                    <label title="Password for the configured database user">Password</label>
                    <input type="password" value={db.Password || db.password || ''} placeholder="••••••••" onChange={(e) => updateSection(dbKey, { [Object.keys(db).includes('password') ? 'password' : 'Password']: e.target.value })} />
                  </div>
                </div>
              </>
            )}
          </div>

          {/* Logging */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="SyslogLogging configuration">Logging</div>
            <div className="grid-2">
              <div className="form-row"><label title="Mirror log entries to stdout"><input type="checkbox" checked={!!(log.ConsoleLogging ?? log.consoleLogging)} onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('consoleLogging') ? 'consoleLogging' : 'ConsoleLogging']: e.target.checked })} style={{ width: 'auto' }} /> Console logging</label></div>
              <div className="form-row"><label title="Write log entries to disk in the configured directory"><input type="checkbox" checked={!!(log.FileLogging ?? log.fileLogging)} onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('fileLogging') ? 'fileLogging' : 'FileLogging']: e.target.checked })} style={{ width: 'auto' }} /> File logging</label></div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Directory where log files are written; created if missing">Log directory</label>
                <input value={log.LogDirectory || log.logDirectory || ''} placeholder="./logs" onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('logDirectory') ? 'logDirectory' : 'LogDirectory']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title="Base filename for log files (date-stamped suffixes are added automatically)">Log filename</label>
                <input value={log.LogFilename || log.logFilename || ''} placeholder="tempo.log" onChange={(e) => updateSection(logKey, { [Object.keys(log).includes('logFilename') ? 'logFilename' : 'LogFilename']: e.target.value })} />
              </div>
            </div>
          </div>

          {/* Auth */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="Token issuance and signing">Authentication</div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Issuer name embedded in tokens (informational)">Issuer</label>
                <input value={auth.Issuer || auth.issuer || ''} placeholder="tempo" onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('issuer') ? 'issuer' : 'Issuer']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title="Token lifetime in minutes. Range: 5 to 525600 (1 year)">Token expiration (min)</label>
                <input type="number" min={5} max={525600} value={auth.TokenExpirationMinutes ?? auth.tokenExpirationMinutes ?? 1440} placeholder="1440" onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('tokenExpirationMinutes') ? 'tokenExpirationMinutes' : 'TokenExpirationMinutes']: numeric(e.target.value, 1440) })} />
              </div>
            </div>
            <div className="form-row">
              <label title="AES-256 signing key. Strings shorter than 32 bytes are SHA-256 hashed. Override via TEMPO_AUTH_SIGNING_KEY environment variable">Signing key</label>
              <input type="password" value={auth.SigningKey || auth.signingKey || ''} placeholder="32+ character secret" onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('signingKey') ? 'signingKey' : 'SigningKey']: e.target.value })} />
              <div className="form-help">Stored on disk as plaintext. Prefer the TEMPO_AUTH_SIGNING_KEY environment variable for production.</div>
            </div>
            <div className="form-row">
              <label title="If set, the x-api-key header bypasses normal auth and authenticates as a global admin. Empty disables the bypass">Admin API key (bypass)</label>
              <input type="password" value={auth.AdminApiKey || auth.adminApiKey || ''} placeholder="leave blank to disable" onChange={(e) => updateSection(authKey, { [Object.keys(auth).includes('adminApiKey') ? 'adminApiKey' : 'AdminApiKey']: e.target.value })} />
            </div>
          </div>

          {/* Request history */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="Request capture, retention, and pruning">Request history</div>
            <div className="form-row"><label title="Master toggle for request capture middleware"><input type="checkbox" checked={!!(rh.Enabled ?? rh.enabled)} onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('enabled') ? 'enabled' : 'Enabled']: e.target.checked })} style={{ width: 'auto' }} /> Capture enabled</label></div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Maximum request body bytes captured before truncation. Range: 0 to 1048576">Max request body bytes</label>
                <input type="number" min={0} max={1048576} value={rh.MaxRequestBodyBytes ?? rh.maxRequestBodyBytes ?? 65536} placeholder="65536" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('maxRequestBodyBytes') ? 'maxRequestBodyBytes' : 'MaxRequestBodyBytes']: numeric(e.target.value, 65536) })} />
              </div>
              <div className="form-row">
                <label title="Maximum response body bytes captured before truncation. Range: 0 to 1048576">Max response body bytes</label>
                <input type="number" min={0} max={1048576} value={rh.MaxResponseBodyBytes ?? rh.maxResponseBodyBytes ?? 65536} placeholder="65536" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('maxResponseBodyBytes') ? 'maxResponseBodyBytes' : 'MaxResponseBodyBytes']: numeric(e.target.value, 65536) })} />
              </div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Days to retain captured rows before they're eligible for pruning. Range: 1 to 3650">Retention (days)</label>
                <input type="number" min={1} max={3650} value={rh.RetentionDays ?? rh.retentionDays ?? 30} placeholder="30" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('retentionDays') ? 'retentionDays' : 'RetentionDays']: numeric(e.target.value, 30) })} />
              </div>
              <div className="form-row">
                <label title="Minutes between retention prune passes. Range: 1 to 1440">Prune interval (min)</label>
                <input type="number" min={1} max={1440} value={rh.PruneIntervalMinutes ?? rh.pruneIntervalMinutes ?? 60} placeholder="60" onChange={(e) => updateSection(rhKey, { [Object.keys(rh).includes('pruneIntervalMinutes') ? 'pruneIntervalMinutes' : 'PruneIntervalMinutes']: numeric(e.target.value, 60) })} />
              </div>
            </div>
          </div>

          {/* Engine */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="Workflow execution engine and queue worker">Workflow engine</div>
            <div className="form-row"><label title="When disabled, queued runs accumulate but are not dispatched"><input type="checkbox" checked={!!(eng.QueueEnabled ?? eng.queueEnabled)} onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('queueEnabled') ? 'queueEnabled' : 'QueueEnabled']: e.target.checked })} style={{ width: 'auto' }} /> Queue worker enabled</label></div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Upper bound on concurrent flow runs in this process. Range: 1 to 1024">Max concurrent runs</label>
                <input type="number" min={1} max={1024} value={eng.MaxConcurrentRuns ?? eng.maxConcurrentRuns ?? 4} placeholder="4" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('maxConcurrentRuns') ? 'maxConcurrentRuns' : 'MaxConcurrentRuns']: numeric(e.target.value, 4) })} />
              </div>
              <div className="form-row">
                <label title="Polling interval (ms) when no runs are queued. Range: 100 to 60000">Poll interval (ms)</label>
                <input type="number" min={100} max={60000} value={eng.PollIntervalMs ?? eng.pollIntervalMs ?? 1000} placeholder="1000" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('pollIntervalMs') ? 'pollIntervalMs' : 'PollIntervalMs']: numeric(e.target.value, 1000) })} />
              </div>
            </div>
            <div className="form-row">
              <label title="Comma-separated list of assembly paths to scan for [StepMethod] attributes at startup">Step assembly paths</label>
              <input value={eng.StepAssemblyPaths || eng.stepAssemblyPaths || ''} placeholder="./MySteps.dll,./MoreSteps.dll" onChange={(e) => updateSection(engKey, { [Object.keys(eng).includes('stepAssemblyPaths') ? 'stepAssemblyPaths' : 'StepAssemblyPaths']: e.target.value })} />
            </div>
          </div>

          {/* Hydration */}
          <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
            <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="First-boot defaults seeded into an empty database">Hydration / seeding</div>
            <div className="form-row"><label title="When checked, an empty database is seeded with default tenant/admin/user/credentials and four protected roles"><input type="checkbox" checked={!!(hyd.SeedDefaults ?? hyd.seedDefaults)} onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('seedDefaults') ? 'seedDefaults' : 'SeedDefaults']: e.target.checked })} style={{ width: 'auto' }} /> Seed defaults on empty database</label></div>
            <div className="form-row">
              <label title="Display name of the default tenant created on first boot">Default tenant name</label>
              <input value={hyd.DefaultTenantName || hyd.defaultTenantName || ''} placeholder="Default Tenant" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultTenantName') ? 'defaultTenantName' : 'DefaultTenantName']: e.target.value })} />
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Email of the default global administrator">Default admin email</label>
                <input value={hyd.DefaultAdminEmail || hyd.defaultAdminEmail || ''} placeholder="admin@tempo.local" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultAdminEmail') ? 'defaultAdminEmail' : 'DefaultAdminEmail']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title="Plaintext password for the default admin (SHA-256 hashed at insert time)">Default admin password</label>
                <input type="password" value={hyd.DefaultAdminPassword || hyd.defaultAdminPassword || ''} placeholder="password" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultAdminPassword') ? 'defaultAdminPassword' : 'DefaultAdminPassword']: e.target.value })} />
              </div>
            </div>
            <div className="grid-2">
              <div className="form-row">
                <label title="Email of the default tenant user">Default user email</label>
                <input value={hyd.DefaultUserEmail || hyd.defaultUserEmail || ''} placeholder="user@tempo.local" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultUserEmail') ? 'defaultUserEmail' : 'DefaultUserEmail']: e.target.value })} />
              </div>
              <div className="form-row">
                <label title="Plaintext password for the default user (SHA-256 hashed at insert time)">Default user password</label>
                <input type="password" value={hyd.DefaultUserPassword || hyd.defaultUserPassword || ''} placeholder="password" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('defaultUserPassword') ? 'defaultUserPassword' : 'DefaultUserPassword']: e.target.value })} />
              </div>
            </div>
            <div className="form-row">
              <label title="Optional path to a hydration JSON file containing flows, steps, and triggers to load on startup">Hydration file</label>
              <input value={hyd.HydrationFile || hyd.hydrationFile || ''} placeholder="./hydration.json" onChange={(e) => updateSection(hydKey, { [Object.keys(hyd).includes('hydrationFile') ? 'hydrationFile' : 'HydrationFile']: e.target.value })} />
            </div>
          </div>

          <div style={{ marginBottom: 'var(--spacing-md)', display: 'flex', gap: 'var(--spacing-sm)', alignItems: 'center' }}>
            <button className="button-secondary" onClick={() => setShowRaw(true)} title="Switch to raw JSON editor for fields not surfaced above">Edit raw JSON…</button>
            <span style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>File: <code className="monospace">{serverPath || 'tempo.json'}</code></span>
          </div>
        </>
      )}

      {isAdmin && settings && showRaw && (
        <div className="card" style={{ marginBottom: 'var(--spacing-md)' }}>
          <div className="card-header">
            <div className="card-title" title="Raw JSON view of the entire settings object">Raw JSON ({serverPath || 'tempo.json'})</div>
            <div style={{ display: 'flex', gap: 'var(--spacing-sm)', alignItems: 'center' }}>
              <CopyButton value={rawText} title="Copy JSON" />
              <button className="button-secondary" onClick={() => { setShowRaw(false); load(); }} title="Switch back to sectioned form view">Back to form</button>
            </div>
          </div>
          {rawInvalid && <div className="login-error">Invalid JSON — fix syntax errors before saving.</div>}
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
        <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title="End the current dashboard session and return to the login page">Session</div>
        <button className="button-danger" onClick={logout} title="Sign out of the dashboard">Sign out</button>
      </div>

      <ConfirmModal
        open={confirmSave}
        title="Save server settings"
        message={'Replace the server settings on disk and reload the in-memory copy?' + (showRaw && !parsedRaw ? ' (Invalid JSON; save will fail.)' : '')}
        confirmLabel="Save"
        onConfirm={() => { setConfirmSave(false); handleSave(); }}
        onCancel={() => setConfirmSave(false)}
      />
    </div>
  );
}

export default SettingsView;
