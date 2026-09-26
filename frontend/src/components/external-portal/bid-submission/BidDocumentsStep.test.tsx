import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import BidDocumentsStep from './BidDocumentsStep';
import { parseBidDocumentRequirements } from './documentRequirements';
import type { CreateTenderBidDto, TenderBidDocumentDto } from '@/services/tenderBidService';
import type { TenderDetailDto } from '@/services/tenderService';

const show = (requiredDocuments?: string) => render(<BidDocumentsStep
  bidData={{} as CreateTenderBidDto} updateBidData={vi.fn()} bidId="saved-bid"
  tender={{ requiredDocuments } as TenderDetailDto} />);

describe('Published bid document requirements', () => {
  it('does not count item evidence as a required bid-level document', () => {
    render(<BidDocumentsStep bidData={{} as CreateTenderBidDto} updateBidData={vi.fn()} bidId="saved-bid"
      tender={{ requiredDocuments: JSON.stringify([{ documentType: 'TaxClearance', documentName: 'Tax evidence', isRequired: true }]) } as TenderDetailDto}
      uploadedDocuments={[{ id: 'doc-1', documentName: 'item-tax.pdf', documentType: 'TaxClearance', tenderBidItemId: 'line-1' } as TenderBidDocumentDto]} />);
    expect(screen.queryByText('item-tax.pdf')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Choose File' })).toBeInTheDocument();
  });

  it('requires confirmation before removing a saved document', async () => {
    const remove = vi.fn().mockResolvedValue(undefined);
    render(<BidDocumentsStep bidData={{} as CreateTenderBidDto} updateBidData={vi.fn()} bidId="saved-bid"
      tender={{ requiredDocuments: JSON.stringify([{ documentType: 'TaxClearance', documentName: 'Tax evidence', isRequired: true }]) } as TenderDetailDto}
      uploadedDocuments={[{ id: 'doc-1', documentName: 'tax.pdf', documentType: 'TaxClearance' } as TenderBidDocumentDto]}
      onDocumentDelete={remove} />);
    fireEvent.click(screen.getByRole('button', { name: /remove/i }));
    expect(remove).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Remove document' }));
    await waitFor(() => expect(remove).toHaveBeenCalledWith('doc-1'));
  });
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
