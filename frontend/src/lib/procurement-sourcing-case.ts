import type { ProcurementMethodType } from '@/types/procurement-policy';
import type {
  CreateProcurementSourcingCase,
  ProcurementSourcingCase,
  ProcurementSourcingCaseLineOption,
} from '@/types/procurement-sourcing-case';

export const procurementSourcingCaseMethodLabels: Record<
  ProcurementMethodType,
  string
> = {
  RequestForQuotation: 'Request for quotation',
  NationalCompetitiveTendering: 'National competitive tendering',
  InternationalCompetitiveTendering: 'International competitive tendering',
  RestrictedTendering: 'Restricted tendering',
  SingleSource: 'Single source',
  PettyPurchase: 'Petty purchase',
  FrameworkCallOff: 'Framework call-off',
  QualityBasedSelection: 'Quality-based selection',
  QualityAndCostBasedSelection: 'Quality and cost-based selection',
};

export const procurementSourcingCaseActions = (
  sourcingCase: Pick<ProcurementSourcingCase, 'status' | 'isSourceCurrent'>,
  canManage: boolean
) => ({
  canClose:
    canManage &&
    sourcingCase.isSourceCurrent &&
    sourcingCase.status === 'InProgress',
  canCancel:
    canManage &&
    (sourcingCase.status === 'Ready' || sourcingCase.status === 'InProgress'),
});

export const validateProcurementSourcingCase = (
  request: CreateProcurementSourcingCase,
  lines: ProcurementSourcingCaseLineOption[],
  recommendedMethod?: ProcurementMethodType
) => {
  if (!request.requisitionId)
    return 'A released purchase requisition is required.';
  if (
    request.selectedMethod &&
    recommendedMethod &&
    request.selectedMethod !== recommendedMethod &&
    (request.methodOverrideReason?.trim().length ?? 0) < 5
  )
    return 'An approved method override requires a reason of at least 5 characters.';
  if (request.justification.trim().length < 5)
    return 'A sourcing justification of at least 5 characters is required.';
  if (!request.lots.length) return 'At least one sourcing lot is required.';

  const sourceIds = new Set(lines.map((line) => line.id));
  const assigned = new Set<string>();
  for (const [index, lot] of request.lots.entries()) {
    if (!lot.title.trim()) return `Lot ${index + 1} requires a title.`;
    if (!lot.purchaseRequisitionItemIds.length)
      return `Lot ${index + 1} must contain at least one requisition line.`;
    for (const itemId of lot.purchaseRequisitionItemIds) {
      if (!sourceIds.has(itemId))
        return `Lot ${index + 1} contains a line outside the selected requisition.`;
      if (assigned.has(itemId))
        return 'Each requisition line can belong to only one lot.';
      assigned.add(itemId);
    }
  }
  if (assigned.size !== sourceIds.size)
    return 'Every requisition line must be assigned to exactly one lot.';
  return undefined;
};
