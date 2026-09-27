import React from 'react';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BusinessPartnerFinanceProfilesPanel } from './BusinessPartnerFinanceProfilesPanel';
import { businessPartnerFinanceProfileService, type BusinessPartnerFinanceProfileSet } from '@/services/businessPartnerFinanceProfileService';
import { businessPartnerService, type BusinessPartnerPostingOptions } from '@/services/businessPartnerService';

vi.mock('sonner', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));
vi.mock('@/services/businessPartnerFinanceProfileService', () => ({ businessPartnerFinanceProfileService: { get: vi.fn(), saveAp: vi.fn(), saveAr: vi.fn(), decide: vi.fn() } }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getPostingOptions: vi.fn() } }));
// Use the actual Radix Select, including its hidden native select and form change events.
Object.assign(globalThis, { React, ResizeObserver: class { observe() {} unobserve() {} disconnect() {} } });
Object.assign(HTMLElement.prototype, { scrollIntoView: vi.fn(), hasPointerCapture: () => false, setPointerCapture: vi.fn(), releasePointerCapture: vi.fn() });

const profiles: BusinessPartnerFinanceProfileSet = {
  businessPartnerId: 'partner', partnerCode: 'SUP-1', partnerName: 'Supplier', roles: [{
    id: 'supplier-role', roleType: 'Supplier', status: 'Active', activeFromUtc: '2026-01-01', arProfiles: [], apProfiles: [{
      id: 'saved-profile', businessPartnerRoleId: 'supplier-role', versionNumber: 1, status: 'Draft', effectiveFrom: '2026-09-01',
      apReferenceNumber: 'Saved reference', paymentTermId: 'net30', defaultExpenseAccountId: 'expense', defaultTaxGroupId: 'purchases-tax', subjectToWithholding: true,
      withholdingDefaults: [{ id: 'wht-line', categoryCode: 'SERVICES', withholdingTaxId: 'services-wht', withholdingTaxCode: 'WHT-S', withholdingTaxName: 'Services WHT', rate: 7.5, isDefaultForAp: true, isActive: true }],
    }],
  }],
};
const options = {
  accounts: [{ id: 'expense', accountCode: '6500', accountName: 'Services expense', accountType: 'Expense', status: 'Active', allowDirectPosting: true, isControlAccount: false }],
  taxGroups: [{ id: 'purchases-tax', code: 'PUR', name: 'Purchase tax', isActive: true, applicability: 'Purchases' }], bankAccounts: [],
} as unknown as BusinessPartnerPostingOptions;
const props = { businessPartnerId: 'partner', paymentTerms: [{ id: 'net30', code: 'NET30', name: 'Net 30' }], withholdingTaxes: [{ id: 'services-wht', code: 'WHT-S', name: 'Services WHT', rate: 7.5 }] } as React.ComponentProps<typeof BusinessPartnerFinanceProfilesPanel>;
const selection = (label: string) => within(screen.getByRole('group', { name: label })).getByRole('combobox');

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue(profiles);
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue(options);
  vi.mocked(businessPartnerFinanceProfileService.saveAp).mockResolvedValue(profiles.roles[0].apProfiles[0]);
});

describe('Finance profiles with real Radix selects in the partner form', () => {
  it('retains saved values through asynchronous profile and option hydration without dirtying the draft', async () => {
    let resolveProfiles!: (value: BusinessPartnerFinanceProfileSet) => void;
    let resolveOptions!: (value: BusinessPartnerPostingOptions) => void;
    vi.mocked(businessPartnerFinanceProfileService.get).mockReturnValueOnce(new Promise(resolve => { resolveProfiles = resolve; }));
    vi.mocked(businessPartnerService.getPostingOptions).mockReturnValueOnce(new Promise(resolve => { resolveOptions = resolve; }));
    render(<form><BusinessPartnerFinanceProfilesPanel {...props} /></form>);
    await act(async () => resolveProfiles(profiles));
    await screen.findByDisplayValue('Saved reference');
    await act(async () => resolveOptions(options));
    await waitFor(() => {
      expect(selection('Default expense / cost account')).toHaveTextContent('6500 — Services expense');
      expect(selection('Default tax group')).toHaveTextContent('PUR — Purchase tax');
      expect(selection('Payment terms')).toHaveTextContent('NET30 — Net 30');
      expect(selection('WHT configuration')).toHaveTextContent('WHT-S — 7.5%');
      expect(selection('AP role')).toHaveTextContent('Supplier');
      expect(selection('AP profile version')).toHaveTextContent('Version 1 — Draft');
      expect(screen.queryByText('Save draft changes before submitting.')).not.toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAp).toHaveBeenCalledWith('partner', 'saved-profile', expect.objectContaining({
      defaultExpenseAccountId: 'expense', defaultTaxGroupId: 'purchases-tax', paymentTermId: 'net30',
      withholdingDefaults: [expect.objectContaining({ withholdingTaxId: 'services-wht' })],
    })));
  });

  it('still clears an optional account, tax group and payment term when the user explicitly chooses None', async () => {
    render(<form><BusinessPartnerFinanceProfilesPanel {...props} /></form>);
    await screen.findByDisplayValue('Saved reference');
    for (const label of ['Default expense / cost account', 'Default tax group', 'Payment terms']) {
      fireEvent.keyDown(selection(label), { key: 'Enter' });
      fireEvent.click(await screen.findByRole('option', { name: 'None' }));
      expect(selection(label)).toHaveTextContent('None');
    }
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAp).toHaveBeenCalledWith('partner', 'saved-profile', expect.objectContaining({ defaultExpenseAccountId: null, defaultTaxGroupId: null, paymentTermId: null })));
  });
});
