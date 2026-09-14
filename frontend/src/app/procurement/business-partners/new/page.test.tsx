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

describe('new business partner defaults', () => {
  it('saves supplier credit, TIN and WHT from their respective tabs without customer-only gating', async () => {
    render(<NewBusinessPartnerPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Supplier', exact: true }));
    fireEvent.change(screen.getByLabelText('Partner Name *'), { target: { value: 'Freight Supplier' } });
    fireEvent.click(screen.getByRole('switch', { name: 'Subject To Withholding Deduction' }));
    fireEvent.change(screen.getByLabelText('WHT Rate (%)'), { target: { value: '5' } });
    await openTab('Options');
    fireEvent.change(screen.getByLabelText('TIN'), { target: { value: 'TIN-NEW' } });
    fireEvent.change(screen.getByLabelText('Credit Limit'), { target: { value: '9000' } });
    await openTab('Accounts');
    expect(screen.getByText('Purchase Price Variance')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save Partner', exact: true }));
    await waitFor(() => expect(businessPartnerService.createPartner).toHaveBeenCalledWith(expect.objectContaining({ partnerType: 'Supplier', creditLimit: 9000, taxNumber: 'TIN-NEW', postingDefaults: expect.objectContaining({ subjectToWithholdingDeduction: true, withholdingTaxRate: 5 }) })));
    expect(push).toHaveBeenCalledWith('/procurement/business-partners/new-supplier');
  });

  it('keeps Credit Limit in Options only and requests the customer tax catalogue for customers', async () => {
    render(<NewBusinessPartnerPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Customer', exact: true }));
    await waitFor(() => expect(businessPartnerService.getPostingOptions).toHaveBeenCalledWith('Customer'));
    await openTab('Customer Details');
    expect(screen.queryByLabelText('Credit Limit')).not.toBeInTheDocument();
    await openTab('Options');
    expect(screen.getAllByLabelText('Credit Limit')).toHaveLength(1);
  });
});
