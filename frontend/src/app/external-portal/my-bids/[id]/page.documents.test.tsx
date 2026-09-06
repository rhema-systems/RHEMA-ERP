import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import BidDetailPage from './page';
import BidReviewStep from '@/components/external-portal/bid-submission/BidReviewStep';

const api = vi.hoisted(() => ({ bid: vi.fn(), tender: vi.fn(), download: vi.fn(), push: vi.fn() }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'bid-1' }), useRouter: () => ({ push: api.push }) }));
vi.mock('@/services/tenderBidService', () => ({ getBidById: api.bid, downloadBidDocument: api.download }));
vi.mock('@/services/tenderService', () => ({ tenderService: { getTenderById: api.tender } }));
vi.mock('@/services/performanceBondService', () => ({}));
vi.mock('@/services/tenderAwardService', () => ({}));

const documents = [
  { id: 'technical-1', documentType: 'TechnicalProposal', documentName: 'technical.pdf', fileSize: 1000, uploadedDate: '2026-09-05T19:00:00Z' },
  { id: 'commercial-1', documentType: 'CommercialProposal', documentName: 'commercial.pdf', fileSize: 1200, uploadedDate: '2026-09-05T19:01:00Z' },
];
const requirements = documents.map(document => ({ documentType: document.documentType, documentName: document.documentType, isRequired: true }));
const tender = { id: 'tender-1', tenderNumber: 'TND-1', title: 'Test tender', tenderType: 'ITB', currency: 'GHS', estimatedValue: 100, lots: [], requiredDocuments: JSON.stringify(requirements) };

beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  api.tender.mockResolvedValue(tender);
  api.bid.mockResolvedValue({ id: 'bid-1', tenderId: 'tender-1', status: 'Submitted', bidNumber: 'BID-1', submittedDate: '2026-09-05T19:02:00Z', totalBidAmount: 100, currency: 'GHS', items: [], documents });
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

async function openDocuments(count: number) {
  render(<BidDetailPage />);
  const tab = await screen.findByRole('tab', { name: `Documents (${count})` });
  fireEvent.mouseDown(tab, { button: 0, ctrlKey: false });
  return screen.findByRole('tabpanel', { name: `Documents (${count})` });
}

