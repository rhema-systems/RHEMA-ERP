import { beforeEach, describe, expect, it, vi } from 'vitest';
import { purchasingService } from './purchasingService';

describe('purchase-order SOD client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('loads the dedicated tenant-safe SOD readiness endpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          purchaseOrderId: 'po-0405',
          orderNumber: 'PO-0405',
          status: 'Pending Approval',
          currentActorUserId: 'approver',
          canApprove: true,
          canReceive: true,
          code: 'PO_SOD_READY',
          message: 'Independent.',
          evaluatedAtUtc: '2026-07-30T00:00:00Z',
          decisionKeys: ['DEC-001', 'DEC-014'],
          receiptActionCoverage: [
            'CreatePurchaseOrderReceipt',
            'ApproveReceiptInspection',
            'PostGoodsReceiptNoteToInventory',
          ],
          checks: [],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    const result =
      await purchasingService.getPurchaseOrderSodReadiness('po-0405');

    expect(result.canApprove).toBe(true);
    expect(result.receiptActionCoverage).toEqual([
      'CreatePurchaseOrderReceipt',
      'ApproveReceiptInspection',
      'PostGoodsReceiptNoteToInventory',
    ]);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/PurchaseOrders/po-0405/sod-readiness',
      expect.objectContaining({ headers: expect.any(Object) })
    );
  });

  it('surfaces the structured role-conflict blocker', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            code: 'PO_SOD_APPROVAL_BLOCKED',
            message: 'The PO creator cannot approve this purchase order.',
          }),
          { status: 403, headers: { 'Content-Type': 'application/json' } }
        )
      )
    );

    await expect(
      purchasingService.approvePurchaseOrder('po-0405', { approved: true })
    ).rejects.toThrow('The PO creator cannot approve this purchase order.');
  });

  it('shows PO role-separation read guidance when readiness is forbidden', async () => {
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
      purchasingService.getPurchaseOrderSodReadiness('po-0405')
    ).rejects.toThrow('procurement.records.read');
  });

  it('shows the exact PO approval permission when approval is forbidden', async () => {
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
      purchasingService.approvePurchaseOrder('po-0405', { approved: true })
    ).rejects.toThrow('procurement.purchase-order.approve');
  });
});
