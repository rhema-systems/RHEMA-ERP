import { beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from './purchasingService';

describe('purchase-order approved source client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('loads tenant-safe candidates through the requisition-filtered source endpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          ready: false,
          candidateCount: 0,
          blockedReasons: ['No approved source.'],
          sources: [],
          frameworkCallOffRoute: '/procurement/framework-call-offs'
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.getPurchaseOrderSourceOptions('pr-0403');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/PurchaseOrders/source-options?purchaseRequisitionId=pr-0403',
      expect.objectContaining({ headers: expect.any(Object) })
    );
  });

  it('sends the exact approved source type and id when creating a PO', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'po-1' }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' }
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.createPurchaseOrder({
      sourceType: 'Contract',
      sourceId: 'contract-1',
      supplierId: 'supplier-1',
      requestedById: 'user-1',
      items: [
        {
          inventoryItemId: 'item-1',
          orderedQuantity: 1,
          unitOfMeasure: 'EA',
          unitPrice: 10
        }
      ]
    });

    const request = fetchMock.mock.calls[0][1] as RequestInit;
    expect(JSON.parse(String(request.body))).toMatchObject({
      sourceType: 'Contract',
      sourceId: 'contract-1',
      supplierId: 'supplier-1'
    });
  });
});
