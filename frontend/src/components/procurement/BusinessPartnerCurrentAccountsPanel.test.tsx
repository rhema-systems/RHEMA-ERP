import React from 'react';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BusinessPartnerCurrentAccountsPanel } from './BusinessPartnerCurrentAccountsPanel';
import { financeDataService } from '@/services/finance/finance-data.service';
import { businessPartnerService, type BusinessPartnerDetailDto } from '@/services/businessPartnerService';
import { businessPartnerFinanceProfileService, type BusinessPartnerFinanceProfileSet, type BusinessPartnerApProfile } from '@/services/businessPartnerFinanceProfileService';
import type { FinanceSettings, Account } from '@/types/finance';

vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: { getFinanceSettings: vi.fn() } }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getPostingOptions: vi.fn(), getById: vi.fn() } }));
vi.mock('@/services/businessPartnerFinanceProfileService', () => ({ businessPartnerFinanceProfileService: { get: vi.fn() } }));
Object.assign(globalThis, { React });

const account = (id: string, accountCode: string, accountName: string) => ({ id, accountCode, accountName }) as Account;
const approved = (overrides: Partial<BusinessPartnerApProfile> = {}): BusinessPartnerApProfile => ({
  id: 'ap-approved', businessPartnerRoleId: 'supplier-role', versionNumber: 1, status: 'Approved',
  effectiveFrom: '2020-01-01', defaultExpenseAccountId: 'expense', subjectToWithholding: false,
  withholdingDefaults: [], ...overrides,
});
const profileSet = (apProfiles = [approved()]): BusinessPartnerFinanceProfileSet => ({
  businessPartnerId: 'partner', partnerCode: 'BP-001', partnerName: 'Partner',
  roles: [
    { id: 'supplier-role', roleType: 'Supplier', status: 'Active', activeFromUtc: '2020-01-01', apProfiles, arProfiles: [] },
    { id: 'customer-role', roleType: 'Customer', status: 'Active', activeFromUtc: '2020-01-01', apProfiles: [], arProfiles: [] },
  ],
});
const row = (purpose: string) => within(screen.getByText(purpose).closest('tr')!);
const show = (ledger: 'payables' | 'receivables' = 'payables') => render(
  <BusinessPartnerCurrentAccountsPanel businessPartnerId="partner" partnerType="Both" ledger={ledger} />
);
const ready = () => screen.findByRole('table');

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({
    controlAccountApId: 'central-ap', controlAccountArId: 'central-ar',
    controlAccountCOGSId: 'cogs', controlAccountInventoryId: 'inventory',
    discountAllowedAccountId: 'discount-allowed', discountReceivedAccountId: 'discount-received',
    controlAccountGRVAccrualId: 'grni', writeOffExpenseAccountId: 'ppv',
  } as FinanceSettings);
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue({
    accounts: [
      account('central-ap', '2100', 'Payables'), account('central-ar', '1200', 'Receivables'),
      account('expense', '5100', 'Purchases'), account('draft-expense', '5999', 'Draft account'),
      account('contractor-expense', '5200', 'Contract services'), account('cogs', '5000', 'Cost of goods sold'),
      account('inventory', '1300', 'Inventory control'), account('discount-allowed', '4180', 'Sales discounts'),
      account('discount-received', '5180', 'Purchase discounts'), account('grni', '2200', 'Goods received accrual'),
      account('ppv', '5190', 'Price variance'), account('supplier-grni', '2210', 'Supplier accrual'),
      account('supplier-ppv', '5191', 'Supplier price variance'), account('returns', '4190', 'Sales returns'),
      account('ignored', '9999', 'Retired mapping'),
    ], bankAccounts: [], taxGroups: [],
  });
  vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue(profileSet());
  vi.mocked(businessPartnerService.getById).mockResolvedValue({
    id: 'partner', partnerName: 'Partner', partnerType: 'Both',
    postingDefaults: { defaultApAccountId: 'ignored', defaultExpenseAccountId: 'ignored',
      defaultCashAccountId: 'ignored', defaultFinanceChargesAccountId: 'ignored' },
    receivablesDefaults: { defaultArAccountId: 'ignored', financeChargesAccountId: 'ignored',
      writeoffAccountId: 'ignored', overpaymentWriteoffAccountId: 'ignored', termsDiscountsTakenAccountId: 'ignored' },
  } as BusinessPartnerDetailDto);
});

