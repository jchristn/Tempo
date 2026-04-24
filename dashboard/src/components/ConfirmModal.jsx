import Modal from './Modal';
import ModalRecordId from './ModalRecordId';
import { useTranslation } from 'react-i18next';
import { translateLiteral } from '../utils/i18n';

function ConfirmModal({
  open,
  title,
  message,
  confirmLabel,
  cancelLabel,
  danger = false,
  recordId = '',
  recordIdLabel,
  onConfirm,
  onCancel
}) {
  const { t } = useTranslation();
  const resolvedTitle = title ? translateLiteral(t, title) : t('common.actions.confirm');
  const resolvedConfirmLabel = confirmLabel ? translateLiteral(t, confirmLabel) : t('common.actions.confirm');
  const resolvedCancelLabel = cancelLabel ? translateLiteral(t, cancelLabel) : t('common.actions.cancel');
  const resolvedRecordIdLabel = recordIdLabel ? translateLiteral(t, recordIdLabel) : t('components.modal.id');
  const resolvedMessage = typeof message === 'string' ? translateLiteral(t, message) : message;

  return (
    <Modal
      open={open}
      size="small"
      onClose={onCancel}
      title={resolvedTitle}
      headerMeta={recordId ? <ModalRecordId label={resolvedRecordIdLabel} value={recordId} /> : null}
      footer={
        <>
          <button className="button-secondary" onClick={onCancel}>{resolvedCancelLabel}</button>
          <button className={danger ? 'button-danger' : 'button-primary'} onClick={onConfirm}>{resolvedConfirmLabel}</button>
        </>
      }
    >
      <div style={{ fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }}>{resolvedMessage}</div>
    </Modal>
  );
}

export default ConfirmModal;
