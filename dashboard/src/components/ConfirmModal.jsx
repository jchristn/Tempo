import Modal from './Modal';

function ConfirmModal({ open, title = 'Confirm', message, confirmLabel = 'Confirm', cancelLabel = 'Cancel', danger = false, onConfirm, onCancel }) {
  return (
    <Modal
      open={open}
      size="small"
      onClose={onCancel}
      title={title}
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