describe('business partner posting account grid', () => {
  it('opens the local Finance profiles tab without navigation or form submission when embedded in edit', async () => {
    const openProfiles = vi.fn();
    const submit = vi.fn(event => event.preventDefault());
    render(<form onSubmit={submit}><BusinessPartnerCurrentAccountsPanel businessPartnerId="partner" partnerType="Supplier"
      ledger="payables" onOpenFinanceProfiles={openProfiles} /></form>);
    await ready();
    fireEvent.click(screen.getByRole('button', { name: 'Finance profiles' }));
    expect(openProfiles).toHaveBeenCalledOnce();
    expect(submit).not.toHaveBeenCalled();
    expect(screen.queryByRole('link', { name: 'Finance profiles' })).not.toBeInTheDocument();
  });

  it('shows actual canonical codes and names instead of legacy AP overrides or source-only prose', async () => {
    show(); await ready();
    expect(row('Accounts payable').getByText('2100')).toBeInTheDocument();
    expect(row('Accounts payable').getByText('Payables')).toBeInTheDocument();
    expect(row('Purchases / expense').getByText('5100')).toBeInTheDocument();
    expect(row('Purchases / expense').getByText('Supplier AP profile v1')).toBeInTheDocument();
    expect(screen.queryByText('Retired mapping')).not.toBeInTheDocument();
    expect(row('Finance charges').getByText('Selected on transaction')).toBeInTheDocument();
    expect(row('Cash').getByText('Selected on transaction')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Finance profiles' })).toHaveAttribute('href', '/procurement/business-partners/partner/edit?tab=finance-profiles');
    expect(screen.getByRole('link', { name: 'Finance settings' })).toHaveAttribute('href', '/finance/settings');
    expect(businessPartnerService.getPostingOptions).toHaveBeenCalledWith('Supplier');
  });

  it('uses approved accounting-date profiles rather than a newer draft or future approval', async () => {
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue(profileSet([
      approved({ effectiveTo: '2026-09-30' }),
      approved({ id: 'draft', status: 'Draft', versionNumber: 3, defaultExpenseAccountId: 'draft-expense' }),
      approved({ id: 'future', versionNumber: 2, effectiveFrom: '2026-10-01', defaultExpenseAccountId: 'contractor-expense' }),
    ]));
    show(); await ready();
    fireEvent.change(screen.getByLabelText('Profile date'), { target: { value: '2026-09-30' } });
    expect(row('Purchases / expense').getByText('Purchases')).toBeInTheDocument();
    expect(screen.queryByText('Draft account')).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Profile date'), { target: { value: '2026-10-01' } });
    expect(row('Purchases / expense').getByText('Contract services')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Profile date'), { target: { value: '2019-12-31' } });
    expect(row('Purchases / expense').getByText('No approved profile for this date')).toBeInTheDocument();
  });

  it('lets Supplier and Contractor roles retain their own approved expense defaults', async () => {
    const profiles = profileSet();
    profiles.roles.push({ id: 'contractor-role', roleType: 'Contractor', status: 'Active', activeFromUtc: '2020-01-01',
      arProfiles: [], apProfiles: [approved({ businessPartnerRoleId: 'contractor-role', defaultExpenseAccountId: 'contractor-expense' })] });
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue(profiles);
    show(); await ready();
    fireEvent.change(screen.getByRole('combobox', { name: 'Role' }), { target: { value: 'contractor-role' } });
    expect(row('Purchases / expense').getByText('Contract services')).toBeInTheDocument();
    expect(row('Purchases / expense').getByText('Contractor AP profile v1')).toBeInTheDocument();
  });

  it('does not treat an inactive role as an effective posting profile', async () => {
    const profiles = profileSet();
    profiles.roles[0].status = 'Inactive';
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue(profiles);
    show(); await ready();
    expect(row('Purchases / expense').getByText('No approved profile for this date')).toBeInTheDocument();
    expect(row('Purchases / expense').queryByText('5100')).not.toBeInTheDocument();
  });

  it('shows retained receipt overrides only for their verified receipt purpose', async () => {
    vi.mocked(businessPartnerService.getById).mockResolvedValue({
      id: 'partner', postingDefaults: { defaultAccruedPurchasesAccountId: 'supplier-grni',
        defaultPurchasePriceVarianceAccountId: 'supplier-ppv', defaultExpenseAccountId: 'ignored' },
    } as BusinessPartnerDetailDto);
    show(); await ready();
    expect(row('Accrued purchases').getByText('2210')).toBeInTheDocument();
    expect(row('Receipt purchase price variance').getByText('5191')).toBeInTheDocument();
    expect(row('Invoice purchase price variance').getByText('Selected on transaction')).toBeInTheDocument();
    expect(row('Purchases / expense').getByText('5100')).toBeInTheDocument();
  });

  it('shows all GP customer purposes, canonical COGS and retained Sales-return mappings without WHT controls', async () => {
    vi.mocked(businessPartnerService.getById).mockResolvedValue({
      id: 'partner', receivablesDefaults: { salesReturnsAccountId: 'returns', defaultArAccountId: 'ignored',
        financeChargesAccountId: 'ignored', writeoffAccountId: 'ignored', overpaymentWriteoffAccountId: 'ignored' },
    } as BusinessPartnerDetailDto);
    show('receivables'); await ready();
    expect(row('Accounts receivable').getByText('1200')).toBeInTheDocument();
    expect(row('Cost of sales').getByText('5000')).toBeInTheDocument();
    expect(row('Sales order returns').getByText('4190')).toBeInTheDocument();
    for (const purpose of ['Cash', 'Sales', 'Finance charges', 'Writeoffs', 'Overpayment writeoffs'])
      expect(row(purpose).getByText('Selected on transaction')).toBeInTheDocument();
    expect(row('Terms discounts taken').getByText('4180')).toBeInTheDocument();
    expect(row('Terms discounts available').getByText('Recognized when taken')).toBeInTheDocument();
    expect(screen.queryByText('Retired mapping')).not.toBeInTheDocument();
    expect(screen.queryByRole('switch')).not.toBeInTheDocument();
    expect(screen.queryByText(/withholding/i)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Customer adjustments' })).toHaveAttribute('href', '/finance/subledger-adjustments/new?module=AR');
    expect(businessPartnerService.getPostingOptions).toHaveBeenCalledWith('Customer');
  });

  it('shows the Finance sales-return fallback when the customer has no retained override', async () => {
    show('receivables'); await ready();
    expect(row('Sales order returns').getByText('4180')).toBeInTheDocument();
    expect(row('Sales order returns').getByText('Finance discount-allowed fallback')).toBeInTheDocument();
  });

  it.each([
    [{}, 'Unavailable'],
    [{ controlAccountCOGSId: null }, 'Not configured'],
  ] as const)('distinguishes an older COGS read contract from an explicit empty mapping: %s', async (setting, expected) => {
    vi.mocked(financeDataService.getFinanceSettings).mockResolvedValue({ controlAccountArId: 'central-ar', ...setting } as FinanceSettings);
    show('receivables'); await ready();
    expect(row('Cost of sales').getByText(expected)).toBeInTheDocument();
  });

  it('keeps loaded tenant accounts visible when profile read access is denied', async () => {
    vi.mocked(businessPartnerFinanceProfileService.get).mockRejectedValue(new Error('Forbidden'));
    show(); await ready();
    expect(screen.getByRole('alert')).toHaveTextContent('Finance read access is required');
    expect(row('Purchases / expense').getByText('Unavailable')).toBeInTheDocument();
    expect(row('Accounts payable').getByText('Payables')).toBeInTheDocument();
    expect(screen.queryByText('No approved AP profile for this date')).not.toBeInTheDocument();
  });

  it('distinguishes failed settings from an unconfigured account and retries', async () => {
    vi.mocked(financeDataService.getFinanceSettings).mockRejectedValueOnce(new Error('Forbidden'));
    show(); await ready();
    expect(row('Accounts payable').getByText('Unavailable')).toBeInTheDocument();
    expect(row('Purchases / expense').getByText('Purchases')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry accounts' }));
    await ready();
    expect(row('Accounts payable').getByText('2100')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('does not call a configured but unavailable catalogue account unconfigured', async () => {
    vi.mocked(businessPartnerService.getPostingOptions).mockRejectedValue(new Error('Forbidden'));
    show(); await ready();
    expect(row('Accounts payable').getByText('Configured account unavailable')).toBeInTheDocument();
    expect(row('Accounts payable').queryByText('Not configured')).not.toBeInTheDocument();
  });

  it('ignores late responses for the previous partner', async () => {
    let finish!: (value: BusinessPartnerFinanceProfileSet) => void;
    vi.mocked(businessPartnerFinanceProfileService.get).mockReturnValueOnce(new Promise(resolve => { finish = resolve; }));
    const view = show();
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue(profileSet([approved({ defaultExpenseAccountId: 'contractor-expense' })]));
    view.rerender(<BusinessPartnerCurrentAccountsPanel businessPartnerId="next-partner" partnerType="Supplier" ledger="payables" />);
    await ready();
    expect(row('Purchases / expense').getByText('Contract services')).toBeInTheDocument();
    await act(async () => { finish(profileSet()); });
    expect(row('Purchases / expense').getByText('Contract services')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Finance profiles' })).toHaveAttribute('href', '/procurement/business-partners/next-partner/edit?tab=finance-profiles');
  });
});
