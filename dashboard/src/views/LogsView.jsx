import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation, useNavigate } from 'react-router-dom';
import ConfirmModal from '../components/ConfirmModal';
import CopyButton from '../components/CopyButton';
import CopyableId from '../components/CopyableId';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import { formatBytes, formatTime } from '../utils/formatters';
import { normalizeApiError, translateLiteral } from '../utils/i18n';

function stateLabel(source, t) {
  if (!source) return '-';
  if (source.sourceKind === 'server') return translateLiteral(t, 'Online');
  return source.state ? translateLiteral(t, source.state) : (source.active ? translateLiteral(t, 'Online') : translateLiteral(t, 'Offline'));
}

function sourceKey(sourceKind, sourceId) {
  return (sourceKind || '') + ':' + (sourceId || '');
}

function parseNumber(value, fallback) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function updateSearch(navigate, location, updates) {
  const params = new URLSearchParams(location.search);
  for (const [key, value] of Object.entries(updates)) {
    if (value === undefined || value === null || value === '') params.delete(key);
    else params.set(key, value);
  }
  navigate('/dashboard/logs' + (params.toString() ? '?' + params.toString() : ''), { replace: true });
}

function LogsView({ apiClient, principal }) {
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
  const navigate = useNavigate();
  const location = useLocation();
  const search = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const isAdmin = !!principal?.isAdmin || principal?.type === 'administrator';

  const selectedSourceKind = search.get('sourceKind') || 'server';
  const selectedSourceId = search.get('sourceId') || 'server';
  const selectedPath = search.get('path') || '';

  const [sources, setSources] = useState([]);
  const [files, setFiles] = useState([]);
  const [fileData, setFileData] = useState(null);
  const [loadingSources, setLoadingSources] = useState(false);
  const [loadingFiles, setLoadingFiles] = useState(false);
  const [loadingContent, setLoadingContent] = useState(false);
  const [error, setError] = useState('');
  const [tailLines, setTailLines] = useState('200');
  const [maxBytes, setMaxBytes] = useState('131072');
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const refresh = () => setRefreshKey((current) => current + 1);

  useEffect(() => {
    if (!autoRefresh) return undefined;
    const timer = window.setInterval(() => setRefreshKey((current) => current + 1), 5000);
    return () => window.clearInterval(timer);
  }, [autoRefresh]);

  useEffect(() => {
    if (!apiClient || !isAdmin) return;
    let cancelled = false;
    setLoadingSources(true);
    setError('');
    apiClient.listLogSources()
      .then((rows) => {
        if (cancelled) return;
        const nextSources = rows || [];
        setSources(nextSources);

        const selected = nextSources.find((source) => source.sourceKind === selectedSourceKind && source.sourceId === selectedSourceId);
        if (!selected) {
          const fallback = nextSources.find((source) => source.sourceKind === 'server' && source.sourceId === 'server') || nextSources[0];
          if (fallback) updateSearch(navigate, location, { sourceKind: fallback.sourceKind, sourceId: fallback.sourceId, path: '' });
        }
      })
      .catch((err) => { if (!cancelled) setError(normalizeApiError(err, t)); })
      .finally(() => { if (!cancelled) setLoadingSources(false); });
    return () => { cancelled = true; };
  }, [apiClient, isAdmin, navigate, location, selectedSourceKind, selectedSourceId, refreshKey]);

  useEffect(() => {
    if (!apiClient || !isAdmin || !selectedSourceKind || !selectedSourceId) return;
    let cancelled = false;
    setLoadingFiles(true);
    apiClient.listLogFiles(selectedSourceKind, selectedSourceId)
      .then((rows) => {
        if (cancelled) return;
        const nextFiles = rows || [];
        setFiles(nextFiles);
        setPageNumber(1);

        const existing = nextFiles.find((file) => file.path === selectedPath);
        if (!existing) {
          const fallback = nextFiles.find((file) => file.isCurrent) || nextFiles[0];
          if (fallback) updateSearch(navigate, location, { path: fallback.path });
          else if (selectedPath) updateSearch(navigate, location, { path: '' });
        }
      })
      .catch((err) => {
        if (cancelled) return;
        setFiles([]);
        setFileData(null);
        setError(normalizeApiError(err, t));
      })
      .finally(() => { if (!cancelled) setLoadingFiles(false); });
    return () => { cancelled = true; };
  }, [apiClient, isAdmin, navigate, location, selectedSourceKind, selectedSourceId, selectedPath, refreshKey]);

  useEffect(() => {
    if (!apiClient || !isAdmin || !selectedSourceKind || !selectedSourceId || !selectedPath) {
      setFileData(null);
      return;
    }

    let cancelled = false;
    setLoadingContent(true);
    apiClient.readLogFile(selectedSourceKind, selectedSourceId, selectedPath, {
      tailLines: parseNumber(tailLines, 200),
      maxBytes: parseNumber(maxBytes, 131072)
    })
      .then((result) => {
        if (cancelled) return;
        setFileData(result);
      })
      .catch((err) => {
        if (cancelled) return;
        setFileData(null);
        setError(normalizeApiError(err, t));
      })
      .finally(() => { if (!cancelled) setLoadingContent(false); });

    return () => { cancelled = true; };
  }, [apiClient, isAdmin, selectedSourceKind, selectedSourceId, selectedPath, tailLines, maxBytes, refreshKey]);

  const selectedSource = sources.find((source) => source.sourceKind === selectedSourceKind && source.sourceId === selectedSourceId) || null;
  const selectedFile = files.find((file) => file.path === selectedPath) || null;

  const fileColumns = [
    {
      key: 'fileName',
      label: 'File',
      tip: 'Log file name relative to the selected source',
      style: { width: '100%' },
      cellStyle: { width: '100%' },
      render: (file) => (
        <div style={{ minWidth: 0 }}>
          <div title={file.fileName} style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            <code className="monospace">{file.fileName}</code>
          </div>
          <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            <span title={file.path}><code className="monospace">{file.path}</code></span>
          </div>
        </div>
      )
    },
    {
      key: 'byteLength',
      label: 'Size',
      tip: 'Current file size on disk',
      style: { width: '7rem', whiteSpace: 'nowrap' },
      cellStyle: { whiteSpace: 'nowrap' },
      render: (file) => formatBytes(file.byteLength)
    },
    {
      key: 'lastModifiedUtc',
      label: 'Modified',
      tip: 'Last write time in UTC',
      style: { width: '10rem', whiteSpace: 'nowrap' },
      cellStyle: { whiteSpace: 'nowrap' },
      render: (file) => formatTime(file.lastModifiedUtc)
    }
  ];

  const runDownload = async (file) => {
    if (!file) return;
    try {
      const result = await apiClient.downloadLogFile(selectedSourceKind, selectedSourceId, file.path);
      const url = URL.createObjectURL(result.blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = result.fileName || file.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(normalizeApiError(err, t));
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    try {
      await apiClient.deleteLogFile(selectedSourceKind, selectedSourceId, deleteTarget.path);
      setDeleteTarget(null);
      refresh();
    } catch (err) {
      setDeleteTarget(null);
      setError(normalizeApiError(err, t));
    }
  };

  if (!isAdmin) {
    return (
      <div>
        <PageHeader title={tl('Logs')} subtitle={tl('The log viewer is only available to administrators.')} />
        <div className="login-error">{tl('This view is only available to administrators.')}</div>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title={tl('Logs')}
        subtitle={tl('Browse server and worker log files, read bounded tails, download complete files, and clear or delete files from stable storage.')}
      />

      <div className="logs-page-controls">
        <label className="logs-page-source-picker" title={tl('Select the server or worker log source to browse')}>
          <span className="logs-page-controls-label">{tl('Source')}</span>
          <select
            value={sourceKey(selectedSourceKind, selectedSourceId)}
            onChange={(e) => {
              const [sourceKind, sourceId] = e.target.value.split(':');
              updateSearch(navigate, location, { sourceKind, sourceId, path: '' });
            }}
            title={tl('Select a server or worker log source')}
          >
            {sources.map((source) => (
              <option key={sourceKey(source.sourceKind, source.sourceId)} value={sourceKey(source.sourceKind, source.sourceId)}>
                {source.displayName} ({tl(source.sourceKind)})
              </option>
            ))}
          </select>
        </label>
        <label className="logs-page-auto-refresh" title={tl('Refresh the selected source and file every five seconds')}>
          <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} style={{ width: 'auto' }} />
          {tl('Auto-refresh')}
        </label>
        <button className="button-secondary" onClick={refresh} title={tl('Refresh sources, files, and the selected file content')}>{t('common.actions.refresh')}</button>
      </div>

      {error && <div className="login-error">{error}</div>}

      <div className="summary-tiles">
        <div className="summary-tile">
          <div className="label">{tl('Source')}</div>
          <div className="value" style={{ fontSize: '1.2rem' }}>{selectedSource?.displayName || '-'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{tl('Source ID')}</div>
          <div className="value" style={{ fontSize: '1rem' }}>{selectedSource ? <CopyableId value={selectedSource.sourceId} max={18} /> : '-'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{tl('State')}</div>
          <div className="value" style={{ fontSize: '1.2rem' }}>{stateLabel(selectedSource, t)}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{tl('Files')}</div>
          <div className="value">{selectedSource?.fileCount ?? files.length}</div>
        </div>
      </div>

      <div className="logs-workspace">
        <div className="logs-sidebar-panel">
          <div className="logs-panel-header">
            <div>
               <div className="drawer-section-title">{tl('Files')}</div>
               <div className="view-subtitle">{tl('Choose a log file from the selected source.')}</div>
             </div>
           </div>
          <TableFrame
            columns={fileColumns}
            items={files}
            totalRecords={files.length}
            pageNumber={pageNumber}
            pageSize={pageSize}
            onPageChange={setPageNumber}
            onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
            onRefresh={refresh}
            loading={loadingSources || loadingFiles}
            emptyMessage={selectedSource ? tl('No log files are visible for this source.') : t('common.generic.loading')}
            onRowClick={(file) => updateSearch(navigate, location, { path: file.path })}
          />
        </div>

        <div className="log-viewer-panel">
          <div className="logs-panel-header">
            <div>
               <div className="drawer-section-title">{tl('Viewer')}</div>
               <div className="view-subtitle">{selectedFile ? tl('Reading {{fileName}}', { fileName: selectedFile.fileName }) : tl('Select a file to read its bounded tail.')}</div>
             </div>
             {selectedFile && (
               <div className="log-viewer-toolbar">
                 <button className="button-secondary" onClick={() => runDownload(selectedFile)} title={tl('Download the complete selected log file')}>{t('common.actions.download')}</button>
                 <button className={selectedFile.isCurrent ? 'button-danger' : 'button-secondary'} onClick={() => setDeleteTarget(selectedFile)} title={selectedFile.isCurrent ? tl('Clear the current log file by truncating it to zero bytes') : tl('Delete this archived log file')}>
                   {selectedFile.isCurrent ? tl('Clear current log') : tl('Delete file')}
                 </button>
                 <CopyButton value={fileData?.content || ''} title={tl('Copy the current log viewer text to the clipboard')} />
               </div>
             )}
           </div>

          <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
            <div className="field">
              <label title={tl('Maximum number of lines returned from the end of the file')}>{tl('Tail lines')}</label>
              <input type="number" min="1" value={tailLines} onChange={(e) => setTailLines(e.target.value)} title={tl('Maximum number of lines returned from the end of the file')} />
            </div>
            <div className="field">
              <label title={tl('Maximum number of UTF-8 bytes returned in the viewer')}>{tl('Max bytes')}</label>
              <input type="number" min="1" value={maxBytes} onChange={(e) => setMaxBytes(e.target.value)} title={tl('Maximum number of UTF-8 bytes returned in the viewer')} />
            </div>
            <div style={{ display: 'flex', alignItems: 'end' }}>
              <button className="button-secondary" onClick={refresh} style={{ width: '100%' }} title={tl('Re-read the selected file using the current tail bounds')}>{tl('Re-read')}</button>
            </div>
          </div>

          {!selectedFile && <div className="empty-state">{tl('Select a file from the list to read it.')}</div>}
          {selectedFile && (
            <>
              <div className="logs-meta-strip">
                <span title={tl('Selected file path')}><code className="monospace">{selectedFile.path}</code></span>
                <span title={tl('Current file size')}>{formatBytes(selectedFile.byteLength)}</span>
                <span title={tl('Last write time')}>{formatTime(selectedFile.lastModifiedUtc)}</span>
                <span title={tl('Delete behavior for this file')}>{selectedFile.deleteMode === 'Truncate' ? tl('Clears current file') : tl('Deletes archived file')}</span>
              </div>
              {fileData?.truncated && (
                <div className="callout callout-warning">
                  {tl('Viewer output is truncated to the last {{tailLines}} lines and {{maxBytes}}.', { tailLines: fileData.tailLines, maxBytes: formatBytes(fileData.maxBytes) })}
                </div>
              )}
              <div className="log-viewer-content" title={tl('Bounded log text rendered in a monospace viewer')}>
                {loadingContent ? t('common.generic.loading') : fileData?.content || ''}
              </div>
            </>
          )}
        </div>
      </div>

      <ConfirmModal
        open={!!deleteTarget}
        danger={deleteTarget?.isCurrent}
        title={deleteTarget?.isCurrent ? tl('Clear current log') : tl('Delete log file')}
        recordId={deleteTarget?.path || ''}
        recordIdLabel={tl('Path')}
        message={
          deleteTarget?.isCurrent
            ? tl('Clear this current log file by truncating it to zero bytes? The running process can continue writing to the same path afterward.')
            : tl('Delete this archived log file from disk? This cannot be undone.')
        }
        confirmLabel={deleteTarget?.isCurrent ? t('common.actions.clear') : t('common.actions.delete')}
        onConfirm={confirmDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}

export default LogsView;
