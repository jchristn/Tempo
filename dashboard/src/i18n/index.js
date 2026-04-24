import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';
import { DEFAULT_LOCALE, getDirectionForLocale, LOCALE_STORAGE_KEY, normalizeLocale } from './localeRegistry';
import { resources } from './resources';

function syncDocumentLanguage(nextLocale) {
  if (typeof document === 'undefined') return;
  const locale = normalizeLocale(nextLocale);
  document.documentElement.lang = locale;
  document.documentElement.dir = getDirectionForLocale(locale);
}

if (!i18n.isInitialized) {
  i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
      resources,
      lng: DEFAULT_LOCALE,
      fallbackLng: DEFAULT_LOCALE,
      supportedLngs: Object.keys(resources),
      defaultNS: 'translation',
      ns: ['translation'],
      returnNull: false,
      returnEmptyString: false,
      interpolation: {
        escapeValue: false
      },
      detection: {
        order: ['querystring', 'localStorage', 'navigator'],
        lookupQuerystring: 'lang',
        lookupLocalStorage: LOCALE_STORAGE_KEY,
        caches: ['localStorage'],
        convertDetectedLanguage: normalizeLocale
      }
    });
}

syncDocumentLanguage(i18n.resolvedLanguage || i18n.language);
i18n.on('languageChanged', syncDocumentLanguage);

export default i18n;
