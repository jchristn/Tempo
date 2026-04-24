import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import LanguageSelector from './LanguageSelector';

function Login() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const [serverUrl, setServerUrl] = useState('http://localhost:8901');
  const [email, setEmail] = useState('admin@tempo.local');
  const [password, setPassword] = useState('password');
  const [tenantId, setTenantId] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    document.title = t('common.actions.signIn') + ' | ' + t('common.appName');
  }, [t]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(serverUrl, { email, password, tenantId: tenantId || null });
    } catch (err) {
      setError(err?.message || t('login.connectError'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-container">
        <div className="login-header">
          <img src="/logo.png" alt={t('common.appName')} className="login-logo" />
          <h1>{t('login.title')}</h1>
          <p>{t('login.subtitle')}</p>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <div className="form-row">
            <label htmlFor="serverUrl">{t('login.serverUrl')}</label>
            <input id="serverUrl" type="text" value={serverUrl} onChange={(e) => setServerUrl(e.target.value)} placeholder="http://localhost:8901" required disabled={loading} />
          </div>

          <div className="form-row">
            <label htmlFor="email">{t('login.email')}</label>
            <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required disabled={loading} />
          </div>

          <div className="form-row">
            <label htmlFor="password">{t('login.password')}</label>
            <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required disabled={loading} />
          </div>

          <div className="form-row">
            <label htmlFor="tenantId">
              {t('login.tenantId')} <span style={{ color: 'var(--color-text-muted)', fontWeight: 400 }}>({t('common.generic.optional')})</span>
            </label>
            <input id="tenantId" type="text" value={tenantId} onChange={(e) => setTenantId(e.target.value)} placeholder={t('login.tenantPlaceholder')} disabled={loading} />
          </div>

          {error && <div className="login-error">{error}</div>}

          <button type="submit" className="button-primary" style={{ width: '100%' }} disabled={loading}>
            {loading ? t('login.connecting') : t('common.actions.signIn')}
          </button>
        </form>

        <div className="login-footer">{t('login.seededCredentials')}</div>

        <div className="login-language-row">
          <LanguageSelector className="login-language-selector" />
        </div>
      </div>
    </div>
  );
}

export default Login;
