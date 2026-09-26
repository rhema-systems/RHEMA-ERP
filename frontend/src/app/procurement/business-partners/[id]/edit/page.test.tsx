import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import EditBusinessPartnerPage from './page';
import {
  businessPartnerService,
  type BusinessPartnerDetailDto,
} from '@/services/businessPartnerService';
import {
  paymentTermService,
  procurementCurrencyService,
} from '@/services/financeCommonService';
import { priceListService } from '@/services/priceListService';

const { push, errorToast } = vi.hoisted(() => ({
  push: vi.fn(),
  errorToast: vi.fn(),
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, back: vi.fn() }),
  useParams: () => ({ id: 'partner-1' }),
  useSearchParams: () => new URLSearchParams(),
}));
vi.mock('@/components/finance/BusinessPartnerFinanceProfilesPanel', () => ({ BusinessPartnerFinanceProfilesPanel: () => <div>Governed profiles</div> }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: errorToast } }));
vi.mock('@/services/businessPartnerService', () => ({
  businessPartnerService: {
    getPartnerById: vi.fn(),
    getAllPartnersForDropdown: vi.fn(),
    updatePartner: vi.fn(),
    getPostingOptions: vi.fn(),
  },
}));
vi.mock('@/services/financeCommonService', () => ({
  paymentTermService: { getActive: vi.fn() },
  procurementCurrencyService: { getActive: vi.fn() },
}));
vi.mock('@/services/priceListService', () => ({
  priceListService: { getActivePriceLists: vi.fn() },
}));
Object.assign(globalThis, {
  React,
  ResizeObserver: class {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
});

const saved = {
  id: 'partner-1',
  partnerCode: 'SUP-001',
  partnerName: 'Supplier One',
  partnerType: 'Supplier',
  status: 'Active',
  taxNumber: 'TIN-001',
  creditLimit: 1500,
  paymentTermId: 'term-1',
  currency: 'GHS',
  isPreferred: true,
  postingDefaults: {
    subjectToWithholdingDeduction: true,
    withholdingTaxRate: 7.5,
    cashAccountSource: 'BusinessPartner',
    defaultApAccountId: 'saved-ap',
    defaultTaxGroupId: 'saved-tax',
    defaultBankAccountId: 'saved-bank',
  },
} as BusinessPartnerDetailDto;

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(businessPartnerService.getPartnerById).mockResolvedValue(saved);
  vi.mocked(businessPartnerService.getAllPartnersForDropdown).mockResolvedValue(
    []
  );
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue({
    accounts: [],
    bankAccounts: [],
    taxGroups: [],
  });
  vi.mocked(paymentTermService.getActive).mockResolvedValue([]);
  vi.mocked(procurementCurrencyService.getActive).mockResolvedValue([]);
  vi.mocked(priceListService.getActivePriceLists).mockResolvedValue([]);
  vi.mocked(businessPartnerService.updatePartner).mockResolvedValue(saved);
});

async function openTab(name: string) {
  fireEvent.mouseDown(screen.getByRole('tab', { name }), {
    button: 0,
    ctrlKey: false,
  });
  await waitFor(() =>
    expect(screen.getByRole('tab', { name })).toHaveAttribute(
      'data-state',
      'active'
    )
  );
}

describe('business partner governed finance setup', () => {
  it.each(['Approved', 'PendingApproval'])('preserves stored %s status while routing finance setup to profiles', async status => {
    vi.mocked(businessPartnerService.getPartnerById).mockResolvedValue({ ...saved, status });
    render(<EditBusinessPartnerPage />);
    await screen.findByLabelText('Company Name *');
    expect(screen.queryByRole('tab', { name: 'Accounts Payable' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Accounts Receivable' })).not.toBeInTheDocument();
    await openTab('Finance Profiles');
    expect(screen.getByText('Governed profiles')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(businessPartnerService.updatePartner).toHaveBeenCalledWith('partner-1', expect.objectContaining({ status })));
    const request = vi.mocked(businessPartnerService.updatePartner).mock.calls[0][1];
    expect(request).not.toHaveProperty('postingDefaults');
    expect(request).not.toHaveProperty('receivablesDefaults');
  });
  it('retains master TIN editing without exposing legacy WHT defaults', async () => {
    render(<EditBusinessPartnerPage />);
    await screen.findByLabelText('Company Name *');
    expect(screen.queryByLabelText('WHT Rate (%)')).not.toBeInTheDocument();
    await openTab('Options');
    fireEvent.change(screen.getByLabelText('TIN'), { target: { value: 'TIN-002' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(businessPartnerService.updatePartner).toHaveBeenCalledWith('partner-1', expect.objectContaining({ taxNumber: 'TIN-002' })));
  });
  it('retains edits after failed save and exposes the server reason', async () => {
    vi.mocked(businessPartnerService.updatePartner).mockRejectedValue(
      new Error('GL account has been deactivated. (PARTNER_ACCOUNT_INVALID)')
    );
    render(<EditBusinessPartnerPage />);
    fireEvent.change(await screen.findByLabelText('Company Name *'), {
      target: { value: 'Updated Supplier' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() =>
      expect(errorToast).toHaveBeenCalledWith(
        'GL account has been deactivated. (PARTNER_ACCOUNT_INVALID)'
      )
    );
    expect(screen.getByLabelText('Company Name *')).toHaveValue(
      'Updated Supplier'
    );
    expect(push).not.toHaveBeenCalled();
  });
});
