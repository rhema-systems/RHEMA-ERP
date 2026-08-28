import { beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from './purchasingService';

describe('purchase-order compliance client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('loads the dedicated tenant-safe compliance readiness endpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          purchaseOrderId: 'po-0404',
          orderNumber: 'PO-0404',
          status: 'Draft',
          action: 'Preview',
          isCompliant: false,
          code: 'PO_COMPLIANCE_BLOCKED',
          message: 'Blocked.',
          evaluatedAtUtc: '2026-07-29T00:00:00Z',
          decisionKeys: ['DEC-001', 'DEC-014'],
          checks: [],
          blockedReasons: ['Budget commitment missing.'],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await purchasingService.getPurchaseOrderComplianceReadiness(
      'po-0404',
      'Approve'
    );

    expect(result.isCompliant).toBe(false);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/PurchaseOrders/po-0404/compliance-readiness?action=Approve',
      expect.objectContaining({ headers: expect.any(Object) })
    );
  });

  it('surfaces the structured server blocker when submission fails closed', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            code: 'PO_COMPLIANCE_BLOCKED',
            message: 'Purchase order is blocked by 2 compliance checks.',
          }),
          { status: 422, headers: { 'Content-Type': 'application/json' } }
        )
      )
    );

    await expect(
      purchasingService.submitPurchaseOrder('po-0404')
    ).rejects.toThrow('Purchase order is blocked by 2 compliance checks.');
  });

  it('shows PO read guidance instead of requisition guidance when readiness is forbidden', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            detail:
              'The current actor has no Security role granting this procurement privilege.',
          }),
          { status: 403, headers: { 'Content-Type': 'application/json' } }
        )
      )
    );

    await expect(
      purchasingService.getPurchaseOrderComplianceReadiness('po-0404')
    ).rejects.toThrow('procurement.records.read');
  });

  it('shows the exact PO submit permission when submission is forbidden', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            detail:
              'The current actor has no Security role granting this procurement privilege.',
          }),
          { status: 403, headers: { 'Content-Type': 'application/json' } }
        )
      )
    );

    await expect(
      purchasingService.submitPurchaseOrder('po-0404')
    ).rejects.toThrow('procurement.purchase-order.create');
  });
});
