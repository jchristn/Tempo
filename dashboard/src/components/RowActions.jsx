import ActionMenu from './ActionMenu';

/**
 * Standard row action menu. Order: Edit, View, View JSON, extras, Delete.
 * Pass callbacks to enable each action; omitted callbacks hide the item.
 * Most CRUD tables only need onEdit + onViewJson + onDelete; row-click usually
 * mirrors onEdit so the menu is for keyboard / touch / explicit access.
 */
function RowActions({ onEdit, onView, onViewJson, onDelete, deleteDisabled = false, extra = [] }) {
  const items = [];
  if (onEdit) items.push({ label: 'Edit', onClick: onEdit });
  if (onView) items.push({ label: 'View', onClick: onView });
  if (onViewJson) items.push({ label: 'View JSON', onClick: onViewJson });
  for (const it of extra) items.push(it);
  if (onDelete) items.push({ label: 'Delete', onClick: onDelete, variant: 'danger', hidden: deleteDisabled });
  return <ActionMenu items={items} />;
}

export default RowActions;
