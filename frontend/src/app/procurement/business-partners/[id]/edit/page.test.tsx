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

const { push, errorToast, navigation } = vi.hoisted(() => ({
  push: vi.fn(),
  errorToast: vi.fn(),
  navigation: { query: '' },
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, back: vi.fn() }),
  useParams: () => ({ id: 'partner-1' }),
  useSearchParams: () => new URLSearchParams(navigation.query),
}));
vi.mock('@/components/finance/BusinessPartnerFinanceProfilesPanel', () => ({ BusinessPartnerFinanceProfilesPanel: () => <div>Governed profiles</div> }));
vi.mock('@/components/procurement/BusinessPartnerCurrentAccountsPanel', () => ({ BusinessPartnerCurrentAccountsPanel: ({ ledger, onOpenFinanceProfiles }: { ledger: string; onOpenFinanceProfiles?: () => void }) => <div>Current {ledger} accounts<button type="button" onClick={onOpenFinanceProfiles}>Finance profiles</button></div> }));
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
  navigation.query = '';
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
  it.each([
    ['Supplier', 'Accounts Payable'],
    ['Customer', 'Accounts Receivable'],
  ])('returns from the %s account grid when the URL already selects Finance profiles', async (partnerType, tabName) => {
    vi.mocked(businessPartnerService.getPartnerById).mockResolvedValue({ ...saved, partnerType });
    navigation.query = 'tab=finance-profiles';
    render(<EditBusinessPartnerPage />);
    expect(await screen.findByText('Governed profiles')).toBeInTheDocument();
    await openTab(tabName);
    expect(screen.queryByText('Governed profiles')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Finance profiles' }));
    expect(await screen.findByText('Governed profiles')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Finance Profiles' })).toHaveAttribute('data-state', 'active');
    expect(navigation.query).toBe('tab=finance-profiles');
    expect(businessPartnerService.updatePartner).not.toHaveBeenCalled();
  });

  it('follows Finance-profile links from an already mounted account tab', async () => {
    navigation.query = 'tab=accounts-payable';
    const view = render(<EditBusinessPartnerPage />);
    expect(await screen.findByText('Current payables accounts')).toBeInTheDocument();
    navigation.query = 'tab=finance-profiles';
    view.rerender(<EditBusinessPartnerPage />);
    expect(await screen.findByText('Governed profiles')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Finance Profiles' })).toHaveAttribute('data-state', 'active');
  });

  it('opens customer account mappings from the edit query route', async () => {
    vi.mocked(businessPartnerService.getPartnerById).mockResolvedValue({ ...saved, partnerType: 'Customer' });
    navigation.query = 'tab=accounts-receivable';
    render(<EditBusinessPartnerPage />);
    expect(await screen.findByText('Current receivables accounts')).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Accounts Payable' })).not.toBeInTheDocument();
  });

  it.each(['Approved', 'PendingApproval'])('preserves stored %s status while routing finance setup to profiles', async status => {
    vi.mocked(businessPartnerService.getPartnerById).mockResolvedValue({ ...saved, status });
    render(<EditBusinessPartnerPage />);
    await screen.findByLabelText('Company Name *');
    expect(screen.getByText(status)).toBeInTheDocument();
    expect(screen.getByText(/status changes only through submit, approval, suspension, and reactivation actions/i)).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /status/i })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Accounts Payable' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Accounts Receivable' })).not.toBeInTheDocument();
    await openTab('Finance Profiles');
    expect(screen.getByText('Governed profiles')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(businessPartnerService.updatePartner).toHaveBeenCalledWith('partner-1', expect.any(Object)));
    const request = vi.mocked(businessPartnerService.updatePartner).mock.calls[0][1];
    expect(request).not.toHaveProperty('status');
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
