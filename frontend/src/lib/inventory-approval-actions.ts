type ApprovalRecord = { status: string; approvalRequired?: boolean; requestedById?: string };

/** Visibility only: the API still owns tenant, location, stock and Finance authorization. */
export function canPostInventoryRecord(record: ApprovalRecord, actorId: string | undefined, postingAllowed: boolean) {
  if (!postingAllowed || !actorId || !record.requestedById) return false;
  if (record.approvalRequired === false) return record.status === 'ReadyToPost';
  return record.status === 'Approved' && record.requestedById.toLowerCase() !== actorId.toLowerCase();
}

export function canDecideInventoryRecord(record: ApprovalRecord, actorId: string | undefined, approvalAllowed: boolean) {
  return approvalAllowed && !!actorId && !!record.requestedById && record.approvalRequired !== false &&
    record.status === 'PendingApproval' && record.requestedById.toLowerCase() !== actorId.toLowerCase();
}
