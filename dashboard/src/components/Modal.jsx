import { useEffect } from 'react';
import { createPortal } from 'react-dom';

function Modal({ open, onClose, title, headerMeta = null, children, footer, size = 'large' }) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    document.body.style.overflow = 'hidden';
    return () => {
      window.removeEventListener('keydown', onKey);
      document.body.style.overflow = '';
    };
  }, [open, onClose]);

  if (!open) return null;

  const sizeClass =
    size === 'small' ? ' small'
      : size === 'large' ? ' large'
        : size === 'xlarge' ? ' xlarge'
          : size === 'drawer' ? ' drawer'
            : '';

  return createPortal(
    <div className="modal-backdrop" onClick={onClose}>
      <div className={'modal' + sizeClass} onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="modal-heading">
            <div className="modal-title">{title}</div>
            {headerMeta && <div className="modal-header-meta">{headerMeta}</div>}
          </div>
          <button className="button-ghost" onClick={onClose} aria-label="Close" title="Close">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M18 6L6 18M6 6l12 12" /></svg>
          </button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>,
    document.body
  );
}

export default Modal;
