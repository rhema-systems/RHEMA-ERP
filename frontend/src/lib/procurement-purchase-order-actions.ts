export type ProcurementPermissionCheck = (permission: string) => boolean;

export function getPurchaseOrderActionAccess(
  status: string,
  hasPermission: ProcurementPermissionCheck
) {
  const canManage = hasPermission('procurement.purchase-order.create');
  const canApprove = hasPermission('procurement.purchase-order.approve');
  const awaitingApproval = status === 'Pending Approval' || status === 'Submitted';

  return {
    canEdit: canManage && status === 'Draft',
    canSubmit: canManage && status === 'Draft',
    canApproveReject: canApprove && awaitingApproval,
  };
}
