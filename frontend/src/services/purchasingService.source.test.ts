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

  it('preserves RFQ ProblemDetails detail and code for the user', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({
        title: 'Request could not be completed',
        status: 422,
        detail: 'The approved requisition is missing required specifications.',
        code: 'RFQ_SPECIFICATION_REQUIRED'
      }), { status: 422, headers: { 'Content-Type': 'application/problem+json' } })
    ));

    await expect(
      purchasingService.createRfqFromPurchaseRequisition('pr-1')
    ).rejects.toThrow(
      'The approved requisition is missing required specifications. (RFQ_SPECIFICATION_REQUIRED)'
    );
  });

  it('does not show requisition-specific guidance for a PO permission failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({
        status: 403,
        detail: 'The current actor has no Security role granting this procurement privilege.',
        extensions: { code: 'PO_COMPLIANCE_FORBIDDEN' }
      }), { status: 403, headers: { 'Content-Type': 'application/problem+json' } })
    ));

    await expect(
      purchasingService.createPurchaseOrder({
        sourceType: 'ApprovedException',
        sourceId: 'exception-1',
        supplierId: 'supplier-1',
        requestedById: 'user-1',
        items: []
      })
    ).rejects.toThrow(
      'Your assigned Security roles do not authorize this procurement action. Ask a Security administrator to assign the permission required for the action and try again. (PO_COMPLIANCE_FORBIDDEN)'
    );
  });

  it('shows field validation errors instead of only the generic ProblemDetails title', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      title: 'One or more validation errors occurred.',
      errors: { '$.sourceId': ['Invalid identifier.'], Items: ['At least one line is required.'] },
      code: 'VALIDATION_FAILED'
    }), { status: 400 })));
    await expect(purchasingService.createPurchaseOrder({
      sourceType: 'ApprovedException', sourceId: 'source-1', supplierId: 'supplier-1',
      requestedById: 'user-1', items: []
    })).rejects.toThrow('One or more validation errors occurred. $.sourceId: Invalid identifier. Items: At least one line is required. (VALIDATION_FAILED)');
  });
});
