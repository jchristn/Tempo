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
import { formatTime } from '../utils/formatters';

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

function formatBytes(value) {
  const n = Number(value || 0);
  if (n < 1024) return n + ' B';
  if (n < 1024 * 1024) return (n / 1024).toFixed(1) + ' KB';
  if (n < 1024 * 1024 * 1024) return (n / (1024 * 1024)).toFixed(1) + ' MB';
  return (n / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
}

function pick(obj, camel, pascal, fallback = undefined) {
  if (!obj) return fallback;
  if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
  if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
  return fallback;
}

function artifactSettings(settings) {
  const artifacts = pick(settings, 'artifacts', 'Artifacts', null);
  if (!artifacts) return null;
  return {
    maxUploadBytes: Number(pick(artifacts, 'maxUploadBytes', 'MaxUploadBytes', 0)),
    maxBytesPerTenant: Number(pick(artifacts, 'maxBytesPerTenant', 'MaxBytesPerTenant', 0)),
    maxVersionsPerArtifact: Number(pick(artifacts, 'maxVersionsPerArtifact', 'MaxVersionsPerArtifact', 0)),
    versionGracePeriodDays: Number(pick(artifacts, 'versionGracePeriodDays', 'VersionGracePeriodDays', 0))
  };
}

async function fetchAllPaged(fetchPage, pageSize = 500) {
  const items = [];
  let pageNumber = 1;
  while (pageNumber < 1000) {
    const page = await fetchPage(pageNumber, pageSize);
    const pageItems = page?.items || [];
    items.push(...pageItems);
    const total = Number(page?.totalCount ?? 0);
    if (total > 0 ? items.length >= total : pageItems.length < pageSize) break;
    pageNumber += 1;
  }
  return items;
}

async function loadArtifactUsage(apiClient, tenantId) {
  let settings = null;
  let settingsError = null;
  try {
    const result = await apiClient.getSettings();
    settings = artifactSettings(result?.settings || {});
  } catch (err) {
    settingsError = err;
  }

  const artifacts = await fetchAllPaged((pageNumber, pageSize) => apiClient.listArtifacts(tenantId, { pageNumber, pageSize, includeInactive: true }));
  const usage = { artifacts: artifacts.length, retainedBytes: 0, activeVersions: 0, retainedVersions: 0, deletedVersions: 0 };
  for (const artifact of artifacts) {
    const versions = await fetchAllPaged((pageNumber, pageSize) => apiClient.listArtifactVersions(tenantId, artifact.id, { pageNumber, pageSize, includeInactive: true }));
    for (const version of versions) {
      usage.retainedVersions += 1;
      usage.retainedBytes += Number(version.byteLength || 0);
      if (version.deletedUtc) usage.deletedVersions += 1;
      if (version.active !== false && !version.deletedUtc) usage.activeVersions += 1;
    }
  }
  return { settings, settingsError, usage };
}

async function sha256Hex(file) {
  const buffer = await file.arrayBuffer();
  const hash = await crypto.subtle.digest('SHA-256', buffer);
  return Array.from(new Uint8Array(hash)).map((b) => b.toString(16).padStart(2, '0')).join('');
}

function newImportVersion() {
  const d = new Date();
  const stamp = d.toISOString().replace(/[-:.TZ]/g, '').slice(0, 14);
  return 'import-' + stamp;
}

function ArtifactsView({ apiClient, principal }) {
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [files, setFiles] = useState([]);
  const [versions, setVersions] = useState([]);
  const [selected, setSelected] = useState(null);
  const [selectedPath, setSelectedPath] = useState('');
  const [editor, setEditor] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [filePageNumber, setFilePageNumber] = useState(1);
  const [filePageSize, setFilePageSize] = useState(25);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [loading, setLoading] = useState(false);
  const [filesLoading, setFilesLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [quotaUsage, setQuotaUsage] = useState({ loading: false, error: null, settingsError: null, settings: null, usage: null });
  const [editingArtifact, setEditingArtifact] = useState(null);
  const [importing, setImporting] = useState(null);
  const [importForm, setImportForm] = useState({ version: newImportVersion(), file: null, sha256: '' });
  const [formError, setFormError] = useState('');
  const [snapshotStatus, setSnapshotStatus] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [confirmFileDelete, setConfirmFileDelete] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [fileRefreshKey, setFileRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((k) => k + 1);
  const refreshFiles = () => setFileRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!apiClient || !tenantId) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listArtifacts(tenantId, { pageNumber, pageSize, includeInactive })
      .then((d) => {
        if (cancelled) return;
        setData(d);
        if (selected && !(d.items || []).some((a) => a.id === selected.id)) {
          setSelected(null);
          setEditor(null);
          setSelectedPath('');
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, includeInactive, refreshKey]);

  useEffect(() => {
    if (!apiClient || !tenantId) return;
    let cancelled = false;
    setQuotaUsage((current) => ({ ...current, loading: true, error: null }));
    loadArtifactUsage(apiClient, tenantId)
      .then((result) => { if (!cancelled) setQuotaUsage({ loading: false, error: null, ...result }); })
      .catch((err) => { if (!cancelled) setQuotaUsage((current) => ({ ...current, loading: false, error: normalizeError(err) })); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, refreshKey, fileRefreshKey]);

  useEffect(() => {
    if (!apiClient || !tenantId || !selected) {
      setFiles([]);
      setVersions([]);
      return;
    }
    let cancelled = false;
    setFilesLoading(true);
    Promise.all([
      apiClient.listArtifactFiles(tenantId, selected.id),
      apiClient.listArtifactVersions(tenantId, selected.id, { pageNumber: 1, pageSize: 100, includeInactive: true })
    ])
      .then(([fileRows, versionPage]) => {
        if (cancelled) return;
        const nextFiles = fileRows || [];
        setFiles(nextFiles);
        setVersions(versionPage?.items || []);
        if (selectedPath && !nextFiles.some((f) => f.path === selectedPath)) {
          setSelectedPath('');
          setEditor(null);
        }
      })
      .finally(() => { if (!cancelled) setFilesLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, selected, fileRefreshKey]);

  useEffect(() => {
    const totalPages = Math.max(1, Math.ceil(files.length / filePageSize));
    if (filePageNumber > totalPages) setFilePageNumber(totalPages);
  }, [files.length, filePageNumber, filePageSize]);

  const selectArtifact = (artifact) => {
    setSelected(artifact);
    setSelectedPath('');
    setEditor(null);
    setSnapshotStatus(null);
    setFormError('');
    setFilePageNumber(1);
  };

  const selectFile = async (path) => {
    if (!selected || !path) return;
    setFormError('');
    setSelectedPath(path);
    try {
      const file = await apiClient.readArtifactFile(tenantId, selected.id, path);
      setEditor({
        path: pick(file, 'path', 'Path', path),
        content: pick(file, 'content', 'Content', '') || '',
        contentType: pick(file, 'contentType', 'ContentType', '') || '',
        isBinary: !!pick(file, 'isBinary', 'IsBinary', false)
      });
    } catch (err) {
      setFormError(normalizeError(err));
    }
  };

  const startNewFile = () => {
    setSelectedPath('');
    setEditor({ path: '', content: '', contentType: 'text/plain', isBinary: false });
    setSnapshotStatus(null);
    setFormError('');
  };

  const saveArtifact = async () => {
    setFormError('');
    try {
      const body = {
        name: editingArtifact.name || '',
        description: editingArtifact.description || null,
        active: editingArtifact.active !== false,
        isProtected: !!editingArtifact.isProtected
      };
      if (editingArtifact.id) await apiClient.updateArtifact(tenantId, editingArtifact.id, body);
      else await apiClient.createArtifact(tenantId, { name: body.name, description: body.description });
      setEditingArtifact(null);
      refresh();
    } catch (err) {
      setFormError(normalizeError(err));
    }
  };

  const saveFile = async () => {
    if (!selected || !editor) return;
    setFormError('');
    setSaving(true);
    try {
      const response = await apiClient.saveArtifactFile(tenantId, selected.id, editor.path, {
        path: editor.path,
        content: editor.content || '',
        contentType: editor.contentType || null,
        isBinary: !!editor.isBinary
      });
      setSnapshotStatus(response);
      setSelectedPath(response.file?.path || editor.path);
      refreshFiles();
    } catch (err) {
      setFormError(normalizeError(err));
    } finally {
      setSaving(false);
    }
  };

  const deleteSelectedFile = async (path) => {
    if (!selected || !path) return;
    const response = await apiClient.deleteArtifactFile(tenantId, selected.id, path);
    setSnapshotStatus(response);
    setConfirmFileDelete(null);
    if (selectedPath === path) {
      setSelectedPath('');
      setEditor(null);
    }
    refreshFiles();
  };

  const openImport = (artifact) => {
    setImporting(artifact);
    setImportForm({ version: newImportVersion(), file: null, sha256: '' });
    setFormError('');
  };

  const importZip = async () => {
    setFormError('');
    try {
      if (!importForm.file) throw new Error('ZIP file is required.');
      const sha = importForm.sha256 || await sha256Hex(importForm.file);
      await apiClient.uploadArtifactVersion(tenantId, importing.id, importForm.file, {
        version: importForm.version.trim() || newImportVersion(),
        sha256: sha,
        originalFileName: importForm.file.name,
        contentType: importForm.file.type || 'application/zip'
      });
      setImporting(null);
      selectArtifact(importing);
      refreshFiles();
      refresh();
    } catch (err) {
      setFormError(normalizeError(err));
    }
  };

  const downloadCurrent = async () => {
    if (!selected) return;
    const blob = await apiClient.downloadArtifactVersion(tenantId, selected.id, 'current');
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = selected.id + '-current.zip';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  };

  const artifactColumns = [
    { key: 'name', label: 'Name', tip: 'Tenant-scoped artifact name' },
    { key: 'id', label: 'Identifier', tip: 'Artifact identifier', render: (a) => <CopyableId value={a.id} /> },
    { key: 'active', label: 'Active', tip: 'Inactive artifacts are hidden by default', render: (a) => a.active ? 'Yes' : 'No' },
    { key: 'updated', label: 'Updated', tip: 'Last update time', render: (a) => formatTime(a.lastUpdateUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (a) => (
      <RowActions
        onEdit={() => { setEditingArtifact(a); setFormError(''); }}
        onView={() => selectArtifact(a)}
        onViewJson={() => setJsonRow(a)}
        onDelete={() => setConfirmDelete(a)}
        deleteDisabled={!!a.isProtected}
        extra={[{ label: 'Import ZIP', onClick: () => openImport(a) }]}
      />
    )}
  ];

  const fileColumns = [
    { key: 'path', label: 'Path', tip: 'Artifact-relative file path', cellClass: 'artifact-file-path-cell', render: (f) => <code className="monospace" title={f.path}>{f.path}</code> },
    { key: 'byteLength', label: 'Size', tip: 'Decoded byte length', render: (f) => formatBytes(f.byteLength) },
    { key: 'contentType', label: 'Type', tip: 'Content type', render: (f) => f.contentType || '-' },
    { key: 'isBinary', label: 'Binary', tip: 'Whether content is stored as base64', render: (f) => f.isBinary ? 'Yes' : 'No' },
    { key: 'lastUpdateUtc', label: 'Updated', tip: 'Last file update', render: (f) => formatTime(f.lastUpdateUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (f) => (
      <RowActions
        onView={() => selectFile(f.path)}
        onViewJson={() => setJsonRow(f)}
        onDelete={() => setConfirmFileDelete(f)}
      />
    )}
  ];

  const currentVersion = versions.find((v) => v.version === 'current');
  const fileTotalRecords = files.length;
  const safeFilePageNumber = Math.min(filePageNumber, Math.max(1, Math.ceil(fileTotalRecords / filePageSize)));
  const pagedFiles = files.slice((safeFilePageNumber - 1) * filePageSize, safeFilePageNumber * filePageSize);

  return (
    <div>
      <PageHeader
        title="Artifacts"
        subtitle={'Edit artifact files used by artifact-backed steps. Tempo packages the current files into a runnable current snapshot. ' + (data?.totalCount ?? '-') + ' artifacts total.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => { setEditingArtifact({ name: '', description: '', active: true }); setFormError(''); }}>+ New artifact</button>
          </>
        }
      />

      <ArtifactQuotaTiles quotaUsage={quotaUsage} />

      <TableFrame
        columns={artifactColumns}
        items={data?.items || []}
        totalRecords={data?.totalCount ?? 0}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(s) => { setPageSize(s); setPageNumber(1); }}
        onRefresh={refresh}
        loading={loading}
        emptyMessage="No artifacts found."
        onRowClick={selectArtifact}
        leftSlot={
          <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }} title="Show inactive artifacts">
            <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} style={{ width: 'auto' }} />
            Include inactive
          </label>
        }
      />

      {selected && (
        <div style={{ marginTop: 'var(--spacing-lg)' }}>
          <PageHeader
            title={selected.name + ' files'}
            subtitle={'Edit files for ' + selected.id + '. The current snapshot is what artifact-backed steps run.'}
            actions={
              <>
                <button className="button-secondary" onClick={() => openImport(selected)}>Import ZIP</button>
                <button className="button-secondary" onClick={downloadCurrent} disabled={!currentVersion}>Download current</button>
                <button className="button-primary" onClick={startNewFile}>+ New file</button>
              </>
            }
          />

          {snapshotStatus?.snapshotUpdated && (
            <div className="callout callout-success">Current snapshot updated: <CopyableId value={snapshotStatus.artifactVersion?.sha256} max={18} />.</div>
          )}
          {snapshotStatus?.snapshotError && (
            <div className="callout callout-warning">File saved, but the current snapshot was not rebuilt: {snapshotStatus.snapshotError}</div>
          )}
          {formError && <div className="login-error">{formError}</div>}

          <div className="artifact-file-workspace">
            <div className="artifact-file-list-column">
              <TableFrame
                columns={fileColumns}
                items={pagedFiles}
                totalRecords={fileTotalRecords}
                pageNumber={safeFilePageNumber}
                pageSize={filePageSize}
                onPageChange={setFilePageNumber}
                onPageSizeChange={(s) => { setFilePageSize(s); setFilePageNumber(1); }}
                onRefresh={refreshFiles}
                loading={filesLoading}
                emptyMessage="No files in this artifact."
                onRowClick={(file) => selectFile(file.path)}
              />
            </div>

            <div className="artifact-editor-panel">
              <div className="artifact-editor-title">File editor</div>
              {!editor && <div className="empty-state">Select a file or create a new one.</div>}
              {editor && (
                <>
                  <div className="form-grid two">
                    <div className="form-row"><label>Path</label><input value={editor.path} placeholder="handler.js" onChange={(e) => setEditor({ ...editor, path: e.target.value })} /></div>
                    <div className="form-row"><label>Content type</label><input value={editor.contentType || ''} placeholder="text/plain" onChange={(e) => setEditor({ ...editor, contentType: e.target.value })} /></div>
                  </div>
                  <div className="form-row"><label><input type="checkbox" checked={!!editor.isBinary} onChange={(e) => setEditor({ ...editor, isBinary: e.target.checked })} style={{ width: 'auto' }} /> Base64 binary content</label></div>
                  <div className="form-row">
                    <label>{editor.isBinary ? 'Base64 content' : 'Text content'}</label>
                    <textarea rows={22} spellCheck={false} value={editor.content || ''} onChange={(e) => setEditor({ ...editor, content: e.target.value })} style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8125rem' }} />
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--spacing-sm)' }}>
                    {selectedPath && <button className="button-secondary" onClick={() => setConfirmFileDelete({ path: selectedPath })}>Delete file</button>}
                    <button className="button-primary" onClick={saveFile} disabled={saving}>{saving ? 'Saving...' : 'Save file'}</button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {editingArtifact && (
        <Modal
          open
          size="small"
          onClose={() => setEditingArtifact(null)}
          title={editingArtifact.id ? 'Edit artifact' : 'Create artifact'}
          headerMeta={<ModalRecordId label="Artifact ID" value={editingArtifact.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setEditingArtifact(null)}>Cancel</button>
            <button className="button-primary" onClick={saveArtifact}>Save</button>
          </>}
        >
          {formError && <div className="login-error">{formError}</div>}
          <div className="form-row"><label>Name</label><input value={editingArtifact.name || ''} placeholder="python-order-enricher" onChange={(e) => setEditingArtifact({ ...editingArtifact, name: e.target.value })} /></div>
          <div className="form-row"><label>Description</label><textarea rows={3} value={editingArtifact.description || ''} onChange={(e) => setEditingArtifact({ ...editingArtifact, description: e.target.value })} /></div>
          {editingArtifact.id && <div className="form-row"><label><input type="checkbox" checked={editingArtifact.active !== false} onChange={(e) => setEditingArtifact({ ...editingArtifact, active: e.target.checked })} style={{ width: 'auto' }} /> Active</label></div>}
        </Modal>
      )}

      {importing && (
        <Modal
          open
          size="small"
          onClose={() => setImporting(null)}
          title={'Import ZIP into ' + importing.name}
          headerMeta={<ModalRecordId label="Artifact ID" value={importing.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setImporting(null)}>Cancel</button>
            <button className="button-primary" onClick={importZip}>Import</button>
          </>}
        >
          {formError && <div className="login-error">{formError}</div>}
          <div className="callout callout-warning">Importing replaces the editable files for this artifact and rebuilds the current snapshot.</div>
          <div className="form-row"><label>Archive file</label><input type="file" accept=".zip,application/zip" onChange={async (e) => {
            const file = e.target.files && e.target.files[0];
            if (!file) return setImportForm({ ...importForm, file: null, sha256: '' });
            setImportForm({ ...importForm, file, sha256: '' });
            try {
              const sha = await sha256Hex(file);
              setImportForm((current) => current.file === file ? { ...current, sha256: sha } : current);
            } catch { }
          }} /></div>
          <div className="form-row"><label>Import version label</label><input value={importForm.version} onChange={(e) => setImportForm({ ...importForm, version: e.target.value })} /></div>
          {importForm.file && <div className="form-row"><label>SHA-256</label><input value={importForm.sha256 || 'Calculating...'} readOnly /></div>}
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Artifact JSON" />
      <ConfirmModal
        open={!!confirmDelete}
        danger
        title="Delete artifact"
        recordId={confirmDelete?.id || ''}
        recordIdLabel="Artifact ID"
        message={'Delete artifact "' + (confirmDelete?.name || '') + '"? Artifacts referenced by steps cannot be deleted.'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteArtifact(tenantId, confirmDelete.id); setConfirmDelete(null); setSelected(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)}
      />
      <ConfirmModal
        open={!!confirmFileDelete}
        danger
        title="Delete artifact file"
        message={'Delete file "' + (confirmFileDelete?.path || '') + '"? The current snapshot will be rebuilt if the remaining files are runnable.'}
        confirmLabel="Delete"
        onConfirm={() => deleteSelectedFile(confirmFileDelete.path)}
        onCancel={() => setConfirmFileDelete(null)}
      />
    </div>
  );
}

function ArtifactQuotaTiles({ quotaUsage }) {
  const usage = quotaUsage.usage || {};
  const settings = quotaUsage.settings || null;
  const retainedBytes = Number(usage.retainedBytes || 0);
  const tenantQuota = Number(settings?.maxBytesPerTenant || 0);
  const quotaClass = tenantQuota > 0 && retainedBytes >= tenantQuota
    ? 'danger'
    : tenantQuota > 0 && retainedBytes >= tenantQuota * 0.8
      ? 'warning'
      : '';

  return (
    <>
      <div className="summary-tiles">
        <div className={'summary-tile ' + quotaClass}>
          <div className="label">Retained bytes</div>
          <div className="value">{quotaUsage.loading ? '-' : formatBytes(retainedBytes)}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Tenant quota</div>
          <div className="value">{quotaUsage.loading ? '-' : tenantQuota > 0 ? formatBytes(tenantQuota) : 'Unavailable'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Max upload</div>
          <div className="value">{quotaUsage.loading ? '-' : settings?.maxUploadBytes ? formatBytes(settings.maxUploadBytes) : 'Unavailable'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Snapshots</div>
          <div className="value">{quotaUsage.loading ? '-' : Number(usage.activeVersions || 0).toLocaleString() + ' active'}</div>
        </div>
      </div>
      {!quotaUsage.loading && quotaUsage.settingsError && (
        <div className="callout callout-warning" style={{ marginTop: 'calc(var(--spacing-md) * -1)' }}>
          Artifact settings are not visible for this session. Usage is estimated from artifact version records.
        </div>
      )}
      {!quotaUsage.loading && quotaUsage.error && <div className="login-error">{quotaUsage.error}</div>}
    </>
  );
}

export default ArtifactsView;
