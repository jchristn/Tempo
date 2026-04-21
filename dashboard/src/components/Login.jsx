import { useState } from 'react';
import { useAuth } from '../context/AuthContext';

function Login() {
  const { login } = useAuth();
  const [serverUrl, setServerUrl] = useState('http://localhost:8901');
  const [email, setEmail] = useState('admin@tempo.local');
  const [password, setPassword] = useState('password');
  const [tenantId, setTenantId] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(serverUrl, { email, password, tenantId: tenantId || null });
    } catch (err) {
      setError(err?.message || 'Failed to connect. Check the URL and credentials.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-container">
        <div className="login-header">
          <img src="/logo.png" alt="Tempo" className="login-logo" />
          <h1>Tempo</h1>
          <p>Data flow orchestration</p>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <div className="form-row">
            <label htmlFor="serverUrl">Server URL</label>
            <input id="serverUrl" type="text" value={serverUrl} onChange={(e) => setServerUrl(e.target.value)} placeholder="http://localhost:8901" required disabled={loading} />
          </div>

          <div className="form-row">
            <label htmlFor="email">Email</label>
            <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required disabled={loading} />
          </div>

          <div className="form-row">
            <label htmlFor="password">Password</label>
            <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required disabled={loading} />
          </div>

          <div className="form-row">
            <label htmlFor="tenantId">Tenant ID <span style={{ color: 'var(--color-text-muted)', fontWeight: 400 }}>(optional)</span></label>
            <input id="tenantId" type="text" value={tenantId} onChange={(e) => setTenantId(e.target.value)} placeholder="Leave blank for administrator login" disabled={loading} />
          </div>

          {error && <div className="login-error">{error}</div>}

          <button type="submit" className="button-primary" style={{ width: '100%' }} disabled={loading}>
            {loading ? 'Connecting…' : 'Sign in'}
          </button>
        </form>

        <div className="login-footer">Default seeded credentials: admin@tempo.local / password</div>
      </div>
    </div>
  );
}

export default Login;
