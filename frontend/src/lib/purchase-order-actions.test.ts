import { describe, expect, it, vi } from 'vitest';
import {
  PURCHASE_ORDER_APPROVE_PERMISSION,
  PURCHASE_ORDER_CREATE_PERMISSION,
  resolvePurchaseOrderActionAccess,
} from './purchase-order-actions';

function permissions(...granted: string[]) {
  return vi.fn((permission: string) => granted.includes(permission));
}

describe('purchase-order action access', () => {
  it('allows an authorized creator to edit and submit a draft', () => {
    const result = resolvePurchaseOrderActionAccess(
      'Draft',
      permissions(PURCHASE_ORDER_CREATE_PERMISSION)
    );

    expect(result).toEqual({
      canEdit: true,
      canAmend: false,
      canSubmit: true,
      canApproveReject: false,
    });
  });

  it('hides draft submission actions without the create permission', () => {
    const result = resolvePurchaseOrderActionAccess(
      'Draft',
      permissions(PURCHASE_ORDER_APPROVE_PERMISSION)
    );

    expect(result.canEdit).toBe(false);
    expect(result.canSubmit).toBe(false);
  });

  it('offers a controlled amendment for an approved PO to an authorized creator', () => {
    const result = resolvePurchaseOrderActionAccess(
      'Approved',
      permissions(PURCHASE_ORDER_CREATE_PERMISSION)
    );

    expect(result).toEqual({
      canEdit: false,
      canAmend: true,
      canSubmit: false,
      canApproveReject: false,
    });
  });

  it('does not expose controlled amendments without the create permission', () => {
    const result = resolvePurchaseOrderActionAccess(
      'Approved',
      permissions(PURCHASE_ORDER_APPROVE_PERMISSION)
    );

    expect(result.canAmend).toBe(false);
  });

  it('allows approval only for an authorized approver at an approval status', () => {
    expect(
      resolvePurchaseOrderActionAccess(
        'Pending Approval',
        permissions(PURCHASE_ORDER_APPROVE_PERMISSION)
      ).canApproveReject
    ).toBe(true);
    expect(
      resolvePurchaseOrderActionAccess('Submitted', permissions())
        .canApproveReject
    ).toBe(false);
  });
});
