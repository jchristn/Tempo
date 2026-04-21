import { PAGE_SIZES } from '../utils/constants';

function Pagination({ pageNumber, pageSize, totalCount, onPageChange, onPageSizeChange }) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const canPrev = pageNumber > 1;
  const canNext = pageNumber < totalPages;

  return (
    <div className="pagination">
      <div>
        <span>{totalCount.toLocaleString()} items · Page {pageNumber} of {totalPages}</span>
      </div>
      <div className="controls">
        <button className="button-secondary" disabled={!canPrev} onClick={() => onPageChange(1)}>First</button>
        <button className="button-secondary" disabled={!canPrev} onClick={() => onPageChange(pageNumber - 1)}>Prev</button>
        <button className="button-secondary" disabled={!canNext} onClick={() => onPageChange(pageNumber + 1)}>Next</button>
        <button className="button-secondary" disabled={!canNext} onClick={() => onPageChange(totalPages)}>Last</button>
        {onPageSizeChange && (
          <select className="page-size-select" value={pageSize} onChange={(e) => onPageSizeChange(parseInt(e.target.value, 10))}>
            {PAGE_SIZES.map((s) => <option key={s} value={s}>{s} / page</option>)}
          </select>
        )}
      </div>
    </div>
  );
}

export default Pagination;
