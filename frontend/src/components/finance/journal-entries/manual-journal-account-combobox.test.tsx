import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ManualJournalAccountCombobox } from './manual-journal-account-combobox';
import type { Account, AccountingBook } from '@/types/finance';

Object.assign(globalThis, { React });
Object.assign(globalThis, {
  ResizeObserver: class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
});
Object.defineProperty(Element.prototype, 'scrollIntoView', {
  configurable: true,
  value: vi.fn(),
});

const books: AccountingBook[] = [{
  id: 'ifrs', tenantId: 'tenant', code: 'IFRS', name: 'IFRS', purpose: 'Primary',
  isActive: true, isDefault: true, allowsPosting: true, isSystemDefined: true, sortOrder: 1,
}];

const accounts = [
  {
    id: 'salary', accountNumber: '100-6000-0000', accountCode: 'SALARY',
    accountName: 'Salaries - Finance & Administration', isIFRSClassified: true,
  },
  {
    id: 'accrued', accountNumber: '000-2100-0000', accountCode: 'ACCRUED',
    accountName: 'Accrued Expenses', isIFRSClassified: true,
  },
] as Account[];

describe('ManualJournalAccountCombobox', () => {
  it('supports account search and selection in the shared create/edit control', () => {
    const onSelect = vi.fn();
    render(
      <ManualJournalAccountCombobox
        accounts={accounts}
        selectedAccountId=""
        lineNumber={1}
        targetAccountingBooks={books}
        fallbackBookCode="IFRS"
        targetBookLabel="IFRS"
        onSelect={onSelect}
        onClear={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole('combobox', { name: 'Line 1 account' }));
    fireEvent.change(screen.getByPlaceholderText('Search accounts...'), { target: { value: 'accrued' } });
    expect(screen.queryByText(/Salaries - Finance/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByText('000-2100-0000 - Accrued Expenses'));
    expect(onSelect).toHaveBeenCalledWith('accrued');
  });

  it('lets an editor clear the selected account explicitly', () => {
    const onClear = vi.fn();
    render(
      <ManualJournalAccountCombobox
        accounts={accounts}
        selectedAccountId="salary"
        lineNumber={2}
        targetAccountingBooks={books}
        fallbackBookCode="IFRS"
        targetBookLabel="IFRS"
        onSelect={vi.fn()}
        onClear={onClear}
      />
    );

    fireEvent.click(screen.getByRole('combobox', { name: 'Line 2 account' }));
    fireEvent.click(screen.getByRole('button', { name: 'Clear account' }));
    expect(onClear).toHaveBeenCalledOnce();
  });
});
