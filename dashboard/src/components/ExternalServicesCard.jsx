import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import CopyButton from './CopyButton';
import { getExternalServices } from '../utils/constants';

const PROBE_TIMEOUT_MS = 3000;

async function probe(url) {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), PROBE_TIMEOUT_MS);
  try {
    await fetch(url, { mode: 'no-cors', cache: 'no-store', signal: controller.signal });
    return 'reachable';
  } catch {
    return 'unreachable';
  } finally {
    window.clearTimeout(timer);
  }
}

function ExternalServicesCard() {
  const { t } = useTranslation();
  const [services] = useState(() => getExternalServices());
  const [status, setStatus] = useState({});

  useEffect(() => {
    let cancelled = false;
    services.forEach((service) => {
      probe(service.url + service.healthPath).then((result) => {
        if (!cancelled) setStatus((previous) => ({ ...previous, [service.id]: result }));
      });
    });
    return () => { cancelled = true; };
  }, [services]);

  const statusLabel = (id) => {
    const value = status[id];
    if (value === 'reachable') return t('views.home.externalServices.reachable');
    if (value === 'unreachable') return t('views.home.externalServices.unreachable');
    return t('views.home.externalServices.checking');
  };

  return (
    <div className="card external-services-card">
      <div className="card-header">
        <div>
          <div className="card-title">{t('views.home.externalServices.title')}</div>
          <div className="external-services-subtitle">{t('views.home.externalServices.subtitle')}</div>
        </div>
      </div>
      <div className="data-table-wrapper">
        <table className="data-table">
          <thead>
            <tr>
              <th>{t('views.home.externalServices.service')}</th>
              <th>{t('views.home.externalServices.url')}</th>
              <th>{t('views.home.externalServices.credentials')}</th>
              <th>{t('views.home.externalServices.status')}</th>
            </tr>
          </thead>
          <tbody>
            {services.map((service) => (
              <tr key={service.id} className={status[service.id] === 'unreachable' ? 'external-service-unavailable' : ''}>
                <td>
                  <div className="external-service-name">{service.name}</div>
                  <div className="external-service-purpose">{t('views.home.externalServices.' + service.purposeKey)}</div>
                </td>
                <td className="monospace">
                  <a href={service.url} target="_blank" rel="noopener noreferrer">{service.url}</a>
                  <CopyButton value={service.url} />
                </td>
                <td>
                  {service.credentials
                    ? (<span className="monospace external-service-token">{service.credentials}</span>)
                    : t('views.home.externalServices.noLogin')}
                </td>
                <td>
                  <span className={'external-service-status is-' + (status[service.id] || 'checking')}>{statusLabel(service.id)}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default ExternalServicesCard;
