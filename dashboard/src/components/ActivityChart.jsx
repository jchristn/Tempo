import { useMemo, useState } from 'react';
import { RefreshIcon } from './Icons';

export const TIME_RANGES = [
  { label: 'Last Hour', value: 'hour', hours: 1, stepMs: 60_000, bucketMinutes: 1 },
  { label: 'Last Day', value: 'day', hours: 24, stepMs: 900_000, bucketMinutes: 15 },
  { label: 'Last Week', value: 'week', hours: 168, stepMs: 3_600_000, bucketMinutes: 60 },
  { label: 'Last Month', value: 'month', hours: 720, stepMs: 21_600_000, bucketMinutes: 360 }
];

export function getTimeRange(value) {
  return TIME_RANGES.find((r) => r.value === value) || TIME_RANGES[1];
}

function floorToStep(ts, stepMs) { return Math.floor(ts / stepMs) * stepMs; }

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
    const m = map.get(b._key);
    if (!m) return b;
    return {
      ...b,
      successCount: m.successCount || 0,
      failureCount: m.failureCount || 0,
      averageDurationMs: m.averageDurationMs || 0
    };
  });
}

/**
 * Conductor-style activity chart: pre-generates every expected bucket locally
 * and merges with whatever the server returns. Hour/Day/Week/Month always
 * show the right bucket count regardless of server bucketing.
 */
function ActivityChart({ summary, rangeId = 'day', onRangeChange, onBucketClick, onRefresh, loading = false }) {
  const [hovered, setHovered] = useState(null);
  const range = getTimeRange(rangeId);

  const buckets = useMemo(() => {
    const endMs = Date.now();
    const startMs = endMs - range.hours * 3_600_000;
    return mergeBuckets(generateAllBuckets(startMs, endMs, range.stepMs), summary?.buckets || [], range.stepMs);
  }, [summary, rangeId, range.hours, range.stepMs]);

  const maxCount = Math.max(1, ...buckets.map((b) => (b.successCount || 0) + (b.failureCount || 0)));
  const yMax = computeYCeiling(maxCount);
  const yTicks = computeYTicks(yMax);

  const width = 1000, height = 220, padTop = 14, padBottom = 26, padLeft = 44, padRight = 14;
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
        <h2>Request Activity</h2>
        <div className="rh-chart-controls">
          <div className="rh-time-tabs">
            {TIME_RANGES.map((r) => (
              <button key={r.value} className={'rh-time-tab' + (rangeId === r.value ? ' active' : '')} onClick={() => onRangeChange?.(r.value)}>
                {r.label}
              </button>
            ))}
          </div>
          {onRefresh && (
            <button className="rh-refresh-btn" onClick={onRefresh} disabled={loading} title="Refresh" aria-label="Refresh">
              <RefreshIcon size={16} />
            </button>
          )}
        </div>
      </div>

      <div className="rh-stats">
        <div className="rh-stat"><span className="rh-stat-value">{totalCount.toLocaleString()}</span><span className="rh-stat-label">Total</span></div>
        <div className="rh-stat"><span className="rh-stat-value" style={{ color: 'var(--color-success)' }}>{totalSuccess.toLocaleString()}</span><span className="rh-stat-label">Success</span></div>
        <div className="rh-stat"><span className="rh-stat-value" style={{ color: 'var(--color-danger)' }}>{totalFailure.toLocaleString()}</span><span className="rh-stat-label">Failed</span></div>
      </div>

      {buckets.length === 0 ? (
        <div className="rh-chart-empty">No request data for this time range</div>
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

            {buckets.map((b, i) => {
              const success = b.successCount || 0;
              const failure = b.failureCount || 0;
              const successH = (success / yMax) * plotH;
              const failureH = (failure / yMax) * plotH;
              const x = padLeft + i * barGroupW + (barGroupW - barW) / 2;
              const successY = padTop + plotH - successH - failureH;
              const failureY = padTop + plotH - failureH;
              const isCompound = range.bucketMinutes >= 60 && range.hours > 48;
              const estChars = isCompound ? 14 : 6;
              const estPx = estChars * 6 + 10;
              const labelInterval = Math.max(1, Math.ceil(buckets.length / Math.max(1, Math.floor(plotW / estPx))));
              const showLabel = i % labelInterval === 0;

              return (
                <g key={i}
                   onMouseEnter={() => setHovered(i)}
                   onMouseLeave={() => setHovered(null)}
                   onClick={() => onBucketClick?.(b)}
                   style={{ cursor: onBucketClick ? 'pointer' : 'default' }}>
                  <rect x={padLeft + i * barGroupW} y={padTop} width={barGroupW} height={plotH + padBottom} fill="transparent" />
                  {success > 0 && <rect x={x} y={successY} width={barW} height={successH} rx="2" fill="var(--color-success)" opacity={hovered === i ? 1 : 0.85} />}
                  {failure > 0 && <rect x={x} y={failureY} width={barW} height={failureH} rx="2" fill="var(--color-danger)" opacity={hovered === i ? 1 : 0.85} />}
                  {showLabel && (
                    <text x={padLeft + i * barGroupW + barGroupW / 2} y={height - 6} textAnchor="middle" fontSize="10" fill="var(--color-text-muted)">
                      {formatBucketLabel(b.bucketStartUtc, range)}
                    </text>
                  )}
                </g>
              );
            })}
          </svg>

          {hovered !== null && buckets[hovered] && (
            <div className="rh-chart-tooltip" style={{ left: ((hovered + 0.5) / buckets.length) * 100 + '%' }}>
              <div style={{ fontWeight: 600, marginBottom: 4 }}>{formatTooltipTime(buckets[hovered].bucketStartUtc, range)}</div>
              <div><span style={{ color: 'var(--color-success)' }}>Success:</span> {(buckets[hovered].successCount || 0).toLocaleString()}</div>
              <div><span style={{ color: 'var(--color-danger)' }}>Failed:</span> {(buckets[hovered].failureCount || 0).toLocaleString()}</div>
              <div>Total: {((buckets[hovered].successCount || 0) + (buckets[hovered].failureCount || 0)).toLocaleString()}</div>
            </div>
          )}
        </div>
      )}

      <div className="rh-chart-legend">
        <span className="rh-legend-item"><span className="rh-legend-color" style={{ background: 'var(--color-success)' }} /> Success (1xx-3xx)</span>
        <span className="rh-legend-item"><span className="rh-legend-color" style={{ background: 'var(--color-danger)' }} /> Failed (4xx-5xx)</span>
      </div>
    </div>
  );
}

function computeYCeiling(max) {
  if (max <= 0) return 1;
  const step = Math.max(1, Math.ceil(max / 4));
  let v = 0;
  while (v < max) v += step;
  return v || max;
}

function computeYTicks(max) {
  if (max <= 0) return [0];
  const step = Math.max(1, Math.ceil(max / 4));
  const ticks = [];
  for (let i = 0; i <= max; i += step) ticks.push(i);
  return ticks;
}

function formatBucketLabel(ts, range) {
  const d = new Date(ts);
  if (range.bucketMinutes <= 15) return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  if (range.bucketMinutes >= 60 && range.hours > 48) {
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) + ' ' +
      d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  }
  return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function formatTooltipTime(ts, range) {
  const d = new Date(ts);
  if (range.bucketMinutes >= 1440) return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
  return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export default ActivityChart;
