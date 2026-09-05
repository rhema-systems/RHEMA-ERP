import { describe, expect, it, vi } from 'vitest';

import {
  PendingPurchaseRequisitionDocument,
  REQUISITION_DOCUMENT_CLASSIFICATIONS,
  getPurchaseRequisitionDocumentErrorMessage,
  uploadPendingPurchaseRequisitionDocuments,
} from './PurchaseRequisitionDocuments';

function pending(name: string): PendingPurchaseRequisitionDocument {
  return {
    clientId: name,
    file: new File([name], name, { type: 'application/pdf' }),
    classification: 'Supporting document',
    title: name,
  };
}

describe('purchase requisition supporting documents', () => {
  it('uses controlled business classifications without technical identifiers', () => {
    expect(REQUISITION_DOCUMENT_CLASSIFICATIONS).toContain('Specification');
    expect(REQUISITION_DOCUMENT_CLASSIFICATIONS).toContain('Supporting document');
    expect(REQUISITION_DOCUMENT_CLASSIFICATIONS.join(' ')).not.toMatch(/checksum|record id/i);
  });

  it('uploads every queued document against the created requisition', async () => {
    const upload = vi.fn().mockResolvedValue(undefined);
    const documents = [pending('specification.pdf'), pending('estimate.pdf')];

    const result = await uploadPendingPurchaseRequisitionDocuments(
      'pr-1',
      documents,
      upload
    );

    expect(result).toEqual({ uploaded: 2, failed: [], errorMessages: [] });
    expect(upload).toHaveBeenNthCalledWith(
      1,
      'pr-1',
      'Supporting document',
      documents[0].file,
      'specification.pdf'
    );
  });

  it('retains failed files for a safe retry instead of hiding partial failure', async () => {
    const upload = vi.fn()
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce({ detail: 'The file failed the controlled upload.' });
    const documents = [pending('scope.pdf'), pending('drawing.pdf')];

    const result = await uploadPendingPurchaseRequisitionDocuments(
      'pr-2',
      documents,
      upload
    );

    expect(result.uploaded).toBe(1);
    expect(result.failed).toEqual([documents[1]]);
    expect(result.errorMessages).toEqual(['The file failed the controlled upload.']);
  });

  it('shows server ProblemDetails detail and code', () => {
    expect(getPurchaseRequisitionDocumentErrorMessage({
      message: 'Request failed',
      response: {
        detail: 'Supporting documents are locked after submission.',
        extensions: { code: 'PR_DOCUMENTS_DRAFT_ONLY' },
      },
    }, 'Upload failed.')).toBe(
      'Supporting documents are locked after submission. (PR_DOCUMENTS_DRAFT_ONLY)'
    );
  });
});
