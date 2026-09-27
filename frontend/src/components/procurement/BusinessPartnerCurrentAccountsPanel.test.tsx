import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BusinessPartnerCurrentAccountsPanel } from './BusinessPartnerCurrentAccountsPanel';
import { financeDataService } from '@/services/finance/finance-data.service';
import { businessPartnerService } from '@/services/businessPartnerService';
import type { FinanceSettings, Account } from '@/types/finance';

vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: { getFinanceSettings: vi.fn() } }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getPostingOptions: vi.fn() } }));
Object.assign(globalThis, { React });

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({ controlAccountApId: 'central-ap', controlAccountArId: 'central-ar' } as FinanceSettings);
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue({
    accounts: [{ id: 'central-ap', accountCode: '2100', accountName: 'Payables' }, { id: 'central-ar', accountCode: '1200', accountName: 'Receivables' }] as Account[],
    bankAccounts: [], taxGroups: [],
  });
});

describe('current business partner account sources', () => {
  it('shows the configured AP control account with source ownership, without an ineffective editable override', async () => {
    render(<BusinessPartnerCurrentAccountsPanel businessPartnerId="supplier" partnerType="Supplier" ledger="payables" />);
    expect(await screen.findByText('2100 · Payables')).toBeInTheDocument();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open Finance Profiles' })).toHaveAttribute('href', '/procurement/business-partners/supplier/edit?tab=finance-profiles');
    expect(screen.getByText('Receipt accrual / purchase price variance')).toBeInTheDocument();
  });

  it('shows customer adjustment purposes individually without supplier withholding controls', async () => {
    render(<BusinessPartnerCurrentAccountsPanel businessPartnerId="customer" partnerType="Customer" ledger="receivables" />);
    expect(await screen.findByText('1200 · Receivables')).toBeInTheDocument();
    for (const label of ['Finance charges', 'Writeoffs', 'Overpayment writeoffs', 'Sales returns'])
      expect(screen.getByText(label)).toBeInTheDocument();
    expect(screen.queryByRole('switch')).not.toBeInTheDocument();
    expect(businessPartnerService.getPostingOptions).toHaveBeenCalledWith('Customer');
  });

  it('distinguishes unavailable settings from an unconfigured account and permits retry', async () => {
    vi.mocked(financeDataService.getFinanceSettings).mockRejectedValueOnce(new Error('Forbidden'));
    render(<BusinessPartnerCurrentAccountsPanel businessPartnerId="supplier" partnerType="Supplier" ledger="payables" />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Finance settings could not be loaded');
    expect(screen.queryByText('Not configured')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry account sources' }));
    expect(await screen.findByText('2100 · Payables')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
