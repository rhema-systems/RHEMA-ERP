import React from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from '@/services/purchasingService';
import { ReceiptSourceEvidenceControl } from './ReceiptSourceEvidenceControl';

vi.mock('@/services/purchasingService', () => ({ purchasingService: {
  getReceiptSourceEvidence: vi.fn(), uploadReceiptSourceEvidence: vi.fn(),
} }));
afterEach(cleanup);

describe('ReceiptSourceEvidenceControl disclosure', () => {
  it('keeps the upload and missing-waybill notice visible but folds explanatory policy text', async () => {
    vi.mocked(purchasingService.getReceiptSourceEvidence).mockResolvedValue({
      receiptId: 'receipt-1', receiptNumber: 'REC-1', purchaseOrderNumber: 'PO-1', supplierName: 'Supplier',
      waybillRequired: true, waybillReady: false, canUpload: true, inspectionEvidenceLocked: false,
      financeOwnershipNotice: 'Invoice copies do not create Finance postings.', evidence: [],
    });
    render(<ReceiptSourceEvidenceControl receiptId="receipt-1" />);
    expect(await screen.findByText('Waybill required')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Attach' })).toBeVisible();
    expect(document.querySelector('input[type="file"]')).toBeVisible();
    expect(screen.getByText('Invoice copies do not create Finance postings.')).not.toBeVisible();
    fireEvent.click(screen.getByText('About invoice copies'));
    expect(screen.getByText('Invoice copies do not create Finance postings.')).toBeVisible();
    expect(purchasingService.uploadReceiptSourceEvidence).not.toHaveBeenCalled();
  });
});
