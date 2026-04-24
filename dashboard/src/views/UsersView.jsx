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
import TenantPicker from '../components/TenantPicker';
import { formatTime } from '../utils/formatters';
import { normalizeApiError, translateLiteral } from '../utils/i18n';

function UsersView({ apiClient, principal }) {
  const { t } = useTranslation();
  const tl = (value, options) => translateLiteral(t, value, options);
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(null);
  const [editingRoles, setEditingRoles] = useState([]);
  const [allRoles, setAllRoles] = useState([]);
  const [rolesBusy, setRolesBusy] = useState(false);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listUsers(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    apiClient.listRoles(tenantId, { pageSize: 500 })
      .then((d) => setAllRoles(d.items || []))
      .catch(() => setAllRoles([]));
  }, [apiClient, tenantId, refreshKey]);

  const startEdit = async (u) => {
    setEditing({ ...u, passwordSha256: '' });
    setEditingRoles([]);
    if (u.id) {
      try {
        const maps = await apiClient.listUserRoles(tenantId, u.id);
        setEditingRoles(maps || []);
      } catch { setEditingRoles([]); }
    }
  };

  const startCreate = () => {
    setEditing({ email: '', firstName: '', lastName: '', active: true });
    setEditingRoles([]);
  };

  const save = async () => {
    const body = { ...editing };
    if (body.id) await apiClient.updateUser(tenantId, body.id, body);
    else await apiClient.createUser(tenantId, body);
    setEditing(null);
    refresh();
  };

  const toggleRole = async (roleId) => {
    if (!editing?.id) return;
    setRolesBusy(true);
    try {
      const existing = editingRoles.find((m) => m.roleId === roleId);
      if (existing) {
        await apiClient.deleteUserRoleMap(tenantId, existing.id);
        setEditingRoles((rs) => rs.filter((m) => m.id !== existing.id));
      } else {
        const created = await apiClient.createUserRoleMap(tenantId, { userId: editing.id, roleId });
        setEditingRoles((rs) => [...rs, created]);
      }
    } catch (err) {
      alert(normalizeApiError(err, t));
    } finally {
      setRolesBusy(false);
    }
  };

  const columns = [
    { key: 'email', label: tl('Email'), tip: tl('Email used at password sign-in (x-email header / login form)') },
    { key: 'name', label: tl('Name'), tip: tl('First and last name combined', { defaultValue: 'First and last name combined' }), render: (u) => [u.firstName, u.lastName].filter(Boolean).join(' ') || '-' },
    { key: 'id', label: tl('Identifier'), tip: tl('Globally unique user id (prefix usr_)'), render: (u) => <CopyableId value={u.id} /> },
    { key: 'isTenantAdmin', label: tl('Tenant Admin'), tip: tl('Full administrative access within this tenant only'), render: (u) => u.isTenantAdmin ? t('common.boolean.yes') : t('common.boolean.no') },
    { key: 'isAdmin', label: tl('Global Admin'), tip: tl('Root operator with full access across every tenant'), render: (u) => u.isAdmin ? t('common.boolean.yes') : t('common.boolean.no') },
    { key: 'createdUtc', label: tl('Created'), tip: tl('When the user was created'), render: (u) => formatTime(u.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (u) => (
      <RowActions
        onEdit={() => startEdit(u)}
        onViewJson={() => setJsonRow(u)}
        onDelete={() => setConfirmDelete(u)}
        deleteDisabled={!!u.isProtected}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title={tl('Users')}
        subtitle={tl('Manage tenant-scoped dashboard users and their role assignments. {{count}} users in selected tenant.', { count: data?.totalCount ?? '-' })}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={startCreate}>{tl('+ New user')}</button>
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
        onPageSizeChange={(s) => { setPageSize(s); setPageNumber(1); }}
        onRefresh={refresh}
        loading={loading}
        onRowClick={(u) => startEdit(u)}
      />

      {editing && (
        <Modal
          open
          size="medium"
          onClose={() => setEditing(null)}
          title={editing.id ? tl('Edit user') : tl('Create user')}
          headerMeta={<ModalRecordId label={tl('User ID')} value={editing.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>{t('common.actions.cancel')}</button>
            <button className="button-primary" onClick={save}>{t('common.actions.save')}</button>
          </>}
        >
          <div className="form-row"><label title={tl('Email used at password sign-in; must be unique within the tenant')}>{tl('Email')}</label><input value={editing.email || ''} placeholder="alice@example.com" onChange={(e) => setEditing({ ...editing, email: e.target.value })} /></div>
          <div className="grid-2">
            <div className="form-row"><label title={tl('First name (optional)')}>{tl('First name')}</label><input value={editing.firstName || ''} placeholder={tl('Alice')} onChange={(e) => setEditing({ ...editing, firstName: e.target.value })} /></div>
            <div className="form-row"><label title={tl('Last name (optional)')}>{tl('Last name')}</label><input value={editing.lastName || ''} placeholder={tl('Anderson')} onChange={(e) => setEditing({ ...editing, lastName: e.target.value })} /></div>
          </div>
          <div className="form-row">
            <label title={tl('Plaintext password; the server hashes it as SHA-256 before storing')}>{editing.id ? tl('Password (leave blank to keep current)') : tl('Password')}</label>
            <input type="password" value={editing.passwordSha256 || ''} placeholder={editing.id ? tl('Unchanged') : tl('Choose a strong password')} onChange={(e) => setEditing({ ...editing, passwordSha256: e.target.value })} />
            <div className="form-help">{tl('Plaintext is automatically hashed on the server.')}</div>
          </div>
          <div className="grid-2">
            <div className="form-row"><label title={tl('Global admin: full root access across all tenants. Use sparingly')}><input type="checkbox" checked={!!editing.isAdmin} onChange={(e) => setEditing({ ...editing, isAdmin: e.target.checked })} style={{ width: 'auto' }} /> {tl('Global admin')}</label></div>
            <div className="form-row"><label title={tl('Tenant admin: full access within this tenant, no access to others')}><input type="checkbox" checked={!!editing.isTenantAdmin} onChange={(e) => setEditing({ ...editing, isTenantAdmin: e.target.checked })} style={{ width: 'auto' }} /> {tl('Tenant admin')}</label></div>
          </div>
          <div className="form-row"><label title={tl('Inactive users cannot sign in')}><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> {t('common.generic.active')}</label></div>

          {editing.id && (
            <div className="card" style={{ marginTop: 'var(--spacing-md)' }}>
              <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }} title={tl('Roles assigned to this user. Permissions mapped to these roles take effect immediately')}>{tl('Role assignments')}</div>
              {allRoles.length === 0 ? (
                <div className="form-help">{tl('No roles defined in this tenant. Create roles on the Roles page first.')}</div>
              ) : (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {allRoles.map((r) => {
                    const assigned = !!editingRoles.find((m) => m.roleId === r.id);
                    return (
                      <label key={r.id} className={'pill ' + (assigned ? 'pill-success' : 'pill-neutral')} style={{ cursor: rolesBusy ? 'wait' : 'pointer', opacity: rolesBusy ? 0.6 : 1 }} title={r.description || r.name}>
                        <input type="checkbox" checked={assigned} disabled={rolesBusy} onChange={() => toggleRole(r.id)} style={{ display: 'none' }} />
                        {r.name}{r.isProtected ? ' ★' : ''}
                      </label>
                    );
                  })}
                </div>
              )}
              <div className="form-help" style={{ marginTop: 'var(--spacing-xs)' }}>{tl('Click a role to toggle assignment. Changes save immediately.')}</div>
            </div>
          )}
          {!editing.id && (
            <div className="form-help" style={{ marginTop: 'var(--spacing-sm)' }}>{tl('Save the user first to assign roles.')}</div>
          )}
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title={tl('User JSON')} />
      <ConfirmModal open={!!confirmDelete} danger title={tl('Delete user')}
        recordId={confirmDelete?.id || ''}
        recordIdLabel={tl('User ID')}
        message={tl('Delete user "{{email}}"? Credentials and role maps will also be removed.', { email: confirmDelete?.email || '' })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={async () => { await apiClient.deleteUser(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default UsersView;
