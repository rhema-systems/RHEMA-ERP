import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { toast } from 'sonner';
import type { TenderDetailDto } from '@/services/tenderService';
import type { CreateTenderBidDto, TenderBidDocumentDto } from '@/services/tenderBidService';
import { BidLotItemDocuments } from './BidLotItemDocuments';
import { validateBidDocumentFile } from './bid-document-files';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/services/tenderBidService', () => ({ downloadBidDocument: vi.fn() }));
Object.assign(globalThis, { React });

const tender = { lots: [
  { id: 'lot-1', lotNumber: 1, title: 'Equipment', items: [{ id: 'item-1', description: 'Laptop' }] },
  { id: 'lot-2', lotNumber: 2, title: 'Furniture', items: [{ id: 'item-2', description: 'Desk' }] },
] } as TenderDetailDto;
const bidData = { selectedLotIds: ['lot-1'] } as CreateTenderBidDto;

describe('lot item supporting documents', () => {
  it('uploads against the selected lot item and shows every attached file', async () => {
    const upload = vi.fn().mockResolvedValue(undefined);
    const remove = vi.fn();
    const documents = ['a.pdf', 'b.pdf'].map((name, index) => ({ id: `doc-${index}`, documentName: name, tenderItemId: 'item-1' })) as TenderBidDocumentDto[];
    render(<BidLotItemDocuments tender={tender} bidData={bidData} bidId="bid-1" documents={documents} onUpload={upload} onDelete={remove} />);
    expect(screen.queryByText('Desk')).not.toBeInTheDocument();
    expect(screen.getByText('a.pdf')).toBeInTheDocument();
    expect(screen.getByText('b.pdf')).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole('button', { name: 'Remove' })[1]);
    expect(remove).toHaveBeenCalledWith('doc-1');
    const file = new File(['specification'], 'spec.pdf', { type: 'application/pdf' });
    fireEvent.change(screen.getByLabelText('Supporting file for Laptop'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Upload file for Laptop' }));
    await waitFor(() => expect(upload).toHaveBeenCalledWith(file, 'TechnicalItemSupportingDocument', 'item-1'));
    await waitFor(() => expect(screen.queryByText('spec.pdf')).not.toBeInTheDocument());
  });

  it('retains the selected file and displays the server message when the deadline blocks upload', async () => {
    const upload = vi.fn().mockRejectedValue(new Error('The submission deadline has passed.'));
    render(<BidLotItemDocuments tender={tender} bidData={bidData} bidId="bid-1" documents={[]} onUpload={upload} />);
    const file = new File(['specification'], 'retry.pdf', { type: 'application/pdf' });
    fireEvent.change(screen.getByLabelText('Supporting file for Laptop'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Upload file for Laptop' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('The submission deadline has passed.'));
    expect(screen.getByText('retry.pdf')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Upload file for Laptop' })).toBeEnabled();
  });

  it('requires a saved draft and rejects unsupported files and files exceeding the server limit', () => {
    render(<BidLotItemDocuments tender={tender} bidData={bidData} documents={[]} />);
    expect(screen.getByLabelText('Supporting file for Laptop')).toBeDisabled();
    expect(validateBidDocumentFile(new File(['binary'], 'program.exe'))).toBeTruthy();
    const file = new File(['data'], 'large.pdf');
    Object.defineProperty(file, 'size', { value: 20_000_001 });
    expect(validateBidDocumentFile(file)).toBeTruthy();
    expect(validateBidDocumentFile(new File(['data'], 'certificate.PDF'))).toBeNull();
  });
});
