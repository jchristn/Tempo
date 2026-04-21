import { statusClass } from '../utils/formatters';

function StatusPill({ code }) {
  return <span className={statusClass(code)}>{code}</span>;
}
export default StatusPill;
