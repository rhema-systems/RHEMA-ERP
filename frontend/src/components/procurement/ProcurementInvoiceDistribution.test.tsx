import React from 'react';
import '@testing-library/jest-dom/vitest';
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { ProcurementInvoiceDistribution } from './ProcurementInvoiceDistribution';
import { apiService } from '@/services/api.service';

vi.mock('@/services/api.service', () => ({
  apiService: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}));
vi.mock('@/components/finance/PostingAccountPicker', () => ({
  PostingAccountPicker: ({ id, value, accounts, onChange, disabled }: any) => (
    <select
      id={id}
      value={value}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value)}
    >
      {accounts.map((account: any) => (
        <option key={account.id} value={account.id}>
          {account.accountName}
        </option>
      ))}
    </select>
  ),
}));
beforeEach(() => vi.resetAllMocks());

const editable = () => ({
  invoiceId: 'supplier-invoice',
  status: 'Proposed',
  currency: 'GHS',
  basis: 'Invoice posting rules',
  canEdit: true,
  version: 'version-one',
  basisVersion: 'basis-one',
  totalDebit: 100,
  totalCredit: 100,
  groups: [
    {
      groupId: '1',
      type: 'Accounts payable',
      description: 'Supplier payable',
      accountId: 'ap',
      canChangeAccount: false,
      debit: 0,
      credit: 100,
    },
    {
      groupId: '2',
      type: 'Expense',
      description: 'Services',
      accountId: 'expense',
      canChangeAccount: true,
      debit: 100,
      credit: 0,
    },
  ],
  lines: [
    {
      lineId: '00000000-0000-0000-0000-000000000001',
      groupId: '1',
      accountId: 'ap',
      accountCode: '2100',
      accountName: 'Supplier payable',
      type: 'Accounts payable',
      debit: 0,
      credit: 100,
    },
    {
      lineId: '00000000-0000-0000-0000-000000000002',
      groupId: '2',
      accountId: 'expense',
      accountCode: '5000',
      accountName: 'Purchases',
      type: 'Expense',
      debit: 100,
      credit: 0,
    },
  ],
});
async function openEditable(source = editable()) {
  vi.mocked(apiService.get).mockImplementation(async (path) =>
    path.endsWith('/accounts')
      ? [
          { id: 'expense', accountCode: '5000', accountName: 'Purchases' },
          { id: 'other', accountCode: '5100', accountName: 'Other expense' },
        ]
      : source
  );
  render(<ProcurementInvoiceDistribution invoiceId="supplier-invoice" />);
  fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
  await screen.findByText('Supplier payable');
  await waitFor(() =>
    expect(screen.getByRole('button', { name: 'Add line' })).toBeEnabled()
  );
}

it('reads supplier distributions through the Procurement boundary without posting', async () => {
  vi.mocked(apiService.get).mockResolvedValue({
    invoiceId: 'supplier-invoice',
    status: 'Proposed',
    currency: 'GHS',
    basis: 'Supplier account defaults',
    totalDebit: 100,
    totalCredit: 100,
    lines: [
      {
        lineId: 'expense',
        accountCode: '5000',
        accountName: 'Purchases',
        debit: 100,
        credit: 0,
      },
      {
        lineId: 'payable',
        accountCode: '2100',
        accountName: 'Supplier payable',
        debit: 0,
        credit: 100,
      },
    ],
  });
  render(<ProcurementInvoiceDistribution invoiceId="supplier-invoice" />);
  expect(apiService.get).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
  expect(await screen.findByText('Supplier payable')).toBeInTheDocument();
  expect(apiService.get).toHaveBeenCalledExactlyOnceWith(
    '/procurement/supplier-invoices/supplier-invoice/distribution'
  );
  expect(screen.getByText('Balanced')).toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Post' })
  ).not.toBeInTheDocument();
}, 30_000);

