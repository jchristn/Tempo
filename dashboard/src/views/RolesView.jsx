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

function RolesView({ apiClient, principal }) {
  const [tenantId, setTenantId] = useState(principal?.tenantId || '');
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!tenantId || !apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listRoles(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  const save = async () => {
    if (editing.id) await apiClient.updateRole(tenantId, editing.id, editing);
    else await apiClient.createRole(tenantId, editing);
    setEditing(null);
    refresh();
  };

  const columns = [
    { key: 'name', label: 'Name', tip: 'Role name; assign this role to users to grant the role\'s permissions' },
    { key: 'description', label: 'Description', tip: 'Optional description shown when assigning the role', render: (r) => r.description || '-' },
    { key: 'protected', label: 'Protected', tip: 'Built-in roles cannot be deleted (Administrator/Editor/Operator/ReadOnly)', render: (r) => r.isProtected ? 'Yes' : 'No' },
    { key: 'id', label: 'Identifier', tip: 'Globally unique role id (prefix rol_)', render: (r) => <CopyableId value={r.id} /> },
    { key: 'createdUtc', label: 'Created', tip: 'When the role was created', render: (r) => formatTime(r.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (r) => (
      <RowActions
        onEdit={() => setEditing(r)}
        onViewJson={() => setJsonRow(r)}
        onDelete={() => setConfirmDelete(r)}
        deleteDisabled={!!r.isProtected}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title="Roles"
        subtitle={'Group permissions so users receive the right tenant access. ' + (data?.totalCount ?? '-') + ' roles in selected tenant.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => setEditing({ name: '', active: true })}>+ New role</button>
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
        onRowClick={(r) => setEditing(r)}
      />

      {editing && (
        <Modal open size="small" onClose={() => setEditing(null)} title={editing.id ? 'Edit role' : 'Create role'}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}>
          <div className="form-row"><label title="Role name; users assigned to this role inherit its mapped permissions">Name</label><input value={editing.name || ''} placeholder="DevOps" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          <div className="form-row"><label title="Optional description of what this role allows">Description</label><input value={editing.description || ''} placeholder="Can run flows but cannot edit them" onChange={(e) => setEditing({ ...editing, description: e.target.value })} /></div>
          <div className="form-row"><label title="Inactive roles do not contribute permissions to users they're mapped to"><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> Active</label></div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Role JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete role"
        message={'Delete role "' + (confirmDelete?.name || '') + '"? Role mappings will also be removed.'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteRole(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default RolesView;
