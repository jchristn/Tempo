import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronLeftIcon, ChevronRightIcon, ChevronsLeftIcon, ChevronsRightIcon, RefreshIcon } from './Icons';
import { PAGE_SIZES } from '../utils/constants';
import { formatNumber } from '../utils/formatters';

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
  const { t, i18n } = useTranslation();
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
    <div className="table-pagination" role="group" aria-label={t('components.table.pagination.groupLabel')}>
      <div className="table-pagination-summary">
        <span className="table-pagination-total">
          <strong>{formatNumber(totalRecords, undefined, i18n.resolvedLanguage)}</strong> {t('components.table.pagination.records', { count: totalRecords })}
        </span>
        {leftSlot}
      </div>
      <div className="table-pagination-controls">
        {rightSlot}
        {onPageSizeChange && (
          <label className="table-pagination-size">
            <span>{t('components.table.pagination.perPage')}</span>
            <select value={pageSize} onChange={(e) => onPageSizeChange(parseInt(e.target.value, 10))} disabled={disabled}>
              {pageSizeOptions.map((s) => <option key={s} value={s}>{formatNumber(s, undefined, i18n.resolvedLanguage)}</option>)}
            </select>
          </label>
        )}
        <button className="table-pagination-btn" disabled={!canPrev} onClick={() => onPageChange(1)} aria-label={t('components.table.pagination.firstPage')} title={t('components.table.pagination.firstPage')}>
          <ChevronsLeftIcon size={14} />
        </button>
        <button className="table-pagination-btn" disabled={!canPrev} onClick={() => onPageChange(pageNumber - 1)} aria-label={t('components.table.pagination.previousPage')} title={t('components.table.pagination.previousPage')}>
          <ChevronLeftIcon size={14} />
        </button>
        <label className="table-pagination-jump">
          <span>{t('components.table.pagination.page')}</span>
          <input
            type="text"
            inputMode="numeric"
            value={input}
            onChange={(e) => setInput(e.target.value.replace(/[^\d]/g, ''))}
            onBlur={submit}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); submit(); } }}
            disabled={disabled || totalPages <= 1}
          />
          <span>{t('components.table.pagination.of')} {formatNumber(totalPages, undefined, i18n.resolvedLanguage)}</span>
        </label>
        <button className="table-pagination-btn" disabled={!canNext} onClick={() => onPageChange(pageNumber + 1)} aria-label={t('components.table.pagination.nextPage')} title={t('components.table.pagination.nextPage')}>
          <ChevronRightIcon size={14} />
        </button>
        <button className="table-pagination-btn" disabled={!canNext} onClick={() => onPageChange(totalPages)} aria-label={t('components.table.pagination.lastPage')} title={t('components.table.pagination.lastPage')}>
          <ChevronsRightIcon size={14} />
        </button>
        {onRefresh && (
          <button className="table-pagination-btn" onClick={onRefresh} aria-label={t('common.actions.refresh')} title={t('common.actions.refresh')} disabled={disabled}>
            <RefreshIcon size={14} />
          </button>
        )}
      </div>
    </div>
  );
}

export default TablePagination;
