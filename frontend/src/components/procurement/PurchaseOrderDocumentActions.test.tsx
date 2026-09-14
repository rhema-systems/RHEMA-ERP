import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from 'sonner';
import { PurchaseOrderDocumentActions } from './PurchaseOrderDocumentActions';
import { buildPurchaseOrderPdf } from '@/lib/purchase-order-document';
import type { PurchaseOrderDetailDto } from '@/services/purchasingService';

vi.mock('next/dynamic', () => ({ default: () => (props: { fileData?: Uint8Array }) => <div data-testid="pdf-bytes">{Array.from(props.fileData || []).join(',')}</div> }));
vi.mock('@/services/api.service', () => ({ apiService: { downloadBlob: vi.fn() } }));
vi.mock('@/services/document-management.service', () => ({ documentManagementService: {} }));
vi.mock('sonner', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));
vi.mock('@/lib/purchase-order-document', () => ({ buildPurchaseOrderPdf: vi.fn() }));
const order = { id: 'po1', orderNumber: 'PO-1' } as PurchaseOrderDetailDto;
const company = { name: 'Current authorized company' };
beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(buildPurchaseOrderPdf).mockReturnValue({ output: () => new Uint8Array([37, 80, 68, 70]).buffer } as never);
});
describe('purchase order document actions', () => {
  it.each(['Print', 'Export PDF'])('%s opens an observable in-page preview from the existing generator', async (action) => {
    const popup = vi.spyOn(window, 'open').mockReturnValue(null);
    render(<PurchaseOrderDocumentActions order={order} getCompany={async () => company} />);
    fireEvent.click(screen.getByRole('button', { name: action }));
    expect(await screen.findByTestId('pdf-bytes')).toHaveTextContent('37,80,68,70');
    expect(screen.getByRole('dialog', { name: 'PO-1' })).toBeInTheDocument();
    expect(buildPurchaseOrderPdf).toHaveBeenCalledWith(order, company);
    expect(popup).not.toHaveBeenCalled();
    expect(toast.success).not.toHaveBeenCalled();
    popup.mockRestore();
  });
  it('retains actions and reports failure without claiming a download', async () => {
    render(<PurchaseOrderDocumentActions order={order} getCompany={async () => { throw new Error('Company details unavailable.'); }} />);
    fireEvent.click(screen.getByRole('button', { name: 'Export PDF' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Company details unavailable.'));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Export PDF' })).toBeEnabled();
    expect(toast.success).not.toHaveBeenCalled();
  });
  it('prevents duplicate print/export while company details are loading', async () => {
    let release!: (value: typeof company) => void;
    const getCompany = vi.fn(() => new Promise<typeof company>(resolve => { release = resolve; }));
    render(<PurchaseOrderDocumentActions order={order} getCompany={getCompany} />);
    fireEvent.click(screen.getByRole('button', { name: 'Print' }));
    expect(screen.getByRole('button', { name: 'Export PDF' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Export PDF' }));
    expect(getCompany).toHaveBeenCalledTimes(1);
    release(company);
    await screen.findByTestId('pdf-bytes');
    expect(buildPurchaseOrderPdf).toHaveBeenCalledTimes(1);
  });
});
