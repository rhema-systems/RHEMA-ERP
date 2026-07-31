import { beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from './purchasingService';

describe('receipt inspection client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('loads only the authenticated supplier inspection queue', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('[]', {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.getSupplierReceiptInspectionControls();

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/PurchaseOrderReceipts/inspection-control/supplier',
      expect.objectContaining({ headers: expect.any(Object) })
    );
  });

  it('sends row version and controlled evidence on submission', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'case-0502' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.submitReceiptInspection('case-0502', {
      comment: 'Inspection completed.',
      rowVersion: 'AQID',
      evidence: [{
        actionKey: 'SubmitReceiptInspection',
        requirementKey: 'INSPECTION_REPORT',
        referenceKind: 1,
        fileUploadRecordId: 'upload-0502',
        evidenceReference: 'DMS-0502',
      }],
    });

    const request = fetchMock.mock.calls[0][1] as RequestInit;
    expect(JSON.parse(String(request.body))).toMatchObject({
      rowVersion: 'AQID',
      evidence: [{ fileUploadRecordId: 'upload-0502' }],
    });
  });

  it('surfaces structured workflow and concurrency conflicts', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      code: 'RCV_ROW_VERSION_CONFLICT',
      detail: 'The receipt inspection changed. Reload and retry.',
    }), { status: 409, headers: { 'Content-Type': 'application/json' } })));

    await expect(purchasingService.decideReceiptInspection('case-0502', {
      approved: true,
      comment: 'Approve.',
      rowVersion: 'stale',
    })).rejects.toThrow('The receipt inspection changed. Reload and retry.');
  });
});
