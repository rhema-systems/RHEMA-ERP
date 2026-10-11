import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BusinessPartnerFinanceProfilesPanel } from './BusinessPartnerFinanceProfilesPanel';
import { businessPartnerFinanceProfileService, type BusinessPartnerApProfile, type BusinessPartnerFinanceProfileSet } from '@/services/businessPartnerFinanceProfileService';
import { businessPartnerService, type BusinessPartnerPostingOptions } from '@/services/businessPartnerService';

const { errorToast } = vi.hoisted(() => ({ errorToast: vi.fn() }));
const auth = vi.hoisted(() => ({ userId: 'checker-user', canApprove: true }));
vi.mock('sonner', () => ({ toast: { error: errorToast, success: vi.fn() } }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: auth.userId }, hasPermission: () => auth.canApprove }) }));
vi.mock('@/services/businessPartnerFinanceProfileService', () => ({ businessPartnerFinanceProfileService: { get: vi.fn(), saveAp: vi.fn(), saveAr: vi.fn(), decide: vi.fn() } }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getPostingOptions: vi.fn() } }));
// Native selections keep these tests focused on profile selection/save behaviour, not Radix portals.
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, value, onValueChange, disabled }: { children: React.ReactNode; value?: string; onValueChange: (value: string) => void; disabled?: boolean }) => <select value={value} onChange={event => onValueChange(event.target.value)} disabled={disabled}>{children}</select>,
  SelectTrigger: () => null, SelectValue: () => null,
  SelectContent: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ children, value, disabled }: { children: React.ReactNode; value: string; disabled?: boolean }) => <option value={value} disabled={disabled}>{children}</option>,
}));
Object.assign(globalThis, { React, ResizeObserver: class { observe() {} unobserve() {} disconnect() {} } });

const draft: BusinessPartnerApProfile = {
  id: 'ap-draft', businessPartnerRoleId: 'supplier-role', versionNumber: 2, status: 'Draft', effectiveFrom: '2026-09-01',
  apReferenceNumber: 'AP-42', paymentTermId: 'net30', defaultExpenseAccountId: 'expense', defaultTaxGroupId: 'purchases-tax',
  subjectToWithholding: true,
  withholdingDefaults: [
    { id: 'wht-active', categoryCode: 'SERVICES', withholdingTaxId: 'wht', withholdingTaxCode: 'WHT', withholdingTaxName: 'Services WHT', rate: 7.5, isDefaultForAp: true, isActive: true },
    { id: 'wht-inactive', categoryCode: 'OLD', withholdingTaxId: 'wht', withholdingTaxCode: 'WHT', withholdingTaxName: 'Services WHT', rate: 7.5, isDefaultForAp: false, isActive: false },
  ],
};
const approved: BusinessPartnerApProfile = { ...draft, id: 'ap-approved', versionNumber: 1, status: 'Approved', apReferenceNumber: 'Approved reference', defaultExpenseAccountId: 'asset', subjectToWithholding: false, withholdingDefaults: [] };
const data: BusinessPartnerFinanceProfileSet = {
  businessPartnerId: 'partner-1', partnerCode: 'BP-1', partnerName: 'Both Partner', roles: [
    { id: 'supplier-role', roleType: 'Supplier', status: 'Active', activeFromUtc: '2026-01-01', apProfiles: [approved, draft], arProfiles: [] },
    { id: 'customer-role', roleType: 'Customer', status: 'Active', activeFromUtc: '2026-01-01', apProfiles: [], arProfiles: [{ id: 'ar-draft', businessPartnerRoleId: 'customer-role', versionNumber: 1, status: 'Draft', effectiveFrom: '2026-09-01', paymentTermId: 'net30', creditLimit: 7500, isWithholdingAgent: true }] },
  ],
};
const options = {
  accounts: [
    { id: 'expense', accountCode: '6000', accountName: 'Expenses', accountType: 'Expense', status: 'Active', allowDirectPosting: true, isControlAccount: false },
    { id: 'asset', accountCode: '1400', accountName: 'Asset cost', accountType: 'Asset', status: 'Active', allowDirectPosting: true, isControlAccount: false },
    { id: 'control', accountCode: '2100', accountName: 'AP control', accountType: 'Liability', status: 'Active', allowDirectPosting: true, isControlAccount: true },
    { id: 'summary', accountCode: '6500', accountName: 'Expense summary', accountType: 'Expense', status: 'Active', allowDirectPosting: false, isControlAccount: false },
  ],
  taxGroups: [
    { id: 'purchases-tax', code: 'PUR', name: 'Purchase tax', isActive: true, applicability: 'Purchases' },
    { id: 'sales-tax', code: 'SAL', name: 'Sales tax', isActive: true, applicability: 'Sales' },
  ], bankAccounts: [],
} as unknown as BusinessPartnerPostingOptions;
const props = { businessPartnerId: 'partner-1', paymentTerms: [{ id: 'net30', code: 'N30', name: 'Net 30' }], withholdingTaxes: [{ id: 'wht', code: 'WHT', name: 'Services WHT', rate: 7.5 }] } as React.ComponentProps<typeof BusinessPartnerFinanceProfilesPanel>;
const apCard = () => screen.getByRole('region', { name: 'Accounts Payable profile' });
const group = (label: string) => screen.getByRole('group', { name: label });
const input = (label: string) => within(group(label)).getByRole('textbox');
const select = (label: string) => within(group(label)).getByRole('combobox');
async function ready() { await screen.findByDisplayValue('AP-42'); }

