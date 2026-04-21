import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import PageHeader from '../components/PageHeader';
import ActivityChart, { getTimeRange } from '../components/ActivityChart';
import DataTable from '../components/DataTable';
import TablePagination from '../components/TablePagination';
import MethodPill from '../components/MethodPill';
import StatusPill from '../components/StatusPill';
import RequestDetailsModal from '../components/RequestDetailsModal';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import RowActions from '../components/RowActions';
import { formatDuration, formatTime, isoOrNull, truncate } from '../utils/formatters';
import { HTTP_METHODS } from '../utils/constants';

function toLocalDate(iso) {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    const pad = (n) => n.toString().padStart(2, '0');
    return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
  } catch { return ''; }
}

function RequestHistoryView({ apiClient, principal }) {
  const location = useLocation();
  const search = useMemo(() => new URLSearchParams(location.search), [location.search]);
  const isAdmin = !!principal?.isAdmin || principal?.type === 'administrator';

  const [filters, setFilters] = useState({
    method: '',
    statusCode: '',
    pathContains: '',
    fromUtc: toLocalDate(search.get('fromUtc')),
    toUtc: toLocalDate(search.get('toUtc')),
    tenantId: '',
    userId: ''
  });
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [rangeId, setRangeId] = useState('day');
  const [summary, setSummary] = useState(null);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState(null);
  const [jsonEntry, setJsonEntry] = useState(null);
  const [confirmRow, setConfirmRow] = useState(null);
  const [confirmBulk, setConfirmBulk] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [selected, setSelected] = useState(new Set());

  const buildListQuery = useCallback(() => {
    const q = {
      pageNumber, pageSize,
      method: filters.method || undefined,
      statusCode: filters.statusCode || undefined,
      pathContains: filters.pathContains || undefined,
      fromUtc: isoOrNull(filters.fromUtc) || undefined,
      toUtc: isoOrNull(filters.toUtc) || undefined
    };
    if (isAdmin) {
      if (filters.tenantId) q.tenantId = filters.tenantId;
      if (filters.userId) q.userId = filters.userId;
    }
    return q;
  }, [filters, pageNumber, pageSize, isAdmin]);

  const buildSummaryQuery = useCallback(() => {
    const range = getTimeRange(rangeId);
    const endUtc = new Date().toISOString();
    const startUtc = new Date(Date.now() - range.hours * 3_600_000).toISOString();
    return {
      fromUtc: startUtc,
      toUtc: endUtc,
      bucketMinutes: range.bucketMinutes,
      method: filters.method || undefined,
      statusCode: filters.statusCode || undefined,
      pathContains: filters.pathContains || undefined,
      tenantId: isAdmin ? (filters.tenantId || undefined) : undefined,
      userId: isAdmin ? (filters.userId || undefined) : undefined
    };
  }, [filters, rangeId, isAdmin]);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.getRequestHistory(buildListQuery())
      .then((d) => { if (!cancelled) setData(d); })
      .catch(() => { if (!cancelled) setData({ items: [], totalCount: 0 }); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, buildListQuery, refreshKey]);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    apiClient.getRequestHistorySummary(buildSummaryQuery())
      .then((s) => { if (!cancelled) setSummary(s); })
      .catch(() => { if (!cancelled) setSummary(null); });
    return () => { cancelled = true; };
  }, [apiClient, buildSummaryQuery, refreshKey]);

  const updateFilter = (k, v) => { setFilters((f) => ({ ...f, [k]: v })); setPageNumber(1); };
  const clearFilters = () => {
    setFilters({ method: '', statusCode: '', pathContains: '', fromUtc: '', toUtc: '', tenantId: '', userId: '' });
    setPageNumber(1);
  };
  const refresh = () => setRefreshKey((k) => k + 1);

  const handleBucketClick = (bucket) => {
    setFilters((f) => ({
      ...f,
      fromUtc: toLocalDate(bucket.bucketStartUtc),
      toUtc: toLocalDate(bucket.bucketEndUtc)
    }));
    setPageNumber(1);
  };

  const handleDeleteRow = async () => {
    if (!confirmRow) return;
    await apiClient.deleteRequestHistoryEntry(confirmRow.id);
    setConfirmRow(null);
    refresh();
  };

  const handleBulkDelete = async () => {
    await apiClient.deleteRequestHistoryBulk(buildListQuery());
    setConfirmBulk(false);
    setSelected(new Set());
    refresh();
  };

  const columns = [
    { key: 'createdUtc', label: 'Time', tip: 'When the request hit the server (UTC)', render: (r) => <span title={r.createdUtc}>{formatTime(r.createdUtc)}</span> },
    { key: 'method', label: 'Method', tip: 'HTTP method', render: (r) => <MethodPill method={r.method} /> },
    { key: 'path', label: 'Path', tip: 'Request path; hover the cell for the full URL with query string', cellClass: 'monospace', render: (r) => <span title={r.url}>{truncate(r.path, 80)}</span> },
    { key: 'statusCode', label: 'Status', tip: 'HTTP status code returned to the client', render: (r) => <StatusPill code={r.statusCode} /> },
    { key: 'durationMs', label: 'Duration', tip: 'Server-side processing time in milliseconds', cellClass: 'right', render: (r) => formatDuration(r.durationMs) },
    { key: 'principalName', label: 'Principal', tip: 'Authenticated user (or credential) that made the request', render: (r) => r.principalName || r.userId || '-' },
    { key: 'actions', label: '', style: { width: 48 }, render: (r) => (
      <RowActions
        onView={() => setSelectedId(r.id)}
        onViewJson={() => setJsonEntry(r)}
        onDelete={() => setConfirmRow(r)}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title="Request History"
        subtitle="Audit captured HTTP requests, response codes, timings, and payload excerpts."
        actions={<button className="button-danger" onClick={() => setConfirmBulk(true)}>Delete matching</button>}
      />

      <ActivityChart
        summary={summary}
        rangeId={rangeId}
        onRangeChange={setRangeId}
        onBucketClick={handleBucketClick}
        onRefresh={refresh}
        loading={loading}
      />

      <div className="filter-bar compact" style={{ marginBottom: 'var(--spacing-sm)' }}>
        <div className="field">
          <label title="Filter to one HTTP method">Method</label>
          <select value={filters.method} onChange={(e) => updateFilter('method', e.target.value)}>
            <option value="">Any</option>
            {HTTP_METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </div>
        <div className="field">
          <label title="Filter to a single HTTP status code (exact match)">Status</label>
          <input value={filters.statusCode} onChange={(e) => updateFilter('statusCode', e.target.value)} placeholder="500" />
        </div>
        <div className="field">
          <label title="Substring match against the request path; supports any segment">Path contains</label>
          <input value={filters.pathContains} onChange={(e) => updateFilter('pathContains', e.target.value)} placeholder="/flows" />
        </div>
        <div className="field">
          <label title="Earliest timestamp to include (local, converted to UTC)">From (UTC)</label>
          <input type="datetime-local" value={filters.fromUtc} onChange={(e) => updateFilter('fromUtc', e.target.value)} />
        </div>
        <div className="field">
          <label title="Latest timestamp to include (local, converted to UTC)">To (UTC)</label>
          <input type="datetime-local" value={filters.toUtc} onChange={(e) => updateFilter('toUtc', e.target.value)} />
        </div>
        {isAdmin && (
          <>
            <div className="field">
              <label title="Restrict to one tenant (admin-only)">Tenant ID</label>
              <input value={filters.tenantId} onChange={(e) => updateFilter('tenantId', e.target.value)} placeholder="ten_…" />
            </div>
            <div className="field">
              <label title="Restrict to one user (admin-only)">User ID</label>
              <input value={filters.userId} onChange={(e) => updateFilter('userId', e.target.value)} placeholder="usr_…" />
            </div>
          </>
        )}
        <div style={{ display: 'flex', alignItems: 'end' }}>
          <button className="button-secondary" onClick={clearFilters} style={{ width: '100%' }}>Clear filters</button>
        </div>
      </div>

      <TablePagination
        totalRecords={data?.totalCount ?? 0}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(s) => { setPageSize(s); setPageNumber(1); }}
        onRefresh={refresh}
        disabled={loading}
      />
      <DataTable
        columns={columns}
        items={data?.items || []}
        loading={loading}
        emptyMessage="No requests match the current filters."
        selectable
        selected={selected}
        onSelectedChange={setSelected}
        onRowClick={(r) => setSelectedId(r.id)}
      />

      <RequestDetailsModal entryId={selectedId} open={!!selectedId} onClose={() => setSelectedId(null)} apiClient={apiClient} />
      <JsonViewerModal open={!!jsonEntry} onClose={() => setJsonEntry(null)} value={jsonEntry} title="Request history entry" />
      <ConfirmModal open={!!confirmRow} danger title="Delete request" message={'Delete this request entry?'} confirmLabel="Delete"
        onConfirm={handleDeleteRow} onCancel={() => setConfirmRow(null)} />
      <ConfirmModal open={confirmBulk} danger title="Delete matching requests"
        message={'Delete all request-history rows matching the current filters? This cannot be undone.'}
        confirmLabel="Delete" onConfirm={handleBulkDelete} onCancel={() => setConfirmBulk(false)} />
    </div>
  );
}

export default RequestHistoryView;
