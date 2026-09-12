import React, { useState } from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PurchaseOrderPlannedCosts } from './PurchaseOrderPlannedCosts';
import type { PlannedCostLine } from '@/lib/purchase-order-landed-costs';

function Harness() {
  const [costs, setCosts] = useState<PlannedCostLine[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  return <>
    <button onClick={() => setSelected('A')}>Line A</button><button onClick={() => setSelected('B')}>Line B</button>
    <output data-testid="saved">{JSON.stringify(costs)}</output>
    <PurchaseOrderPlannedCosts costs={costs} onChange={setCosts} currency="GHS" onCurrencyChange={() => {}}
      notes="" onNotesChange={() => {}} suppliers={[]} lines={[{ tempId: 'A', itemName: 'Device' }, { tempId: 'B', itemName: 'Pipe' }]}
      selectedLineKey={selected} onCloseLine={() => setSelected(null)} />
  </>;
}
describe('line planned cost dialog', () => {
  it('saves a line-specific estimate without changing PO-wide estimates', () => {
    render(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Add planned cost' }));
    fireEvent.change(screen.getByLabelText('Cost 1 amount'), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Line A' }));
    const dialog = within(screen.getByRole('dialog'));
    fireEvent.click(dialog.getByRole('button', { name: 'Add planned cost' }));
    fireEvent.change(dialog.getByLabelText('Cost 1 amount'), { target: { value: '40' } });
    expect(dialog.queryByLabelText('Cost 1 allocation method')).toBeNull();
    fireEvent.click(dialog.getByRole('button', { name: 'Save line costs' }));
    const saved = JSON.parse(screen.getByTestId('saved').textContent || '[]');
    expect(saved).toHaveLength(2);
    expect(saved[0].purchaseOrderLineKey).toBeUndefined();
    expect(saved[1]).toMatchObject({ purchaseOrderLineKey: 'A', amount: 40 });
    fireEvent.click(screen.getByRole('button', { name: 'Line B' }));
    expect(within(screen.getByRole('dialog')).getByText('No planned costs added.')).toBeTruthy();
  });
  it('cancel discards only modal edits', () => {
    render(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Line A' }));
    const dialog = within(screen.getByRole('dialog'));
    fireEvent.click(dialog.getByRole('button', { name: 'Add planned cost' }));
    fireEvent.change(dialog.getByLabelText('Cost 1 amount'), { target: { value: '40' } });
    fireEvent.click(dialog.getByRole('button', { name: 'Cancel' }));
    expect(screen.getByTestId('saved').textContent).toBe('[]');
  });
  it('invalid entries leave the dialog and user input open', () => {
    render(<Harness />);
    fireEvent.click(screen.getByRole('button', { name: 'Line A' }));
    const dialog = within(screen.getByRole('dialog'));
    fireEvent.click(dialog.getByRole('button', { name: 'Add planned cost' }));
    fireEvent.click(dialog.getByRole('button', { name: 'Save line costs' }));
    expect(dialog.getByRole('alert').textContent).toContain('positive amount');
    expect(screen.getByTestId('saved').textContent).toBe('[]');
  });
});
