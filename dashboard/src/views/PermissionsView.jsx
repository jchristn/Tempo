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

const RESOURCE_TYPES = ['All', 'Account', 'Administrator', 'Tenant', 'User', 'Credential', 'Role', 'Permission', 'DataFlow', 'Step', 'Trigger', 'FlowRun', 'RequestHistory'];
const OPERATION_TYPES = ['All', 'Create', 'Read', 'Update', 'Delete', 'Execute'];

function PermissionsView({ apiClient, principal }) {
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
    apiClient.listPermissions(tenantId, { pageNumber, pageSize })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, tenantId, pageNumber, pageSize, refreshKey]);

  const save = async () => {
    if (editing.id) await apiClient.updatePermission(tenantId, editing.id, editing);
    else await apiClient.createPermission(tenantId, editing);
    setEditing(null);
    refresh();
  };

  const toggleArrayValue = (arr, value) => {
    const next = arr.includes(value) ? arr.filter((x) => x !== value) : [...arr, value];
    return next.length === 0 ? ['All'] : next;
  };

  const columns = [
    { key: 'name', label: 'Name', tip: 'Permission name; map this permission to a role to grant access' },
    { key: 'permissionType', label: 'Effect', tip: 'Permit grants access; Deny revokes it (Deny wins over Permit)', render: (p) => <span className={'pill ' + (p.permissionType === 'Deny' ? 'pill-danger' : 'pill-success')}>{p.permissionType}</span> },
    { key: 'resourceTypes', label: 'Resources', tip: 'Resource categories this permission applies to', render: (p) => (p.resourceTypes || []).join(', ') },
    { key: 'operationTypes', label: 'Operations', tip: 'Operations (Create/Read/Update/Delete/Execute) this permission grants or denies', render: (p) => (p.operationTypes || []).join(', ') },
    { key: 'protected', label: 'Protected', tip: 'System permissions cannot be deleted', render: (p) => p.isProtected ? 'Yes' : 'No' },
    { key: 'id', label: 'Identifier', tip: 'Globally unique permission id (prefix prm_)', render: (p) => <CopyableId value={p.id} /> },
    { key: 'createdUtc', label: 'Created', tip: 'When the permission was created', render: (p) => formatTime(p.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (p) => (
      <RowActions
        onEdit={() => setEditing(p)}
        onViewJson={() => setJsonRow(p)}
        onDelete={() => setConfirmDelete(p)}
        deleteDisabled={!!p.isProtected}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title="Permissions"
        subtitle={'Grant or deny operations over tenant resources. ' + (data?.totalCount ?? '-') + ' permissions in selected tenant.'}
        actions={
          <>
            <TenantPicker apiClient={apiClient} value={tenantId} onChange={setTenantId} />
            <button className="button-primary" onClick={() => setEditing({ name: '', permissionType: 'Permit', resourceTypes: ['All'], operationTypes: ['All'], active: true })}>+ New permission</button>
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
        onRowClick={(p) => setEditing(p)}
      />

      {editing && (
        <Modal open onClose={() => setEditing(null)} title={editing.id ? 'Edit permission' : 'Create permission'}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}>
          <div className="form-row"><label title="Permission name; map this to a role to grant the access it describes">Name</label><input value={editing.name || ''} placeholder="Read-only flows" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          <div className="form-row">
            <label title="Permit grants access; Deny revokes it. Deny always wins when both apply">Effect</label>
            <select value={editing.permissionType} onChange={(e) => setEditing({ ...editing, permissionType: e.target.value })}>
              <option value="Permit">Permit</option>
              <option value="Deny">Deny</option>
            </select>
          </div>
          <div className="form-row">
            <label title="Pick one or more resource categories this rule applies to. 'All' covers everything">Resource types</label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
              {RESOURCE_TYPES.map((r) => (
                <label key={r} className={'pill ' + ((editing.resourceTypes || []).includes(r) ? 'pill-info' : 'pill-neutral')} style={{ cursor: 'pointer' }} title={r}>
                  <input type="checkbox" checked={(editing.resourceTypes || []).includes(r)} onChange={() => setEditing({ ...editing, resourceTypes: toggleArrayValue(editing.resourceTypes || [], r) })} style={{ display: 'none' }} />
                  {r}
                </label>
              ))}
            </div>
          </div>
          <div className="form-row">
            <label title="Pick one or more operations this rule applies to. 'All' covers everything">Operation types</label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
              {OPERATION_TYPES.map((o) => (
                <label key={o} className={'pill ' + ((editing.operationTypes || []).includes(o) ? 'pill-info' : 'pill-neutral')} style={{ cursor: 'pointer' }} title={o}>
                  <input type="checkbox" checked={(editing.operationTypes || []).includes(o)} onChange={() => setEditing({ ...editing, operationTypes: toggleArrayValue(editing.operationTypes || [], o) })} style={{ display: 'none' }} />
                  {o}
                </label>
              ))}
            </div>
          </div>
          <div className="form-row"><label title="Inactive permissions are skipped during authorization checks"><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> Active</label></div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Permission JSON" />
      <ConfirmModal open={!!confirmDelete} danger title="Delete permission"
        message={'Delete permission "' + (confirmDelete?.name || '') + '"?'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deletePermission(tenantId, confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)} />
    </div>
  );
}

export default PermissionsView;
