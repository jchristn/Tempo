import { useTranslation } from 'react-i18next';
import { PAGE_SIZES } from '../utils/constants';
import { formatNumber } from '../utils/formatters';

function Pagination({ pageNumber, pageSize, totalCount, onPageChange, onPageSizeChange }) {
  const { t, i18n } = useTranslation();
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const canPrev = pageNumber > 1;
  const canNext = pageNumber < totalPages;

  return (
    <div className="pagination">
      <div>
        <span>
          {formatNumber(totalCount, undefined, i18n.resolvedLanguage)} {t('components.table.pagination.records', { count: totalCount })} · {t('components.table.pagination.page')} {formatNumber(pageNumber, undefined, i18n.resolvedLanguage)} {t('components.table.pagination.of')} {formatNumber(totalPages, undefined, i18n.resolvedLanguage)}
        </span>
      </div>
      <div className="controls">
        <button className="button-secondary" disabled={!canPrev} onClick={() => onPageChange(1)}>{t('common.actions.first')}</button>
        <button className="button-secondary" disabled={!canPrev} onClick={() => onPageChange(pageNumber - 1)}>{t('common.actions.previous')}</button>
        <button className="button-secondary" disabled={!canNext} onClick={() => onPageChange(pageNumber + 1)}>{t('common.actions.next')}</button>
        <button className="button-secondary" disabled={!canNext} onClick={() => onPageChange(totalPages)}>{t('common.actions.last')}</button>
        {onPageSizeChange && (
          <select className="page-size-select" value={pageSize} onChange={(e) => onPageSizeChange(parseInt(e.target.value, 10))}>
            {PAGE_SIZES.map((s) => <option key={s} value={s}>{formatNumber(s, undefined, i18n.resolvedLanguage)} / {t('components.table.pagination.page')}</option>)}
          </select>
        )}
      </div>
    </div>
  );
}

export default Pagination;
