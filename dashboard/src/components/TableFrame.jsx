import { useState } from 'react';
import TablePagination from './TablePagination';
import DataTable from './DataTable';
import ConfirmModal from './ConfirmModal';
import { TrashIcon } from './Icons';

/**
 * Combines TablePagination (above), optional bulk-action bar (when rows are
 * selected), and DataTable into a single operated unit. Handles multi-select
 * state and an optional bulk-delete callback.
 */
function TableFrame({
  columns,
  items,
  totalRecords,
  pageNumber,
  pageSize,
  onPageChange,
  onPageSizeChange,
  onRefresh,
  loading = false,
  emptyMessage,
  onRowClick,
  selectable = false,
  rowId = (item) => item.id,
  onBulkDelete,
  bulkDeleteLabel = 'Delete Selected',
  leftSlot,
  rightSlot
}) {
  const [selected, setSelected] = useState(new Set());
  const [confirmOpen, setConfirmOpen] = useState(false);

  const selectedIds = Array.from(selected);

  const handleBulkDelete = async () => {
    if (!onBulkDelete) return;
    setConfirmOpen(false);
    try {
      await onBulkDelete(selectedIds);
      setSelected(new Set());
    } catch (err) {
      console.error(err);
    }
  };

  return (
    <>
      <TablePagination
        totalRecords={totalRecords}
        pageNumber={pageNumber}
        pageSize={pageSize}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
        onRefresh={onRefresh}
        disabled={loading}
        leftSlot={leftSlot}
        rightSlot={rightSlot}
      />
      {selectable && onBulkDelete && selectedIds.length > 0 && (
        <div className="bulk-action-bar">
          <span><strong>{selectedIds.length}</strong> selected</span>
          <button className="button-secondary" onClick={() => setSelected(new Set())}>Clear</button>
          <button className="button-danger" onClick={() => setConfirmOpen(true)}>
            <TrashIcon size={14} /> {bulkDeleteLabel}
          </button>
        </div>
      )}
      <DataTable
        columns={columns}
        items={items}
        loading={loading}
        emptyMessage={emptyMessage}
        onRowClick={onRowClick}
        selectable={selectable}
        rowId={rowId}
        selected={selected}
        onSelectedChange={setSelected}
      />
      <ConfirmModal
        open={confirmOpen}
        danger
        title={bulkDeleteLabel}
        message={`Delete ${selectedIds.length} selected item${selectedIds.length === 1 ? '' : 's'}? This cannot be undone.`}
        confirmLabel="Delete"
        onConfirm={handleBulkDelete}
        onCancel={() => setConfirmOpen(false)}
      />
    </>
  );
}

export default TableFrame;
