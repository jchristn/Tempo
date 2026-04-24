import Modal from './Modal';
import CopyButton from './CopyButton';
import ModalRecordId from './ModalRecordId';
import { useTranslation } from 'react-i18next';
import { translateLiteral } from '../utils/i18n';

function pickId(value) {
  if (!value || typeof value !== 'object') return '';
  return value.id || value.Id || '';
}

function JsonViewerModal({ open, onClose, title, value }) {
  const { t } = useTranslation();
  const text = typeof value === 'string' ? value : JSON.stringify(value ?? {}, null, 2);
  const recordId = pickId(value);
  const resolvedTitle = title ? translateLiteral(t, title) : t('components.modal.json');
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={resolvedTitle}
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
