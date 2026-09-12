// Presentation checks only. The API still enforces permission, scope and independence.
export function canIssueRequisition(
  requisition: { status: number | string; requestedById?: string; approvedById?: string } | null,
  userId: string | undefined,
  hasIssuePermission: boolean
): boolean {
  if (!requisition || !userId || !hasIssuePermission) return false;
  const actor = userId.toLowerCase();
  return [3, 4, 5, 'Approved', 'InProgress', 'PartiallyIssued'].includes(requisition.status)
    && actor !== requisition.requestedById?.toLowerCase()
    && actor !== requisition.approvedById?.toLowerCase();
}
