import CopyableId from './CopyableId';
import { useTranslation } from 'react-i18next';
import { translateLiteral } from '../utils/i18n';

function ModalRecordId({ label, value }) {
  const { t } = useTranslation();
  const resolvedLabel = label ? translateLiteral(t, label) : t('components.modal.id');

  if (!value) return null;

  return (
    <div className="modal-record-id" title={resolvedLabel + ': ' + value}>
      <span className="modal-record-id-label">{resolvedLabel}</span>
      <CopyableId value={value} max={24} />
    </div>
  );
}

export default ModalRecordId;
