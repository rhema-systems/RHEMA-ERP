import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { InvoiceDistribution } from './InvoiceDistribution';
import { accountsPayableService } from '@/services/accountsPayableService';
import type { VendorInvoiceDistribution } from '@/types/ap';

vi.mock('@/services/accountsPayableService', () => ({
  accountsPayableService: { getInvoiceDistribution: vi.fn() },
}));

const distribution: VendorInvoiceDistribution = {
  invoiceId: 'invoice',
  status: 'Proposed',
  currency: 'GHS',
  basis: 'Landed-cost draft posting',
  totalDebit: 310,
  totalCredit: 310,
  lines: [
    {
      lineId: 'debit',
      accountId: 'accrued',
      accountCode: '2111',
      accountName: 'Landed cost clearing',
      type: 'Accrued',
      source: 'Receipt journal',
      description: 'Freight',
      debit: 310,
      credit: 0,
    },
    {
      lineId: 'credit',
      accountId: 'ap',
      accountCode: '2100',
      accountName: 'Accounts payable',
      type: 'Payables',
      source: 'Supplier',
      description: 'Freight',
      debit: 0,
      credit: 310,
    },
  ],
};
describe('invoice Distribution', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(accountsPayableService.getInvoiceDistribution).mockResolvedValue(
      distribution
    );
  });
  it('loads only on demand and shows landed-cost accrual/AP distribution without adding invoice WHT', async () => {
    render(<InvoiceDistribution invoiceId="invoice" />);
    expect(
      accountsPayableService.getInvoiceDistribution
    ).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    await screen.findByText('Landed cost clearing');
    expect(screen.getByText('Accounts payable')).toBeInTheDocument();
    expect(screen.getByText('Balanced')).toBeInTheDocument();
    expect(screen.queryByText(/withholding/i)).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Save' })
    ).not.toBeInTheDocument();
    expect(accountsPayableService.getInvoiceDistribution).toHaveBeenCalledWith(
      'invoice'
    );
  });
  it('keeps the original posted journal read-only', async () => {
    vi.mocked(accountsPayableService.getInvoiceDistribution).mockResolvedValue({
      ...distribution,
      status: 'Posted',
      journalEntryNumber: 'JE-INV-001',
    });
    render(<InvoiceDistribution invoiceId="invoice" />);
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    expect(await screen.findByText('GHS · JE-INV-001')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Post' })
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });
  it('uses fixed-height full-page and Restore controls without reloading', async () => {
    render(<InvoiceDistribution invoiceId="invoice" />);
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    await screen.findByText('Landed cost clearing');
    const dialog = screen.getByRole('dialog', { name: 'Invoice Distribution' });
    expect(dialog.className).toContain('h-[min(620px,90dvh)]');
    fireEvent.click(screen.getByRole('button', { name: 'Full page' }));
    expect(dialog.className).toContain('h-[calc(100dvh-2rem)]');
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    expect(dialog.className).toContain('h-[min(620px,90dvh)]');
    expect(accountsPayableService.getInvoiceDistribution).toHaveBeenCalledTimes(
      1
    );
  });
  it('shows a precise server error and retries the same invoice', async () => {
    vi.mocked(
      accountsPayableService.getInvoiceDistribution
    ).mockRejectedValueOnce({
      response: {
        data: {
          detail: 'Select the invoice purchases account.',
          code: 'AP_ACCOUNT_REQUIRED',
        },
      },
    });
    render(<InvoiceDistribution invoiceId="invoice" />);
    fireEvent.click(screen.getByRole('button', { name: 'Distribution' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'AP_ACCOUNT_REQUIRED'
    );
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() =>
      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    );
    expect(await screen.findByText('Landed cost clearing')).toBeInTheDocument();
  });
});
