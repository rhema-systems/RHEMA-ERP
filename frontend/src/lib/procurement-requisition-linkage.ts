import type {
  PurchaseRequisitionLinkageDto,
  PurchaseRequisitionType,
  SavePurchaseRequisitionLinkageRequest,
} from '@/services/purchasingService';

export const EMPTY_REQUISITION_LINKAGE: SavePurchaseRequisitionLinkageRequest = {
  requisitionType: 'StockReplenishment',
};

export function toEditableRequisitionLinkage(
  linkage?: PurchaseRequisitionLinkageDto
): SavePurchaseRequisitionLinkageRequest {
  if (!linkage) return { ...EMPTY_REQUISITION_LINKAGE };
  return {
    sourcePlanItemId: linkage.sourcePlanItemId,
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
