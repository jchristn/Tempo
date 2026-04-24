import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshIcon } from './Icons';
import { formatNumber } from '../utils/formatters';

export const TIME_RANGES = [
  { labelKey: 'components.chart.lastHour', value: 'hour', hours: 1, stepMs: 60_000, bucketMinutes: 1 },
  { labelKey: 'components.chart.lastDay', value: 'day', hours: 24, stepMs: 900_000, bucketMinutes: 15 },
  { labelKey: 'components.chart.lastWeek', value: 'week', hours: 168, stepMs: 3_600_000, bucketMinutes: 60 },
  { labelKey: 'components.chart.lastMonth', value: 'month', hours: 720, stepMs: 21_600_000, bucketMinutes: 360 }
];

export function getTimeRange(value) {
  return TIME_RANGES.find((r) => r.value === value) || TIME_RANGES[1];
}

function floorToStep(ts, stepMs) {
  return Math.floor(ts / stepMs) * stepMs;
}

function generateAllBuckets(startMs, endMs, stepMs) {
  const buckets = [];
  const flooredStart = floorToStep(startMs, stepMs);
  for (let t = flooredStart; t < endMs; t += stepMs) {
    buckets.push({
      bucketStartUtc: new Date(t).toISOString(),
      bucketEndUtc: new Date(t + stepMs).toISOString(),
      successCount: 0,
      failureCount: 0,
      averageDurationMs: 0,
      _key: t
    });
  }
  return buckets;
}

function mergeBuckets(allBuckets, apiBuckets, stepMs) {
  const map = new Map();
  for (const b of apiBuckets || []) {
    map.set(floorToStep(new Date(b.bucketStartUtc).getTime(), stepMs), b);
  }
  return allBuckets.map((b) => {
    const match = map.get(b._key);
    if (!match) return b;
    return {
      ...b,
      successCount: match.successCount || 0,
      failureCount: match.failureCount || 0,
      averageDurationMs: match.averageDurationMs || 0
    };
  });
}

function computeYCeiling(max) {
  if (max <= 0) return 1;
  const step = Math.max(1, Math.ceil(max / 4));
  let value = 0;
  while (value < max) value += step;
  return value || max;
}

function computeYTicks(max) {
  if (max <= 0) return [0];
  const step = Math.max(1, Math.ceil(max / 4));
  const ticks = [];
  for (let i = 0; i <= max; i += step) ticks.push(i);
  return ticks;
}

