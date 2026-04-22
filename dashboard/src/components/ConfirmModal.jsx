import Modal from './Modal';
import ModalRecordId from './ModalRecordId';

function ConfirmModal({
  open,
  title = 'Confirm',
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  danger = false,
  recordId = '',
  recordIdLabel = 'ID',
  onConfirm,
  onCancel
}) {
  return (
    <Modal
      open={open}
      size="small"
      onClose={onCancel}
      title={title}
      headerMeta={recordId ? <ModalRecordId label={recordIdLabel} value={recordId} /> : null}
      footer={
        <>
          <button className="button-secondary" onClick={onCancel}>{cancelLabel}</button>
          <button className={danger ? 'button-danger' : 'button-primary'} onClick={onConfirm}>{confirmLabel}</button>
        </>
      }
    >
      <div style={{ fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }}>{message}</div>
    </Modal>
  );
}

export default ConfirmModal;
