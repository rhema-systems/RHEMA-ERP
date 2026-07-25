import type {
  ProcurementComplianceDecisionRequest,
  ProcurementComplianceOutcome,
  ProcurementComplianceSimulatorForm,
} from '@/types/procurement-compliance';
import type { ProcurementCategoryClass, ProcurementMethodType } from '@/types/procurement-policy';

export const procurementComplianceCategoryOptions: Array<{ value: ProcurementCategoryClass; label: string }> = [
  { value: 'Goods', label: 'Goods' },
  { value: 'Works', label: 'Works' },
  { value: 'TechnicalServices', label: 'Technical services' },
  { value: 'ConsultancyServices', label: 'Consultancy services' },
  { value: 'GeneralServices', label: 'General services' },
];

export const procurementComplianceMethodOptions: Array<{ value: ProcurementMethodType; label: string }> = [
  { value: 'RequestForQuotation', label: 'Request for quotation' },
  { value: 'NationalCompetitiveTendering', label: 'National competitive tendering' },
  { value: 'InternationalCompetitiveTendering', label: 'International competitive tendering' },
  { value: 'RestrictedTendering', label: 'Restricted tendering' },
  { value: 'SingleSource', label: 'Single source' },
  { value: 'PettyPurchase', label: 'Petty purchase' },
  { value: 'FrameworkCallOff', label: 'Framework call-off' },
  { value: 'QualityBasedSelection', label: 'Quality-based selection (QBS)' },
  { value: 'QualityAndCostBasedSelection', label: 'Quality and cost-based selection (QCBS)' },
];

export const splitComplianceValues = (value: string): string[] =>
  Array.from(new Set(value.split(/[\n,]/).map(item => item.trim()).filter(Boolean)));

export const createProcurementComplianceRequest = (
  form: ProcurementComplianceSimulatorForm,
): ProcurementComplianceDecisionRequest => ({
  policySetId: form.policySetId || undefined,
  category: form.category,
  serviceClass: form.serviceClass.trim() || undefined,
  amount: Number(form.amount),
  currencyCode: form.currencyCode.trim().toUpperCase(),
  requestedMethod: form.requestedMethod === 'Auto' ? undefined : form.requestedMethod,
  sourceType: form.sourceType.trim(),
  sourceReference: form.sourceReference.trim(),
  atUtc: form.atDate ? new Date(`${form.atDate}T12:00:00.000Z`).toISOString() : undefined,
  actorUserId: form.actorUserId.trim() || undefined,
  actorRoles: splitComplianceValues(form.actorRoles),
  sourceOwnerUserId: form.sourceOwnerUserId.trim() || undefined,
  sourceOwnerRoles: splitComplianceValues(form.sourceOwnerRoles),
  entityType: form.entityType.trim(),
  action: form.action.trim(),
  exceptionType: form.exceptionType.trim() || undefined,
  justificationProvided: form.justificationProvided,
  exceptionApprovalReference: form.exceptionApprovalReference.trim() || undefined,
  evidenceReferenceKeys: splitComplianceValues(form.evidenceReferenceKeys),
});

export const complianceOutcomeTone: Record<ProcurementComplianceOutcome, string> = {
  Allowed: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300',
  ReviewRequired: 'border-amber-500/40 bg-amber-500/10 text-amber-700 dark:text-amber-300',
  Blocked: 'border-destructive/40 bg-destructive/10 text-destructive',
};
