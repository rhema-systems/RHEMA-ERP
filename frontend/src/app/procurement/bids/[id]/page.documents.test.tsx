import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import BidDetailPage from './page';

const api = vi.hoisted(() => ({ bid: vi.fn(), tender: vi.fn(), download: vi.fn() }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'bid-1' }), useRouter: () => ({ push: vi.fn(), replace: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => false }) }));
vi.mock('@/services/tenderBidService', () => ({ getBidById: api.bid, getBidPayments: vi.fn().mockResolvedValue([]), downloadBidDocument: api.download }));
vi.mock('@/services/tenderService', () => ({ tenderService: { getTenderById: api.tender } }));
vi.mock('@/components/quantity-survey/QuantitySurveyTenderBoqVettingPanel', () => ({ QuantitySurveyTenderBoqVettingPanel: () => null }));

const file = (id: string, documentType: string, documentName: string) => ({
  id, documentType, documentName, fileSize: 1000, uploadedDate: '2026-09-05T19:00:00Z',
});
const proposals = [
  file('technical-1', 'TechnicalProposal', 'technical.pdf'),
  file('commercial-1', 'CommercialProposal', 'commercial.pdf'),
];
const certificates = [
  file('registration-1', 'CompanyRegistration', 'registration.pdf'),
  file('tax-1', 'TaxClearance', 'tax.pdf'),
];
const requirement = (documentType: string, documentName: string, isRequired = true) => ({ documentType, documentName, isRequired });
const proposalRequirements = [
  requirement('TechnicalProposal', 'Technical Proposal'),
  requirement('CommercialProposal', 'Commercial Proposal'),
];
const requirements = [
  ...proposalRequirements,
  requirement('CompanyRegistration', 'Business registration'),
  requirement('TaxClearance', 'Tax clearance'),
  requirement('FinancialStatements', 'Financial statements'),
];
const bid = {
  id: 'bid-1', tenderId: 'tender-1', bidNumber: 'BID-1', status: 'Opened', isSealed: false,
  totalBidAmount: 52000, currency: 'GHS', items: [], documents: [...proposals, ...certificates],
};
const tender = {
  id: 'tender-1', status: 'Closed', usesControlledTenderLifecycle: false,
  requiredDocuments: JSON.stringify(requirements),
};

beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  api.tender.mockResolvedValue(tender);
  api.bid.mockResolvedValue(bid);
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

async function openDocuments(count: number) {
  render(<BidDetailPage />);
  const tab = await screen.findByRole('tab', { name: `Documents (${count})` });
  fireEvent.mouseDown(tab, { button: 0, ctrlKey: false });
  return screen.findByRole('tabpanel', { name: `Documents (${count})` });
}

describe('back-office tender supporting documents', () => {
  it('lists tender-configured certificates, matches uploads and highlights genuinely missing financial statements', async () => {
    const panel = await openDocuments(2);
    const checklist = within(panel).getByRole('region', { name: 'Document requirements' });
    expect(within(checklist).getByText('Business registration').closest('li')).toHaveTextContent('Uploaded (1)');
    expect(within(checklist).getByText('Tax clearance').closest('li')).toHaveTextContent('Uploaded (1)');
    expect(within(checklist).getByText('Financial statements').closest('li')).toHaveTextContent('Missing');
    expect(within(panel).getByText('registration.pdf')).toBeInTheDocument();
    expect(within(panel).getByText('tax.pdf')).toBeInTheDocument();
    expect(within(panel).queryByText('technical.pdf')).not.toBeInTheDocument();
    expect(within(panel).queryByText('commercial.pdf')).not.toBeInTheDocument();
    fireEvent.click(within(panel).getAllByRole('button', { name: 'Download' })[0]);
    expect(api.download).toHaveBeenCalledExactlyOnceWith('bid-1', 'registration-1', 'registration.pdf');
  });

  it('keeps proposals exclusively in Proposals and explains the zero supporting count for a proposal-only tender', async () => {
    api.bid.mockResolvedValue({ ...bid, documents: proposals });
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: JSON.stringify(proposalRequirements) });
    const panel = await openDocuments(0);
    expect(within(panel).getByText(/No supporting-document requirements were configured/)).toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
    expect(within(panel).queryByText('Missing')).not.toBeInTheDocument();
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Proposals' }), { button: 0, ctrlKey: false });
    const proposalPanel = await screen.findByRole('tabpanel', { name: 'Proposals' });
    expect(within(proposalPanel).getAllByRole('button', { name: 'Download' })).toHaveLength(2);
    expect(within(proposalPanel).getAllByText('technical.pdf').length).toBeGreaterThan(0);
    expect(within(proposalPanel).getAllByText('commercial.pdf').length).toBeGreaterThan(0);
  });

  it('retains supporting uploads with no matching requirement without counting proposals', async () => {
    api.bid.mockResolvedValue({ ...bid, documents: [...bid.documents, file('other-1', 'Other', 'brochure.pdf')] });
    const panel = await openDocuments(3);
    expect(within(panel).getByText('Supporting Documents')).toBeInTheDocument();
    expect(within(panel).getAllByRole('row')).toHaveLength(4);
    expect(within(panel).getAllByText('brochure.pdf')).toHaveLength(1);
  });

  it.each([[], undefined])('shows required certificates as missing when uploads are %s', async (documents) => {
    api.bid.mockResolvedValue({ ...bid, documents });
    const panel = await openDocuments(0);
    expect(within(panel).getByText('No supporting files uploaded')).toBeInTheDocument();
    expect(within(panel).getAllByText('Missing')).toHaveLength(3);
    expect(within(panel).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
  });

  it('does not mislabel phase-withheld supporting files as missing', async () => {
    api.bid.mockResolvedValue({ ...bid, isFinancialProposalSealed: true, documents: proposals.slice(0, 1) });
    const panel = await openDocuments(0);
    expect(within(panel).getAllByText('Not available in this phase')).toHaveLength(3);
    expect(within(panel).queryByText('Missing')).not.toBeInTheDocument();
    expect(within(panel).queryByText('technical.pdf')).not.toBeInTheDocument();
  });

  it('distinguishes optional requirements from missing mandatory certificates', async () => {
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: JSON.stringify([requirement('Other', 'Company brochure', false)]) });
    const panel = await openDocuments(2);
    expect(within(panel).getByText('Not supplied')).toBeInTheDocument();
    expect(within(panel).queryByText('Missing')).not.toBeInTheDocument();
  });

  it.each(['invalid JSON', '{}'])('does not claim completeness for invalid requirements %s', async (requiredDocuments) => {
    api.tender.mockResolvedValue({ ...tender, requiredDocuments });
    const panel = await openDocuments(2);
    expect(within(panel).getByText('Document requirements unavailable')).toBeInTheDocument();
    expect(within(panel).getByText('registration.pdf')).toBeInTheDocument();
  });

  it('does not invent certificates if the tender configured no requirements', async () => {
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: '[]' });
    const panel = await openDocuments(2);
    expect(within(panel).getByText(/No supporting-document requirements were configured/)).toBeInTheDocument();
    expect(within(panel).queryByText('Missing')).not.toBeInTheDocument();
  });

  it('does not expose supporting uploads for a sealed bid even when stale data contains them', async () => {
    api.bid.mockResolvedValue({ ...bid, status: 'Submitted', isSealed: true });
    render(<BidDetailPage />);
    expect(await screen.findByText('Bid sealed')).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: /Documents/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
    expect(screen.queryByText('registration.pdf')).not.toBeInTheDocument();
    expect(api.download).not.toHaveBeenCalled();
  });
});