function formatBucketLabel(ts, range, locale) {
  const date = new Date(ts);
  if (range.bucketMinutes <= 15) return date.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' });
  if (range.bucketMinutes >= 60 && range.hours > 48) {
    return date.toLocaleDateString(locale, { month: 'short', day: 'numeric' }) + ' ' +
      date.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' });
}

function formatTooltipTime(ts, range, locale) {
  const date = new Date(ts);
  if (range.bucketMinutes >= 1440) return date.toLocaleDateString(locale, { weekday: 'short', month: 'short', day: 'numeric' });
  return date.toLocaleString(locale, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

/**
 * Conductor-style activity chart: pre-generates every expected bucket locally
 * and merges with whatever the server returns. Hour/Day/Week/Month always
 * show the right bucket count regardless of server bucketing.
 */
function ActivityChart({
  summary,
  rangeId = 'day',
  onRangeChange,
  onBucketClick,
  onRefresh,
  loading = false,
  title,
  totalLabel,
  successLabel,
  failureLabel,
  successLegend,
  failureLegend,
  emptyMessage,
  successColor = 'var(--color-success)',
  failureColor = 'var(--color-danger)'
}) {
  const { t, i18n } = useTranslation();
  const [hovered, setHovered] = useState(null);
  const range = getTimeRange(rangeId);
  const locale = i18n.resolvedLanguage;
  const resolvedTitle = title || t('components.chart.activity');
  const resolvedTotalLabel = totalLabel || t('components.chart.total');
  const resolvedSuccessLabel = successLabel || t('components.chart.success');
  const resolvedFailureLabel = failureLabel || t('components.chart.failed');
  const resolvedSuccessLegend = successLegend || t('components.chart.successSeries');
  const resolvedFailureLegend = failureLegend || t('components.chart.failureSeries');
  const resolvedEmptyMessage = emptyMessage || t('components.chart.noActivity');

  const buckets = useMemo(() => {
    const endMs = Date.now();
    const startMs = endMs - range.hours * 3_600_000;
    return mergeBuckets(generateAllBuckets(startMs, endMs, range.stepMs), summary?.buckets || [], range.stepMs);
  }, [summary, range.hours, range.stepMs]);

  const maxCount = Math.max(1, ...buckets.map((b) => (b.successCount || 0) + (b.failureCount || 0)));
  const yMax = computeYCeiling(maxCount);
  const yTicks = computeYTicks(yMax);

  const width = 1000;
  const height = 220;
  const padTop = 14;
  const padBottom = 26;
  const padLeft = 44;
  const padRight = 14;
  const plotH = height - padTop - padBottom;
  const plotW = width - padLeft - padRight;
  const barGroupW = plotW / Math.max(1, buckets.length);
  const barW = Math.max(2, Math.min(40, barGroupW * 0.7));

  const totalCount = summary?.totalCount ?? 0;
  const totalSuccess = summary?.totalSuccess ?? 0;
  const totalFailure = summary?.totalFailure ?? 0;

  return (
    <div className="rh-chart">
      <div className="rh-chart-header">
        <h2>{resolvedTitle}</h2>
        <div className="rh-chart-controls">
          <div className="rh-time-tabs">
            {TIME_RANGES.map((item) => (
              <button key={item.value} className={'rh-time-tab' + (rangeId === item.value ? ' active' : '')} onClick={() => onRangeChange?.(item.value)}>
                {t(item.labelKey)}
              </button>
            ))}
          </div>
          {onRefresh && (
            <button className="rh-refresh-btn" onClick={onRefresh} disabled={loading} title={t('components.chart.refresh')} aria-label={t('components.chart.refresh')}>
              <RefreshIcon size={16} />
            </button>
          )}
        </div>
      </div>

      <div className="rh-stats">
        <div className="rh-stat"><span className="rh-stat-value">{formatNumber(totalCount, undefined, locale)}</span><span className="rh-stat-label">{resolvedTotalLabel}</span></div>
        <div className="rh-stat"><span className="rh-stat-value" style={{ color: successColor }}>{formatNumber(totalSuccess, undefined, locale)}</span><span className="rh-stat-label">{resolvedSuccessLabel}</span></div>
        <div className="rh-stat"><span className="rh-stat-value" style={{ color: failureColor }}>{formatNumber(totalFailure, undefined, locale)}</span><span className="rh-stat-label">{resolvedFailureLabel}</span></div>
      </div>

      {buckets.length === 0 ? (
        <div className="rh-chart-empty">{resolvedEmptyMessage}</div>
      ) : (
        <div className="rh-chart-canvas">
          <svg width="100%" viewBox={'0 0 ' + width + ' ' + height} preserveAspectRatio="xMidYMid meet" style={{ display: 'block' }}>
            {yTicks.map((tick) => {
              const y = padTop + plotH - (tick / yMax) * plotH;
              return (
                <g key={tick}>
                  <line x1={padLeft} x2={width - padRight} y1={y} y2={y} stroke="var(--color-border)" strokeDasharray={tick === 0 ? 'none' : '4,4'} strokeWidth="0.5" />
                  <text x={padLeft - 6} y={y + 3} textAnchor="end" fontSize="10" fill="var(--color-text-muted)">{tick}</text>
                </g>
              );
            })}

            {buckets.map((bucket, index) => {
              const success = bucket.successCount || 0;
              const failure = bucket.failureCount || 0;
              const successH = (success / yMax) * plotH;
              const failureH = (failure / yMax) * plotH;
              const x = padLeft + index * barGroupW + (barGroupW - barW) / 2;
              const successY = padTop + plotH - successH - failureH;
              const failureY = padTop + plotH - failureH;
              const isCompound = range.bucketMinutes >= 60 && range.hours > 48;
              const estChars = isCompound ? 14 : 6;
              const estPx = estChars * 6 + 10;
              const labelInterval = Math.max(1, Math.ceil(buckets.length / Math.max(1, Math.floor(plotW / estPx))));
              const showLabel = index % labelInterval === 0;

              return (
                <g
                  key={index}
                  onMouseEnter={() => setHovered(index)}
                  onMouseLeave={() => setHovered(null)}
                  onClick={() => onBucketClick?.(bucket)}
                  style={{ cursor: onBucketClick ? 'pointer' : 'default' }}
                >
                  <rect x={padLeft + index * barGroupW} y={padTop} width={barGroupW} height={plotH + padBottom} fill="transparent" />
                  {success > 0 && <rect x={x} y={successY} width={barW} height={successH} rx="2" fill={successColor} opacity={hovered === index ? 1 : 0.85} />}
                  {failure > 0 && <rect x={x} y={failureY} width={barW} height={failureH} rx="2" fill={failureColor} opacity={hovered === index ? 1 : 0.85} />}
                  {showLabel && (
                    <text x={padLeft + index * barGroupW + barGroupW / 2} y={height - 6} textAnchor="middle" fontSize="10" fill="var(--color-text-muted)">
                      {formatBucketLabel(bucket.bucketStartUtc, range, locale)}
                    </text>
                  )}
                </g>
              );
            })}
          </svg>

          {hovered !== null && buckets[hovered] && (
            <div className="rh-chart-tooltip" style={{ left: ((hovered + 0.5) / buckets.length) * 100 + '%' }}>
              <div style={{ fontWeight: 600, marginBottom: 4 }}>{formatTooltipTime(buckets[hovered].bucketStartUtc, range, locale)}</div>
              <div><span style={{ color: successColor }}>{resolvedSuccessLabel}:</span> {formatNumber(buckets[hovered].successCount || 0, undefined, locale)}</div>
              <div><span style={{ color: failureColor }}>{resolvedFailureLabel}:</span> {formatNumber(buckets[hovered].failureCount || 0, undefined, locale)}</div>
              <div>{t('components.chart.totalSeries')}: {formatNumber((buckets[hovered].successCount || 0) + (buckets[hovered].failureCount || 0), undefined, locale)}</div>
            </div>
          )}
        </div>
      )}

      <div className="rh-chart-legend">
        <span className="rh-legend-item"><span className="rh-legend-color" style={{ background: successColor }} /> {resolvedSuccessLegend}</span>
        <span className="rh-legend-item"><span className="rh-legend-color" style={{ background: failureColor }} /> {resolvedFailureLegend}</span>
      </div>
    </div>
  );
}

export default ActivityChart;
