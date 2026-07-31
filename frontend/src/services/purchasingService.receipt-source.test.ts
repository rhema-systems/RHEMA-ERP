import { beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from './purchasingService';

describe('purchase-order receipt source client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('loads authoritative source and remaining-quantity readiness', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          purchaseOrderId: 'po-0501',
          orderNumber: 'PO-0501',
          purchaseOrderStatus: 'Approved',
          sourceReference: 'CON-0501',
          sourceIntegrityHash: 'A'.repeat(64),
          tolerancePercent: 5,
          sourceValid: true,
          canReceive: true,
          code: 'RCV_SOURCE_READY',
          message: 'Ready.',
          evaluatedAtUtc: '2026-07-31T00:00:00Z',
          decisionKeys: ['DEC-001', 'DEC-014'],
          lines: [],
          requiredActions: [],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    const result =
      await purchasingService.getReceiptSourceReadiness('po-0501');

    expect(result.canReceive).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/PurchaseOrders/po-0501/receipt-source-readiness',
      expect.objectContaining({ headers: expect.any(Object) })
    );
  });

  it('sends the stable receipt idempotency key in header and body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'receipt-0501' }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await purchasingService.receivePurchaseOrder('po-0501', {
      purchaseOrderId: 'po-0501',
      receivedById: 'user-1',
      requiresInspection: false,
      idempotencyKey: 'receipt-attempt-1',
      items: [
        {
          purchaseOrderItemId: 'po-line-1',
          receivedQuantity: 5,
          acceptedQuantity: 5,
          rejectedQuantity: 0,
          locationId: 'location-1',
        },
      ],
    });

    const request = fetchMock.mock.calls[0][1] as RequestInit;
    expect(request.headers).toMatchObject({
      'Idempotency-Key': 'receipt-attempt-1',
    });
    expect(JSON.parse(String(request.body))).toMatchObject({
      idempotencyKey: 'receipt-attempt-1',
    });
  });

  it('surfaces structured capacity blockers', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            code: 'RCV_REMAINING_QUANTITY_EXCEEDED',
            message: 'Received quantity exceeds governed remaining quantity.',
          }),
          { status: 409, headers: { 'Content-Type': 'application/json' } }
        )
      )
    );

    await expect(
      purchasingService.receivePurchaseOrder('po-0501', {
        purchaseOrderId: 'po-0501',
        receivedById: 'user-1',
        requiresInspection: false,
        items: [],
      })
    ).rejects.toThrow(
      'Received quantity exceeds governed remaining quantity.'
    );
  });
});
