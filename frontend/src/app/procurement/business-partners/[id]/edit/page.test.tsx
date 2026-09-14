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
}));
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

describe('business partner edit defaults', () => {
  it('reopens stored WHT values and keeps Options and Accounts in equal-width tabs', async () => {
    render(<EditBusinessPartnerPage />);
    expect(await screen.findByLabelText('WHT Rate (%)')).toHaveValue(7.5);
    expect(screen.getByRole('switch')).toBeChecked();
    expect(screen.getByRole('tablist')).toHaveClass('grid-cols-4');
    await openTab('Options');
    expect(screen.getByLabelText('TIN')).toHaveValue('TIN-001');
    expect(screen.getByLabelText('Credit Limit')).toHaveValue(1500);
    await openTab('Accounts');
    expect(screen.getByLabelText('Accounts Payable')).toHaveTextContent(
      'Saved account unavailable'
    );
  });

  it('saves changes without clearing saved IDs when reference catalogues fail', async () => {
    vi.mocked(businessPartnerService.getPostingOptions).mockRejectedValue(
      new Error('Unavailable')
    );
    render(<EditBusinessPartnerPage />);
    await screen.findByLabelText('Company Name *');
    await openTab('Options');
    fireEvent.change(screen.getByLabelText('Credit Limit'), {
      target: { value: '2500' },
    });
    fireEvent.change(screen.getByLabelText('TIN'), {
      target: { value: 'TIN-002' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() =>
      expect(businessPartnerService.updatePartner).toHaveBeenCalledWith(
        'partner-1',
        expect.objectContaining({
          creditLimit: 2500,
          taxNumber: 'TIN-002',
          paymentTermId: 'term-1',
          postingDefaults: saved.postingDefaults,
        })
      )
    );
    expect(push).toHaveBeenCalledWith(
      '/procurement/business-partners/partner-1'
    );
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
