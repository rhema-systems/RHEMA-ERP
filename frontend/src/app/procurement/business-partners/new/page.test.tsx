import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import NewBusinessPartnerPage from './page';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';
import { paymentTermService, procurementCurrencyService } from '@/services/financeCommonService';
import { priceListService } from '@/services/priceListService';

const { push } = vi.hoisted(() => ({ push: vi.fn() }));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push }) }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { createPartner: vi.fn(), getAllPartnersForDropdown: vi.fn(), getPostingOptions: vi.fn() } }));
vi.mock('@/services/financeCommonService', () => ({ paymentTermService: { getActive: vi.fn() }, procurementCurrencyService: { getActive: vi.fn() } }));
vi.mock('@/services/priceListService', () => ({ priceListService: { getActivePriceLists: vi.fn() } }));
Object.assign(globalThis, { React, ResizeObserver: class { observe() {} unobserve() {} disconnect() {} } });

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(businessPartnerService.getAllPartnersForDropdown).mockResolvedValue([]);
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue({ accounts: [], bankAccounts: [], taxGroups: [] });
  vi.mocked(paymentTermService.getActive).mockResolvedValue([]);
  vi.mocked(procurementCurrencyService.getActive).mockResolvedValue([]);
  vi.mocked(priceListService.getActivePriceLists).mockResolvedValue([]);
  vi.mocked(businessPartnerService.createPartner).mockResolvedValue({ id: 'new-supplier' } as BusinessPartnerDetailDto);
});

async function openTab(name: string) {
  fireEvent.mouseDown(screen.getByRole('tab', { name }), { button: 0, ctrlKey: false });
  await waitFor(() => expect(screen.getByRole('tab', { name })).toHaveAttribute('data-state', 'active'));
}

describe('new business partner canonical finance setup', () => {
  it('saves canonical Supplier role, TIN and SSNIT then opens governed Finance Profiles', async () => {
    render(<NewBusinessPartnerPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Supplier' }));
    fireEvent.change(screen.getByLabelText('Partner Name *'), { target: { value: 'Freight Supplier' } });
    expect(screen.queryByRole('tab', { name: 'Accounts Payable' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('WHT Rate (%)')).not.toBeInTheDocument();
    await openTab('Options');
    fireEvent.change(screen.getByLabelText('TIN'), { target: { value: 'TIN-NEW' } });
    fireEvent.change(screen.getByLabelText('SSNIT Number'), { target: { value: 'SSNIT-NEW' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Partner' }));
    await waitFor(() => expect(businessPartnerService.createPartner).toHaveBeenCalledWith(expect.objectContaining({
      roleTypes: ['Supplier'],
      taxNumber: 'TIN-NEW',
      ssnitNumber: 'SSNIT-NEW',
    })));
    const request = vi.mocked(businessPartnerService.createPartner).mock.calls[0][0];
    expect(request).not.toHaveProperty('creditLimit');
    expect(request).not.toHaveProperty('postingDefaults');
    expect(request).not.toHaveProperty('receivablesDefaults');
    expect(push).toHaveBeenCalledWith('/procurement/business-partners/new-supplier/edit?tab=finance-profiles');
  });
  it('allows independent Supplier and Customer roles on one partner', async () => {
    render(<NewBusinessPartnerPage />);
    expect(screen.queryByRole('button', { name: 'Supplier & Customer' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Supplier' }));
    fireEvent.click(screen.getByRole('button', { name: 'Customer' }));
    expect(screen.getByRole('tab', { name: 'Customer Details' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Partner Name *'), { target: { value: 'Dual Partner' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Partner' }));
    await waitFor(() => expect(businessPartnerService.createPartner).toHaveBeenCalledWith(expect.objectContaining({
      partnerType: 'CustomerAndSupplier',
      roleTypes: ['Supplier', 'Customer'],
    })));
  });
  it('hides legacy account and WHT editors for customer records', async () => {
    render(<NewBusinessPartnerPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Customer' }));
    expect(screen.queryByRole('switch', { name: 'Subject To Withholding Deduction' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Accounts Receivable' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Accounts Payable' })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Customer Details' })).toBeInTheDocument();
  });
});
