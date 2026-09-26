import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { BusinessPartnerReceivablesFields } from './BusinessPartnerReceivablesFields';
import type { Account } from '@/types/finance';

vi.mock('@/components/finance/PostingAccountPicker', () => ({ PostingAccountPicker: ({ id, value, onChange, accounts }: any) =>
  <select aria-label={id} value={value ?? ''} onChange={e => onChange(e.target.value)}><option value="">None</option>{accounts.map((a: any) => <option key={a.id} value={a.id}>{a.accountName}</option>)}</select> }));

describe('customer adjustment accounts', () => {
  const accounts = [
    { id: 'income', accountName: 'Finance income', accountType: 'Revenue', status: 'Active', allowDirectPosting: true },
    { id: 'loss', accountName: 'Bad debts', accountType: 'Expense', status: 'Active', allowDirectPosting: true },
    { id: 'asset', accountName: 'Cash', accountType: 'Asset', status: 'Active', allowDirectPosting: true },
  ] as Account[];
  it('limits each adjustment mapping to its posting account type', () => {
    render(<BusinessPartnerReceivablesFields accounts={accounts} onChange={() => {}} />);
    expect(screen.getByLabelText('financeChargesAccountId').textContent).toContain('Finance income');
    expect(screen.getByLabelText('financeChargesAccountId').textContent).not.toContain('Bad debts');
    expect(screen.getByLabelText('writeoffAccountId').textContent).toContain('Bad debts');
    expect(screen.getByLabelText('writeoffAccountId').textContent).not.toContain('Finance income');
    expect(screen.getByLabelText('overpaymentWriteoffAccountId').textContent).toContain('Finance income');
    expect(screen.getByLabelText('overpaymentWriteoffAccountId').textContent).not.toContain('Cash');
  });
  it('retains existing receivables mappings when setting an overpayment account', () => {
    const onChange = vi.fn();
    render(<BusinessPartnerReceivablesFields accounts={accounts} value={{ salesAccountId: 'income', writeoffAccountId: 'loss' }} onChange={onChange} />);
    fireEvent.change(screen.getByLabelText('overpaymentWriteoffAccountId'), { target: { value: 'income' } });
    expect(onChange).toHaveBeenCalledWith({ salesAccountId: 'income', writeoffAccountId: 'loss', overpaymentWriteoffAccountId: 'income' });
  });
});
