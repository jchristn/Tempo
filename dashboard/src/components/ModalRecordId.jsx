import CopyableId from './CopyableId';

function ModalRecordId({ label = 'ID', value }) {
  if (!value) return null;

  return (
    <div className="modal-record-id" title={label + ': ' + value}>
      <span className="modal-record-id-label">{label}</span>
      <CopyableId value={value} max={24} />
    </div>
  );
}

export default ModalRecordId;
