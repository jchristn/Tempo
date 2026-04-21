import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import PageHeader from '../components/PageHeader';
import ActivityChart, { getTimeRange } from '../components/ActivityChart';
import { formatDuration } from '../utils/formatters';

function HomeView({ apiClient }) {
  const navigate = useNavigate();
  const [rangeId, setRangeId] = useState('day');
  const [summary, setSummary] = useState(null);
  const [runtimeStatus, setRuntimeStatus] = useState(null);
  const [error, setError] = useState(null);
  const [runtimeError, setRuntimeError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    const range = getTimeRange(rangeId);
    const endUtc = new Date().toISOString();
    const startUtc = new Date(Date.now() - range.hours * 3_600_000).toISOString();
    setSummary(null);
    setRuntimeStatus(null);
    setError(null);
    setRuntimeError(null);
    apiClient.getRequestHistorySummary({ fromUtc: startUtc, toUtc: endUtc, bucketMinutes: range.bucketMinutes })
      .then((s) => { if (!cancelled) setSummary(s); })
      .catch((err) => { if (!cancelled) setError(err.message); });
    apiClient.getExternalExecutionStatus()
      .then((s) => { if (!cancelled) setRuntimeStatus(s); })
      .catch((err) => { if (!cancelled) setRuntimeError(err.message); });
    return () => { cancelled = true; };
  }, [apiClient, rangeId, refreshKey]);

  const handleBucketClick = (bucket) => {
    const from = encodeURIComponent(bucket.bucketStartUtc);
    const to = encodeURIComponent(bucket.bucketEndUtc);
    navigate('/dashboard/requests?fromUtc=' + from + '&toUtc=' + to);
  };

  const runtime = normalizeRuntimeStatus(runtimeStatus);

  return (
    <div>
      <PageHeader title="Home" subtitle="Monitor request activity, runtime pressure, and recent health at a glance." />

      <div className="summary-tiles">
        <div className="summary-tile"><div className="label">Total requests</div><div className="value">{summary ? summary.totalCount.toLocaleString() : '—'}</div></div>
        <div className="summary-tile success"><div className="label">Successful</div><div className="value">{summary ? summary.totalSuccess.toLocaleString() : '—'}</div></div>
        <div className="summary-tile danger"><div className="label">Failed</div><div className="value">{summary ? summary.totalFailure.toLocaleString() : '—'}</div></div>
        <div className="summary-tile"><div className="label">Avg duration</div><div className="value">{summary ? formatDuration(summary.averageDurationMs) : '—'}</div></div>
      </div>

      <div className="summary-tiles external-execution-tiles">
        <div className="summary-tile">
          <div className="label">Artifact execution</div>
          <div className="value">{runtimeStatus ? 'Available' : '-'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Active processes</div>
          <div className="value">{runtimeStatus ? runtime.activeServerWide.toLocaleString() : '-'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Queued steps</div>
          <div className="value">{runtimeStatus ? runtime.queuedServerWide.toLocaleString() : '-'}</div>
        </div>
        <div className="summary-tile">
          <div className="label">Tenants queued</div>
          <div className="value">{runtimeStatus ? runtime.tenantsQueued.toLocaleString() : '-'}</div>
        </div>
      </div>

      {error && <div className="login-error">{error}</div>}
      {runtimeError && <div className="login-error">{runtimeError}</div>}

      <ActivityChart
        summary={summary}
        rangeId={rangeId}
        onRangeChange={setRangeId}
        onBucketClick={handleBucketClick}
        onRefresh={() => setRefreshKey((k) => k + 1)}
      />
    </div>
  );
}

function normalizeRuntimeStatus(status) {
  const capacity = pick(status, 'capacity', 'Capacity', {});
  const queuedByTenant = pick(capacity, 'queuedByTenant', 'QueuedByTenant', {});
  return {
    activeServerWide: pick(capacity, 'activeServerWide', 'ActiveServerWide', 0),
    queuedServerWide: pick(capacity, 'queuedServerWide', 'QueuedServerWide', 0),
    tenantsQueued: Object.values(queuedByTenant || {}).filter((v) => Number(v) > 0).length
  };
}

function pick(obj, camel, pascal, fallback) {
  if (!obj) return fallback;
  if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
  if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
  return fallback;
}

export default HomeView;
