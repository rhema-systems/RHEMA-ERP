import type { User } from '@/types';
import type { ProcurementMethodType } from '@/types/procurement-policy';

// Petty Purchase uses a tender-backed source record, but never the ordinary
// public-tender publication route. Its dedicated exceptional controls remain
// responsible for supplier selection, evidence and independent approval.
const tenderBackedMethods = new Set<ProcurementMethodType>([
  'NationalCompetitiveTendering',
  'InternationalCompetitiveTendering',
  'RestrictedTendering',
  'SingleSource',
  'PettyPurchase',
  'QualityBasedSelection',
  'QualityAndCostBasedSelection',
]);

export function getRequisitionSourcingEntryAction(context: {
  approved: boolean;
  canEnterSourcing: boolean;
  isMethodCompliant?: boolean;
  method?: ProcurementMethodType;
  hasExistingSource: boolean;
  isSourceStale: boolean;
}) {
  return {
    label: context.method === 'PettyPurchase'
      ? 'Prepare Petty Purchase'
      : 'Create Tender',
    enabled: context.approved && context.canEnterSourcing &&
      context.isMethodCompliant === true &&
      Boolean(context.method && tenderBackedMethods.has(context.method)) &&
      !context.hasExistingSource && !context.isSourceStale,
  };
}

export const PURCHASE_REQUISITION_APPROVE_PERMISSION =
  'procurement.requisition.approve';

const SUPER_ADMIN_ROLE = 'superadmin';

const normalize = (value: string | null | undefined) =>
  value?.trim().toLowerCase() ?? '';

const hasNormalizedValue = (
  values: string[] | null | undefined,
  expected: string
) => values?.some((value) => normalize(value) === expected) ?? false;

/**
 * PR approval is separation-of-duties sensitive. Unlike the general navigation
 * permission helper, TenantAdmin is not an approval bypass: the server only
 * permits an explicit permission or protected SuperAdmin.
 */
export function hasPurchaseRequisitionApprovalCapability(
  user: User | null | undefined
) {
  return (
    hasNormalizedValue(user?.roles, SUPER_ADMIN_ROLE) ||
    hasNormalizedValue(
      user?.permissions,
      PURCHASE_REQUISITION_APPROVE_PERMISSION
    )
  );
}

export function canUsePurchaseRequisitionApprovalActions(
  status: string | null | undefined,
  user: User | null | undefined
) {
  const normalizedStatus = normalize(status).replace(/\s+/g, '');
  const isPendingApproval =
    normalizedStatus === 'pendingapproval' || normalizedStatus === 'submitted';

  return isPendingApproval && hasPurchaseRequisitionApprovalCapability(user);
}

export function canRenderPurchaseRequisitionApprovalActions(
  status: string | null | undefined,
  user: User | null | undefined,
  workflowCanCurrentUserApprove: boolean | null | undefined
) {
  return (
    workflowCanCurrentUserApprove === true &&
    canUsePurchaseRequisitionApprovalActions(status, user)
  );
}
