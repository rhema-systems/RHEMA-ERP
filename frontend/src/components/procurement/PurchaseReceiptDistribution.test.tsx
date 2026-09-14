import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { PurchaseReceiptDistribution } from './PurchaseReceiptDistribution';
import {
  purchaseReceiptDistributionService,
  type PurchaseReceiptDistribution as Distribution,
} from '@/services/purchaseReceiptDistributionService';

vi.mock('@/services/purchaseReceiptDistributionService', () => ({
  purchaseReceiptDistributionService: {
    get: vi.fn(),
    accounts: vi.fn(),
    save: vi.fn(),
    reset: vi.fn(),
  },
}));
Object.assign(globalThis, {
  ResizeObserver: class {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
});
Element.prototype.scrollIntoView = vi.fn();
const distribution = {
  status: 'Posted' as const,
  currency: 'GHS',
  basis: 'Original posted journal',
  journalEntryNumber: 'JE-123',
  totalDebit: 120,
  totalCredit: 120,
  lines: [
    {
      accountId: 'one',
      accountCode: '1300',
      accountName: 'Inventory',
      type: 'Inventory',
      source: 'Posted journal',
      debit: 120,
      credit: 0,
    },
  ],
};

const accounts = [
  { id: 'one', accountCode: '1300', accountName: 'Inventory' },
  {
    id: 'other',
    accountCode: '1310',
    accountName: 'Receipt inventory override',
  },
  { id: 'clearing', accountCode: '2100', accountName: 'Accrued purchases' },
];
const draft = (): Distribution => ({
  status: 'Proposed',
  currency: 'GHS',
  basis: 'Receipt defaults',
  canEdit: true,
  version: 'v1',
  basisVersion: 'b1',
  totalDebit: 120,
  totalCredit: 120,
  groups: [
    {
      inventoryItemId: 'item',
      itemCode: 'SKU-1',
      itemName: 'Pipe',
      purpose: 'Inventory',
      defaultAccountId: 'one',
      totalDebit: 120,
      totalCredit: 0,
    },
    {
      inventoryItemId: 'item',
      itemCode: 'SKU-1',
      itemName: 'Pipe',
      purpose: 'AccruedPurchases',
      defaultAccountId: 'clearing',
      totalDebit: 0,
      totalCredit: 120,
    },
  ],
  lines: [
    {
      lineId: 'line-one',
      inventoryItemId: 'item',
      itemCode: 'SKU-1',
      itemName: 'Pipe',
      purpose: 'Inventory',
      accountId: 'one',
      accountCode: '1300',
      accountName: 'Inventory',
      type: 'Inventory',
      source: 'Item',
      debit: 120,
      credit: 0,
    },
    {
      lineId: 'line-two',
      inventoryItemId: 'item',
      itemCode: 'SKU-1',
      itemName: 'Pipe',
      purpose: 'AccruedPurchases',
      accountId: 'clearing',
      accountCode: '2100',
      accountName: 'Accrued purchases',
      type: 'Accrued purchases',
      source: 'Supplier',
      debit: 0,
      credit: 120,
    },
  ],
});
async function openDraft(value: Distribution = draft()) {
  vi.mocked(purchaseReceiptDistributionService.get).mockResolvedValue(value);
  render(<PurchaseReceiptDistribution receiptId="receipt-one" />);
  fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
  await screen.findByLabelText('Debit line 1');
  await waitFor(() =>
    expect(
      screen.getByRole('combobox', { name: 'Account line 1' })
    ).toBeEnabled()
  );
}

describe('Purchase receipt Distribution', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(purchaseReceiptDistributionService.get).mockResolvedValue(
      distribution
    );
    vi.mocked(purchaseReceiptDistributionService.accounts).mockResolvedValue(
      accounts
    );
    vi.mocked(purchaseReceiptDistributionService.save).mockResolvedValue({
      ...draft(),
      version: 'v2',
      hasOverrides: true,
    });
    vi.mocked(purchaseReceiptDistributionService.reset).mockResolvedValue(
      draft()
    );
  });
  afterEach(cleanup);
  it('loads only on request and displays the original journal and currency', async () => {
    render(<PurchaseReceiptDistribution receiptId="receipt-one" />);
    expect(purchaseReceiptDistributionService.get).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    expect(await screen.findByText('GHS · JE-123')).toBeInTheDocument();
    expect(screen.getByText('1300')).toBeInTheDocument();
    expect(purchaseReceiptDistributionService.get).toHaveBeenCalledWith(
      'receipt-one'
    );
    expect(
      screen.queryByRole('button', { name: 'Post' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Save' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Add line' })
    ).not.toBeInTheDocument();
    expect(purchaseReceiptDistributionService.accounts).not.toHaveBeenCalled();
  });
  it('maximizes and restores without fetching or changing the receipt', async () => {
    render(<PurchaseReceiptDistribution receiptId="receipt-one" />);
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    await screen.findByText('1300');
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    expect(screen.getByRole('button', { name: 'Restore' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    expect(purchaseReceiptDistributionService.get).toHaveBeenCalledTimes(1);
  });
  it('shows the server error and allows a read-only retry', async () => {
    vi.mocked(purchaseReceiptDistributionService.get).mockRejectedValueOnce(
      new Error('Inventory account is inactive.')
    );
    render(<PurchaseReceiptDistribution receiptId="receipt-one" />);
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Inventory account is inactive.'
    );
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() =>
      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    );
    expect(await screen.findByText('1300')).toBeInTheDocument();
  });

  it('splits, searches accounts, and saves balanced rows with original item/purpose lineage', async () => {
    await openDraft();
    fireEvent.click(screen.getByRole('button', { name: 'Split line 1' }));
    expect(screen.getByLabelText('Debit line 1')).toHaveValue(60);
    expect(screen.getByLabelText('Debit line 2')).toHaveValue(60);
    fireEvent.click(screen.getByRole('combobox', { name: 'Account line 2' }));
    fireEvent.change(screen.getByPlaceholderText('Search accounts...'), {
      target: { value: '1310' },
    });
    fireEvent.click(
      screen.getByRole('option', { name: /Receipt inventory override/ })
    );
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    expect(screen.getByText('Balanced')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await screen.findByText('Distribution saved.');
    const payload = vi.mocked(purchaseReceiptDistributionService.save).mock
      .calls[0][1];
    expect(payload.version).toBe('v1');
    expect(payload.basisVersion).toBe('b1');
    expect(payload.lines).toHaveLength(3);
    expect(payload.lines[0]).toMatchObject({
      lineId: 'line-one',
      inventoryItemId: 'item',
      purpose: 'Inventory',
      debit: 60,
    });
    expect(payload.lines[1]).toMatchObject({
      inventoryItemId: 'item',
      purpose: 'Inventory',
      accountId: 'other',
      debit: 60,
    });
    expect(payload.lines[1].lineId).not.toBe('line-one');
    expect(payload.lines[2]).toMatchObject({
      lineId: 'line-two',
      purpose: 'AccruedPurchases',
      credit: 120,
    });
  });

  it('adds and removes rows without losing source options or amounts', async () => {
    await openDraft();
    fireEvent.click(screen.getByRole('button', { name: 'Add line' }));
    expect(screen.getByLabelText('Debit line 3')).toHaveValue(0);
    fireEvent.click(screen.getByRole('button', { name: 'Remove line 3' }));
    expect(screen.queryByLabelText('Debit line 3')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Debit line 1')).toHaveValue(120);
  });

  it('blocks unbalanced or two-sided rows locally and retains typed amounts', async () => {
    await openDraft();
    fireEvent.change(screen.getByLabelText('Debit line 1'), {
      target: { value: '119' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(screen.getByRole('alert')).toHaveTextContent(
      'out of balance by 1.00 GHS'
    );
    expect(screen.getByLabelText('Debit line 1')).toHaveValue(119);
    fireEvent.change(screen.getByLabelText('Credit line 1'), {
      target: { value: '1' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(screen.getByRole('alert')).toHaveTextContent(
      'positive debit or credit, not both'
    );
    expect(purchaseReceiptDistributionService.save).not.toHaveBeenCalled();
  });

  it('retains edits and displays precise server concurrency errors', async () => {
    await openDraft();
    vi.mocked(purchaseReceiptDistributionService.save).mockRejectedValue({
      response: {
        data: {
          detail: 'Another user changed this receipt.',
          code: 'RECEIPT_DISTRIBUTION_CONFLICT',
        },
      },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Split line 1' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Another user changed this receipt.'
    );
    expect(screen.getByRole('alert')).toHaveTextContent(
      'RECEIPT_DISTRIBUTION_CONFLICT'
    );
    expect(screen.getByLabelText('Debit line 2')).toHaveValue(60);
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled();
  });

  it('confirms reset and dirty close with app dialogs without losing edits on cancel', async () => {
    await openDraft();
    fireEvent.click(screen.getByRole('button', { name: 'Split line 1' }));
    fireEvent.click(screen.getAllByRole('button', { name: 'Close' })[0]);
    const closePrompt = screen.getByRole('dialog', {
      name: 'Discard unsaved changes?',
    });
    fireEvent.click(
      within(closePrompt).getByRole('button', { name: 'Cancel' })
    );
    expect(screen.getByLabelText('Debit line 2')).toHaveValue(60);
    fireEvent.click(screen.getByRole('button', { name: 'Reset defaults' }));
    expect(purchaseReceiptDistributionService.reset).not.toHaveBeenCalled();
    fireEvent.click(
      within(
        screen.getByRole('dialog', { name: 'Reset distribution?' })
      ).getByRole('button', { name: 'Reset defaults' })
    );
    await screen.findByText('Default distribution restored.');
    expect(purchaseReceiptDistributionService.reset).toHaveBeenCalledWith(
      'receipt-one',
      { version: 'v1', basisVersion: 'b1' }
    );
    expect(screen.getByLabelText('Debit line 1')).toHaveValue(120);
  });

  it('retries the account catalogue without discarding entered lines', async () => {
    vi.mocked(
      purchaseReceiptDistributionService.accounts
    ).mockRejectedValueOnce(new Error('Account catalogue unavailable.'));
    vi.mocked(purchaseReceiptDistributionService.get).mockResolvedValue(
      draft()
    );
    render(<PurchaseReceiptDistribution receiptId="receipt-one" />);
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    await screen.findByText('Account catalogue unavailable.');
    fireEvent.change(screen.getByLabelText('Debit line 1'), {
      target: { value: '119' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Retry accounts' }));
    await waitFor(() =>
      expect(
        screen.queryByText('Account catalogue unavailable.')
      ).not.toBeInTheDocument()
    );
    expect(screen.getByLabelText('Debit line 1')).toHaveValue(119);
    expect(purchaseReceiptDistributionService.get).toHaveBeenCalledTimes(1);
  });

  it('honours CanEdit false on an unposted receipt', async () => {
    vi.mocked(purchaseReceiptDistributionService.get).mockResolvedValue({
      ...draft(),
      canEdit: false,
      editBlockReason: 'Receipt update permission is required.',
    });
    render(<PurchaseReceiptDistribution receiptId="receipt-one" />);
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    await screen.findByText('Receipt update permission is required.');
    expect(
      screen.queryByRole('button', { name: 'Save' })
    ).not.toBeInTheDocument();
    expect(purchaseReceiptDistributionService.accounts).not.toHaveBeenCalled();
  });
});
