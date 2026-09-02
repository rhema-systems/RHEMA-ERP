export function canDecideTenderAward(
  status: string | undefined,
  hasApprovePermission: boolean
): boolean {
  return status === 'PendingApproval' && hasApprovePermission;
}

export function isFinalTenderAward(status: string | undefined): boolean {
  return status === 'Awarded';
}
