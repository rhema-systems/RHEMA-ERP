import React, { useState } from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { PhysicalCountItemsGrid } from './PhysicalCountItemsGrid';
import type { PhysicalCountDetailDto } from '@/services/inventoryManagementService';

const makeCount = (size = 300) => ({ id: 'count', status: 'UnderReview', canReview: true, systemQuantityVisible: true,
  items: Array.from({ length: size }, (_, index) => ({ id: `line-${index + 1}`, itemCode: `ITEM-${String(index + 1).padStart(4, '0')}`,
    itemName: `Stock item ${index + 1}`, locationName: 'BIN-A', isCounted: true, systemQuantity: 10, countedQuantity: 9, varianceQuantity: -1 }))
}) as PhysicalCountDetailDto;
const noop = () => {};
const callbacks = { onRemove: noop, onAdd: noop, onImport: noop, onExport: noop };
function Harness({ count = makeCount(), save = noop }: { count?: PhysicalCountDetailDto; save?: (edits: Map<string, number>) => void }) {
  const [edits, setEdits] = useState(new Map<string, number>());
  const [fullPage, setFullPage] = useState(false);
  return <><PhysicalCountItemsGrid count={count} edits={edits} busy={false} fullPage={fullPage} onToggleFullPage={() => setFullPage(v => !v)}
    {...callbacks} onQuantity={(id, value) => setEdits(previous => { const next = new Map(previous); if (value === undefined) next.delete(id); else next.set(id, value); return next; })} />
    {count.canReview && ['InProgress', 'UnderReview'].includes(count.status) && <button disabled={!edits.size} onClick={() => save(edits)}>Save Counts</button>}</>;
}

