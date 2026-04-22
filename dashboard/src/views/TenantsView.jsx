import { useEffect, useState } from 'react';
import PageHeader from '../components/PageHeader';
import TableFrame from '../components/TableFrame';
import Modal from '../components/Modal';
import CopyableId from '../components/CopyableId';
import ConfirmModal from '../components/ConfirmModal';
import JsonViewerModal from '../components/JsonViewerModal';
import ModalRecordId from '../components/ModalRecordId';
import RowActions from '../components/RowActions';
import { formatTime } from '../utils/formatters';

function TenantsView({ apiClient }) {
  const [data, setData] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = () => setRefreshKey((k) => k + 1);

  useEffect(() => {
    if (!apiClient) return;
    let cancelled = false;
    setLoading(true);
    apiClient.listTenants({ pageNumber, pageSize, includeInactive })
      .then((d) => { if (!cancelled) setData(d); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [apiClient, pageNumber, pageSize, includeInactive, refreshKey]);

  const save = async () => {
    if (editing.id) await apiClient.updateTenant(editing.id, editing);
    else await apiClient.createTenant(editing);
    setEditing(null);
    refresh();
  };

  const columns = [
    { key: 'name', label: 'Name', tip: 'Display name shown to users in this tenant' },
    { key: 'id', label: 'Identifier', tip: 'Globally unique tenant id (prefix ten_)', render: (t) => <CopyableId value={t.id} /> },
    { key: 'region', label: 'Region', tip: 'Optional grouping label for routing or analytics', render: (t) => t.region || '-' },
    { key: 'active', label: 'Active', tip: 'Inactive tenants are hidden by default and reject auth', render: (t) => t.active ? 'Yes' : 'No' },
    { key: 'createdUtc', label: 'Created', tip: 'When the tenant record was created', render: (t) => formatTime(t.createdUtc) },
    { key: 'actions', label: '', style: { width: 48 }, render: (t) => (
      <RowActions
        onEdit={() => setEditing(t)}
        onViewJson={() => setJsonRow(t)}
        onDelete={() => setConfirmDelete(t)}
        deleteDisabled={!!t.isProtected}
      />
    )}
  ];

  return (
    <div>
      <PageHeader
        title="Tenants"
        subtitle={'Create isolation boundaries that own users, flows, runs, and credentials. ' + (data?.totalCount ?? '-') + ' tenants total.'}
        actions={
          <>
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--font-size-sm)', color: 'var(--color-text-secondary)' }} title="Show tenants whose Active flag is false">
              <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} style={{ width: 'auto' }} />
              Include inactive
            </label>
            <button className="button-primary" onClick={() => setEditing({ name: '', active: true })}>+ New tenant</button>
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
        onRowClick={(t) => setEditing(t)}
      />

      {editing && (
        <Modal
          open
          size="small"
          onClose={() => setEditing(null)}
          title={editing.id ? 'Edit tenant' : 'Create tenant'}
          headerMeta={<ModalRecordId label="Tenant ID" value={editing.id} />}
          footer={<>
            <button className="button-secondary" onClick={() => setEditing(null)}>Cancel</button>
            <button className="button-primary" onClick={save}>Save</button>
          </>}
        >
          <div className="form-row"><label title="Display name for the tenant; visible in the dashboard and audit trail">Name</label><input value={editing.name || ''} placeholder="Acme Corporation" onChange={(e) => setEditing({ ...editing, name: e.target.value })} /></div>
          <div className="form-row"><label title="Optional free-form region label, e.g. us-east, eu-west, dc1">Region</label><input value={editing.region || ''} placeholder="us-east" onChange={(e) => setEditing({ ...editing, region: e.target.value })} /></div>
          <div className="form-row"><label title="Inactive tenants reject all authentication"><input type="checkbox" checked={!!editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} style={{ width: 'auto' }} /> Active</label></div>
        </Modal>
      )}

      <JsonViewerModal open={!!jsonRow} onClose={() => setJsonRow(null)} value={jsonRow} title="Tenant JSON" />
      <ConfirmModal
        open={!!confirmDelete}
        danger
        title="Delete tenant"
        recordId={confirmDelete?.id || ''}
        recordIdLabel="Tenant ID"
        message={'Delete tenant "' + (confirmDelete?.name || '') + '"? All users, flows, and runs under this tenant will be deleted.'}
        confirmLabel="Delete"
        onConfirm={async () => { await apiClient.deleteTenant(confirmDelete.id); setConfirmDelete(null); refresh(); }}
        onCancel={() => setConfirmDelete(null)}
      />
    </div>
  );
}

export default TenantsView;