it('splits, changes accounts, saves and uses the returned version on the next save', async () => {
  await openEditable();
  fireEvent.click(screen.getByRole('button', { name: 'Split line 2' }));
  fireEvent.change(screen.getByLabelText('Account 3'), {
    target: { value: 'other' },
  });
  vi.mocked(apiService.put).mockImplementation(async (_path, input: any) => ({
    ...editable(),
    version: 'version-two',
    hasOverrides: true,
    lines: input.lines.map((line: any) => ({
      ...line,
      accountCode: '5100',
      accountName: 'Saved account',
    })),
  }));
  fireEvent.click(screen.getByRole('button', { name: 'Save distribution' }));
  await screen.findByText('Distribution saved.');
  expect(apiService.put).toHaveBeenCalledWith(
    '/procurement/supplier-invoices/supplier-invoice/distribution',
    expect.objectContaining({
      version: 'version-one',
      basisVersion: 'basis-one',
      lines: expect.arrayContaining([
        expect.objectContaining({
          groupId: '2',
          accountId: 'other',
          debit: 50,
          credit: 0,
        }),
        expect.objectContaining({
          groupId: '2',
          accountId: 'expense',
          debit: 50,
          credit: 0,
        }),
      ]),
    })
  );
  fireEvent.click(screen.getByRole('button', { name: 'Remove line 3' }));
  fireEvent.change(screen.getByLabelText('Debit 2'), {
    target: { value: '100' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Save distribution' }));
  await waitFor(() => expect(apiService.put).toHaveBeenCalledTimes(2));
  expect(vi.mocked(apiService.put).mock.calls[1][1]).toMatchObject({
    version: 'version-two',
  });
});

it('adds and removes lines and rejects an unbalanced save without calling the API', async () => {
  await openEditable();
  fireEvent.click(screen.getByRole('button', { name: 'Add line' }));
  fireEvent.change(screen.getByLabelText('Debit 3'), {
    target: { value: '10' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Save distribution' }));
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Total debits and credits must balance'
  );
  expect(apiService.put).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Remove line 3' }));
  expect(screen.queryByLabelText('Debit 3')).not.toBeInTheDocument();
  expect(screen.getByText('Balanced')).toBeInTheDocument();
});

it('rejects balanced inflation and sub-cent amounts before saving', async () => {
  await openEditable();
  fireEvent.change(screen.getByLabelText('Debit 2'), {
    target: { value: '110' },
  });
  fireEvent.change(screen.getByLabelText('Credit 1'), {
    target: { value: '110' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Save distribution' }));
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Preserve each invoice amount'
  );
  fireEvent.change(screen.getByLabelText('Debit 2'), {
    target: { value: '100.001' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Save distribution' }));
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'at most two decimal places'
  );
  expect(apiService.put).not.toHaveBeenCalled();
});

it('retains edits on a stale-version response and confirms discarding unsaved changes', async () => {
  await openEditable();
  fireEvent.click(screen.getByRole('button', { name: 'Split line 2' }));
  vi.mocked(apiService.put).mockRejectedValue({
    response: {
      data: {
        detail: 'The invoice changed. Reload and review.',
        code: 'AP_DISTRIBUTION_CHANGED',
      },
    },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Save distribution' }));
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'The invoice changed'
  );
  expect(screen.getByLabelText('Debit 3')).toHaveValue(50);
  fireEvent.click(screen.getAllByRole('button', { name: 'Close' })[0]);
  const confirmation = await screen.findByRole('dialog', {
    name: 'Discard unsaved changes?',
  });
  fireEvent.click(within(confirmation).getByRole('button', { name: 'Cancel' }));
  expect(screen.getByLabelText('Debit 3')).toHaveValue(50);
});

it('resets only after confirmation and never invokes a posting endpoint', async () => {
  await openEditable();
  vi.mocked(apiService.post).mockResolvedValue(editable());
  fireEvent.click(screen.getByRole('button', { name: 'Reset to defaults' }));
  expect(apiService.post).not.toHaveBeenCalled();
  const confirmation = await screen.findByRole('dialog', {
    name: 'Reset distribution?',
  });
  fireEvent.click(
    within(confirmation).getByRole('button', { name: 'Reset distribution' })
  );
  await screen.findByText('Default distribution restored.');
  expect(apiService.post).toHaveBeenCalledExactlyOnceWith(
    '/procurement/supplier-invoices/supplier-invoice/distribution/reset',
    {
      version: 'version-one',
      basisVersion: 'basis-one',
      lines: [],
    }
  );
});

it('keeps posted distributions read-only even if canEdit is incorrectly true', async () => {
  vi.mocked(apiService.get).mockResolvedValue({
    ...editable(),
    status: 'Posted',
  });
  render(<ProcurementInvoiceDistribution invoiceId="supplier-invoice" />);
  fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
  await screen.findByText('Supplier payable');
  expect(
    screen.queryByRole('button', { name: 'Add line' })
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Save distribution' })
  ).not.toBeInTheDocument();
  expect(apiService.get).toHaveBeenCalledTimes(1);
});
