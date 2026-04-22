import { useEffect, useState } from 'react';
import Modal from './Modal';
import MethodPill from './MethodPill';
import StatusPill from './StatusPill';
import CopyableId from './CopyableId';
import CopyButton from './CopyButton';
import ModalRecordId from './ModalRecordId';
import { formatDuration, formatTime } from '../utils/formatters';

function Section({ title, extra, children, defaultOpen = true }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="details-section">
      <div className="details-section-header" onClick={() => setOpen((o) => !o)}>
        <span>{title}</span>
        <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          {extra}
          <span style={{ color: 'var(--color-text-muted)' }}>{open ? '−' : '+'}</span>
        </span>
      </div>
      {open && <div className="details-section-body">{children}</div>}
    </div>
  );
}

function RequestDetailsModal({ entryId, open, onClose, apiClient }) {
  const [entry, setEntry] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!open || !entryId || !apiClient) { setEntry(null); setError(null); return; }
    let cancelled = false;
    apiClient.getRequestHistoryEntry(entryId)
      .then((e) => { if (!cancelled) setEntry(e); })
      .catch((err) => { if (!cancelled) setError(err.message); });
    return () => { cancelled = true; };
  }, [entryId, open, apiClient]);

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="large"
      title="Request details"
      headerMeta={<ModalRecordId label="Request ID" value={entry?.id || entryId} />}
    >
      {error && <div className="login-error">{error}</div>}
      {!entry && !error && <div className="loading-spinner" style={{ margin: '2rem auto' }} />}
      {entry && (
        <>
          <Section title="Metadata">
            <dl className="details-kv">
              <dt>ID</dt><dd><CopyableId value={entry.id} max={40} /></dd>
              <dt>Method / Status</dt><dd><MethodPill method={entry.method} /> <StatusPill code={entry.statusCode} /></dd>
              <dt>Path</dt><dd className="monospace">{entry.path}</dd>
              <dt>URL</dt><dd className="monospace">{entry.url}</dd>
              <dt>Created</dt><dd>{formatTime(entry.createdUtc)}</dd>
              <dt>Completed</dt><dd>{formatTime(entry.completedUtc)}</dd>
              <dt>Duration</dt><dd>{formatDuration(entry.durationMs)}</dd>
              <dt>Source IP</dt><dd>{entry.sourceIp || '-'}</dd>
              <dt>Principal</dt><dd>{entry.principalName || '-'}</dd>
              <dt>Tenant</dt><dd><CopyableId value={entry.tenantId} /></dd>
              <dt>User</dt><dd><CopyableId value={entry.userId} /></dd>
            </dl>
          </Section>

          <Section title="Request headers"
            extra={<CopyButton value={JSON.stringify(entry.requestHeaders || {}, null, 2)} />}>
            <pre className="code-block">{JSON.stringify(entry.requestHeaders || {}, null, 2)}</pre>
          </Section>

          <Section
            title={'Request body' + (entry.requestBodyTruncated ? ' (truncated · ' + entry.requestBodyBytes + ' bytes)' : '')}
            extra={entry.requestBody ? <CopyButton value={entry.requestBody} /> : null}
          >
            <pre className="code-block">{entry.requestBody || '(empty)'}</pre>
          </Section>

          <Section title="Response headers" defaultOpen={false}
            extra={<CopyButton value={JSON.stringify(entry.responseHeaders || {}, null, 2)} />}>
            <pre className="code-block">{JSON.stringify(entry.responseHeaders || {}, null, 2)}</pre>
          </Section>

          <Section
            title={'Response body' + (entry.responseBodyTruncated ? ' (truncated · ' + entry.responseBodyBytes + ' bytes)' : '')}
            defaultOpen={Boolean(entry.responseBody)}
            extra={entry.responseBody ? <CopyButton value={entry.responseBody} /> : null}
          >
            <pre className="code-block">{entry.responseBody || '(empty)'}</pre>
          </Section>

          <Section title="Raw JSON" defaultOpen={false}
            extra={<CopyButton value={JSON.stringify(entry, null, 2)} />}>
            <pre className="code-block">{JSON.stringify(entry, null, 2)}</pre>
          </Section>
        </>
      )}
    </Modal>
  );
}

export default RequestDetailsModal;
