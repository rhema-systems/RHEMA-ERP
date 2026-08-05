import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import { PurchaseOrderSodControl } from './PurchaseOrderSodControl';

describe('PurchaseOrderSodControl receipt scope', () => {
  it('shows only the receipt decision and every enforced receipt action', () => {
    const markup = renderToStaticMarkup(
      <PurchaseOrderSodControl
        purchaseOrderId="po-0503"
        status="Approved"
        scope="receipt"
        initialReadiness={{
          purchaseOrderId: 'po-0503',
          orderNumber: 'PO-0503',
          status: 'Approved',
          currentActorUserId: 'receiver-0503',
          canApprove: false,
          canReceive: true,
          code: 'PO_SOD_READY',
          message: 'The receiver is independent.',
          evaluatedAtUtc: '2026-07-31T00:00:00Z',
          decisionKeys: Array.from(
            { length: 14 },
            (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`
          ),
          receiptActionCoverage: [
            'CreatePurchaseOrderReceipt',
            'ApproveReceiptInspection',
            'ConfirmReplacementReceipt',
            'PostGoodsReceiptNoteToInventory',
          ],
          checks: [
            {
              key: 'approval',
              label: 'Independent PO approver',
              action: 'Approve',
              controlCode: 'SOD-INITIATOR-APPROVER',
              allowed: false,
              code: 'SOD_CONFLICT',
              message: 'Approval is restricted.',
              participantRoles: ['PO creator'],
              prohibitedActorUserIds: ['creator-0503'],
            },
            {
              key: 'receipt',
              label: 'Independent goods receiver',
              action: 'Receive',
              controlCode: 'SOD-PO-CREATOR-RECEIVER',
              allowed: true,
              code: 'SOD_ALLOWED',
              message: 'Receipt actions are allowed.',
              participantRoles: ['PO creator'],
              prohibitedActorUserIds: ['creator-0503'],
            },
          ],
        }}
      />
    );

    expect(markup).toContain('Receipt segregation of duties');
    expect(markup).toContain('Independent goods receiver');
    expect(markup).not.toContain('Independent PO approver');
    expect(markup).toContain('ApproveReceiptInspection');
    expect(markup).toContain('ConfirmReplacementReceipt');
    expect(markup).toContain('PostGoodsReceiptNoteToInventory');
    expect(markup).toContain('DEC-001');
    expect(markup).toContain('DEC-014');
  });
});
