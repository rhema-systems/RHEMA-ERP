import { beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from './purchasingService';

describe('purchase receipt governed source-evidence client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'tenant-token');
  });

  it('uses the receipt-scoped tenant-safe overview and download routes', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ evidence: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }))
      .mockResolvedValueOnce(new Response(new Blob(['waybill']), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.getReceiptSourceEvidence('receipt-1');
    await purchasingService.downloadReceiptSourceEvidence('receipt-1', 'evidence-1');

    expect(fetchMock.mock.calls.map(([path]) => path)).toEqual([
      '/api/procurement/purchase-order-receipts/receipt-1/source-evidence',
      '/api/procurement/purchase-order-receipts/receipt-1/source-evidence/evidence-1/download',
    ]);
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe('Bearer tenant-token');
  });

  it('posts typed evidence as multipart without overriding the browser boundary', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'evidence-1', evidenceKind: 'Waybill' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);
    const file = new File(['waybill'], 'waybill.pdf', { type: 'application/pdf' });

    await purchasingService.uploadReceiptSourceEvidence('receipt-1', {
      evidenceKind: 1,
      referenceNumber: 'WB-1001',
      documentDate: '2026-08-12',
      clientRequestId: 'request-1',
      file,
    });

    const [, init] = fetchMock.mock.calls[0];
    expect(init.method).toBe('POST');
    expect(init.headers).toEqual({ Authorization: 'Bearer tenant-token' });
    expect(init.body).toBeInstanceOf(FormData);
    expect(init.body.get('evidenceKind')).toBe('1');
    expect(init.body.get('referenceNumber')).toBe('WB-1001');
    const uploadedFile = init.body.get('file') as File;
    expect(uploadedFile.name).toBe('waybill.pdf');
    expect(uploadedFile.type).toBe('application/pdf');
    expect(uploadedFile.size).toBe(file.size);
  });

  it.each([[1, 1], ['1', 1], ['Waybill', 1], [2, 2], ['2', 2], ['VatInvoiceCopy', 2]])(
    'normalizes API evidence kind %s without hiding retained files', async (wireKind, expected) => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
        waybillReady: true, canUpload: false,
        evidence: [{ id: 'retained-file', evidenceKind: wireKind, originalFileName: 'LOCAL-UAT-ONLY.pdf' }],
      }), { status: 200 })));

      const result = await purchasingService.getReceiptSourceEvidence('receipt-1');

      expect(result).toMatchObject({ waybillReady: true, canUpload: false,
        evidence: [{ id: 'retained-file', evidenceKind: expected, originalFileName: 'LOCAL-UAT-ONLY.pdf' }] });
    }
  );

  it('rejects unknown kinds instead of presenting missing evidence as ready', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      evidence: [{ evidenceKind: 'UnknownKind' }],
    }), { status: 200 })));
    await expect(purchasingService.getReceiptSourceEvidence('receipt-1')).rejects.toThrow('Unrecognized receipt evidence type');
  });
});
