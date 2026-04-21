/**
 * Formatting helpers shared across views.
 */

export function formatDuration(ms) {
  if (ms == null || Number.isNaN(ms)) return '-';
  if (ms < 1) return ms.toFixed(2) + ' ms';
  if (ms < 1000) return ms.toFixed(0) + ' ms';
  if (ms < 60000) return (ms / 1000).toFixed(2) + ' s';
  return (ms / 60000).toFixed(1) + ' m';
}

export function formatTime(utcString) {
  if (!utcString) return '-';
  try {
    const d = new Date(utcString);
    return d.toLocaleString();
  } catch { return utcString; }
}

export function formatRelative(utcString) {
  if (!utcString) return '-';
  try {
    const then = new Date(utcString).getTime();
    const diff = (Date.now() - then) / 1000;
    if (diff < 60) return Math.round(diff) + 's ago';
    if (diff < 3600) return Math.round(diff / 60) + 'm ago';
    if (diff < 86400) return Math.round(diff / 3600) + 'h ago';
    return Math.round(diff / 86400) + 'd ago';
  } catch { return utcString; }
}

export function statusClass(status) {
  if (status >= 200 && status < 400) return 'pill pill-success';
  if (status >= 400) return 'pill pill-danger';
  return 'pill pill-neutral';
}

export function methodClass(method) {
  if (!method) return 'explorer-method get';
  return 'explorer-method ' + method.toLowerCase();
}

export function truncate(value, len = 60) {
  if (!value) return '';
  return value.length > len ? value.slice(0, len - 1) + '…' : value;
}

export function isoOrNull(value) {
  if (!value) return null;
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return null;
  return d.toISOString();
}
