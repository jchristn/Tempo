import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import i18n from '../i18n';
import ApiClient from '../utils/api';

const AuthContext = createContext(null);

const KEY_URL = 'tempo.server.url';
const KEY_TOKEN = 'tempo.auth.token';
const KEY_THEME = 'tempo.theme';

function translateLiteral(value, options = {}) {
  const text = String(value || '');
  return i18n.t(text, {
    defaultValue: text,
    keySeparator: false,
    ...options
  });
}

export function AuthProvider({ children }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [apiClient, setApiClient] = useState(null);
  const [serverUrl, setServerUrl] = useState('');
  const [token, setToken] = useState('');
  const [principal, setPrincipal] = useState(null);
  const [theme, setTheme] = useState('light');

  useEffect(() => {
    const savedUrl = localStorage.getItem(KEY_URL) || '';
    const savedToken = localStorage.getItem(KEY_TOKEN) || '';
    const savedTheme = localStorage.getItem(KEY_THEME) || 'light';
    setTheme(savedTheme);
    document.body.setAttribute('data-theme', savedTheme);

    const tryResume = async () => {
      if (!savedUrl || !savedToken) { setIsLoading(false); return; }
      try {
        const client = new ApiClient(savedUrl, savedToken);
        await client.validateToken();
        const who = await client.me().catch(() => null);
        setServerUrl(savedUrl);
        setToken(savedToken);
        setApiClient(client);
        setPrincipal(who);
        setIsAuthenticated(true);
      } catch {
        localStorage.removeItem(KEY_URL);
        localStorage.removeItem(KEY_TOKEN);
      } finally {
        setIsLoading(false);
      }
    };
    tryResume();
  }, []);

  useEffect(() => {
    const handler = () => logout();
    window.addEventListener('auth:unauthorized', handler);
    return () => window.removeEventListener('auth:unauthorized', handler);
  }, []);

  const login = useCallback(async (url, credentials) => {
    const tempClient = new ApiClient(url, null);
    const payload = await tempClient.login(credentials.email, credentials.password, credentials.tenantId || null);
    const newToken = payload && payload.token;
    if (!newToken) throw new Error(translateLiteral('Login did not return a token.'));
    const client = new ApiClient(url, newToken);
    const who = await client.me().catch(() => null);
    localStorage.setItem(KEY_URL, url);
    localStorage.setItem(KEY_TOKEN, newToken);
    setServerUrl(url);
    setToken(newToken);
    setApiClient(client);
    setPrincipal(who);
    setIsAuthenticated(true);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem(KEY_URL);
    localStorage.removeItem(KEY_TOKEN);
    setServerUrl('');
    setToken('');
    setApiClient(null);
    setPrincipal(null);
    setIsAuthenticated(false);
  }, []);

  const toggleTheme = useCallback(() => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);
    localStorage.setItem(KEY_THEME, next);
    document.body.setAttribute('data-theme', next);
  }, [theme]);

  const value = {
    isAuthenticated, isLoading, apiClient, serverUrl, token, principal, theme,
    login, logout, toggleTheme
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error(translateLiteral('useAuth must be used inside AuthProvider'));
  return ctx;
}

export default AuthContext;
