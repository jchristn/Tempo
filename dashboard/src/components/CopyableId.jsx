import CopyButton from './CopyButton';
import { truncate } from '../utils/formatters';

function CopyableId({ value, max = 24 }) {
  if (!value) return <span style={{ color: 'var(--color-text-muted)' }}>-</span>;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.375rem' }}>
      <code className="monospace" title={value}>{truncate(value, max)}</code>
      <CopyButton value={value} />
    </span>
  );
}

export default CopyableId;
