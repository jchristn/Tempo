import Modal from './Modal';
import CopyButton from './CopyButton';

function JsonViewerModal({ open, onClose, title = 'JSON', value }) {
  const text = typeof value === 'string' ? value : JSON.stringify(value ?? {}, null, 2);
  return (
    <Modal open={open} onClose={onClose} title={title} size="large">
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '0.5rem' }}>
        <CopyButton value={text} />
      </div>
      <pre className="code-block" style={{ maxHeight: '60vh' }}>{text}</pre>
    </Modal>
  );
}

export default JsonViewerModal;
