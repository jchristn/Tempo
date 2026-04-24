import { useEffect } from 'react';

const SELECTOR = [
  'button',
  'a',
  'label',
  'input:not([type="hidden"])',
  'select',
  'textarea',
  'th',
  '[role="button"]',
  '.summary-tile .label',
  '.card-title',
  '.drawer-section-title',
  '.details-section-header',
  '.table-pagination-total',
  '.page-header-title',
  '.page-header-subtitle'
].join(', ');

function normalize(text) {
  return String(text || '').replace(/\s+/g, ' ').trim();
}

function labelTextForField(element) {
  if (!element) return '';

  if (element.labels && element.labels.length > 0) {
    const direct = normalize(element.labels[0].textContent);
    if (direct) return direct;
  }

  const wrappingLabel = element.closest('label');
  if (wrappingLabel) {
    const wrapped = normalize(wrappingLabel.textContent);
    if (wrapped) return wrapped;
  }

  const field = element.closest('.form-row, .field, .table-pagination-size, .table-pagination-jump');
  if (field) {
    const label = field.querySelector('label, .label');
    if (label) {
      const fieldLabel = normalize(label.textContent);
      if (fieldLabel) return fieldLabel;
    }
  }

  if (element.previousElementSibling?.tagName === 'LABEL') {
    const previous = normalize(element.previousElementSibling.textContent);
    if (previous) return previous;
  }

  return '';
}

function inferTitle(element) {
  if (!element || element.closest('.code-block')) return '';

  const explicit = normalize(element.getAttribute('data-tooltip'));
  if (explicit) return explicit;

  const aria = normalize(element.getAttribute('aria-label'));
  if (aria) return aria;

  const tag = element.tagName.toUpperCase();

  if (tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA') {
    const label = labelTextForField(element);
    if (label) return label;
    const placeholder = normalize(element.getAttribute('placeholder'));
    if (placeholder) return placeholder;
  }

  const text = normalize(element.textContent);
  if (text) return text;

  return '';
}

function syncTitle(element) {
  if (!(element instanceof HTMLElement)) return;
  const existing = normalize(element.getAttribute('title'));
  const isAuto = element.dataset.autoTitle === 'true';

  if (existing && !isAuto) return;

  const title = inferTitle(element);
  if (title) {
    element.setAttribute('title', title);
    element.dataset.autoTitle = 'true';
  } else if (isAuto) {
    element.removeAttribute('title');
    delete element.dataset.autoTitle;
  }
}

function syncTree(node) {
  if (!(node instanceof HTMLElement)) return;
  if (node.matches(SELECTOR)) syncTitle(node);
  node.querySelectorAll(SELECTOR).forEach(syncTitle);
}

function useAutoTitles(enabled = true) {
  useEffect(() => {
    if (!enabled || typeof window === 'undefined' || typeof document === 'undefined') return undefined;

    let frame = 0;
    const run = () => {
      frame = 0;
      syncTree(document.body);
    };
    const schedule = () => {
      if (frame) window.cancelAnimationFrame(frame);
      frame = window.requestAnimationFrame(run);
    };

    schedule();

    const observer = new MutationObserver(() => schedule());
    observer.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      characterData: true
    });

    return () => {
      observer.disconnect();
      if (frame) window.cancelAnimationFrame(frame);
    };
  }, [enabled]);
}

export default useAutoTitles;
