import type {
  ProcurementAccessCapabilityRequest, ProcurementAccessPermission, ProcurementAccessRole,
  ProcurementCommittee, ProcurementCommitteeMemberKind, ProcurementResponsibilityAssignment,
  SaveProcurementResponsibilityAssignment,
} from '@/types/procurement-access-control';

export const roleRequiresWarehouseScope = (
  role: ProcurementAccessRole | undefined,
  permissions: ProcurementAccessPermission[],
) => Boolean(role?.permissionCodes.some(code => permissions.some(permission =>
  permission.code === code && permission.isWarehouseScoped)));

export const validateResponsibilityAssignment = (
  request: SaveProcurementResponsibilityAssignment,
  warehouseScoped: boolean,
) => {
  if (!request.userId || !request.roleName) return 'User and responsibility are required.';
  if (!request.reason.trim()) return 'An audit reason is required.';
  if (request.effectiveTo && request.effectiveTo < request.effectiveFrom) return 'The end date cannot precede the start date.';
  if (warehouseScoped && request.warehouseScopeMode === 'None') return 'This responsibility requires All or Restricted warehouse access.';
  if (!warehouseScoped && request.warehouseScopeMode !== 'None') return 'Warehouse scope is not applicable to this responsibility.';
  if (request.warehouseScopeMode === 'Restricted' && request.warehouseIds.length === 0) return 'Select at least one warehouse.';
  if (warehouseScoped && request.locationScopeMode === 'None') return 'This responsibility requires All or Restricted location access.';
  if (!warehouseScoped && request.locationScopeMode !== 'None') return 'Location scope is not applicable to this responsibility.';
  if (request.locationScopeMode === 'Restricted' && request.locationIds.length === 0) return 'Select at least one warehouse location.';
  return undefined;
};

export const canActivateCommittee = (committee: Pick<ProcurementCommittee, 'activeVotingMemberCount' | 'requiredQuorum'>) =>
  committee.activeVotingMemberCount >= committee.requiredQuorum;

export const committeeAssignmentEligibilityIssue = (
  assignment: ProcurementResponsibilityAssignment,
  requiredRoleName: string,
  memberKind: ProcurementCommitteeMemberKind,
  effectiveFrom: string,
  effectiveTo?: string,
) => {
  if (!assignment.isActive) return 'This responsibility assignment is inactive.';

  const isObserverResponsibility = assignment.roleName === 'TDC_OBSERVER' ||
    assignment.roleName === 'TDC_INTERNAL_AUDIT';
  if (assignment.roleName !== requiredRoleName &&
      !(memberKind === 'Observer' && isObserverResponsibility)) {
    return memberKind === 'Observer'
      ? `Observers require ${requiredRoleName}, TDC_OBSERVER, or TDC_INTERNAL_AUDIT.`
      : `This committee role requires ${requiredRoleName}.`;
  }

  const assignmentFrom = assignment.effectiveFrom.slice(0, 10);
  const assignmentTo = assignment.effectiveTo?.slice(0, 10);
  if (assignmentFrom > effectiveFrom) {
    return `The responsibility starts on ${assignmentFrom}; the membership cannot start earlier.`;
  }
  if (assignmentTo && assignmentTo < effectiveFrom) {
    return `The responsibility ended on ${assignmentTo}.`;
  }
  if (assignmentTo && !effectiveTo) {
    return `Set the membership end date no later than ${assignmentTo}.`;
  }
  if (assignmentTo && effectiveTo && assignmentTo < effectiveTo) {
    return `The membership cannot continue after the responsibility ends on ${assignmentTo}.`;
  }

  return undefined;
};

export const canCheckAccessCapability = (request: ProcurementAccessCapabilityRequest, warehouseRequired: boolean) =>
  Boolean(request.permissionCode && request.sourceType.trim() && request.sourceReference.trim() &&
    (!warehouseRequired || request.warehouseId));
