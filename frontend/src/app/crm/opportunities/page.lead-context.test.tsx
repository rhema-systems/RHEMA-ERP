import React from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import CrmOpportunitiesPage from './page';

const mocks = vi.hoisted(() => ({
  query: 'leadId=lead-1&new=1', getLead: vi.fn(), getLeads: vi.fn(),
  getOpportunities: vi.fn(), getOpportunity: vi.fn(), createOpportunity: vi.fn(),
  listPartners: vi.fn(), error: vi.fn(), getBaseCurrency: vi.fn(), getActive: vi.fn(),
}));
vi.mock('next/navigation', () => ({ useSearchParams: () => new URLSearchParams(mocks.query) }));
vi.mock('@/services/crmService', () => ({ crmService: mocks }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getAllPartnersForDropdown: mocks.listPartners } }));
vi.mock('@/services/salesReferenceService', () => ({ salesReferenceService: { getActiveCurrencies: mocks.getActive } }));
vi.mock('../components/SalesHandoffActions', () => ({ SalesHandoffActions: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: mocks.error } }));

const emptyPage = { items: [], totalCount: 0, page: 1, pageSize: 12, totalPages: 1 };
const lead = { leadId: 'lead-1', fullName: 'Ama Lead', convertedBusinessPartnerId: 'customer-1', opportunities: [] };
beforeEach(() => {
  vi.stubGlobal('React', React); vi.clearAllMocks(); mocks.query = 'leadId=lead-1&new=1';
  // The selected Lead is deliberately outside the paged selector lookup.
  mocks.getLeads.mockResolvedValue(emptyPage);
  mocks.getLead.mockResolvedValue(lead);
  mocks.getOpportunities.mockResolvedValue(emptyPage);
  mocks.listPartners.mockResolvedValue([{ id: 'customer-1', partnerName: 'Ama Customer', partnerType: 'Customer', status: 'Active' }]);
  mocks.createOpportunity.mockResolvedValue({ opportunityId: 'created' });
  mocks.getOpportunity.mockResolvedValue({ opportunityId: 'created', name: 'Created opportunity', stage: 'Prospecting', amount: 0,
    weightedValue: 0, currency: 'GHS', probability: 10, expectedCloseDate: '2026-10-31', createdAt: '2026-10-01', quotes: [], relatedContracts: [], relatedProjects: [],
    conversionChain: { nodes: [] } });
  mocks.getBaseCurrency.mockResolvedValue({ code: 'GHS' });
  mocks.getActive.mockResolvedValue([{ code: 'GHS', name: 'Ghana Cedi', isActive: true }]);
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

async function dialog() { return within(await screen.findByRole('dialog')); }

describe('Opportunity creation from a selected Lead', () => {
  it('loads an exact linked opportunity outside the current filtered page and shows its detail first', async () => {
    mocks.query = 'opportunityId=target-1&stage=Closed%20Won&search=unrelated';
    mocks.getOpportunities.mockResolvedValue({
      ...emptyPage,
      items: [{ opportunityId: 'other-1', name: 'Other opportunity', stage: 'Closed Won',
        amount: 500, currency: 'GHS', weightedValue: 500, probability: 100,
        expectedCloseDate: '2026-10-31', isClosingSoon: false }],
      totalCount: 25,
    });
    mocks.getOpportunity.mockResolvedValue({
      opportunityId: 'target-1', name: 'Linked property opportunity', stage: 'Closed Won',
      amount: 10000, currency: 'GHS', probability: 100,
      expectedCloseDate: '2026-10-31', createdAt: '2026-10-01',
      quotes: [], relatedContracts: [], relatedProjects: [], conversionChain: { nodes: [] },
    });
    render(<CrmOpportunitiesPage />);

    expect(await screen.findByText('Linked property opportunity')).toBeVisible();
    expect(mocks.getOpportunity).toHaveBeenCalledWith('target-1');
    expect(screen.getByText('Viewing the linked opportunity')).toBeVisible();
    expect(screen.getByText('Linked Opportunity').closest('.order-1')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Browse all opportunities' })).toHaveAttribute('href', '/crm/opportunities');
    expect(screen.getByText('Other opportunity')).toBeInTheDocument();
  });

  it('accepts legacy id links and keeps all related records available as full-width tabs', async () => {
    mocks.query = 'id=target-1';
    mocks.getOpportunity.mockResolvedValue({
      opportunityId: 'target-1', name: 'Linked property opportunity', stage: 'Qualification',
      amount: 10000, currency: 'GHS', probability: 20,
      expectedCloseDate: '2026-10-31', createdAt: '2026-10-01',
      quotes: [{ quoteId: 'quote-1', quoteName: 'Quote A', documentNumber: 'Q-1', quoteStatus: 'Draft', value: 10000, currency: 'GHS' }],
      conversionChain: { nodes: [{ entityType: 'Lead', entityId: 'lead-1', stage: 'Lead', relationshipType: 'Origin', title: 'Lead A', status: 'Converted' }] },
      relatedContracts: [{ contractId: 'contract-1', contractTitle: 'Contract A', contractNumber: 'C-1', relationshipType: 'Origin', status: 'Active', contractValue: 10000 }],
      relatedProjects: [{ projectId: 'project-1', title: 'Project A', projectCode: 'P-1', relationshipType: 'Origin', status: 'Active', value: 10000, progressPercent: 25 }],
    });
    render(<CrmOpportunitiesPage />);

    expect(await screen.findByText('Lead A')).toBeVisible();
    expect(mocks.getOpportunity).toHaveBeenCalledWith('target-1');
    fireEvent.mouseDown(screen.getByRole('tab', { name: /Quotes/ }), { button: 0 });
    expect(await screen.findByText('Quote A')).toBeVisible();
    expect(screen.queryByText('Lead A')).not.toBeInTheDocument();
    fireEvent.mouseDown(screen.getByRole('tab', { name: /Related Contracts/ }), { button: 0 });
    expect(await screen.findByText('Contract A')).toBeVisible();
    fireEvent.mouseDown(screen.getByRole('tab', { name: /Related Projects/ }), { button: 0 });
    expect(await screen.findByText('Project A')).toBeVisible();
  });

  it('shows weighted pipeline values in their own currencies', async () => {
    mocks.query = '';
    mocks.getOpportunities.mockResolvedValue({
      ...emptyPage,
      items: [
        { opportunityId: 'ghs-1', currency: 'GHS', weightedValue: 1000, isClosingSoon: false },
        { opportunityId: 'usd-1', currency: 'USD', weightedValue: 500, isClosingSoon: false },
      ],
      totalCount: 2,
    });
    render(<CrmOpportunitiesPage />);

    const expected = [
      new Intl.NumberFormat('en-US', { style: 'currency', currency: 'GHS', maximumFractionDigits: 0 }).format(1000),
      new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(500),
    ].join(' · ');
    await waitFor(() => expect(
      screen.getByText('Weighted Pipeline').nextElementSibling?.textContent?.replace(/\u00a0/g, ' ')
    ).toBe(expected.replace(/\u00a0/g, ' ')));
    expect(screen.getByText('By currency · current page only')).toBeInTheDocument();
  });

  it('resolves the Lead directly, hides inherited selectors and saves its linked customer', async () => {
    mocks.query += '&businessPartnerId=conflicting-query-customer';
    render(<CrmOpportunitiesPage />);
    const form = await dialog();
    expect(await form.findByText('Ama Lead')).toBeInTheDocument();
    expect(await form.findByText('Ama Customer')).toBeInTheDocument();
    expect(form.queryByRole('combobox', { name: /^Lead$/ })).not.toBeInTheDocument();
    expect(form.queryByRole('combobox', { name: /^CRM Account$/ })).not.toBeInTheDocument();
    fireEvent.change(form.getByLabelText('Opportunity Name'), { target: { value: 'Lead deal' } });
    fireEvent.click(form.getByRole('button', { name: 'Save Opportunity' }));
    await waitFor(() => expect(mocks.createOpportunity).toHaveBeenCalledWith(expect.objectContaining({ leadId: 'lead-1', businessPartnerId: 'customer-1', name: 'Lead deal' })));
    expect(mocks.getLead).toHaveBeenCalledWith('lead-1');
  });
  it('keeps account selection available when the selected Lead has no linked customer', async () => {
    mocks.getLead.mockResolvedValue({ ...lead, convertedBusinessPartnerId: undefined });
    render(<CrmOpportunitiesPage />);
    const form = await dialog();
    expect(await form.findByText('Ama Lead')).toBeInTheDocument();
    expect(await form.findByRole('combobox', { name: /^CRM Account$/ })).toBeInTheDocument();
    expect(form.queryByRole('combobox', { name: /^Lead$/ })).not.toBeInTheDocument();
  });
  it('retains both selectors for standalone opportunity creation', async () => {
    mocks.query = 'new=1'; render(<CrmOpportunitiesPage />);
    const form = await dialog();
    expect(form.getByRole('combobox', { name: /^Lead$/ })).toBeInTheDocument();
    expect(form.getByRole('combobox', { name: /^CRM Account$/ })).toBeInTheDocument();
    expect(form.getByRole('combobox', { name: 'Opportunity Currency' })).toBeEnabled();
    expect(mocks.getLead).not.toHaveBeenCalled();
  });
  it('inherits and locks currency for a property-enquiry Lead', async () => {
    mocks.getLead.mockResolvedValue({
      ...lead,
      propertyEnquiryTicketId: 'ticket-1',
      propertyEnquiryCurrency: 'USD',
    });
    render(<CrmOpportunitiesPage />);
    const form = await dialog();
    const currency = await form.findByRole('combobox', { name: 'Opportunity Currency' });
    await waitFor(() => expect(currency).toHaveTextContent('USD'));
    expect(currency).toBeDisabled();
    expect(form.getByText('Inherited from the linked property enquiry.')).toBeInTheDocument();
  });
  it('keeps both selectors editable for an existing opportunity even in a Lead-scoped register', async () => {
    mocks.query = 'leadId=lead-1&opportunityId=existing';
    render(<CrmOpportunitiesPage />);
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Opportunity' }));
    const form = await dialog();
    expect(form.getByRole('combobox', { name: /^Lead$/ })).toBeInTheDocument();
    expect(form.getByRole('combobox', { name: /^CRM Account$/ })).toBeInTheDocument();
  });
  it('blocks save during lookup and preserves text entered before the Lead arrives', async () => {
    let resolveLead!: (value: typeof lead) => void;
    mocks.getLead.mockReturnValue(new Promise(resolve => { resolveLead = resolve; }));
    render(<CrmOpportunitiesPage />);
    const form = await dialog();
    fireEvent.change(form.getByLabelText('Opportunity Name'), { target: { value: 'Typed while loading' } });
    expect(form.getByRole('button', { name: 'Save Opportunity' })).toBeDisabled();
    await act(async () => resolveLead(lead));
    expect(form.getByLabelText('Opportunity Name')).toHaveValue('Typed while loading');
    expect(form.getByRole('button', { name: 'Save Opportunity' })).toBeEnabled();
    expect(form.getByText('Ama Customer')).toBeInTheDocument();
  });
  it('keeps failed lookup blocked until retry succeeds', async () => {
    mocks.getLead.mockRejectedValueOnce(new Error('Lead access unavailable'));
    render(<CrmOpportunitiesPage />);
    const form = await dialog();
    expect(await form.findByRole('alert')).toHaveTextContent('Lead access unavailable');
    expect(form.getByRole('button', { name: 'Save Opportunity' })).toBeDisabled();
    fireEvent.click(form.getByRole('button', { name: 'Retry Lead' }));
    expect(await form.findByText('Ama Customer')).toBeInTheDocument();
    expect(form.getByRole('button', { name: 'Save Opportunity' })).toBeEnabled();
  });
});
