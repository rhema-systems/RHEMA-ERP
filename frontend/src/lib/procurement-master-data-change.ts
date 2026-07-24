import type {
  ProcurementMasterDataChange,
  ProcurementMasterDataChangeStatus,
  ProcurementMasterDataPolicy,
  SaveProcurementMasterDataChange,
  SaveProcurementMasterDataPolicy,
} from '@/types/procurement-master-data-change';

export const parseRoleList = (value: string) =>
  [
    ...new Set(
      value
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean)
    ),
  ].sort();

export const validatePolicyDraft = (value: SaveProcurementMasterDataPolicy) => {
  if (!value.name.trim()) return 'Policy name is required.';
  if (!value.makerRoles.length) return 'At least one maker role is required.';
  if (!value.checkerRoles.length)
    return 'At least one checker role is required.';
  if (value.makerRoles.some((role) => value.checkerRoles.includes(role)))
    return 'Maker and checker role sets must be disjoint.';
  if (!value.effectiveFromUtc) return 'Effective-from date is required.';
  if (value.effectiveToUtc && value.effectiveToUtc < value.effectiveFromUtc)
    return 'Effective-to cannot precede effective-from.';
  return undefined;
};

export const validateChangeDraft = (
  value: SaveProcurementMasterDataChange,
  singleton = false
) => {
  if (!singleton && !value.targetId)
    return 'A target ID is required for this resource.';
  if (!value.reason.trim()) return 'A business reason is required.';
  if (!value.effectiveAtUtc) return 'An effective date is required.';
  try {
    const patch = JSON.parse(value.proposedChangesJson) as unknown;
    if (!patch || Array.isArray(patch) || typeof patch !== 'object')
      return 'Proposed changes must be a JSON object.';
    if (!Object.keys(patch).length)
      return 'At least one field change is required.';
  } catch {
    return 'Proposed changes must contain valid JSON.';
  }
  return undefined;
};

export const requestActions = (
  request: ProcurementMasterDataChange,
  now = new Date()
) => ({
  canEdit: request.status === 'Draft',
  canSubmit: request.status === 'Draft',
  canDecide: request.status === 'PendingApproval',
  canRevalidate:
    request.status === 'PendingApproval' || request.status === 'Approved',
  canApply:
    request.status === 'Approved' && new Date(request.effectiveAtUtc) <= now,
  canCancel: ['Draft', 'PendingApproval', 'RevalidationFailed'].includes(
    request.status
  ),
});

export const policyActions = (policy: ProcurementMasterDataPolicy) => ({
  canEdit: policy.status === 'Draft',
  canActivate: policy.status === 'Draft',
  canRetire: policy.status === 'Active',
});

export const statusTone = (status: ProcurementMasterDataChangeStatus) => {
  if (status === 'Applied') return 'text-emerald-700 border-emerald-300';
  if (status === 'Rejected' || status === 'RevalidationFailed')
    return 'text-red-700 border-red-300';
  if (status === 'Approved') return 'text-blue-700 border-blue-300';
  if (status === 'PendingApproval') return 'text-amber-700 border-amber-300';
  return '';
};
