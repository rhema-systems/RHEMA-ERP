import type {
  ProcurementAccessCapabilityRequest, ProcurementAccessPermission, ProcurementAccessRole,
  ProcurementCommittee, SaveProcurementResponsibilityAssignment,
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

export const canCheckAccessCapability = (request: ProcurementAccessCapabilityRequest, warehouseRequired: boolean) =>
  Boolean(request.permissionCode && request.sourceType.trim() && request.sourceReference.trim() &&
    (!warehouseRequired || request.warehouseId));
