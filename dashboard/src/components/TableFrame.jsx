import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import TablePagination from './TablePagination';
import DataTable from './DataTable';
import ConfirmModal from './ConfirmModal';
import { TrashIcon } from './Icons';
import { formatNumber } from '../utils/formatters';

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
  bulkDeleteLabel,
  leftSlot,
  rightSlot
}) {
  const { t, i18n } = useTranslation();
  const [selected, setSelected] = useState(new Set());
  const [confirmOpen, setConfirmOpen] = useState(false);

  const selectedIds = Array.from(selected);
  const resolvedBulkDeleteLabel = bulkDeleteLabel || t('components.table.bulkDeleteLabel');

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
          <span><strong>{formatNumber(selectedIds.length, undefined, i18n.resolvedLanguage)}</strong> {t('components.table.bulkSelected', { count: selectedIds.length })}</span>
          <button className="button-secondary" onClick={() => setSelected(new Set())}>{t('common.actions.clear')}</button>
          <button className="button-danger" onClick={() => setConfirmOpen(true)}>
            <TrashIcon size={14} /> {resolvedBulkDeleteLabel}
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
        title={resolvedBulkDeleteLabel}
        message={t('components.table.bulkDeleteConfirm', { count: selectedIds.length })}
        confirmLabel={t('common.actions.delete')}
        onConfirm={handleBulkDelete}
        onCancel={() => setConfirmOpen(false)}
      />
    </>
  );
}

export default TableFrame;
