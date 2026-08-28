import {
  ProcurementExceptionalSourcingControlStatus as Status,
  type PrepareExceptionalSourcingRequest,
  type ProcurementExceptionalSourcingControl,
  type ProcurementExceptionalSourcingReadiness,
} from '@/types/procurement-exceptional-sourcing-control';

export const exceptionalSourcingStatusLabel: Record<Status, string> = {
  [Status.Prepared]: 'Prepared',
  [Status.PendingApproval]: 'Authority approval pending',
  [Status.Approved]: 'Approved and released',
  [Status.Negotiated]: 'Negotiation recorded',
  [Status.Recommended]: 'Recommendation recorded',
  [Status.Awarded]: 'Award recorded',
  [Status.Contracted]: 'Contract recorded',
  [Status.Accepted]: 'Supplier acceptance recorded',
  [Status.Filed]: 'Post-award filing complete',
  [Status.Rejected]: 'Rejected',
};

export const exceptionalMethodLabel = (method: number) =>
  method === 5
    ? 'Petty Purchase'
    : method === 4
      ? 'Single Source'
      : method === 3
        ? 'Restricted Tendering'
        : 'Controlled sourcing';

export function getExceptionalSourcingActions(
  control: ProcurementExceptionalSourcingControl
) {
  return {
    canSubmitApproval: control.status === Status.Prepared,
    canDecideApproval: control.status === Status.PendingApproval,
    canNegotiate: control.status === Status.Approved,
    canRecommend: control.status === Status.Negotiated,
    canAward: control.status === Status.Recommended,
    canContract: control.status === Status.Awarded,
    canAccept: control.status === Status.Contracted,
    canFile: control.status === Status.Accepted && control.method !== 5,
    immutable:
      control.status === Status.Filed ||
      control.status === Status.Rejected ||
      (control.status === Status.Accepted && control.method === 5),
  };
}

export function validateExceptionalPreparation(
  readiness: ProcurementExceptionalSourcingReadiness,
  request: PrepareExceptionalSourcingRequest
) {
  if (
    readiness.justificationRequired &&
    request.justification.trim().length < 20
  )
    return 'Enter the configured sourcing justification of at least 20 characters.';
  if (
    readiness.justificationRequired &&
    !request.justificationEvidenceReference.trim()
  )
    return 'Justification evidence is required.';
  if (!request.supplierSelectionEvidenceReference.trim())
    return 'Supplier-selection evidence is required.';
  if (request.businessPartnerIds.length < readiness.minimumSupplierCount)
    return `Select at least ${readiness.minimumSupplierCount} eligible supplier${readiness.minimumSupplierCount === 1 ? '' : 's'}.`;
  for (const requirement of readiness.evidenceRequirements) {
    const supplied = request.evidenceChecklist.find(
      (item) => item.requirementKey === requirement.requirementKey
    );
    if (!supplied?.evidenceReference.trim())
      return `Evidence is required for ${requirement.evidenceName}.`;
    if (
      requirement.requiresVerification &&
      !supplied.verificationReference.trim()
    )
      return `Verified shared-evidence reference is required for ${requirement.evidenceName}.`;
  }
  return null;
}
