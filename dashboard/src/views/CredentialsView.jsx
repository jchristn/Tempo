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
import { formatTime } from '../utils/formatters';

function CredentialsView({ apiClient, principal }) {
  const { t } = useTranslation();
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [users, setUsers] = useState([]);
  const [editing, setEditing] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((value) => value + 1);

  useEffect(() => {
    if (!apiClient || !tenantId) return;
    apiClient.listUsers(tenantId, { pageSize: 500 }).then((result) => setUsers(result.items || [])).catch(() => {});
  }, [apiClient, tenantId]);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listCredentials(tenantId, { pageNumber, pageSize })
      .then((result) => { if (!cancelled) setData(result); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, pageNumber, pageSize, refreshKey, tenantId]);

  const save = async () => {
    if (editing.id) await apiClient.updateCredential(tenantId, editing.id, editing);
    else await apiClient.createCredential(tenantId, editing);
    setEditing(null);
    refresh();
  };

  const columns = [
    { key: 'name', label: t('views.credentials.columns.name', { defaultValue: 'Name' }), tip: t('views.credentials.columns.nameTip', { defaultValue: 'Friendly label for the credential (purpose, owner, etc.)' }) },
    { key: 'accessKey', label: t('views.credentials.columns.accessKey', { defaultValue: 'Access key' }), tip: t('views.credentials.columns.accessKeyTip', { defaultValue: 'Credential access key. Use it as Authorization: Bearer {accessKey} or in x-access-key. Tempo rejects x-secret-key on API requests' }), render: (credential) => <CopyableId value={credential.accessKey} max={32} /> },
    { key: 'userId', label: t('views.credentials.columns.user', { defaultValue: 'User' }), tip: t('views.credentials.columns.userTip', { defaultValue: 'User this credential authenticates as' }), render: (credential) => <CopyableId value={credential.userId} /> },
    { key: 'createdUtc', label: t('views.credentials.columns.created', { defaultValue: 'Created' }), tip: t('views.credentials.columns.createdTip', { defaultValue: 'When the credential was generated' }), render: (credential) => formatTime(credential.createdUtc) },
    {
      key: 'actions',
      label: '',
      style: { width: 48 },
      render: (credential) => (
        <RowActions
          onEdit={() => setEditing(credential)}
          onViewJson={() => setJsonRow(credential)}
          onDelete={() => setConfirmDelete(credential)}
          deleteDisabled={!!credential.isProtected}
        />
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title={t('views.credentials.title')}
        subtitle={t('views.credentials.subtitle', {
          defaultValue: 'Create API access keys for services that call Tempo. Use the access key as a bearer credential or x-access-key. {{count}} credentials in selected tenant.',
          count: data?.totalCount ?? 0
        })}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => setEditing({ name: '', userId: principal?.id || '', active: true })}>{t('views.credentials.newCredential', { defaultValue: '+ New credential' })}</button>
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
        onRowClick={(credential) => setEditing(credential)}
      />

      {editing && (
        <Modal
          open
          size="small"
          onClose={() => setEditing(null)}
          title={editing.id ? t('views.credentials.editTitle', { defaultValue: 'Edit credential' }) : t('views.credentials.createTitle', { defaultValue: 'Create credential' })}
          headerMeta={<ModalRecordId label={t('views.credentials.credentialId', { defaultValue: 'Credential ID' })} value={editing.id} />}
          footer={
            <>
              <button className="button-secondary" onClick={() => setEditing(null)}>{t('common.actions.cancel')}</button>
              <button className="button-primary" onClick={save}>{t('common.actions.save')}</button>
            </>
          }
        >
          <div className="form-row"><label title={t('views.credentials.form.nameTitle', { defaultValue: "Friendly label, e.g. 'CI pipeline' or 'Mobile app prod'" })}>{t('views.credentials.columns.name', { defaultValue: 'Name' })}</label><input value={editing.name || ''} placeholder={t('views.credentials.form.namePlaceholder', { defaultValue: 'CI pipeline' })} onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          <div className="form-row">
            <label title={t('views.credentials.form.userTitle', { defaultValue: "The user identity this credential authenticates as. Permissions follow the user's role assignments" })}>{t('views.credentials.columns.user', { defaultValue: 'User' })}</label>
            <select value={editing.userId || ''} onChange={(e) => setEditing({ ...editing, userId: e.target.value })}>
              <option value="">{t('views.credentials.selectUser', { defaultValue: 'Select user...' })}</option>
              {users.map((user) => <option key={user.id} value={user.id}>{user.email}</option>)}
            </select>
          </div>
          <div className="form-row"><label title={t('views.credentials.form.activeTitle', { defaultValue: 'Inactive credentials are rejected at authentication' })}><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> {t('views.credentials.active', { defaultValue: 'Active' })}</label></div>
          <div className="form-help">{t('views.credentials.help', { defaultValue: 'Access keys (pub_...) are generated automatically when the credential is created. Tempo rejects x-secret-key on API requests.' })}</div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title={t('views.credentials.jsonTitle', { defaultValue: 'Credential JSON' })} />
      <ConfirmModal
        open={!!confirmDelete}
        danger
        title={t('views.credentials.deleteTitle', { defaultValue: 'Delete credential' })}
        recordId={confirmDelete?.id || ''}
        recordIdLabel={t('views.credentials.credentialId', { defaultValue: 'Credential ID' })}
        message={t('views.credentials.deleteMessage', { defaultValue: 'Delete credential "{{name}}"?', name: confirmDelete?.name || '' })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={async () => { await apiClient.deleteCredential(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)}
      />
    </div>
  );
}

export default CredentialsView;
