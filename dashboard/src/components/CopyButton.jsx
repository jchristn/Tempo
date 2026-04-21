import { useState } from 'react';
import { copyToClipboard } from '../utils/clipboard';
import { CopyIcon, CheckIcon } from './Icons';

function CopyButton({ value, title = 'Copy to clipboard', size = 14, className = '' }) {
  const [copied, setCopied] = useState(false);

  const handle = async (e) => {
    e.stopPropagation();
    try {
      await copyToClipboard(value);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch (err) {
      console.error('Copy failed:', err);
    }
  };

  return (
    <button
      type="button"
      className={'copy-btn' + (copied ? ' is-copied' : '') + (className ? ' ' + className : '')}
      onClick={handle}
      title={copied ? 'Copied!' : title}
      aria-label={copied ? 'Copied' : title}
    >
      {copied ? <CheckIcon size={size} /> : <CopyIcon size={size} />}
    </button>
  );
}

export default CopyButton;