describe('submitted bid document requirements', () => {
  const supportingRequirements = [
    { documentType: 'CompanyRegistration', documentName: 'Business registration', isRequired: true },
    { documentType: 'TaxClearance', documentName: 'Tax clearance', isRequired: true },
    { documentType: 'FinancialStatements', documentName: 'Financial statements', isRequired: true },
  ];
  const supportingFile = { ...documents[0], id: 'registration-1', documentType: 'CompanyRegistration', documentName: 'registration.pdf' };

  it('keeps proposals under Proposals and explains zero supporting documents for a proposal-only tender', async () => {
    const panel = await openDocuments(0);
    expect(within(panel).getByText(/No supporting-document requirements were configured/)).toBeInTheDocument();
    expect(within(panel).queryByText('technical.pdf')).not.toBeInTheDocument();
    expect(within(panel).queryByText('commercial.pdf')).not.toBeInTheDocument();
    expect(within(panel).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Proposals' }), { button: 0, ctrlKey: false });
    const proposals = await screen.findByRole('tabpanel', { name: 'Proposals' });
    expect(within(proposals).getByText('technical.pdf')).toBeInTheDocument();
    expect(within(proposals).getByText('commercial.pdf')).toBeInTheDocument();
    expect(within(proposals).getAllByRole('button', { name: 'Download' })).toHaveLength(2);
  });

  it('shows saved supporting requirements, missing certificates and the correct download identity', async () => {
    const bid = await api.bid();
    api.bid.mockResolvedValue({ ...bid, documents: [...documents, supportingFile] });
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: JSON.stringify([...requirements, ...supportingRequirements]) });
    const panel = await openDocuments(1);
    for (const req of supportingRequirements) expect(within(panel).getByText(req.documentName)).toBeInTheDocument();
    expect(within(panel).getAllByText('No document uploaded for this required supporting document')).toHaveLength(2);
    expect(within(panel).getByText('registration.pdf')).toBeInTheDocument();
    expect(within(panel).queryByText('commercial.pdf')).not.toBeInTheDocument();
    fireEvent.click(within(panel).getByRole('button', { name: 'Download' }));
    expect(api.download).toHaveBeenCalledExactlyOnceWith('bid-1', 'registration-1', 'registration.pdf');
  });

  it('retains unmatched supporting files without double-counting proposal uploads', async () => {
    const bid = await api.bid();
    api.bid.mockResolvedValue({ ...bid, documents: [...documents, { ...documents[0], id: 'extra-1', documentType: 'Other', documentName: 'extra.pdf' }] });
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: JSON.stringify(supportingRequirements) });
    const panel = await openDocuments(1);
    expect(within(panel).getByText('Other Documents')).toBeInTheDocument();
    expect(within(panel).getAllByText('extra.pdf')).toHaveLength(1);
    expect(within(panel).queryByText('technical.pdf')).not.toBeInTheDocument();
    expect(within(panel).queryByText('commercial.pdf')).not.toBeInTheDocument();
  });

  it('does not invent certificate requirements or include proposals for an empty configuration', async () => {
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: '[]' });
    const panel = await openDocuments(0);
    expect(within(panel).getByText(/No supporting-document requirements were configured/)).toBeInTheDocument();
    expect(within(panel).getByText('No supporting files uploaded')).toBeInTheDocument();
  });

  it.each([[], undefined])('keeps missing supporting requirements visible with uploads %s', async (files) => {
    const bid = await api.bid();
    api.bid.mockResolvedValue({ ...bid, documents: files });
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: JSON.stringify(supportingRequirements) });
    const panel = await openDocuments(0);
    expect(within(panel).getAllByText('No document uploaded for this required supporting document')).toHaveLength(3);
    expect(within(panel).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
  });

  it('distinguishes an optional certificate from a missing mandatory upload', async () => {
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: JSON.stringify([{ ...supportingRequirements[0], isRequired: false }]) });
    const panel = await openDocuments(0);
    expect(within(panel).getByText('Optional document not supplied')).toBeInTheDocument();
    expect(within(panel).queryByText('No document uploaded for this required supporting document')).not.toBeInTheDocument();
  });

  it.each(['invalid JSON', '{}', '[{"documentType":"TaxClearance"}]', null])('preserves uploads and warns when requirements are unavailable: %s', async (requiredDocuments) => {
    const bid = await api.bid();
    api.bid.mockResolvedValue({ ...bid, documents: [...documents, supportingFile] });
    api.tender.mockResolvedValue(requiredDocuments === null ? null : { ...tender, requiredDocuments });
    const panel = await openDocuments(1);
    expect(within(panel).getByText('Document requirements unavailable')).toBeInTheDocument();
    expect(within(panel).getByText('registration.pdf')).toBeInTheDocument();
    expect(within(panel).queryByText(/No supporting-document requirements were configured/)).not.toBeInTheDocument();
  });
});

describe('bid review uploaded document summary', () => {
  function review(files = documents) {
    render(<BidReviewStep tender={tender as React.ComponentProps<typeof BidReviewStep>['tender']} bidData={{ tenderId: 'tender-1', items: [] }} uploadedDocuments={files as React.ComponentProps<typeof BidReviewStep>['uploadedDocuments']} />);
  }

  it('includes both required proposal files in the upload count', () => {
    review();
    expect(screen.getByRole('heading', { name: 'Uploaded Documents (2)' })).toBeInTheDocument();
    expect(screen.getAllByText('Uploaded', { exact: true })).toHaveLength(2);
    expect(screen.queryByText('No required documents uploaded')).not.toBeInTheDocument();
  });

  it('does not claim an optional supporting upload is a required document', () => {
    review([{ ...documents[0], documentType: 'Other' }]);
    expect(screen.getByRole('heading', { name: 'Uploaded Documents (1)' })).toBeInTheDocument();
  });

  it('retains the real empty upload state', () => {
    review([]);
    expect(screen.getByRole('heading', { name: 'Uploaded Documents (0)' })).toBeInTheDocument();
    expect(screen.getByText('No documents uploaded')).toBeInTheDocument();
  });
});
