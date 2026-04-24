import { useAuth } from '../context/AuthContext';
import { useTranslation } from 'react-i18next';
import LanguageSelector from './LanguageSelector';
import { GithubIcon, LogoutIcon, MenuIcon, MoonIcon, SunIcon } from './Icons';

const GITHUB_URL = 'https://github.com/jchristn/tempo';
const GITHUB_SIZE = 26;
const ICON_SIZE = 36;

function Topbar({ theme, onToggleTheme, onToggleSidebar, onLogout, principal }) {
  const { t } = useTranslation();
  const { serverUrl } = useAuth();
  const principalLabel = principal ? (principal.email || principal.id || t('navigation.authenticatedPrincipal')) : '';
  return (
    <header className="dashboard-header">
      <div className="topbar">
        <div className="topbar-left">
          {principalLabel && (
            <span className="topbar-principal" title={principal.id || principal.email || t('navigation.authenticatedPrincipal')}>
              {principalLabel}
            </span>
          )}
          {serverUrl && (
            <span className="topbar-server" title={t('navigation.serverAt', { serverUrl })}>
              <span className="topbar-server-sep">@</span>
              <code>{serverUrl}</code>
            </span>
          )}
        </div>
        <div className="topbar-right">
          <LanguageSelector compact className="topbar-language-selector" />
          <button className="topbar-icon-button" onClick={onToggleSidebar} aria-label={t('navigation.toggleSidebar')} title={t('navigation.toggleSidebar')}>
            <MenuIcon size={ICON_SIZE} />
          </button>
          <a className="topbar-icon-button" href={GITHUB_URL} target="_blank" rel="noopener noreferrer" title={t('navigation.github')} aria-label={t('navigation.github')}>
            <GithubIcon size={GITHUB_SIZE} />
          </a>
          <button
            className="topbar-icon-button"
            onClick={onToggleTheme}
            title={t('common.theme.switchTo', { theme: t(theme === 'dark' ? 'common.theme.light' : 'common.theme.dark') })}
            aria-label={t('common.theme.toggle')}
          >
            {theme === 'dark' ? <SunIcon size={ICON_SIZE} /> : <MoonIcon size={ICON_SIZE} />}
          </button>
          <button className="topbar-icon-button" onClick={onLogout} title={t('components.topbar.signOut')} aria-label={t('components.topbar.signOut')}>
            <LogoutIcon size={ICON_SIZE} />
          </button>
        </div>
      </div>
    </header>
  );
}

export default Topbar;
