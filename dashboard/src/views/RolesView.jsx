import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import TenantPicker from '../components/TenantPicker';
import CopyableId from '../components/CopyableId';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ModalRecordId from '../components/ModalRecordId';
import RowActions from '../components/RowActions';
import { formatBoolean, formatTime } from '../utils/formatters';

function RolesView({ apiClient, principal }) {
  const { t } = useTranslation();
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((value) => value + 1);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listRoles(tenantId, { pageNumber, pageSize })
      .then((result) => { if (!cancelled) setData(result); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, pageNumber, pageSize, refreshKey, tenantId]);

  const save = async () => {
    if (editing.id) await apiClient.updateRole(tenantId, editing.id, editing);
    else await apiClient.createRole(tenantId, editing);
    setEditing(null);
    refresh();
  };

  const columns = [
    { key: 'name', label: t('views.roles.columns.name', { defaultValue: 'Name' }), tip: t('views.roles.columns.nameTip', { defaultValue: "Role name; assign this role to users to grant the role's permissions" }) },
    { key: 'description', label: t('views.roles.columns.description', { defaultValue: 'Description' }), tip: t('views.roles.columns.descriptionTip', { defaultValue: 'Optional description shown when assigning the role' }), render: (role) => role.description || t('common.placeholders.dash') },
    { key: 'protected', label: t('views.roles.columns.protected', { defaultValue: 'Protected' }), tip: t('views.roles.columns.protectedTip', { defaultValue: 'Built-in roles cannot be deleted (Administrator/Editor/Operator/ReadOnly)' }), render: (role) => formatBoolean(role.isProtected) },
    { key: 'id', label: t('views.roles.columns.identifier', { defaultValue: 'Identifier' }), tip: t('views.roles.columns.identifierTip', { defaultValue: 'Globally unique role id (prefix rol_)' }), render: (role) => <CopyableId value={role.id} /> },
    { key: 'createdUtc', label: t('views.roles.columns.created', { defaultValue: 'Created' }), tip: t('views.roles.columns.createdTip', { defaultValue: 'When the role was created' }), render: (role) => formatTime(role.createdUtc) },
    {
      key: 'actions',
      label: '',
      style: { width: 48 },
      render: (role) => (
        <RowActions
          onEdit={() => setEditing(role)}
          onViewJson={() => setJsonRow(role)}
          onDelete={() => setConfirmDelete(role)}
          deleteDisabled={!!role.isProtected}
        />
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title={t('views.roles.title')}
        subtitle={t('views.roles.subtitle', {
          defaultValue: 'Group permissions so users receive the right tenant access. {{count}} roles in selected tenant.',
          count: data?.totalCount ?? 0
        })}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => setEditing({ name: '', active: true })}>{t('views.roles.newRole', { defaultValue: '+ New role' })}</button>
          </>
        }
      />
      <TableFrame
        columns={columns}
        items={data?.items || []}
        totalRecords={data?.totalCount ?? 0}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={setPageNumber}
        onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        onRefresh={refresh}
        loading={loading}
        onRowClick={(role) => setEditing(role)}
      />

      {editing && (
        <Modal
          open
          size="small"
          onClose={() => setEditing(null)}
          title={editing.id ? t('views.roles.editTitle', { defaultValue: 'Edit role' }) : t('views.roles.createTitle', { defaultValue: 'Create role' })}
          headerMeta={<ModalRecordId label={t('views.roles.roleId', { defaultValue: 'Role ID' })} value={editing.id} />}
          footer={
            <>
              <button className="button-secondary" onClick={() => setEditing(null)}>{t('common.actions.cancel')}</button>
              <button className="button-primary" onClick={save}>{t('common.actions.save')}</button>
            </>
          }
        >
          <div className="form-row"><label title={t('views.roles.form.nameTitle', { defaultValue: 'Role name; users assigned to this role inherit its mapped permissions' })}>{t('views.roles.columns.name', { defaultValue: 'Name' })}</label><input value={editing.name || ''} placeholder={t('views.roles.form.namePlaceholder', { defaultValue: 'DevOps' })} onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          <div className="form-row"><label title={t('views.roles.form.descriptionTitle', { defaultValue: 'Optional description of what this role allows' })}>{t('views.roles.columns.description', { defaultValue: 'Description' })}</label><input value={editing.description || ''} placeholder={t('views.roles.form.descriptionPlaceholder', { defaultValue: 'Can run flows but cannot edit them' })} onChange={(e) => setEditing({ ...editing, description: e.target.value })} /></div>
          <div className="form-row"><label title={t('views.roles.form.activeTitle', { defaultValue: "Inactive roles do not contribute permissions to users they're mapped to" })}><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> {t('views.roles.active', { defaultValue: 'Active' })}</label></div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title={t('views.roles.jsonTitle', { defaultValue: 'Role JSON' })} />
      <ConfirmModal
        open={!!confirmDelete}
        danger
        title={t('views.roles.deleteTitle', { defaultValue: 'Delete role' })}
        recordId={confirmDelete?.id || ''}
        recordIdLabel={t('views.roles.roleId', { defaultValue: 'Role ID' })}
        message={t('views.roles.deleteMessage', { defaultValue: 'Delete role "{{name}}"? Role mappings will also be removed.', name: confirmDelete?.name || '' })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={async () => { await apiClient.deleteRole(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)}
      />
    </div>
  );
}

export default RolesView;
