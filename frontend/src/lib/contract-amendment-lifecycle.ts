export function canRequestContractAmendment(
  contractStatus: string | undefined,
  hasManagePermission: boolean
): boolean {
  return contractStatus === 'Active' && hasManagePermission;
}

export function canProcessContractAmendment(
  amendmentStatus: string | undefined,
  hasRequiredPermission: boolean
): boolean {
  return amendmentStatus === 'PendingApproval' && hasRequiredPermission;
}
