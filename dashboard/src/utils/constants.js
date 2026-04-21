export const RANGE_PRESETS = {
  hour: { label: 'Hour', bucketMinutes: 1, count: 60 },
  day: { label: 'Day', bucketMinutes: 15, count: 96 },
  week: { label: 'Week', bucketMinutes: 60, count: 168 },
  month: { label: '30 days', bucketMinutes: 360, count: 120 }
};

export const PAGE_SIZES = [10, 25, 50, 100, 250];

export function rangeToParams(id) {
  const preset = RANGE_PRESETS[id] || RANGE_PRESETS.day;
  const now = new Date();
  const to = new Date(now);
  const from = new Date(now.getTime() - preset.count * preset.bucketMinutes * 60_000);
  return {
    fromUtc: from.toISOString(),
    toUtc: to.toISOString(),
    bucketMinutes: preset.bucketMinutes
  };
}

export const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS'];
