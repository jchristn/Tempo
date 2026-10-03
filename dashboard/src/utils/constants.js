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

// Observability tools bundled with docker/compose.yaml. URLs default to this dashboard's own host on the
// host-published ports, and can be overridden at build time with VITE_GRAFANA_URL, VITE_PROMETHEUS_URL,
// VITE_TRACES_URL, and VITE_LOKI_URL. Credentials are the local-development defaults from compose.yaml.
function externalServiceUrl(override, port) {
  if (override) return override.replace(/\/+$/, '');
  if (typeof window === 'undefined' || !window.location) return 'http://127.0.0.1:' + port;
  return window.location.protocol + '//' + window.location.hostname + ':' + port;
}

export function getExternalServices() {
  const env = (typeof import.meta !== 'undefined' && import.meta.env) || {};
  return [
    { id: 'grafana', name: 'Grafana', url: externalServiceUrl(env.VITE_GRAFANA_URL, 3001), healthPath: '/api/health', credentials: 'admin / admin', purposeKey: 'grafanaPurpose' },
    { id: 'prometheus', name: 'Prometheus', url: externalServiceUrl(env.VITE_PROMETHEUS_URL, 9090), healthPath: '/-/healthy', credentials: null, purposeKey: 'prometheusPurpose' },
    { id: 'tempo', name: 'Grafana Tempo', url: externalServiceUrl(env.VITE_TRACES_URL, 3200), healthPath: '/ready', credentials: null, purposeKey: 'tracesPurpose' },
    { id: 'loki', name: 'Loki', url: externalServiceUrl(env.VITE_LOKI_URL, 3100), healthPath: '/ready', credentials: null, purposeKey: 'lokiPurpose' }
  ];
}
