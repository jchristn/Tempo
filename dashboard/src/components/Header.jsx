import { useTranslation } from 'react-i18next';

function Header({ title, subtitle, theme, onToggleTheme, onToggleSidebar, onLogout, principal }) {
  const { t } = useTranslation();
  return (
    <header className="dashboard-header">
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
        <button className="button-ghost" onClick={onToggleSidebar} aria-label={t('navigation.toggleSidebar')} style={{ padding: '0.375rem' }}>
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M3 12h18M3 6h18M3 18h18" /></svg>
        </button>
        <div>
          <h1>{title}</h1>
          {subtitle && <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-secondary)' }}>{subtitle}</div>}
        </div>
      </div>
      <div className="header-actions">
        {principal && (
          <span style={{ fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }}>
            {principal.email || principal.id}
          </span>
        )}
        <button className="button-ghost" onClick={onToggleTheme} title={t('common.theme.toggle')} aria-label={t('common.theme.toggle')}>
          {theme === 'dark' ? (
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" /></svg>
          ) : (
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 12.79A9 9 0 1111.21 3 7 7 0 0021 12.79z" /></svg>
          )}
        </button>
        <button className="button-secondary" onClick={onLogout}>{t('components.topbar.signOut')}</button>
      </div>
    </header>
  );
}

export default Header;