describe('compact physical count grid', () => {
  it('hides selected recount lines by default and keeps retained observations read-only', () => {
    const count = makeCount(2);
    count.items[0].supersededByPhysicalCountId = 'child';
    count.items[0].recountReason = 'Check quantity';
    render(<Harness count={count} />);
    expect(screen.queryByText('ITEM-0001')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Show recount items' }));
    expect(screen.getByText('ITEM-0001')).toBeInTheDocument();
    expect(screen.queryByRole('spinbutton', { name: 'Counted quantity ITEM-0001' })).not.toBeInTheDocument();
    expect(screen.getByRole('spinbutton', { name: 'Counted quantity ITEM-0002' })).toBeInTheDocument();
  });
  it('edits defective observations separately from the unchanged physical quantity and variance', () => {
    const onDefect = vi.fn();
    const count = makeCount(1);
    render(<PhysicalCountItemsGrid count={count} edits={new Map()} defectEdits={new Map()} onDefect={onDefect}
      busy={false} fullPage={false} onToggleFullPage={noop} onQuantity={noop} {...callbacks} />);
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Defective quantity ITEM-0001' }), { target: { value: '2' } });
    expect(onDefect).toHaveBeenLastCalledWith('line-1', 2, '');
    expect(screen.getByRole('spinbutton', { name: 'Counted quantity ITEM-0001' })).toHaveValue(9);
    expect(screen.getByText('-1')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Defective notes ITEM-0001' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Edit defective notes ITEM-0001' }), { target: { value: 'Broken packaging' } });
    expect(onDefect).toHaveBeenLastCalledWith('line-1', 0, 'Broken packaging');
  });
  it('keeps one saved cost column and puts the current average in supporting details without changing quantities', () => {
    const count = makeCount(1);
    count.items[0].countUnitCost = 1918.85;
    count.items[0].itemAverageCost = 1907.09;
    render(<Harness count={count} />);
    expect(screen.queryByText('1,907.09')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Show costs' }));
    expect(screen.getByRole('columnheader', { name: 'Count unit cost' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Item-wide avg. cost' })).not.toBeInTheDocument();
    expect(screen.getByText('1,918.85')).toBeInTheDocument();
    expect(screen.queryByText('1,907.09')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Current cost details ITEM-0001' }));
    expect(screen.getByText('1,907.09')).toBeInTheDocument();
    expect(screen.getByText('Current item average')).toBeInTheDocument();
    expect(screen.getByRole('spinbutton')).toHaveValue(9);
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    expect(screen.getByText('1,907.09')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Hide costs' }));
    expect(screen.queryByText('1,907.09')).not.toBeInTheDocument();
  });
  it('never exposes cost columns or values during blind counting, even if supplied by an older response', () => {
    const count = makeCount(1);
    count.systemQuantityVisible = false;
    count.items[0].countUnitCost = 1918.85;
    count.items[0].itemAverageCost = 1907.09;
    render(<Harness count={count} />);
    expect(screen.queryByRole('button', { name: 'Show costs' })).not.toBeInTheDocument();
    expect(screen.queryByText('1,918.85')).not.toBeInTheDocument();
    expect(screen.queryByText('1,907.09')).not.toBeInTheDocument();
  });
  it('does not substitute the saved count cost when the current item-wide cost is unavailable', () => {
    const count = makeCount(1);
    count.items[0].countUnitCost = 1918.85;
    count.items[0].itemAverageCost = null;
    render(<Harness count={count} />);
    fireEvent.click(screen.getByRole('button', { name: 'Show costs' }));
    expect(screen.getAllByText('1,918.85')).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: 'Current cost details ITEM-0001' }));
    expect(screen.getByText('—')).toBeInTheDocument();
  });
  it('renders only one page from a 300-line count and numbers the last page correctly', () => {
    render(<Harness />);
    expect(screen.getAllByRole('spinbutton')).toHaveLength(25);
    expect(screen.getByText('1–25 of 300 items')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Previous page' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Last page' }));
    expect(screen.getByText('276–300 of 300 items')).toBeInTheDocument();
    expect(screen.getByRole('spinbutton', { name: 'Counted quantity ITEM-0300' })).toHaveValue(9);
    expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled();
    const region = screen.getByRole('region', { name: 'Count items grid' });
    expect(region).toHaveClass('min-h-0', 'flex-1', 'overflow-auto');
    expect(within(region).getAllByRole('rowgroup')[0]).toHaveClass('sticky', 'top-0');
    expect(screen.getByRole('spinbutton', { name: 'Counted quantity ITEM-0300' })).toHaveClass('h-6');
  });
  it('supports 50 and 100 rows and searches the entire count, not just the visible page', () => {
    render(<Harness />);
    for (const size of [50, 100]) {
      fireEvent.change(screen.getByRole('combobox', { name: 'Rows per page' }), { target: { value: String(size) } });
      expect(screen.getAllByRole('spinbutton')).toHaveLength(size);
    }
    fireEvent.change(screen.getByRole('textbox', { name: 'Search count items' }), { target: { value: 'item-0276' } });
    expect(screen.getAllByRole('spinbutton')).toHaveLength(1);
    expect(screen.getByText('ITEM-0276')).toBeInTheDocument();
    fireEvent.change(screen.getByRole('textbox', { name: 'Search count items' }), { target: { value: 'unknown' } });
    expect(screen.getByText('No items match your search.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next page' })).toBeDisabled();
  });
  it('retains edits across pages, search and full-page/restore, and saves all edits together', () => {
    const save = vi.fn(); render(<Harness save={save} />);
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Counted quantity ITEM-0001' }), { target: { value: '8' } });
    fireEvent.click(screen.getByRole('button', { name: 'Next page' }));
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Counted quantity ITEM-0026' }), { target: { value: '7' } });
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    expect(screen.getByText('Page 2 of 12')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Restore dialog' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Search count items' }), { target: { value: 'ITEM-0001' } });
    expect(screen.getByRole('spinbutton', { name: 'Counted quantity ITEM-0001' })).toHaveValue(8);
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    expect(within(screen.getByTestId('count-items-grid')).queryByRole('button', { name: 'Save Counts' })).not.toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Count items grid' })).not.toContainElement(screen.getByRole('button', { name: 'Save Counts' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save Counts' }));
    expect(save).toHaveBeenCalledWith(new Map([['line-1', 8], ['line-26', 7]]));
  });
  it('clamps pagination after the last item on a draft page is removed', () => {
    const count = makeCount(26); count.status = 'Draft';
    const { rerender } = render(<Harness count={count} />);
    fireEvent.click(screen.getByRole('button', { name: 'Last page' }));
    expect(screen.getByText('26–26 of 26 items')).toBeInTheDocument();
    rerender(<Harness count={{ ...count, items: count.items.slice(0, 25) }} />);
    expect(screen.getByText('1–25 of 25 items')).toBeInTheDocument();
  });
  it('preserves blind count protection and read-only approval states', () => {
    render(<Harness count={{ ...makeCount(1), status: 'PendingStoresApproval', systemQuantityVisible: false, canReview: false }} />);
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
    expect(screen.getAllByText('xxx')).toHaveLength(3);
    expect(screen.queryByText('Protected')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save Counts' })).not.toBeInTheDocument();
  });
  it('leaves full-page actions to the shared footer instead of duplicating Save Counts', () => {
    render(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'View items in full page' }));
    expect(screen.getByRole('button', { name: 'Save Counts' })).toBeDisabled();
    expect(screen.getAllByRole('button', { name: 'Save Counts' })).toHaveLength(1);
    expect(screen.getByTestId('count-items-grid')).not.toContainElement(screen.getByRole('button', { name: 'Save Counts' }));
  });
});
