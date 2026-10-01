import React from 'react';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import CrmLeadsPage from './leads/page';
import CrmOpportunitiesPage from './opportunities/page';
import CrmQuotesPage from './quotes/page';
import type { CrmLeadDetailDto, CrmOpportunityDetailDto, CrmQuoteDetailDto } from '@/services/crmService';

const mocks = vi.hoisted(() => ({
  query: '', getLead: vi.fn(), getOpportunity: vi.fn(), getQuote: vi.fn(),
  getLeads: vi.fn(), getOpportunities: vi.fn(), getQuotes: vi.fn(), toast: vi.fn(),
  getBaseCurrency: vi.fn(),
}));
vi.mock('next/navigation', () => ({ useSearchParams: () => new URLSearchParams(mocks.query), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/services/crmService', () => ({ crmService: mocks }));
vi.mock('@/services/businessPartnerService', () => ({ businessPartnerService: { getAllPartnersForDropdown: async () => [] } }));
vi.mock('@/services/salesReferenceService', () => ({
  salesReferenceService: {
    getActiveCurrencies: async () => [
      { code: 'GHS', name: 'Ghana Cedi', isBaseCurrency: true },
      { code: 'USD', name: 'US Dollar', isBaseCurrency: false },
    ],
  },
}));
vi.mock('./components/SalesHandoffActions', () => ({ SalesHandoffActions: () => null }));
vi.mock('sonner', () => ({ toast: { error: mocks.toast, success: vi.fn() } }));

const empty = { items: [], totalCount: 0, page: 1, pageSize: 12, totalPages: 1 };
const chain = { opportunityId: '', opportunityName: '', nodes: [] };
const lead = (id: string): CrmLeadDetailDto => ({
  leadId: id, firstName: 'Search', lastName: id, fullName: `Selected ${id}`, leadSource: 'Direct',
  leadStatus: 'New', qualificationScore: 0, estimatedValue: 0, opportunityCount: 0,
  needsFollowUp: false, createdAt: '2026-09-27', opportunities: [],
});
const opportunity = (id: string): CrmOpportunityDetailDto => ({
  opportunityId: id, name: `Selected ${id}`, stage: 'Prospecting', amount: 0, currency: 'USD',
  probability: 0, weightedValue: 0, expectedCloseDate: '2026-10-01', opportunityType: 'New Business',
  leadSource: 'Direct', isClosingSoon: false, createdAt: '2026-09-27',
  quotes: [], relatedContracts: [], relatedProjects: [], conversionChain: chain,
});
const quote = (id: string): CrmQuoteDetailDto => ({
  quoteId: id, opportunityId: '', quoteName: `Selected ${id}`, quoteStatus: 'Draft', value: 0, currency: 'USD',
  validUntil: '2026-10-01', isExpiringSoon: false, documentNumber: `Q-${id}`, documentDate: '2026-09-27',
  createdAt: '2026-09-27', isAccepted: false, lineItems: [], conversionChain: chain,
});
const cases = [
  { query: 'leadId', Page: CrmLeadsPage, detail: mocks.getLead, list: mocks.getLeads, record: lead },
  { query: 'opportunityId', Page: CrmOpportunitiesPage, detail: mocks.getOpportunity, list: mocks.getOpportunities, record: opportunity },
  { query: 'quoteId', Page: CrmQuotesPage, detail: mocks.getQuote, list: mocks.getQuotes, record: quote },
];

describe.each(cases)('$query global-search navigation', ({ query, Page, detail, list, record }) => {
  beforeEach(() => {
    vi.stubGlobal('React', React); vi.resetAllMocks(); mocks.query = `${query}=first`;
    mocks.getBaseCurrency.mockResolvedValue({ code: 'GHS' });
    mocks.getLeads.mockResolvedValue(empty); mocks.getOpportunities.mockResolvedValue(empty); mocks.getQuotes.mockResolvedValue(empty);
    detail.mockImplementation(async (id: string) => record(id));
  });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('opens a second result on the mounted register and clears a denied selection', async () => {
    const view = render(<Page />);
    expect(await screen.findByText('Selected first')).toBeInTheDocument();
    mocks.query = `${query}=second`;
    view.rerender(<Page />);
    expect(await screen.findByText('Selected second')).toBeInTheDocument();
    expect(screen.queryByText('Selected first')).not.toBeInTheDocument();
    detail.mockRejectedValue(new Error('Record access denied.'));
    mocks.query = `${query}=denied`;
    view.rerender(<Page />);
    await waitFor(() => expect(mocks.toast).toHaveBeenCalledWith('Record access denied.'));
    expect(screen.queryByText('Selected second')).not.toBeInTheDocument();
  });

  it('ignores a late detail response for a previous result', async () => {
    let finish!: (value: ReturnType<typeof record>) => void;
    detail.mockImplementation((id: string) => id === 'first'
      ? new Promise(resolve => { finish = resolve; }) : Promise.resolve(record(id)));
    const view = render(<Page />);
    await waitFor(() => expect(detail).toHaveBeenCalledWith('first'));
    mocks.query = `${query}=second`;
    view.rerender(<Page />);
    expect(await screen.findByText('Selected second')).toBeInTheDocument();
    await act(async () => finish(record('first')));
    expect(screen.queryByText('Selected first')).not.toBeInTheDocument();
    expect(screen.getByText('Selected second')).toBeInTheDocument();
  });

  it('does not let the initial register request restore the old query selection', async () => {
    let finishList!: (value: typeof empty) => void;
    list.mockImplementationOnce(() => new Promise(resolve => { finishList = resolve; }));
    const view = render(<Page />);
    expect(await screen.findByText('Selected first')).toBeInTheDocument();
    mocks.query = `${query}=second`;
    view.rerender(<Page />);
    expect(await screen.findByText('Selected second')).toBeInTheDocument();
    await act(async () => finishList(empty));
    expect(screen.queryByText('Selected first')).not.toBeInTheDocument();
    expect(screen.getByText('Selected second')).toBeInTheDocument();
  });
});
