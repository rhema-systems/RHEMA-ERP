export const PURCHASE_ORDER_CREATE_PERMISSION =
  'procurement.purchase-order.create';
export const PURCHASE_ORDER_APPROVE_PERMISSION =
  'procurement.purchase-order.approve';

export interface PurchaseOrderActionAccess {
  canEdit: boolean;
  canAmend: boolean;
  canSubmit: boolean;
  canApproveReject: boolean;
}

export function resolvePurchaseOrderActionAccess(
  status: string | null | undefined,
  hasPermission: (permission: string) => boolean
): PurchaseOrderActionAccess {
  const normalizedStatus = status?.trim().toLowerCase() ?? '';
  const isDraft = normalizedStatus === 'draft';
  const isPendingApproval =
    normalizedStatus === 'pending approval' || normalizedStatus === 'submitted';
  const isAmendable =
    normalizedStatus === 'approved' ||
    normalizedStatus === 'sent' ||
    normalizedStatus === 'acknowledged';
  const canCreate = hasPermission(PURCHASE_ORDER_CREATE_PERMISSION);
  const canApprove = hasPermission(PURCHASE_ORDER_APPROVE_PERMISSION);

  return {
    canEdit: isDraft && canCreate,
    canAmend: isAmendable && canCreate,
    canSubmit: isDraft && canCreate,
    canApproveReject: isPendingApproval && canApprove,
  };
}
