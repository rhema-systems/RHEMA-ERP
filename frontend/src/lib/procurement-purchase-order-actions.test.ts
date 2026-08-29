import { describe, expect, it } from 'vitest';
import { getPurchaseOrderActionAccess } from './procurement-purchase-order-actions';

describe('purchase-order action visibility', () => {
  it('shows draft maintenance only to a PO creator', () => {
    const access = getPurchaseOrderActionAccess(
      'Draft',
      (permission) => permission === 'procurement.purchase-order.create'
    );

    expect(access).toEqual({
      canEdit: true,
      canSubmit: true,
      canApproveReject: false,
    });
  });

  it('shows approval only to an assigned PO approver permission holder', () => {
    const access = getPurchaseOrderActionAccess(
      'Submitted',
      (permission) => permission === 'procurement.purchase-order.approve'
    );

    expect(access).toEqual({
      canEdit: false,
      canSubmit: false,
      canApproveReject: true,
    });
  });

  it('does not expose a status action without its permission', () => {
    expect(getPurchaseOrderActionAccess('Draft', () => false)).toEqual({
      canEdit: false,
      canSubmit: false,
      canApproveReject: false,
    });
  });
});
