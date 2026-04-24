import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import ActivityChart, { getTimeRange } from '../components/ActivityChart';
import { formatDuration, formatNumber } from '../utils/formatters';
import { normalizeApiError } from '../utils/i18n';

function HomeView({ apiClient }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [rangeId, setRangeId] = useState('day');
  const [summary, setSummary] = useState(null);
  const [runtimeStatus, setRuntimeStatus] = useState(null);
  const [error, setError] = useState(null);
  const [runtimeError, setRuntimeError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const locale = i18n.resolvedLanguage;

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
      .then((result) => { if (!cancelled) setSummary(result); })
      .catch((err) => { if (!cancelled) setError(normalizeApiError(err, t)); });
    apiClient.getExternalExecutionStatus()
      .then((result) => { if (!cancelled) setRuntimeStatus(result); })
      .catch((err) => { if (!cancelled) setRuntimeError(normalizeApiError(err, t)); });
    return () => { cancelled = true; };
  }, [apiClient, rangeId, refreshKey, t]);

  const handleBucketClick = (bucket) => {
    const from = encodeURIComponent(bucket.bucketStartUtc);
    const to = encodeURIComponent(bucket.bucketEndUtc);
    navigate('/dashboard/requests?fromUtc=' + from + '&toUtc=' + to);
  };

  const runtime = normalizeRuntimeStatus(runtimeStatus);
  const dash = t('common.placeholders.dash');

  return (
    <div>
      <PageHeader
        title={t('views.home.title')}
        subtitle={t('views.home.subtitle')}
        actions={
          <button
            className="button-secondary"
            onClick={() => navigate('/dashboard/logs?sourceKind=server&sourceId=server')}
            title={t('views.home.serverLogsTitle', { defaultValue: 'Open the current Tempo Server logs' })}
          >
            {t('views.home.serverLogs')}
          </button>
        }
      />

      <div className="summary-tiles">
        <div className="summary-tile"><div className="label">{t('views.home.totalRequests', { defaultValue: 'Total requests' })}</div><div className="value">{summary ? formatNumber(summary.totalCount, undefined, locale) : dash}</div></div>
        <div className="summary-tile success"><div className="label">{t('views.home.successful', { defaultValue: 'Successful' })}</div><div className="value">{summary ? formatNumber(summary.totalSuccess, undefined, locale) : dash}</div></div>
        <div className="summary-tile danger"><div className="label">{t('views.home.failed', { defaultValue: 'Failed' })}</div><div className="value">{summary ? formatNumber(summary.totalFailure, undefined, locale) : dash}</div></div>
        <div className="summary-tile"><div className="label">{t('views.home.averageDuration', { defaultValue: 'Avg duration' })}</div><div className="value">{summary ? formatDuration(summary.averageDurationMs, locale) : dash}</div></div>
      </div>

      <div className="summary-tiles external-execution-tiles">
        <div className="summary-tile">
          <div className="label">{t('views.home.artifactExecution', { defaultValue: 'Artifact execution' })}</div>
          <div className="value">{runtimeStatus ? t('common.generic.active') : dash}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{t('views.home.activeProcesses', { defaultValue: 'Active processes' })}</div>
          <div className="value">{runtimeStatus ? formatNumber(runtime.activeServerWide, undefined, locale) : dash}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{t('views.home.queuedSteps', { defaultValue: 'Queued steps' })}</div>
          <div className="value">{runtimeStatus ? formatNumber(runtime.queuedServerWide, undefined, locale) : dash}</div>
        </div>
        <div className="summary-tile">
          <div className="label">{t('views.home.tenantsQueued', { defaultValue: 'Tenants queued' })}</div>
          <div className="value">{runtimeStatus ? formatNumber(runtime.tenantsQueued, undefined, locale) : dash}</div>
        </div>
      </div>

      {error && <div className="login-error">{error}</div>}
      {runtimeError && <div className="login-error">{runtimeError}</div>}

      <ActivityChart
        summary={summary}
        rangeId={rangeId}
        onRangeChange={setRangeId}
        onBucketClick={handleBucketClick}
        onRefresh={() => setRefreshKey((value) => value + 1)}
        title={t('views.home.requestActivity', { defaultValue: 'Request activity' })}
        totalLabel={t('components.chart.total')}
        successLabel={t('components.chart.success')}
        failureLabel={t('components.chart.failed')}
        successLegend={t('views.home.requestSuccessLegend', { defaultValue: 'Success (1xx-3xx)' })}
        failureLegend={t('views.home.requestFailureLegend', { defaultValue: 'Failed (4xx-5xx)' })}
        emptyMessage={t('views.home.requestActivityEmpty', { defaultValue: 'No request data for this time range' })}
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
    tenantsQueued: Object.values(queuedByTenant || {}).filter((value) => Number(value) > 0).length
  };
}

function pick(obj, camel, pascal, fallback) {
  if (!obj) return fallback;
  if (obj[camel] !== undefined && obj[camel] !== null) return obj[camel];
  if (obj[pascal] !== undefined && obj[pascal] !== null) return obj[pascal];
  return fallback;
}

export default HomeView;
