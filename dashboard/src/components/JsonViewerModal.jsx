import Modal from './Modal';
import CopyButton from './CopyButton';
import ModalRecordId from './ModalRecordId';

function pickId(value) {
  if (!value || typeof value !== 'object') return '';
  return value.id || value.Id || '';
}

function JsonViewerModal({ open, onClose, title = 'JSON', value }) {
  const text = typeof value === 'string' ? value : JSON.stringify(value ?? {}, null, 2);
  const recordId = pickId(value);
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      size="large"
      headerMeta={recordId ? <ModalRecordId value={recordId} /> : null}
    >
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '0.5rem' }}>
        <CopyButton value={text} />
      </div>
      <pre className="code-block" style={{ maxHeight: '60vh' }}>{text}</pre>
    </Modal>
  );
}

export default JsonViewerModal;
