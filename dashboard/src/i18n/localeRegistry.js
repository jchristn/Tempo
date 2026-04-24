export const LOCALE_STORAGE_KEY = 'tempo.locale';
export const DEFAULT_LOCALE = 'en';

const LOCALE_METADATA = {
  en: { code: 'en', dir: 'ltr', nativeName: 'English' },
  es: { code: 'es', dir: 'ltr', nativeName: 'Espa\u00f1ol' },
  'zh-Hans': { code: 'zh-Hans', dir: 'ltr', nativeName: '\u7b80\u4f53\u4e2d\u6587' },
  'yue-Hant-HK': { code: 'yue-Hant-HK', dir: 'ltr', nativeName: '\u5ee3\u6771\u8a71' },
  ja: { code: 'ja', dir: 'ltr', nativeName: '\u65e5\u672c\u8a9e' },
  de: { code: 'de', dir: 'ltr', nativeName: 'Deutsch' },
  fr: { code: 'fr', dir: 'ltr', nativeName: 'Fran\u00e7ais' },
  it: { code: 'it', dir: 'ltr', nativeName: 'Italiano' },
  'zh-Hant-TW': { code: 'zh-Hant-TW', dir: 'ltr', nativeName: '\u7e41\u9ad4\u4e2d\u6587' }
};

export const SUPPORTED_LOCALES = Object.keys(LOCALE_METADATA);

function startsWith(normalized, prefix) {
  return normalized === prefix || normalized.startsWith(prefix + '-');
}

export function normalizeLocale(value) {
  const raw = String(value || '').trim();
  if (!raw) return DEFAULT_LOCALE;

  const normalized = raw.replace(/_/g, '-');
  const lower = normalized.toLowerCase();

  if (lower === 'kanji') return 'ja';
  if (startsWith(lower, 'en')) return 'en';
  if (startsWith(lower, 'es')) return 'es';
  if (startsWith(lower, 'ja')) return 'ja';
  if (startsWith(lower, 'de')) return 'de';
  if (startsWith(lower, 'fr')) return 'fr';
  if (startsWith(lower, 'it')) return 'it';
  if (startsWith(lower, 'yue')) return 'yue-Hant-HK';
  if (lower === 'zh-hk' || lower === 'zh-mo') return 'yue-Hant-HK';
  if (lower === 'zh-tw' || lower === 'zh-hant' || startsWith(lower, 'zh-hant')) return 'zh-Hant-TW';
  if (lower === 'zh-cn' || lower === 'zh-sg' || lower === 'zh' || lower === 'zh-hans' || startsWith(lower, 'zh-hans')) return 'zh-Hans';

  return SUPPORTED_LOCALES.includes(normalized) ? normalized : DEFAULT_LOCALE;
}

export function getLocaleMeta(locale) {
  return LOCALE_METADATA[normalizeLocale(locale)] || LOCALE_METADATA[DEFAULT_LOCALE];
}

export function getDirectionForLocale(locale) {
  return getLocaleMeta(locale).dir;
}

export function getSupportedLocales() {
  return SUPPORTED_LOCALES.map((locale) => LOCALE_METADATA[locale]);
}
