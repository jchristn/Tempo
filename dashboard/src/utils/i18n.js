import i18n from '../i18n';
import { formatList } from './formatters';

function resolveTranslator(t) {
  return typeof t === 'function' ? t : i18n.t.bind(i18n);
}

function unique(items) {
  return Array.from(new Set(items.filter(Boolean)));
}

function titleCase(text) {
  return text.replace(/\b([a-z])/g, (match) => match.toUpperCase());
}

function literalVariants(text) {
  const normalized = String(text || '').replace(/\s+/g, ' ').trim();
  if (!normalized) return [];

  const deunderscored = normalized.replace(/_/g, ' ');
  const lower = deunderscored.toLowerCase();
  const sentence = lower ? lower.charAt(0).toUpperCase() + lower.slice(1) : lower;
  const titled = titleCase(lower);

  return unique([
    normalized,
    deunderscored,
    sentence,
    titled,
    normalized.toUpperCase(),
    normalized.toLowerCase()
  ]);
}

export function translateLiteral(t, value, options = {}) {
  if (value == null) return '';
  const translate = resolveTranslator(t);
  const text = String(value);

  for (const candidate of literalVariants(text)) {
    const translated = translate(candidate, {
      ...options,
      defaultValue: candidate,
      keySeparator: false
    });
    if (translated !== candidate) return translated;
  }

  return translate(text, {
    ...options,
    defaultValue: text,
    keySeparator: false
  });
}

export function normalizeApiError(err, t) {
  const fallback = translateLiteral(t, 'Request failed.');
  if (!err) return fallback;
  if (err.body) {
    try {
      const parsed = JSON.parse(err.body);
      return parsed.message || parsed.details || err.message || fallback;
    } catch {
      return err.body;
    }
  }
  return err.message || String(err) || fallback;
}

export function translateEnum(t, keyPrefix, value, options = {}) {
  if (value == null || value === '') return options.empty ?? translateLiteral(t, '-');
  return resolveTranslator(t)(keyPrefix + '.' + String(value), {
    defaultValue: translateLiteral(t, value),
    ...options
  });
}

export function translateEnumList(t, keyPrefix, values, locale, options = {}) {
  const items = (values || [])
    .filter((value) => value != null && value !== '')
    .map((value) => translateEnum(t, keyPrefix, value, options));

  if (items.length < 1) return options.empty ?? translateLiteral(t, '-');
  return formatList(items, locale, options.listOptions);
}
