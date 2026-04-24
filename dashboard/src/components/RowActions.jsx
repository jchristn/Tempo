import ActionMenu from './ActionMenu';
import { useTranslation } from 'react-i18next';

/**
 * Standard row action menu. Order: Edit, View, View JSON, extras, Delete.
 * Pass callbacks to enable each action; omitted callbacks hide the item.
 * Most CRUD tables only need onEdit + onViewJson + onDelete; row-click usually
 * mirrors onEdit so the menu is for keyboard / touch / explicit access.
 */
function RowActions({ onEdit, onView, onViewJson, onDelete, deleteDisabled = false, extra = [] }) {
  const { t } = useTranslation();
  const items = [];
  if (onEdit) items.push({ label: t('common.actions.edit'), onClick: onEdit });
  if (onView) items.push({ label: t('common.actions.view'), onClick: onView });
  if (onViewJson) items.push({ label: t('common.actions.viewJson'), onClick: onViewJson });
  for (const it of extra) items.push(it);
  if (onDelete) items.push({ label: t('common.actions.delete'), onClick: onDelete, variant: 'danger', hidden: deleteDisabled });
  return <ActionMenu items={items} />;
}

export default RowActions;
