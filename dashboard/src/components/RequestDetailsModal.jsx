import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Modal from './Modal';
import MethodPill from './MethodPill';
import StatusPill from './StatusPill';
import CopyableId from './CopyableId';
import CopyButton from './CopyButton';
import ModalRecordId from './ModalRecordId';
import { formatDuration, formatTime } from '../utils/formatters';
import { normalizeApiError, translateLiteral } from '../utils/i18n';

function Section({ title, extra, children, defaultOpen = true }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="details-section">
      <div className="details-section-header" onClick={() => setOpen((o) => !o)}>
        <span>{title}</span>
        <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          {extra}
          <span style={{ color: 'var(--color-text-muted)' }}>{open ? '-' : '+'}</span>
        </span>
      </div>
      {open && <div className="details-section-body">{children}</div>}
    </div>
  );
}

function RequestDetailsModal({ entryId, open, onClose, apiClient }) {
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
  const [entry, setEntry] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!open || !entryId || !apiClient) { setEntry(null); setError(null); return; }
    let cancelled = false;
    apiClient.getRequestHistoryEntry(entryId)
      .then((e) => { if (!cancelled) setEntry(e); })
      .catch((err) => { if (!cancelled) setError(normalizeApiError(err, t)); });
    return () => { cancelled = true; };
  }, [entryId, open, apiClient]);

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="large"
      title={tl('Request details')}
      headerMeta={<ModalRecordId label={tl('Request ID')} value={entry?.id || entryId} />}
    >
      {error && <div className="login-error">{error}</div>}
      {!entry && !error && <div className="loading-spinner" style={{ margin: '2rem auto' }} />}
      {entry && (
        <>
          <Section title={tl('Metadata')}>
            <dl className="details-kv">
              <dt>{tl('ID')}</dt><dd><CopyableId value={entry.id} max={40} /></dd>
              <dt>{tl('Method / Status')}</dt><dd><MethodPill method={entry.method} /> <StatusPill code={entry.statusCode} /></dd>
              <dt>{tl('Path')}</dt><dd className="monospace">{entry.path}</dd>
              <dt>{tl('URL')}</dt><dd className="monospace">{entry.url}</dd>
              <dt>{tl('Created')}</dt><dd>{formatTime(entry.createdUtc)}</dd>
              <dt>{tl('Completed')}</dt><dd>{formatTime(entry.completedUtc)}</dd>
              <dt>{tl('Duration')}</dt><dd>{formatDuration(entry.durationMs)}</dd>
              <dt>{tl('Source IP')}</dt><dd>{entry.sourceIp || '-'}</dd>
              <dt>{tl('Principal')}</dt><dd>{entry.principalName || '-'}</dd>
              <dt>{tl('Tenant')}</dt><dd><CopyableId value={entry.tenantId} /></dd>
              <dt>{tl('User')}</dt><dd><CopyableId value={entry.userId} /></dd>
            </dl>
          </Section>

          <Section title={tl('Request headers')}
            extra={<CopyButton value={JSON.stringify(entry.requestHeaders || {}, null, 2)} />}>
            <pre className="code-block">{JSON.stringify(entry.requestHeaders || {}, null, 2)}</pre>
          </Section>

          <Section
            title={entry.requestBodyTruncated
              ? tl('Request body (truncated - {{count}} bytes)', { count: entry.requestBodyBytes })
              : tl('Request body')}
            extra={entry.requestBody ? <CopyButton value={entry.requestBody} /> : null}
          >
            <pre className="code-block">{entry.requestBody || t('common.generic.empty')}</pre>
          </Section>

          <Section title={tl('Response headers')} defaultOpen={false}
            extra={<CopyButton value={JSON.stringify(entry.responseHeaders || {}, null, 2)} />}>
            <pre className="code-block">{JSON.stringify(entry.responseHeaders || {}, null, 2)}</pre>
          </Section>

          <Section
            title={entry.responseBodyTruncated
              ? tl('Response body (truncated - {{count}} bytes)', { count: entry.responseBodyBytes })
              : tl('Response body')}
            defaultOpen={Boolean(entry.responseBody)}
            extra={entry.responseBody ? <CopyButton value={entry.responseBody} /> : null}
          >
            <pre className="code-block">{entry.responseBody || t('common.generic.empty')}</pre>
          </Section>

          <Section title={tl('Raw JSON')} defaultOpen={false}
            extra={<CopyButton value={JSON.stringify(entry, null, 2)} />}>
            <pre className="code-block">{JSON.stringify(entry, null, 2)}</pre>
          </Section>
        </>
      )}
    </Modal>
  );
}

export default RequestDetailsModal;
