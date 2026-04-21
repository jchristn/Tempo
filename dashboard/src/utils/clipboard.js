/**
 * Robust clipboard helper. Falls back to execCommand for non-secure contexts
 * (plain HTTP, file://, etc.) where navigator.clipboard is not available.
 */
export async function copyToClipboard(value) {
  const text = `${value ?? ''}`;

  if (typeof navigator !== 'undefined' && navigator.clipboard && window.isSecureContext) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // fall through
    }
  }

  if (typeof document === 'undefined') {
    throw new Error('Clipboard is not available in this environment.');
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', '');
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  textarea.style.pointerEvents = 'none';
  textarea.style.top = '0';
  textarea.style.left = '0';

  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();
  textarea.setSelectionRange(0, textarea.value.length);

  try {
    const copied = document.execCommand('copy');
    if (!copied) throw new Error('Copy command was rejected.');
    return true;
  } finally {
    document.body.removeChild(textarea);
  }
}
