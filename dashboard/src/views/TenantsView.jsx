import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import CopyableId from '../components/CopyableId';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ModalRecordId from '../components/ModalRecordId';
import RowActions from '../components/RowActions';
import { formatBoolean, formatTime } from '../utils/formatters';

function TenantsView({ apiClient }) {
  const { t } = useTranslation();
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((value) => value + 1);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listTenants({ pageNumber, pageSize, includeInactive })
      .then((result) => { if (!cancelled) setData(result); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, includeInactive, pageNumber, pageSize, refreshKey]);

  const save = async () => {
    if (editing.id) await apiClient.updateTenant(editing.id, editing);
    else await apiClient.createTenant(editing);
    setEditing(null);
    refresh();
  };

  const columns = [
    { key: 'name', label: t('views.tenants.columns.name', { defaultValue: 'Name' }), tip: t('views.tenants.columns.nameTip', { defaultValue: 'Display name shown to users in this tenant' }) },
    { key: 'id', label: t('views.tenants.columns.identifier', { defaultValue: 'Identifier' }), tip: t('views.tenants.columns.identifierTip', { defaultValue: 'Globally unique tenant id (prefix ten_)' }), render: (tenant) => <CopyableId value={tenant.id} /> },
    { key: 'region', label: t('views.tenants.columns.region', { defaultValue: 'Region' }), tip: t('views.tenants.columns.regionTip', { defaultValue: 'Optional grouping label for routing or analytics' }), render: (tenant) => tenant.region || t('common.placeholders.dash') },
    { key: 'active', label: t('views.tenants.columns.active', { defaultValue: 'Active' }), tip: t('views.tenants.columns.activeTip', { defaultValue: 'Inactive tenants are hidden by default and reject auth' }), render: (tenant) => formatBoolean(tenant.active) },
    { key: 'createdUtc', label: t('views.tenants.columns.created', { defaultValue: 'Created' }), tip: t('views.tenants.columns.createdTip', { defaultValue: 'When the tenant record was created' }), render: (tenant) => formatTime(tenant.createdUtc) },
    {
      key: 'actions',
      label: '',
      style: { width: 48 },
      render: (tenant) => (
        <RowActions
          onEdit={() => setEditing(tenant)}
          onViewJson={() => setJsonRow(tenant)}
          onDelete={() => setConfirmDelete(tenant)}
          deleteDisabled={!!tenant.isProtected}
        />
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title={t('views.tenants.title')}
        subtitle={t('views.tenants.subtitle', {
          defaultValue: 'Create isolation boundaries that own users, flows, runs, and credentials. {{count}} tenants total.',
          count: data?.totalCount ?? 0
        })}
        actions={
          <>
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }} title={t('views.tenants.includeInactiveTitle', { defaultValue: 'Show tenants whose Active flag is false' })}>
              <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} style={{ width: 'auto' }} />
              {t('views.tenants.includeInactive', { defaultValue: 'Include inactive' })}
            </label>
            <button className="button-primary" onClick={() => setEditing({ name: '', active: true })}>{t('views.tenants.newTenant', { defaultValue: '+ New tenant' })}</button>
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
        onRowClick={(tenant) => setEditing(tenant)}
      />

      {editing && (
        <Modal
          open
          size="small"
          onClose={() => setEditing(null)}
          title={editing.id ? t('views.tenants.editTitle', { defaultValue: 'Edit tenant' }) : t('views.tenants.createTitle', { defaultValue: 'Create tenant' })}
          headerMeta={<ModalRecordId label={t('views.tenants.tenantId', { defaultValue: 'Tenant ID' })} value={editing.id} />}
          footer={
            <>
              <button className="button-secondary" onClick={() => setEditing(null)}>{t('common.actions.cancel')}</button>
              <button className="button-primary" onClick={save}>{t('common.actions.save')}</button>
            </>
          }
        >
          <div className="form-row"><label title={t('views.tenants.form.nameTitle', { defaultValue: 'Display name for the tenant; visible in the dashboard and audit trail' })}>{t('views.tenants.columns.name', { defaultValue: 'Name' })}</label><input value={editing.name || ''} placeholder={t('views.tenants.form.namePlaceholder', { defaultValue: 'Acme Corporation' })} onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          <div className="form-row"><label title={t('views.tenants.form.regionTitle', { defaultValue: 'Optional free-form region label, e.g. us-east, eu-west, dc1' })}>{t('views.tenants.columns.region', { defaultValue: 'Region' })}</label><input value={editing.region || ''} placeholder="us-east" onChange={(e) => setEditing({ ...editing, region: e.target.value })} /></div>
          <div className="form-row"><label title={t('views.tenants.form.activeTitle', { defaultValue: 'Inactive tenants reject all authentication' })}><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> {t('views.tenants.columns.active', { defaultValue: 'Active' })}</label></div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title={t('views.tenants.jsonTitle', { defaultValue: 'Tenant JSON' })} />
      <ConfirmModal
        open={!!confirmDelete}
        danger
        title={t('views.tenants.deleteTitle', { defaultValue: 'Delete tenant' })}
        recordId={confirmDelete?.id || ''}
        recordIdLabel={t('views.tenants.tenantId', { defaultValue: 'Tenant ID' })}
        message={t('views.tenants.deleteMessage', {
          defaultValue: 'Delete tenant "{{name}}"? All users, flows, and runs under this tenant will be deleted.',
          name: confirmDelete?.name || ''
        })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={async () => { await apiClient.deleteTenant(confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)}
      />
    </div>
  );
}

export default TenantsView;