beforeEach(() => {
  vi.clearAllMocks();
  auth.userId = 'checker-user';
  auth.canApprove = true;
  vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue(structuredClone(data));
  vi.mocked(businessPartnerService.getPostingOptions).mockResolvedValue(options);
  vi.mocked(businessPartnerFinanceProfileService.saveAp).mockResolvedValue(draft);
});

describe('canonical Finance profiles regression', () => {
  it('preserves configured expense, tax, payment and inactive WHT rows when saving another field', async () => {
    render(<BusinessPartnerFinanceProfilesPanel {...props} />);
    await ready();
    fireEvent.change(input('AP reference'), { target: { value: 'Updated reference' } });
    fireEvent.click(within(apCard()).getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAp).toHaveBeenCalledWith('partner-1', 'ap-draft', expect.objectContaining({
      apReferenceNumber: 'Updated reference', defaultExpenseAccountId: 'expense', defaultTaxGroupId: 'purchases-tax', paymentTermId: 'net30',
      withholdingDefaults: [expect.objectContaining({ categoryCode: 'SERVICES', isActive: true, isDefaultForAp: true }), expect.objectContaining({ categoryCode: 'OLD', isActive: false, isDefaultForAp: false })],
    })));
  });

  it('offers only eligible cost and purchase-tax selections and sends explicit clearing as null', async () => {
    render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    const accountSelect = select('Default expense / cost account');
    expect(within(accountSelect).queryByText(/AP control|Expense summary/)).not.toBeInTheDocument();
    expect(within(select('Default tax group')).queryByText(/Sales tax/)).not.toBeInTheDocument();
    fireEvent.change(accountSelect, { target: { value: 'asset' } });
    fireEvent.change(select('Default tax group'), { target: { value: '__none__' } });
    fireEvent.click(within(apCard()).getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAp).toHaveBeenCalledWith('partner-1', 'ap-draft', expect.objectContaining({ defaultExpenseAccountId: 'asset', defaultTaxGroupId: null })));
  });

  it('keeps saved selections when catalogue loading fails', async () => {
    vi.mocked(businessPartnerService.getPostingOptions).mockRejectedValue(new Error('Forbidden'));
    render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    expect(select('Default expense / cost account')).toBeDisabled();
    expect(screen.getByRole('alert')).toHaveTextContent('Existing selections are preserved');
    fireEvent.click(within(apCard()).getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAp).toHaveBeenCalledWith('partner-1', 'ap-draft', expect.objectContaining({ defaultExpenseAccountId: 'expense', defaultTaxGroupId: 'purchases-tax' })));
  });

  it('allows inspection of the approved version without treating a newer draft as approved', async () => {
    render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    expect(select('AP profile version')).toHaveValue('ap-draft');
    fireEvent.change(select('AP profile version'), { target: { value: 'ap-approved' } });
    expect(input('AP reference')).toHaveValue('Approved reference');
    expect(input('AP reference')).toBeDisabled();
    expect(select('Default expense / cost account')).toHaveValue('asset');
    fireEvent.click(within(apCard()).getByRole('button', { name: 'Create new draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAp).toHaveBeenCalledWith('partner-1', null, expect.objectContaining({ defaultExpenseAccountId: 'asset', defaultTaxGroupId: 'purchases-tax' })));
  });

  it('prefers an active AP role and changes to the selected role without carrying values across roles', async () => {
    const inactive = { ...data.roles[0], id: 'inactive-role', status: 'Inactive' as const, apProfiles: [{ ...approved, id: 'inactive-profile', businessPartnerRoleId: 'inactive-role' }] };
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue({ ...data, roles: [inactive, ...data.roles] });
    render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    expect(select('AP role')).toHaveValue('supplier-role');
    fireEvent.change(select('AP role'), { target: { value: 'inactive-role' } });
    expect(input('AP reference')).toHaveValue('Approved reference');
    expect(within(apCard()).getByRole('button', { name: 'Create new draft' })).toBeDisabled();
  });

  it('resets role IDs when navigating to a different partner in the same mounted panel', async () => {
    const { rerender } = render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue({ ...data, businessPartnerId: 'partner-2', roles: [{ ...data.roles[0], id: 'other-role', apProfiles: [{ ...draft, id: 'other-draft', businessPartnerRoleId: 'other-role', apReferenceNumber: 'Other partner' }] }] });
    rerender(<BusinessPartnerFinanceProfilesPanel {...props} businessPartnerId="partner-2" />);
    await screen.findByDisplayValue('Other partner');
    expect(select('AP role')).toHaveValue('other-role');
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAp).toHaveBeenCalledWith('partner-2', 'other-draft', expect.objectContaining({ businessPartnerRoleId: 'other-role' })));
  });

  it('does not leave the previous partner editable when loading the next partner fails', async () => {
    const { rerender } = render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    vi.mocked(businessPartnerFinanceProfileService.get).mockRejectedValue(new Error('PROFILE_ACCESS_DENIED'));
    rerender(<BusinessPartnerFinanceProfilesPanel {...props} businessPartnerId="partner-2" />);
    await screen.findByText('Finance profiles could not be loaded.');
    expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument();
    expect(errorToast).toHaveBeenCalledWith('PROFILE_ACCESS_DENIED');
  });

  it('preserves customer credit, terms and withholding-agent settings on AR save', async () => {
    render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    expect(screen.getByText(/leave blank or enter 0 for unlimited credit/i)).toBeInTheDocument();
    fireEvent.change(input('AR reference'), { target: { value: 'AR-123' } });
    const arCard = screen.getByRole('region', { name: 'Accounts Receivable profile' });
    fireEvent.click(within(arCard).getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.saveAr).toHaveBeenCalledWith('partner-1', 'ar-draft', expect.objectContaining({ businessPartnerRoleId: 'customer-role', arReferenceNumber: 'AR-123', paymentTermId: 'net30', creditLimit: 7500, isWithholdingAgent: true })));
  });

  it('retains edits when the server rejects save and does not submit the enclosing identity form', async () => {
    const submit = vi.fn(event => event.preventDefault());
    vi.mocked(businessPartnerFinanceProfileService.saveAp).mockRejectedValue(new Error('Only draft profiles can be edited. (PROFILE_NOT_DRAFT)'));
    render(<form onSubmit={submit}><BusinessPartnerFinanceProfilesPanel {...props} /></form>); await ready();
    fireEvent.change(input('AP reference'), { target: { value: 'Keep this edit' } });
    expect(fireEvent.keyDown(input('AP reference'), { key: 'Enter' })).toBe(false);
    fireEvent.click(within(apCard()).getByRole('button', { name: 'Save draft' }));
    await waitFor(() => expect(errorToast).toHaveBeenCalledWith(expect.stringContaining('PROFILE_NOT_DRAFT')));
    expect(input('AP reference')).toHaveValue('Keep this edit');
    expect(submit).not.toHaveBeenCalled();
    for (const button of screen.getAllByRole('button')) expect(button).toHaveAttribute('type', 'button');
  });

  it('submits the selected draft and leaves approval authority to the server', async () => {
    render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    fireEvent.click(within(apCard()).getByRole('button', { name: 'Submit for approval' }));
    await waitFor(() => expect(businessPartnerFinanceProfileService.decide).toHaveBeenCalledWith('partner-1', 'ap', 'ap-draft', 'submit', ''));
  });

  it('does not offer approval actions to the user who submitted the profile', async () => {
    auth.userId = 'maker-user';
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue({
      ...data,
      roles: [{ ...data.roles[0], apProfiles: [{ ...draft, status: 'Submitted', submittedById: 'MAKER-USER' }] }],
    });
    render(<BusinessPartnerFinanceProfilesPanel {...props} />);
    await screen.findByText('Version 2 — Submitted');
    expect(screen.getByText(/maker-checker control prevents you/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
  });

  it('offers approval actions only to a different user with Finance profile approval permission', async () => {
    vi.mocked(businessPartnerFinanceProfileService.get).mockResolvedValue({
      ...data,
      roles: [{ ...data.roles[0], apProfiles: [{ ...draft, status: 'Submitted', submittedById: 'maker-user' }] }],
    });
    const { rerender } = render(<BusinessPartnerFinanceProfilesPanel {...props} />);
    await screen.findByText('Version 2 — Submitted');
    expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument();

    auth.canApprove = false;
    rerender(<BusinessPartnerFinanceProfilesPanel {...props} />);
    expect(screen.getByText(/Approve Business Partner Finance Profiles permission/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('requires edited AP and AR drafts to be saved before submitting their persisted versions', async () => {
    render(<BusinessPartnerFinanceProfilesPanel {...props} />); await ready();
    fireEvent.change(input('AP reference'), { target: { value: 'Unsaved AP change' } });
    fireEvent.change(input('AR reference'), { target: { value: 'Unsaved AR change' } });
    for (const button of screen.getAllByRole('button', { name: 'Submit for approval' })) {
      expect(button).toBeDisabled();
      fireEvent.click(button);
    }
    expect(businessPartnerFinanceProfileService.decide).not.toHaveBeenCalled();
    expect(screen.getAllByText('Save draft changes before submitting.')).toHaveLength(2);
    fireEvent.change(input('AP reference'), { target: { value: 'AP-42' } });
    expect(within(apCard()).getByRole('button', { name: 'Submit for approval' })).toBeEnabled();
  });
});
