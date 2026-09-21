import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import BidDocumentsStep from './BidDocumentsStep';
import { parseBidDocumentRequirements } from './documentRequirements';
import type { CreateTenderBidDto } from '@/services/tenderBidService';
import type { TenderDetailDto } from '@/services/tenderService';

const show = (requiredDocuments?: string) => render(<BidDocumentsStep
  bidData={{} as CreateTenderBidDto} updateBidData={vi.fn()} bidId="saved-bid"
  tender={{ requiredDocuments } as TenderDetailDto} />);

describe('Published bid document requirements', () => {
  it.each([undefined, '', '[]'])('does not invent requirements for %s', value => {
    show(value);
    expect(screen.getByText(/no supporting document requirements/)).toBeInTheDocument();
    expect(screen.queryByText('Company Registration Certificate')).not.toBeInTheDocument();
    expect(screen.queryByText('Tax Clearance Certificate')).not.toBeInTheDocument();
  });
  it('retains explicitly configured mandatory documents', () => {
    show(JSON.stringify([{ documentType: 'TaxClearance', documentName: 'Configured tax evidence', isRequired: true }]));
    expect(screen.getByText('Configured tax evidence')).toBeInTheDocument();
    expect(screen.getByText(/Required documents: 1 of 1/)).toBeInTheDocument();
  });
  it.each(['broken-json', '{}', '[{"documentType":"TaxClearance"}]'])('rejects malformed requirements %s', value => {
    expect(() => parseBidDocumentRequirements(value)).toThrow(/contact procurement/);
    show(value);
    expect(screen.getByRole('alert')).toHaveTextContent(/contact procurement/);
    expect(screen.queryByText(/no supporting document requirements/)).not.toBeInTheDocument();
  });
});
