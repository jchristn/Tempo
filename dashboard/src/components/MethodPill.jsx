function MethodPill({ method }) {
  const m = (method || 'GET').toUpperCase();
  return <span className={'explorer-method ' + m.toLowerCase()}>{m}</span>;
}
export default MethodPill;
