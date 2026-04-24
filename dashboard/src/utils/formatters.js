/**
 * Formatting helpers shared across views.
 */
import i18n from '../i18n';

function resolveLocale(locale) {
  return locale || i18n.resolvedLanguage || i18n.language || 'en';
}

function formatNumeric(value, options, locale) {
  return new Intl.NumberFormat(resolveLocale(locale), options).format(value);
}

export function formatNumber(value, options = {}, locale) {
  if (value == null || Number.isNaN(value)) return '-';
  return formatNumeric(Number(value), options, locale);
}

export function formatDuration(ms, locale) {
  if (ms == null || Number.isNaN(ms)) return '-';
  if (ms < 1) return formatNumeric(ms, { maximumFractionDigits: 2, minimumFractionDigits: 2 }, locale) + ' ' + i18n.t('common.units.millisecondsShort');
  if (ms < 1000) return formatNumeric(ms, { maximumFractionDigits: 0 }, locale) + ' ' + i18n.t('common.units.millisecondsShort');
  if (ms < 60000) return formatNumeric(ms / 1000, { maximumFractionDigits: 2, minimumFractionDigits: 0 }, locale) + ' ' + i18n.t('common.units.secondsShort');
  return formatNumeric(ms / 60000, { maximumFractionDigits: 1, minimumFractionDigits: 0 }, locale) + ' ' + i18n.t('common.units.minutesShort');
}

export function formatTime(utcString, locale, options = {}) {
  if (!utcString) return '-';
  try {
    return new Intl.DateTimeFormat(resolveLocale(locale), {
      dateStyle: 'medium',
      timeStyle: 'short',
      ...options
    }).format(new Date(utcString));
  } catch {
    return utcString;
  }
}

export function formatDate(utcString, locale, options = {}) {
  if (!utcString) return '-';
  try {
    return new Intl.DateTimeFormat(resolveLocale(locale), {
      dateStyle: 'medium',
      ...options
    }).format(new Date(utcString));
  } catch {
    return utcString;
  }
}

export function formatRelative(utcString, locale) {
  if (!utcString) return '-';
  try {
    const then = new Date(utcString).getTime();
    const diffSeconds = Math.round((then - Date.now()) / 1000);
    if (Math.abs(diffSeconds) < 5) return i18n.t('common.relativeTime.now');

    const rtf = new Intl.RelativeTimeFormat(resolveLocale(locale), { numeric: 'auto' });
    if (Math.abs(diffSeconds) < 60) return rtf.format(diffSeconds, 'second');
    const diffMinutes = Math.round(diffSeconds / 60);
    if (Math.abs(diffMinutes) < 60) return rtf.format(diffMinutes, 'minute');
    const diffHours = Math.round(diffMinutes / 60);
    if (Math.abs(diffHours) < 24) return rtf.format(diffHours, 'hour');
    return rtf.format(Math.round(diffHours / 24), 'day');
  } catch {
    return utcString;
  }
}

export function formatBytes(value, locale) {
  const n = Number(value || 0);
  if (n < 1024) return formatNumeric(n, { maximumFractionDigits: 0 }, locale) + ' ' + i18n.t('common.units.bytesShort');
  if (n < 1024 * 1024) return formatNumeric(n / 1024, { maximumFractionDigits: 1 }, locale) + ' ' + i18n.t('common.units.kilobytesShort');
  if (n < 1024 * 1024 * 1024) return formatNumeric(n / (1024 * 1024), { maximumFractionDigits: 1 }, locale) + ' ' + i18n.t('common.units.megabytesShort');
  return formatNumeric(n / (1024 * 1024 * 1024), { maximumFractionDigits: 1 }, locale) + ' ' + i18n.t('common.units.gigabytesShort');
}

export function formatBoolean(value) {
  return value ? i18n.t('common.boolean.yes') : i18n.t('common.boolean.no');
}

export function formatList(values, locale, options = {}) {
  const items = (values || []).filter(Boolean);
  if (items.length < 1) return '-';
  return new Intl.ListFormat(resolveLocale(locale), { style: 'long', type: 'conjunction', ...options }).format(items);
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
  return value.length > len ? value.slice(0, len - 1) + '...' : value;
}

export function isoOrNull(value) {
  if (!value) return null;
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return null;
  return d.toISOString();
}
