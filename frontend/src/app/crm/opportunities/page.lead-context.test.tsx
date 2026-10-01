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
