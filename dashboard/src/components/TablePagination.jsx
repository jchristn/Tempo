import { useEffect, useState } from 'react';
import { ChevronLeftIcon, ChevronRightIcon, ChevronsLeftIcon, ChevronsRightIcon, RefreshIcon } from './Icons';
import { PAGE_SIZES } from '../utils/constants';

/**
 * Pagination bar shown ABOVE tables. Contains total record count, jump-to-page input,
 * first/prev/next/last navigation, page-size selector, and a refresh button.
 */
function TablePagination({
  totalRecords = 0,
  pageNumber = 1,
  pageSize = 25,
  pageSizeOptions = PAGE_SIZES,
  onPageChange,
  onPageSizeChange,
  onRefresh,
  disabled = false,
  leftSlot = null,
  rightSlot = null
}) {
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));
  const [input, setInput] = useState(String(pageNumber));

  useEffect(() => { setInput(String(pageNumber)); }, [pageNumber]);

  const canPrev = !disabled && pageNumber > 1;
  const canNext = !disabled && pageNumber < totalPages;

  const submit = () => {
    const n = parseInt(input, 10);
    if (Number.isNaN(n)) { setInput(String(pageNumber)); return; }
    onPageChange(Math.max(1, Math.min(totalPages, n)));
  };

  return (
    <div className="table-pagination" role="group" aria-label="Table pagination">
      <div className="table-pagination-summary">
        <span className="table-pagination-total">
          <strong>{totalRecords.toLocaleString()}</strong> records
        </span>
        {leftSlot}
      </div>
      <div className="table-pagination-controls">
        {rightSlot}
        {onPageSizeChange && (
          <label className="table-pagination-size">
            <span>Per page</span>
            <select value={pageSize} onChange={(e) => onPageSizeChange(parseInt(e.target.value, 10))} disabled={disabled}>
              {pageSizeOptions.map((s) => <option key={s} value={s}>{s.toLocaleString()}</option>)}
            </select>
          </label>
        )}
        <button className="table-pagination-btn" disabled={!canPrev} onClick={() => onPageChange(1)} aria-label="First page" title="First page">
          <ChevronsLeftIcon size={14} />
        </button>
        <button className="table-pagination-btn" disabled={!canPrev} onClick={() => onPageChange(pageNumber - 1)} aria-label="Previous page" title="Previous page">
          <ChevronLeftIcon size={14} />
        </button>
        <label className="table-pagination-jump">
          <span>Page</span>
          <input
            type="text"
            inputMode="numeric"
            value={input}
            onChange={(e) => setInput(e.target.value.replace(/[^\d]/g, ''))}
            onBlur={submit}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); submit(); } }}
            disabled={disabled || totalPages <= 1}
          />
          <span>of {totalPages.toLocaleString()}</span>
        </label>
        <button className="table-pagination-btn" disabled={!canNext} onClick={() => onPageChange(pageNumber + 1)} aria-label="Next page" title="Next page">
          <ChevronRightIcon size={14} />
        </button>
        <button className="table-pagination-btn" disabled={!canNext} onClick={() => onPageChange(totalPages)} aria-label="Last page" title="Last page">
          <ChevronsRightIcon size={14} />
        </button>
        {onRefresh && (
          <button className="table-pagination-btn" onClick={onRefresh} aria-label="Refresh" title="Refresh" disabled={disabled}>
            <RefreshIcon size={14} />
          </button>
        )}
      </div>
    </div>
  );
}

export default TablePagination;
