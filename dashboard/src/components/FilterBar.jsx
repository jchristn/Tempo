import { useTranslation } from 'react-i18next';
import { HTTP_METHODS } from '../utils/constants';
import { translateLiteral } from '../utils/i18n';

function FilterBar({ filters, setFilters, onClear, showTenant = false, showUser = false }) {
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
  const update = (key, value) => setFilters((f) => ({ ...f, [key]: value }));
  return (
    <div className="filter-bar">
      <div className="field">
        <label>{tl('Method')}</label>
        <select value={filters.method || ''} onChange={(e) => update('method', e.target.value)}>
          <option value="">{tl('Any')}</option>
          {HTTP_METHODS.map((m) => <option key={m} value={m}>{m}</option>)}
        </select>
      </div>
      <div className="field">
        <label>{tl('Status code')}</label>
        <input type="text" value={filters.statusCode || ''} onChange={(e) => update('statusCode', e.target.value)} placeholder={tl('e.g. 500')} />
      </div>
      <div className="field">
        <label>{tl('Path contains')}</label>
        <input type="text" value={filters.pathContains || ''} onChange={(e) => update('pathContains', e.target.value)} placeholder={tl('e.g. /flows')} />
      </div>
      <div className="field">
        <label>{tl('From (UTC)')}</label>
        <input type="datetime-local" value={filters.fromUtc || ''} onChange={(e) => update('fromUtc', e.target.value)} />
      </div>
      <div className="field">
        <label>{tl('To (UTC)')}</label>
        <input type="datetime-local" value={filters.toUtc || ''} onChange={(e) => update('toUtc', e.target.value)} />
      </div>
      {showTenant && (
        <div className="field">
          <label>{tl('Tenant ID')}</label>
          <input type="text" value={filters.tenantId || ''} onChange={(e) => update('tenantId', e.target.value)} />
        </div>
      )}
      {showUser && (
        <div className="field">
          <label>{tl('User ID')}</label>
          <input type="text" value={filters.userId || ''} onChange={(e) => update('userId', e.target.value)} />
        </div>
      )}
      <button className="button-secondary" onClick={onClear}>{t('common.actions.clear')}</button>
    </div>
  );
}

export default FilterBar;
