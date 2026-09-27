import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { InventoryItemAccountsTab, inventoryPostingAccountFields } from './InventoryItemAccountsTab';

vi.mock('@/services/inventoryManagementService', () => ({ inventoryManagementService: { getItemPostingAccounts: vi.fn(async () => [
  { id: 'stock', accountType: 'Asset', accountName: 'Inventory', isControlAccount: true, allowDirectPosting: false },
  { id: 'cost', accountType: 'Expense', accountName: 'Expense', isControlAccount: false, allowDirectPosting: true },
  { id: 'parent', accountType: 'Asset', accountName: 'Parent', isControlAccount: false, allowDirectPosting: false },
]) } }));
vi.mock('@/components/finance/PostingAccountPicker', () => ({ PostingAccountPicker: ({ id, value, accounts, onChange, disabled }: any) =>
  <select id={id} value={value || ''} disabled={disabled} onChange={event => onChange(event.target.value || null)}>
    <option value="">Use default</option>{accounts.map((account: any) => <option key={account.id} value={account.id}>{account.accountName}</option>)}
  </select> }));

describe('inventory item accounts', () => {
  it('renders all 17 mappings and filters account purpose without losing other mappings', async () => {
    const onChange = vi.fn();
    render(<InventoryItemAccountsTab value={{ inventoryAccountId: 'stock', salesAccountId: 'sales' }} onChange={onChange} />);
    expect(inventoryPostingAccountFields).toHaveLength(17);
    const inventory = screen.getByLabelText('Inventory');
    await waitFor(() => expect(inventory).not.toBeDisabled());
    expect(inventory).toHaveTextContent('Inventory');
    expect(inventory).not.toHaveTextContent('Expense');
    expect(inventory).not.toHaveTextContent('Parent');
    fireEvent.change(inventory, { target: { value: '' } });
    expect(onChange).toHaveBeenCalledWith({ inventoryAccountId: null, salesAccountId: 'sales' });
  });
});
