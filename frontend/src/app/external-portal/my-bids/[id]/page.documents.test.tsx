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
  it('counts proposal uploads and matches both required documents without claiming they are missing', async () => {
    const panel = await openDocuments(2);
    expect(within(panel).getByText('2 requirement(s) - 2 required, 0 optional • 2 file(s) uploaded')).toBeInTheDocument();
    expect(within(panel).getByText('technical.pdf')).toBeInTheDocument();
    expect(within(panel).getByText('commercial.pdf')).toBeInTheDocument();
    expect(within(panel).getAllByText('1 uploaded')).toHaveLength(2);
    expect(within(panel).queryByText('No document uploaded for this requirement')).not.toBeInTheDocument();
    fireEvent.click(within(panel).getAllByRole('button', { name: 'Download' })[0]);
    expect(api.download).toHaveBeenCalledExactlyOnceWith('bid-1', 'technical-1', 'technical.pdf');
  });

  it('still shows a genuinely missing commercial requirement', async () => {
    const bid = await api.bid();
    api.bid.mockResolvedValue({ ...bid, documents: documents.slice(0, 1) });
    const panel = await openDocuments(1);
    expect(within(panel).getByText('technical.pdf')).toBeInTheDocument();
    expect(within(panel).getAllByText('No document uploaded for this requirement')).toHaveLength(1);
    expect(within(panel).queryByText('commercial.pdf')).not.toBeInTheDocument();
  });

  it('retains unmatched supporting files without double-counting proposal uploads', async () => {
    const bid = await api.bid();
    api.bid.mockResolvedValue({ ...bid, documents: [...documents, { ...documents[0], id: 'extra-1', documentType: 'Other', documentName: 'extra.pdf' }] });
    const panel = await openDocuments(3);
    expect(within(panel).getByText('Other Documents')).toBeInTheDocument();
    for (const name of ['technical.pdf', 'commercial.pdf', 'extra.pdf']) {
      expect(within(panel).getAllByText(name)).toHaveLength(1);
    }
  });

  it('shows all uploaded files when there are no configured requirements', async () => {
    api.tender.mockResolvedValue({ ...tender, requiredDocuments: '[]' });
    const panel = await openDocuments(2);
    expect(within(panel).getByText('2 file(s) uploaded')).toBeInTheDocument();
    expect(within(panel).getByText('technical.pdf')).toBeInTheDocument();
    expect(within(panel).getByText('commercial.pdf')).toBeInTheDocument();
  });

  it('keeps zero and missing indicators when there really are no uploads', async () => {
    const bid = await api.bid();
    api.bid.mockResolvedValue({ ...bid, documents: undefined });
    const panel = await openDocuments(0);
    expect(within(panel).getAllByText('No document uploaded for this requirement')).toHaveLength(2);
    expect(within(panel).queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
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
