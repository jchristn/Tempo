import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { MoreVerticalIcon } from './Icons';
import { translateLiteral } from '../utils/i18n';

/**
 * Three-dot action menu. Dropdown is rendered via portal so it is never clipped
 * by the table or wrapping containers and always floats above them.
 *
 * items = [{ label, onClick, variant?: 'danger', hidden? }]
 */
function ActionMenu({ items }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [pos, setPos] = useState({ top: 0, left: 0, align: 'below' });
  const triggerRef = useRef(null);
  const menuRef = useRef(null);

  useLayoutEffect(() => {
    if (!open || !triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    const menuH = (items?.length || 1) * 36 + 8;
    const spaceBelow = window.innerHeight - rect.bottom;
    const align = spaceBelow < menuH + 16 ? 'above' : 'below';
    const top = align === 'below' ? rect.bottom + 4 : rect.top - menuH - 4;
    const menuW = 180;
    let left = rect.right - menuW;
    if (left < 8) left = 8;
    if (left + menuW > window.innerWidth - 8) left = window.innerWidth - menuW - 8;
    setPos({ top, left, align });
  }, [open, items]);

  useEffect(() => {
    if (!open) return;
    const handler = (e) => {
      if (triggerRef.current?.contains(e.target)) return;
      if (menuRef.current?.contains(e.target)) return;
      setOpen(false);
    };
    window.addEventListener('mousedown', handler);
    window.addEventListener('scroll', () => setOpen(false), true);
    window.addEventListener('resize', () => setOpen(false));
    return () => {
      window.removeEventListener('mousedown', handler);
      window.removeEventListener('scroll', () => setOpen(false), true);
      window.removeEventListener('resize', () => setOpen(false));
    };
  }, [open]);

  const visibleItems = (items || []).filter((it) => !it.hidden);

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className="action-menu-trigger"
        onClick={(e) => { e.stopPropagation(); setOpen((o) => !o); }}
        aria-label={t('common.actions.actions')}
        title={t('common.actions.actions')}
        aria-haspopup="menu"
        aria-expanded={open}
      >
        <MoreVerticalIcon size={16} />
      </button>
      {open && createPortal(
        <div ref={menuRef} className="action-menu-dropdown" role="menu" style={{ position: 'fixed', top: pos.top, left: pos.left }}>
          {visibleItems.map((it, i) => {
            const label = typeof it.label === 'string' ? translateLiteral(t, it.label) : it.label;
            const title = typeof it.title === 'string'
              ? translateLiteral(t, it.title)
              : (typeof it.label === 'string' ? translateLiteral(t, it.label) : it.title || it.label);
            return (
              <button
                key={i}
                type="button"
                className={'action-menu-item' + (it.variant === 'danger' ? ' danger' : '')}
                title={title}
                onClick={(e) => { e.stopPropagation(); setOpen(false); it.onClick?.(); }}
              >
                {label}
              </button>
            );
          })}
        </div>,
        document.body
      )}
    </>
  );
}

export default ActionMenu;
