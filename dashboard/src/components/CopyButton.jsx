import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { copyToClipboard } from '../utils/clipboard';
import { CopyIcon, CheckIcon } from './Icons';
import { translateLiteral } from '../utils/i18n';

function CopyButton({ value, title, size = 14, className = '' }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const resolvedTitle = title ? translateLiteral(t, title) : t('common.actions.copy');

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
      title={copied ? t('common.actions.copied') : resolvedTitle}
      aria-label={copied ? t('common.actions.copied') : resolvedTitle}
    >
      {copied ? <CheckIcon size={size} /> : <CopyIcon size={size} />}
    </button>
  );
}

export default CopyButton;
