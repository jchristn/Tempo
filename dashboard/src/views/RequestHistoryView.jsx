import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
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
    const date = new Date(iso);
    const pad = (value) => value.toString().padStart(2, '0');
    return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate()) + 'T' + pad(date.getHours()) + ':' + pad(date.getMinutes());
  } catch {
    return '';
  }
}

function RequestHistoryView({ apiClient, principal }) {
  const { t } = useTranslation();
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
    const query = {
      pageNumber,
      pageSize,
      method: filters.method || undefined,
      statusCode: filters.statusCode || undefined,
      pathContains: filters.pathContains || undefined,
      fromUtc: isoOrNull(filters.fromUtc) || undefined,
      toUtc: isoOrNull(filters.toUtc) || undefined
    };
    if (isAdmin) {
      if (filters.tenantId) query.tenantId = filters.tenantId;
      if (filters.userId) query.userId = filters.userId;
    }
    return query;
  }, [filters, isAdmin, pageNumber, pageSize]);

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
  }, [filters, isAdmin, rangeId]);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.getRequestHistory(buildListQuery())
      .then((result) => { if (!cancelled) setData(result); })
      .catch(() => { if (!cancelled) setData({ items: [], totalCount: 0 }); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, buildListQuery, refreshKey]);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    apiClient.getRequestHistorySummary(buildSummaryQuery())
      .then((result) => { if (!cancelled) setSummary(result); })
      .catch(() => { if (!cancelled) setSummary(null); });
    return () => { cancelled = true; };
  }, [apiClient, buildSummaryQuery, refreshKey]);

  const updateFilter = (key, value) => {
    setFilters((current) => ({ ...current, [key]: value }));
    setPageNumber(1);
  };

  const clearFilters = () => {
    setFilters({ method: '', statusCode: '', pathContains: '', fromUtc: '', toUtc: '', tenantId: '', userId: '' });
    setPageNumber(1);
  };

  const refresh = () => setRefreshKey((value) => value + 1);

  const handleBucketClick = (bucket) => {
    setFilters((current) => ({
      ...current,
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
    {
      key: 'createdUtc',
      label: t('views.requestHistory.columns.time', { defaultValue: 'Time' }),
      tip: t('views.requestHistory.columns.timeTip', { defaultValue: 'When the request hit the server (UTC)' }),
      render: (row) => <span title={row.createdUtc}>{formatTime(row.createdUtc)}</span>
    },
    {
      key: 'method',
      label: t('views.requestHistory.columns.method', { defaultValue: 'Method' }),
      tip: t('views.requestHistory.columns.methodTip', { defaultValue: 'HTTP method' }),
      render: (row) => <MethodPill method={row.method} />
    },
    {
      key: 'path',
      label: t('views.requestHistory.columns.path', { defaultValue: 'Path' }),
      tip: t('views.requestHistory.columns.pathTip', { defaultValue: 'Request path; hover the cell for the full URL with query string' }),
      cellClass: 'monospace',
      render: (row) => <span title={row.url}>{truncate(row.path, 80)}</span>
    },
    {
      key: 'statusCode',
      label: t('views.requestHistory.columns.status', { defaultValue: 'Status' }),
      tip: t('views.requestHistory.columns.statusTip', { defaultValue: 'HTTP status code returned to the client' }),
      render: (row) => <StatusPill code={row.statusCode} />
    },
    {
      key: 'durationMs',
      label: t('views.requestHistory.columns.duration', { defaultValue: 'Duration' }),
      tip: t('views.requestHistory.columns.durationTip', { defaultValue: 'Server-side processing time in milliseconds' }),
      cellClass: 'right',
      render: (row) => formatDuration(row.durationMs)
    },
    {
      key: 'principalName',
      label: t('views.requestHistory.columns.principal', { defaultValue: 'Principal' }),
      tip: t('views.requestHistory.columns.principalTip', { defaultValue: 'Authenticated user (or credential) that made the request' }),
      render: (row) => row.principalName || row.userId || t('common.placeholders.dash')
    },
    {
      key: 'actions',
      label: '',
      style: { width: 48 },
      render: (row) => (
        <RowActions
          onView={() => setSelectedId(row.id)}
          onViewJson={() => setJsonEntry(row)}
          onDelete={() => setConfirmRow(row)}
        />
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title={t('views.requestHistory.title')}
        subtitle={t('views.requestHistory.subtitle')}
        actions={<button className="button-danger" onClick={() => setConfirmBulk(true)}>{t('views.requestHistory.deleteMatching', { defaultValue: 'Delete matching' })}</button>}
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
          <label title={t('views.requestHistory.filters.methodTip', { defaultValue: 'Filter to one HTTP method' })}>{t('views.requestHistory.filters.method', { defaultValue: 'Method' })}</label>
          <select value={filters.method} onChange={(e) => updateFilter('method', e.target.value)}>
            <option value="">{t('common.generic.any')}</option>
            {HTTP_METHODS.map((method) => <option key={method} value={method}>{method}</option>)}
          </select>
        </div>
        <div className="field">
          <label title={t('views.requestHistory.filters.statusTip', { defaultValue: 'Filter to a single HTTP status code (exact match)' })}>{t('views.requestHistory.filters.status', { defaultValue: 'Status' })}</label>
          <input value={filters.statusCode} onChange={(e) => updateFilter('statusCode', e.target.value)} placeholder="500" />
        </div>
        <div className="field">
          <label title={t('views.requestHistory.filters.pathTip', { defaultValue: 'Substring match against the request path; supports any segment' })}>{t('views.requestHistory.filters.pathContains', { defaultValue: 'Path contains' })}</label>
          <input value={filters.pathContains} onChange={(e) => updateFilter('pathContains', e.target.value)} placeholder="/flows" />
        </div>
        <div className="field">
          <label title={t('views.requestHistory.filters.fromTip', { defaultValue: 'Earliest timestamp to include (local, converted to UTC)' })}>{t('views.requestHistory.filters.fromUtc', { defaultValue: 'From (UTC)' })}</label>
          <input type="datetime-local" value={filters.fromUtc} onChange={(e) => updateFilter('fromUtc', e.target.value)} />
        </div>
        <div className="field">
          <label title={t('views.requestHistory.filters.toTip', { defaultValue: 'Latest timestamp to include (local, converted to UTC)' })}>{t('views.requestHistory.filters.toUtc', { defaultValue: 'To (UTC)' })}</label>
          <input type="datetime-local" value={filters.toUtc} onChange={(e) => updateFilter('toUtc', e.target.value)} />
        </div>
        {isAdmin && (
          <>
            <div className="field">
              <label title={t('views.requestHistory.filters.tenantTip', { defaultValue: 'Restrict to one tenant (admin-only)' })}>{t('views.requestHistory.filters.tenantId', { defaultValue: 'Tenant ID' })}</label>
              <input value={filters.tenantId} onChange={(e) => updateFilter('tenantId', e.target.value)} placeholder="ten_..." />
            </div>
            <div className="field">
              <label title={t('views.requestHistory.filters.userTip', { defaultValue: 'Restrict to one user (admin-only)' })}>{t('views.requestHistory.filters.userId', { defaultValue: 'User ID' })}</label>
              <input value={filters.userId} onChange={(e) => updateFilter('userId', e.target.value)} placeholder="usr_..." />
            </div>
          </>
        )}
        <div style={{ display: 'flex', alignItems: 'end' }}>
          <button className="button-secondary" onClick={clearFilters} style={{ width: '100%' }}>{t('views.requestHistory.clearFilters', { defaultValue: 'Clear filters' })}</button>
        </div>
      </div>

      <TablePagination
        totalRecords={data?.totalCount ?? 0}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        onRefresh={refresh}
        disabled={loading}
      />
      <DataTable
        columns={columns}
        items={data?.items || []}
        loading={loading}
        emptyMessage={t('views.requestHistory.empty', { defaultValue: 'No requests match the current filters.' })}
        selectable
        selected={selected}
        onSelectedChange={setSelected}
        onRowClick={(row) => setSelectedId(row.id)}
      />

      <RequestDetailsModal entryId={selectedId} open={!!selectedId} onClose={() => setSelectedId(null)} apiClient={apiClient} />
      <JsonViewerModal open={!!jsonEntry} onClose={() => setJsonEntry(null)} value={jsonEntry} title={t('views.requestHistory.jsonTitle', { defaultValue: 'Request history entry' })} />
      <ConfirmModal
        open={!!confirmRow}
        danger
        title={t('views.requestHistory.deleteTitle', { defaultValue: 'Delete request' })}
        recordId={confirmRow?.id || ''}
        recordIdLabel={t('components.requestDetails.requestId')}
        message={t('views.requestHistory.deleteMessage', { defaultValue: 'Delete this request entry?' })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={handleDeleteRow}
        onCancel={() => setConfirmRow(null)}
      />
      <ConfirmModal
        open={confirmBulk}
        danger
        title={t('views.requestHistory.deleteMatchingTitle', { defaultValue: 'Delete matching requests' })}
        message={t('views.requestHistory.deleteMatchingMessage', { defaultValue: 'Delete all request-history rows matching the current filters? This cannot be undone.' })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={handleBulkDelete}
        onCancel={() => setConfirmBulk(false)}
      />
    </div>
  );
}

export default RequestHistoryView;
