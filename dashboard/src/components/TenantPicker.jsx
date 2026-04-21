import { useEffect, useState } from 'react';

function TenantPicker({ apiClient, value, onChange }) {
  const [tenants, setTenants] = useState([]);
  useEffect(() => {
    if (!apiClient) return;
    apiClient.listTenants({ pageSize: 500 }).then((d) => {
      setTenants(d.items || []);
      if (!value && d.items && d.items.length) onChange(d.items[0].id);
    }).catch(() => {});
  }, [apiClient]);
  return (
    <select value={value || ''} onChange={(e) => onChange(e.target.value)} style={{ width: 'auto', minWidth: 220 }}>
      {tenants.map((t) => <option key={t.id} value={t.id}>{t.name} ({t.id.slice(0, 12)}…)</option>)}
    </select>
  );
}

export default TenantPicker;
