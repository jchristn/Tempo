export function compactJson(value) {
  if (value === null || value === undefined) return '';
  if (typeof value !== 'string') {
    try {
      return JSON.stringify(value);
    } catch {
      return String(value);
    }
  }

  const trimmed = value.trim();
  if (!trimmed) return '{}';

  try {
    return JSON.stringify(JSON.parse(trimmed));
  } catch {
    return trimmed;
  }
}

function shellSingleQuote(value) {
  return "'" + String(value).replace(/'/g, "'\\''") + "'";
}

function shellDoubleQuote(value) {
  return '"' + String(value).replace(/["\\`]/g, '\\$&') + '"';
}

function windowsCmdDoubleQuote(value) {
  return '"' + String(value).replace(/"/g, '\\"') + '"';
}

export function clientPlatform() {
  if (typeof navigator === 'undefined') return '';
  return navigator.userAgentData?.platform || navigator.platform || '';
}

export function isWindowsPlatform(platform = clientPlatform()) {
  return /win/i.test(platform || '');
}

function joinSegments(segments, separator) {
  return segments.join(separator);
}

export function buildCurlCommand({
  url,
  method = 'POST',
  headers = {},
  body = null,
  platform = clientPlatform(),
  shellExpandableHeaders = []
}) {
  const normalizedMethod = (method || 'POST').toUpperCase();
  const hasBody = normalizedMethod !== 'GET'
    && normalizedMethod !== 'HEAD'
    && !(body === null || body === undefined || (typeof body === 'string' && body.trim().length === 0));
  const headerEntries = Object.entries(headers || {}).filter(([, value]) => value !== undefined && value !== null && value !== '');

  if (isWindowsPlatform(platform)) {
    const segments = ['curl.exe'];
    if (normalizedMethod !== 'GET') segments.push('-X ' + normalizedMethod);
    segments.push(windowsCmdDoubleQuote(url));
    for (const [name, value] of headerEntries) segments.push('-H ' + windowsCmdDoubleQuote(name + ': ' + value));
    if (hasBody) segments.push('--data-raw ' + windowsCmdDoubleQuote(compactJson(body)));
    return {
      label: 'Windows cmd.exe',
      lineSeparator: '^',
      command: joinSegments(segments, ' ^\n  ')
    };
  }

  const expandableHeaders = new Set((shellExpandableHeaders || []).map((value) => String(value).toLowerCase()));
  const segments = ['curl'];
  if (normalizedMethod !== 'GET') segments.push('-X ' + normalizedMethod);
  segments.push(shellSingleQuote(url));
  for (const [name, value] of headerEntries) {
    const headerLine = name + ': ' + value;
    const quotedHeader = expandableHeaders.has(String(name).toLowerCase())
      ? shellDoubleQuote(headerLine)
      : shellSingleQuote(headerLine);
    segments.push('-H ' + quotedHeader);
  }
  if (hasBody) segments.push('--data-raw ' + shellSingleQuote(compactJson(body)));
  return {
    label: 'macOS/Linux shell',
    lineSeparator: '\\',
    command: joinSegments(segments, ' \\\n  ')
  };
}

export function authTokenPlaceholder(platform = clientPlatform()) {
  return isWindowsPlatform(platform) ? '%TEMPO_BEARER%' : '$TEMPO_BEARER';
}
