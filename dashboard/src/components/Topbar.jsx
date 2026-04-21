import { useAuth } from '../context/AuthContext';
import { GithubIcon, LogoutIcon, MenuIcon, MoonIcon, SunIcon } from './Icons';

const GITHUB_URL = 'https://github.com/jchristn/tempo';
const GITHUB_SIZE = 26;
const ICON_SIZE = 36;

function Topbar({ theme, onToggleTheme, onToggleSidebar, onLogout, principal }) {
  const { serverUrl } = useAuth();
  const principalLabel = principal ? (principal.email || principal.id || 'Authenticated') : '';
  return (
    <header className="dashboard-header">
      <div className="topbar">
        <div className="topbar-left">
          {principalLabel && (
            <span className="topbar-principal" title={principal.id || principal.email || 'Authenticated principal'}>
              {principalLabel}
            </span>
          )}
          {serverUrl && (
            <span className="topbar-server" title={'Tempo server at ' + serverUrl}>
              <span className="topbar-server-sep">@</span>
              <code>{serverUrl}</code>
            </span>
          )}
        </div>
        <div className="topbar-right">
          <button className="topbar-icon-button" onClick={onToggleSidebar} aria-label="Toggle sidebar" title="Toggle sidebar">
            <MenuIcon size={ICON_SIZE} />
          </button>
          <a className="topbar-icon-button" href={GITHUB_URL} target="_blank" rel="noopener noreferrer" title="View Tempo on GitHub" aria-label="View Tempo on GitHub">
            <GithubIcon size={GITHUB_SIZE} />
          </a>
          <button className="topbar-icon-button" onClick={onToggleTheme} title={'Switch to ' + (theme === 'dark' ? 'light' : 'dark') + ' theme'} aria-label="Toggle theme">
            {theme === 'dark' ? <SunIcon size={ICON_SIZE} /> : <MoonIcon size={ICON_SIZE} />}
          </button>
          <button className="topbar-icon-button" onClick={onLogout} title="Sign out" aria-label="Sign out">
            <LogoutIcon size={ICON_SIZE} />
          </button>
        </div>
      </div>
    </header>
  );
}

export default Topbar;
