import { beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from './purchasingService';

describe('receipt document lifecycle client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('uses the dedicated receipt-scoped register and reconciliation routes', async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(
      new Response(JSON.stringify({ documents: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    ));
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.getReceiptDocumentControl('receipt-0509');
    await purchasingService.ensureReceiptDocuments('receipt-0509');
    await purchasingService.reconcileReceiptDocuments('receipt-0509');

    expect(fetchMock.mock.calls.map(call => [call[0], (call[1] as RequestInit | undefined)?.method ?? 'GET']))
      .toEqual([
        ['/api/ProcurementReceiptDocuments/receipt/receipt-0509', 'GET'],
        ['/api/ProcurementReceiptDocuments/receipt/receipt-0509/ensure', 'POST'],
        ['/api/ProcurementReceiptDocuments/receipt/receipt-0509/reconcile', 'POST'],
      ]);
  });

  it('sends immutable row versions for sign issue and cancellation', async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(
      new Response(JSON.stringify({ id: 'document-0509' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    ));
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.signReceiptDocument('document-0509', {
      requiredRole: 'Stores', comment: 'Signed.', rowVersion: 'AQID',
    });
    await purchasingService.issueReceiptDocument('document-0509', {
      comment: 'Issue.', rowVersion: 'BAUG',
    });
    await purchasingService.cancelReceiptDocument('document-0509', {
      reason: 'Controlled cancellation.', rowVersion: 'BwgJ',
    });

    const bodies = fetchMock.mock.calls.map(call => JSON.parse(String((call[1] as RequestInit).body)));
    expect(bodies).toEqual([
      { requiredRole: 'Stores', comment: 'Signed.', rowVersion: 'AQID' },
      { comment: 'Issue.', rowVersion: 'BAUG' },
      { reason: 'Controlled cancellation.', rowVersion: 'BwgJ' },
    ]);
  });

  it('surfaces machine-readable issue hard stops from the API', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      code: 'RCV_DOCUMENT_EVIDENCE_INCOMPLETE',
      message: 'Required DEC-013 receipt evidence is missing: Delivery note.',
    }), { status: 422, headers: { 'Content-Type': 'application/json' } })));

    await expect(purchasingService.issueReceiptDocument('document-0509', {
      comment: 'Issue.', rowVersion: 'AQID',
    })).rejects.toThrow('Required DEC-013 receipt evidence is missing: Delivery note.');
  });

  it('downloads the issued central DMS PDF rendition', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(new Blob(['pdf'], { type: 'application/pdf' }), {
      status: 200,
      headers: { 'Content-Type': 'application/pdf' },
    }));
    vi.stubGlobal('fetch', fetchMock);

    const file = await purchasingService.downloadReceiptDocument('document-0509');

    expect(file.type).toBe('application/pdf');
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/ProcurementReceiptDocuments/document-0509/download',
      expect.objectContaining({ headers: expect.any(Object) }),
    );
  });
});
