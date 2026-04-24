import { useTranslation } from 'react-i18next';
import { getSupportedLocales, normalizeLocale } from '../i18n/localeRegistry';

function LanguageSelector({ compact = false, className = '' }) {
  const { t, i18n } = useTranslation();
  const locales = getSupportedLocales();
  const value = normalizeLocale(i18n.resolvedLanguage || i18n.language);

  return (
    <label className={'language-selector' + (compact ? ' compact' : '') + (className ? ' ' + className : '')}>
      {!compact && <span className="language-selector-label">{t('common.language.label')}</span>}
      <select
        aria-label={t('common.language.selector')}
        title={t('common.language.selector')}
        value={value}
        onChange={(e) => { void i18n.changeLanguage(e.target.value); }}
      >
        {locales.map((locale) => (
          <option key={locale.code} value={locale.code}>{locale.nativeName}</option>
        ))}
      </select>
    </label>
  );
}

export default LanguageSelector;
