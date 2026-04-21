/**
 * DataTable is the presentation-only render of rows/columns. Pagination is a
 * separate component (TablePagination) rendered ABOVE the table; the parent
 * owns page state.
 *
 * Optional multi-select: pass selected (Set) and onSelectedChange; the component
 * renders a leading checkbox column and a select-all header checkbox.
 */
function DataTable({
  columns,
  items = [],
  loading = false,
  emptyMessage = 'No items found',
  onRowClick = null,
  selectable = false,
  rowId = (item) => item.id,
  selected = null,
  onSelectedChange = null,
  withPagination = true
}) {
  if (loading) {
    return (
      <div className={'data-table-wrapper' + (withPagination ? ' with-pagination' : '')}>
        <div className="empty-state"><div className="loading-spinner" style={{ margin: '0 auto' }} /></div>
      </div>
    );
  }

  const ids = items.map((x) => rowId(x));
  const allSelected = selectable && ids.length > 0 && ids.every((id) => selected?.has(id));
  const someSelected = selectable && ids.some((id) => selected?.has(id));

  const toggleAll = () => {
    if (!onSelectedChange) return;
    const next = new Set(selected);
    if (allSelected) ids.forEach((id) => next.delete(id));
    else ids.forEach((id) => next.add(id));
    onSelectedChange(next);
  };

  const toggleRow = (id, e) => {
    if (!onSelectedChange) return;
    e?.stopPropagation();
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onSelectedChange(next);
  };

  return (
    <div className={'data-table-wrapper' + (withPagination ? ' with-pagination' : '')}>
      <table className="data-table">
        <thead>
          <tr>
            {selectable && (
              <th className="select">
                <input type="checkbox"
                  checked={allSelected}
                  ref={(el) => { if (el) el.indeterminate = !allSelected && someSelected; }}
                  onChange={toggleAll}
                  aria-label="Select all rows"
                  style={{ width: 'auto' }} />
              </th>
            )}
            {columns.map((col) => (
              <th key={col.key} style={col.style} title={col.tip || undefined}>{col.label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.length > 0 ? items.map((item, i) => {
            const id = rowId(item);
            return (
              <tr key={id || i} className={onRowClick ? 'clickable' : ''} onClick={onRowClick ? () => onRowClick(item) : undefined}>
                {selectable && (
                  <td className="select" onClick={(e) => e.stopPropagation()}>
                    <input type="checkbox"
                      checked={selected?.has(id) || false}
                      onChange={(e) => toggleRow(id, e)}
                      aria-label="Select row"
                      style={{ width: 'auto' }} />
                  </td>
                )}
                {columns.map((col) => (
                  <td key={col.key} className={col.cellClass} style={col.cellStyle}>
                    {col.render ? col.render(item) : item[col.key]}
                  </td>
                ))}
              </tr>
            );
          }) : (
            <tr>
              <td colSpan={(selectable ? 1 : 0) + columns.length} className="empty-state">{emptyMessage}</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

export default DataTable;
