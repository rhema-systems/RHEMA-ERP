import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { PostingAccountPicker } from './PostingAccountPicker';
import type { Account } from '@/types/finance';

beforeAll(() => {
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
  Element.prototype.scrollIntoView = vi.fn();
});
const accounts = [
  { id: 'inventory', accountCode: 'INV', accountNumber: '000-1300-00', accountName: 'Inventory' },
  { id: 'expense', accountCode: 'EXP', accountNumber: '000-5100-00', accountName: 'Consumption' },
] as Account[];

describe('posting account picker', () => {
  it('searches account numbers and emits the selected ID', async () => {
    const onChange = vi.fn();
    render(<PostingAccountPicker id="accounts" accounts={accounts} onChange={onChange} />);
    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.change(screen.getByPlaceholderText('Search accounts...'), { target: { value: '1300' } });
    await waitFor(() => expect(screen.queryByRole('option', { name: /Consumption/ })).not.toBeInTheDocument());
    fireEvent.click(screen.getByRole('option', { name: /Inventory/ }));
    expect(onChange).toHaveBeenCalledWith('inventory');
  });
  it('clears with explicit null for default fallback', () => {
    const onChange = vi.fn();
    render(<PostingAccountPicker id="accounts" value="inventory" accounts={accounts} onChange={onChange} />);
    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.click(screen.getByRole('option', { name: 'Use default' }));
    expect(onChange).toHaveBeenCalledWith(null);
  });
  it('does not disguise an unavailable stored mapping as an unset account', () => {
    render(<PostingAccountPicker id="accounts" value="missing" accounts={accounts} onChange={vi.fn()} />);
    expect(screen.getByRole('combobox')).toHaveTextContent('Saved account unavailable');
  });
});
