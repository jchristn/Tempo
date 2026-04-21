import { useEffect, useState } from 'react';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import TenantPicker from '../components/TenantPicker';
import CopyableId from '../components/CopyableId';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import RowActions from '../components/RowActions';
import { formatTime } from '../utils/formatters';

function CredentialsView({ apiClient, principal }) {
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

  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!apiClient || !tenantId) return;
    apiClient.listUsers(tenantId, { pageSize: 500 }).then((d) => setUsers(d.items || [])).catch(() => {});
  }, [apiClient, tenantId]);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listCredentials(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  const save = async () => {
    if (editing.id) await apiClient.updateCredential(tenantId, editing.id, editing);
    else await apiClient.createCredential(tenantId, editing);
    setEditing(null);
    refresh();
  };

  const columns = [
    { key: 'name', label: 'Name', tip: 'Friendly label for the credential pair (purpose, owner, etc.)' },
    { key: 'accessKey', label: 'Access key', tip: 'Public identifier sent in x-access-key header (prefix pub_)', render: (c) => <CopyableId value={c.accessKey} max={32} /> },
    { key: 'secretKey', label: 'Secret key', tip: 'Secret sent in x-secret-key header (prefix key_); treat as password', render: (c) => <CopyableId value={c.secretKey} max={32} /> },
    { key: 'userId', label: 'User', tip: 'User this credential authenticates as', render: (c) => <CopyableId value={c.userId} /> },
    { key: 'createdUtc', label: 'Created', tip: 'When the credential pair was generated', render: (c) => formatTime(c.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (c) => (
      <RowActions
        onEdit={() => setEditing(c)}
        onViewJson={() => setJsonRow(c)}
        onDelete={() => setConfirmDelete(c)}
        deleteDisabled={!!c.isProtected}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title="Credentials"
        subtitle={'Create API access keys for services that call Tempo. ' + (data?.totalCount ?? '-') + ' credentials in selected tenant.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => setEditing({ name: '', userId: principal?.id || '', active: true })}>+ New credential</button>
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
        onRowClick={(c) => setEditing(c)}
      />

      {editing && (
        <Modal open size="small" onClose={() => setEditing(null)} title={editing.id ? 'Edit credential' : 'Create credential'}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}>
          <div className="form-row"><label title="Friendly label, e.g. 'CI pipeline' or 'Mobile app prod'">Name</label><input value={editing.name || ''} placeholder="CI pipeline" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          <div className="form-row">
            <label title="The user identity this credential authenticates as. Permissions follow the user's role assignments">User</label>
            <select value={editing.userId || ''} onChange={(e) => setEditing({ ...editing, userId: e.target.value })}>
              <option value="">Select user…</option>
              {users.map((u) => <option key={u.id} value={u.id}>{u.email}</option>)}
            </select>
          </div>
          <div className="form-row"><label title="Inactive credentials are rejected at authentication"><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> Active</label></div>
          <div className="form-help">Access (pub_…) and secret (key_…) keys are generated automatically when the credential is created.</div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Credential JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete credential"
        message={'Delete credential "' + (confirmDelete?.name || '') + '"?'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteCredential(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default CredentialsView;
