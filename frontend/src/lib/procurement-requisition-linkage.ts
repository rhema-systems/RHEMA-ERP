import type {
  ProcurementCategoryClass,
  PurchaseRequisitionLinkageDto,
  PurchaseRequisitionLinkageOptionDto,
  PurchaseRequisitionLinkageOptionsDto,
  PurchaseRequisitionType,
  SavePurchaseRequisitionLinkageRequest,
} from '@/services/purchasingService';

const procurementCategories: ProcurementCategoryClass[] = [
  'Goods',
  'Works',
  'TechnicalServices',
  'ConsultancyServices',
  'GeneralServices',
];

export function applyPlanItemToRequisitionLinkage(
  linkage: SavePurchaseRequisitionLinkageRequest,
  option?: PurchaseRequisitionLinkageOptionDto
): SavePurchaseRequisitionLinkageRequest {
  const normalizedCategory = option?.category?.replace(/\s+/g, '').toLowerCase();
  const category = normalizedCategory
    ? procurementCategories.find((value) => value.toLowerCase() === normalizedCategory)
    : undefined;

  return {
    ...linkage,
    sourcePlanItemId: option?.id,
    sourcePlanItemIds: option?.id ? [option.id] : undefined,
    budgetId: option?.linkedBudgetId ?? option?.budgetId,
    procurementCategory: option ? category || linkage.procurementCategory : undefined,
    costCenter: undefined,
  };
}

export const EMPTY_REQUISITION_LINKAGE: SavePurchaseRequisitionLinkageRequest = {
  requisitionType: 'StockReplenishment',
};

export function toEditableRequisitionLinkage(
  linkage?: PurchaseRequisitionLinkageDto
): SavePurchaseRequisitionLinkageRequest {
  if (!linkage) return { ...EMPTY_REQUISITION_LINKAGE };
  return {
    sourcePlanItemId: linkage.sourcePlanItemId,
    sourcePlanItemIds: linkage.sourcePlanItemIds,
    budgetId: linkage.budgetId,
    procurementCategory: linkage.procurementCategory,
    costCenter: linkage.costCenter,
    projectId: linkage.projectId,
    requisitionType: linkage.requisitionType || 'StockReplenishment',
    specificationTemplateId: linkage.specificationTemplateId,
    approvedExceptionRuleId: linkage.approvedExceptionRuleId,
    exceptionWorkflowInstanceId: linkage.exceptionWorkflowInstanceId,
    exceptionApprovalReference: linkage.exceptionApprovalReference,
    exceptionEvidenceReference: linkage.exceptionEvidenceReference,
  };
}

export function normalizeRequisitionLinkage(
  linkage: SavePurchaseRequisitionLinkageRequest
): SavePurchaseRequisitionLinkageRequest {
  const clean = (value?: string) => value?.trim() || undefined;
  const hasException = Boolean(linkage.approvedExceptionRuleId);
  return {
    sourcePlanItemId: clean(linkage.sourcePlanItemId),
    sourcePlanItemIds: linkage.sourcePlanItemIds
      ?.map((value) => clean(value))
      .filter((value): value is string => Boolean(value)),
    budgetId: clean(linkage.budgetId),
    procurementCategory: linkage.procurementCategory,
    costCenter: clean(linkage.costCenter),
    projectId: clean(linkage.projectId),
    requisitionType: (linkage.requisitionType || 'StockReplenishment') as PurchaseRequisitionType,
    specificationTemplateId: clean(linkage.specificationTemplateId),
    approvedExceptionRuleId: clean(linkage.approvedExceptionRuleId),
    exceptionWorkflowInstanceId: hasException ? clean(linkage.exceptionWorkflowInstanceId) : undefined,
    exceptionApprovalReference: hasException ? clean(linkage.exceptionApprovalReference) : undefined,
    exceptionEvidenceReference: hasException ? clean(linkage.exceptionEvidenceReference) : undefined,
  };
}

export function validateExceptionLinkage(linkage: SavePurchaseRequisitionLinkageRequest): string | null {
  if (!linkage.approvedExceptionRuleId) return null;
  if (!linkage.exceptionWorkflowInstanceId) {
    return 'Select the completed shared exception workflow that approved this exception.';
  }
  return null;
}

const PROCUREMENT_CATEGORIES = new Set([
  'Goods',
  'Works',
  'TechnicalServices',
  'ConsultancyServices',
  'GeneralServices',
]);

export function deriveRequisitionLinkageFromPlanItem(
  linkage: SavePurchaseRequisitionLinkageRequest,
  sourcePlanItemId: string,
  options?: PurchaseRequisitionLinkageOptionsDto
): SavePurchaseRequisitionLinkageRequest {
  const planItem = options?.planItems.find((item) => item.id === sourcePlanItemId);
  if (!planItem) return { ...linkage, sourcePlanItemId };

  const category = PROCUREMENT_CATEGORIES.has(planItem.category ?? '')
    ? (planItem.category as SavePurchaseRequisitionLinkageRequest['procurementCategory'])
    : linkage.procurementCategory;

  return {
    ...linkage,
    sourcePlanItemId,
    sourcePlanItemIds: [sourcePlanItemId],
    // Use the plan item's explicit budget relationship. Do not infer it from a
    // shared plan parent, because a plan may have several budgets.
    budgetId: planItem.linkedBudgetId,
    procurementCategory: category,
  };
}

export function getRequisitionCurrency(
  linkage: SavePurchaseRequisitionLinkageRequest,
  options?: PurchaseRequisitionLinkageOptionsDto,
  baseCurrencyCode?: string
) {
  return (
    options?.budgets.find((item) => item.id === linkage.budgetId)?.currency ||
    options?.planItems.find((item) => item.id === linkage.sourcePlanItemId)?.currency ||
    baseCurrencyCode ||
    'XXX'
  );
}

export function formatRequisitionMoney(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat('en-GH', {
      style: 'currency',
      currency,
      currencyDisplay: 'code',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;
  }
}
