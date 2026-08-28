/**
 * Purchasing Service
 * Handles Purchase Requisitions, Purchase Orders, and Purchase Receipts (GRN)
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// Helper function to get auth headers
function getAuthHeaders(): HeadersInit {
  const token =
    localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

function getMultipartAuthHeaders(): HeadersInit {
  const token =
    localStorage.getItem('token') || localStorage.getItem('authToken');
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function getFriendlyErrorMessage(
  response: Response,
  procurementPermissionGuidance?: string
): Promise<string> {
  // Try to extract a meaningful error message from the API.
  // Some endpoints return plain text; others return JSON.
  let raw = '';
  try {
    raw = await response.text();
  } catch {
    raw = '';
  }

  let message = raw?.trim();
  if (message) {
    try {
      const parsed = JSON.parse(message);
      // common shapes: { error: string } or { message: string } or { success: false, error: string }
      message =
        parsed?.error ||
        parsed?.message ||
        parsed?.detail ||
        parsed?.title ||
        (typeof parsed === 'string' ? parsed : message);
    } catch {
      // not JSON, keep text
    }
  }

  // Provide user-friendly guidance for known workflow-guard errors.
  if (
    response.status === 400 &&
    message &&
    message.includes(
      "No active workflow definition found for entity type 'PurchaseRequisition'"
    )
  ) {
    return 'No approval workflow is active for Purchase Requisitions. Please ask an administrator to activate one under Administration → Workflow, then try again.';
  }

  if (
    response.status === 403 &&
    message &&
    message.includes('no Security role granting this procurement privilege')
  ) {
    if (procurementPermissionGuidance) {
      return procurementPermissionGuidance;
    }

    return 'You cannot create this purchase requisition yet. Ask a Security administrator to assign the active TDC Requisitioner, TDC User Department Head, TDC Procurement Officer, or TDC Senior Procurement Officer role. A warehouse responsibility assignment is not required for requisition creation.';
  }

  if (
    response.status === 400 &&
    message &&
    message.includes(
      "No active workflow definition found for entity type 'PurchaseOrder'"
    )
  ) {
    return 'No approval workflow is active for Purchase Orders. Please ask an administrator to activate one under Administration → Workflow, then try again.';
  }

  if (message) return message;
  return `Request failed (${response.status} ${response.statusText})`;
}

// ============================================================================
// PURCHASE REQUISITION INTERFACES
// ============================================================================

export interface PurchaseRequisitionSummaryDto {
  id: string;
  requisitionNumber: string;
  requisitionDate: string;
  requestedByName: string;
  requiredDate?: string;
  status: string;
  priority: string;
  department?: string;
  totalAmount: number;
  currency: string;
  itemCount: number;
  currentWorkflowStepName?: string;
  sourcePlanNumber?: string;
  sourcePlanItemId?: string;
  sourcePlanItemDescription?: string;
  budgetCode?: string;
  procurementCategory?: ProcurementCategoryClass;
  projectCode?: string;
  requisitionType: PurchaseRequisitionType;
  specificationTemplateReference?: string;
  approvedExceptionReference?: string;
}

export interface PurchaseRequisitionDetailDto extends PurchaseRequisitionSummaryDto {
  costCenter?: string;
  justification?: string;
  notes?: string;
  approvedByName?: string;
  approvedAt?: string;
  rejectionReason?: string;
  linkage: PurchaseRequisitionLinkageDto;
  rowVersion: string;
  items: PurchaseRequisitionItemDto[];
}

export type ProcurementCategoryClass =
  | 'Goods'
  | 'Works'
  | 'TechnicalServices'
  | 'ConsultancyServices'
  | 'GeneralServices';

export type PurchaseRequisitionType =
  | 'StockReplenishment'
  | 'CapitalPurchase'
  | 'EmergencyPurchase'
  | 'ProjectPurchase'
  | 'ServiceProcurement';

export interface SavePurchaseRequisitionLinkageRequest {
  sourcePlanItemId?: string;
  budgetId?: string;
  procurementCategory?: ProcurementCategoryClass;
  costCenter?: string;
  projectId?: string;
  requisitionType: PurchaseRequisitionType;
  specificationTemplateId?: string;
  approvedExceptionRuleId?: string;
  exceptionWorkflowInstanceId?: string;
  exceptionApprovalReference?: string;
  exceptionEvidenceReference?: string;
}

export interface PurchaseRequisitionLinkageDto extends SavePurchaseRequisitionLinkageRequest {
  sourcePlanId?: string;
  sourcePlanNumber?: string;
  sourcePlanTitle?: string;
  sourcePlanItemDescription?: string;
  budgetCode?: string;
  budgetAllocated?: number;
  budgetRemaining?: number;
  projectCode?: string;
  projectName?: string;
  specificationTemplateCode?: string;
  specificationTemplateName?: string;
  specificationTemplateVersion?: number;
  approvedExceptionRuleCode?: string;
  approvedExceptionName?: string;
  exceptionApprovedById?: string;
  exceptionApprovedByName?: string;
  exceptionApprovedAtUtc?: string;
  revision: number;
  lastUpdatedAtUtc?: string;
  lastUpdatedById?: string;
  lastUpdatedByName?: string;
}

export interface PurchaseRequisitionLinkageOptionDto {
  id: string;
  code: string;
  name: string;
  status?: string;
  parentId?: string;
  parentReference?: string;
  category?: string;
  amount?: number;
  currency?: string;
  budgetId?: string;
  budgetCode?: string;
  departmentId?: string;
  departmentName?: string;
  inventoryItemId?: string;
  quantity?: number;
  unitOfMeasure?: string;
  unitPrice?: number;
  requiredDate?: string;
  specifications?: string;
  preferredSupplierId?: string;
}

export interface PurchaseRequisitionNamedOptionDto {
  value: number;
  name: string;
  label: string;
}

export interface PurchaseRequisitionLinkageOptionsDto {
  planItems: PurchaseRequisitionLinkageOptionDto[];
  budgets: PurchaseRequisitionLinkageOptionDto[];
  projects: PurchaseRequisitionLinkageOptionDto[];
  specificationTemplates: PurchaseRequisitionLinkageOptionDto[];
  approvedExceptionRules: PurchaseRequisitionLinkageOptionDto[];
  approvedExceptionWorkflows: PurchaseRequisitionLinkageOptionDto[];
  categories: PurchaseRequisitionNamedOptionDto[];
  requestTypes: PurchaseRequisitionNamedOptionDto[];
  costCenters: string[];
}

export interface PurchaseRequisitionLinkageHistoryDto {
  id: string;
  action: string;
  result: string;
  actorName: string;
  reason?: string;
  occurredAtUtc: string;
  integrityHash: string;
}

export interface PurchaseRequisitionSubmissionReadinessDto {
  requisitionId: string;
  requisitionNumber: string;
  status: string;
  isCompliant: boolean;
  canSubmit: boolean;
  decisionCode: string;
  message: string;
  basis?: 'AcknowledgedAPP' | 'ApprovedException';
  sourcePlanId?: string;
  sourcePlanItemId?: string;
  sourcePlanNumber?: string;
  sourcePlanItemDescription?: string;
  appSubmissionId?: string;
  appSubmissionNumber?: string;
  appSubmissionAttemptNumber?: number;
  appSubmissionStatus?: string;
  appAcknowledgementReference?: string;
  appAcknowledgedAtUtc?: string;
  approvedExceptionRuleId?: string;
  approvedExceptionRuleCode?: string;
  exceptionWorkflowInstanceId?: string;
  exceptionApprovalReference?: string;
  exceptionEvidenceReference?: string;
  exceptionApprovedAtUtc?: string;
  requiredActions: string[];
}

export interface PurchaseRequisitionSubmissionControlHistoryDto {
  id: string;
  action: string;
  result: string;
  actorName: string;
  ruleCode: string;
  reason?: string;
  occurredAtUtc: string;
  integrityHash: string;
}

export interface PurchaseRequisitionBudgetReadinessDto {
  requisitionId: string;
  requisitionNumber: string;
  status: string;
  isCompliant: boolean;
  canReserve: boolean;
  decisionCode: string;
  message: string;
  basis?:
    | 'ApprovedBudget'
    | 'AuthorizedOverride'
    | 'ExistingCommitment'
    | 'ReleasedCommitment';
  budgetId?: string;
  budgetCode?: string;
  budgetStatus?: string;
  currency?: string;
  requestedAmount: number;
  allocatedAmount: number;
  utilizedAmount: number;
  committedAmount: number;
  availableAmount: number;
  shortfallAmount: number;
  commitmentId?: string;
  commitmentReference?: string;
  commitmentStatus?: string;
  reservationSequence?: number;
  reservedAtUtc?: string;
  isOverride: boolean;
  overrideRuleId?: string;
  overrideRuleCode?: string;
  overrideWorkflowInstanceId?: string;
  overrideApprovalReference?: string;
  overrideEvidenceReference?: string;
  overrideApprovedAtUtc?: string;
  requiredActions: string[];
}

export interface PurchaseRequisitionBudgetControlHistoryDto {
  id: string;
  action: string;
  result: string;
  actorName: string;
  ruleCode?: string;
  reason?: string;
  occurredAtUtc: string;
  integrityHash: string;
}

export interface PurchaseRequisitionAuthorityStepDto {
  sequence: number;
  ruleId: string;
  ruleCode: string;
  rulePolicyCode: string;
  rulePolicyVersion: number;
  sourceDecisionKey: string;
  authorityName: string;
  authorityRole: string;
  quorum: number;
  isObserver: boolean;
  escalationAuthority?: string;
  currencyCode: string;
  lowerBound: number;
  upperBound?: number;
  lowerInclusive: boolean;
  upperInclusive: boolean;
  workflowStepId: string;
  workflowStepName: string;
  workflowStepOrder: number;
}

export interface PurchaseRequisitionAuthorityFindingDto {
  code: string;
  message: string;
  severity: string | number;
  ruleId?: string;
  ruleCode?: string;
  sourceDecisionKey?: string;
}

export interface PurchaseRequisitionAuthorityReadinessDto {
  requisitionId: string;
  requisitionNumber: string;
  status: string;
  isCompliant: boolean;
  canSubmit: boolean;
  decisionCode: string;
  message: string;
  category?: string;
  amount: number;
  currencyCode: string;
  policySetId?: string;
  policyCode?: string;
  policyName?: string;
  policyVersion?: number;
  workflowDefinitionId?: string;
  workflowName?: string;
  workflowVersion?: number;
  authorityRouteId?: string;
  routeReference?: string;
  attemptNumber?: number;
  capturedAtUtc?: string;
  integrityHash?: string;
  currentWorkflowStage?: string;
  currentWorkflowStageStatus?: string;
  steps: PurchaseRequisitionAuthorityStepDto[];
  findings: PurchaseRequisitionAuthorityFindingDto[];
  requiredActions: string[];
}

export interface PurchaseRequisitionAuthorityRouteHistoryDto {
  id: string;
  routeReference: string;
  attemptNumber: number;
  policyCode: string;
  policyName: string;
  policyVersion: number;
  workflowName: string;
  workflowVersion: number;
  category: string;
  amount: number;
  currencyCode: string;
  capturedAtUtc: string;
  capturedByName: string;
  correlationId: string;
  integrityHash: string;
  steps: PurchaseRequisitionAuthorityStepDto[];
}

export interface PurchaseRequisitionSourcingRequirementDto {
  key: string;
  label: string;
  satisfied: boolean;
  code: string;
  message: string;
  evidenceReference?: string;
}

export interface PurchaseRequisitionSourcingReleaseDto {
  id: string;
  requisitionId: string;
  requisitionNumber: string;
  attemptNumber: number;
  releaseReference: string;
  sourcePlanId?: string;
  sourcePlanItemId?: string;
  appSubmissionId?: string;
  appSubmissionAttemptNumber?: number;
  appAcknowledgementReference?: string;
  approvedExceptionRuleId?: string;
  exceptionApprovalReference?: string;
  specificationTemplateId?: string;
  specificationTemplateCode?: string;
  specificationTemplateVersion?: number;
  budgetCommitmentId?: string;
  budgetCommitmentReference?: string;
  authorityRouteId?: string;
  authorityRouteReference?: string;
  workflowInstanceId?: string;
  releasedAtUtc: string;
  releasedById: string;
  releasedByName: string;
  releaseReason: string;
  correlationId: string;
  controlFingerprint: string;
  integrityHash: string;
}

export interface PurchaseRequisitionSourcingReadinessDto {
  requisitionId: string;
  requisitionNumber: string;
  status: string;
  isCompliant: boolean;
  canRelease: boolean;
  isReleased: boolean;
  hasStaleRelease: boolean;
  decisionCode: string;
  message: string;
  evaluatedAtUtc: string;
  controlFingerprint: string;
  sourcePlanId?: string;
  sourcePlanItemId?: string;
  appSubmissionId?: string;
  appSubmissionAttemptNumber?: number;
  appAcknowledgementReference?: string;
  approvedExceptionRuleId?: string;
  exceptionWorkflowInstanceId?: string;
  exceptionApprovalReference?: string;
  specificationTemplateId?: string;
  specificationTemplateCode?: string;
  specificationTemplateVersion?: number;
  budgetCommitmentId?: string;
  budgetCommitmentReference?: string;
  authorityRouteId?: string;
  authorityRouteReference?: string;
  workflowInstanceId?: string;
  currentRelease?: PurchaseRequisitionSourcingReleaseDto;
  requirements: PurchaseRequisitionSourcingRequirementDto[];
  requiredActions: string[];
}

export interface PurchaseRequisitionItemDto {
  id: string;
  requisitionId: string;
  inventoryItemId?: string;
  itemCode?: string;
  itemName?: string;
  itemDescription: string;
  quantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
  lineTotal: number;
  requiredDate?: string;
  preferredSupplierId?: string; // Maps to BusinessPartnerId
  preferredSupplierName?: string; // Maps to BusinessPartner.PartnerName
  notes?: string;
  specifications?: string;
  status: string;
  purchaseOrderId?: string;
  purchaseOrderNumber?: string;
}

export interface SuggestedSupplierDto {
  supplierId: string;
  itemMatchCount: number;
  preferredItemCount: number;
}

export interface CreateRfqFromPurchaseRequisitionResponseDto {
  rfqId: string;
  rfqNumber: string;
}

export interface CreatePurchaseRequisitionDto {
  requestedById: string;
  requiredDate?: string;
  priority: string;
  department?: string;
  departmentId?: string;
  currency?: string;
  costCenter?: string;
  justification?: string;
  notes?: string;
  linkage: SavePurchaseRequisitionLinkageRequest;
  items: CreatePurchaseRequisitionItemDto[];
}

export interface UpdatePurchaseRequisitionDto extends CreatePurchaseRequisitionDto {
  rowVersion: string;
}

export interface CreatePurchaseRequisitionItemDto {
  inventoryItemId?: string;
  itemDescription: string;
  quantity: number;
  unitOfMeasure?: string;
  estimatedUnitPrice: number;
  requiredDate?: string;
  preferredSupplierId?: string; // Maps to BusinessPartnerId
  notes?: string;
  specifications?: string;
}

// ============================================================================
// PURCHASE ORDER INTERFACES
// ============================================================================

export type ProcurementPurchaseOrderSourceType =
  | 'RfqAward'
  | 'TenderAward'
  | 'Contract'
  | 'ApprovedException'
  | 'FrameworkCallOff'
  | 'HistoricalMigration';

export interface ProcurementPurchaseOrderSourceLineDto {
  sourceLineId: string;
  inventoryItemId?: string;
  itemCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  lineTotal: number;
}

export interface ProcurementPurchaseOrderSourceOptionDto {
  sourceType: ProcurementPurchaseOrderSourceType;
  sourceId: string;
  sourceReference: string;
  sourceLabel: string;
  purchaseRequisitionId: string;
  purchaseRequisitionNumber: string;
  sourcingCaseId: string;
  sourcingReleaseId: string;
  awardReadinessDecisionId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  approvedAmount?: number;
  currencyCode: string;
  approvedLines: ProcurementPurchaseOrderSourceLineDto[];
}

export interface ProcurementPurchaseOrderSourceStatusDto {
  ready: boolean;
  permission: string;
  candidateCount: number;
  blockedReasons: string[];
  sources: ProcurementPurchaseOrderSourceOptionDto[];
  frameworkCallOffRoute: string;
}

export interface ProcurementPurchaseOrderComplianceCheckDto {
  key: string;
  label: string;
  required: boolean;
  passed: boolean;
  code: string;
  message: string;
  referenceId?: string;
  reference?: string;
  integrityHash?: string;
  details: string[];
}

export interface ProcurementPurchaseOrderComplianceDto {
  purchaseOrderId: string;
  orderNumber: string;
  status: string;
  action: string;
  isCompliant: boolean;
  code: string;
  message: string;
  evaluatedAtUtc: string;
  decisionKeys: string[];
  checks: ProcurementPurchaseOrderComplianceCheckDto[];
  blockedReasons: string[];
}

export interface ProcurementPurchaseOrderSodCheckDto {
  key: string;
  label: string;
  action: string;
  controlCode: string;
  allowed: boolean;
  code: string;
  message: string;
  participantRoles: string[];
  prohibitedActorUserIds: string[];
  policySetId?: string;
  policyCode?: string;
  policyVersion?: number;
  ruleId?: string;
  ruleCode?: string;
}

export interface ProcurementPurchaseOrderSodReadinessDto {
  purchaseOrderId: string;
  orderNumber: string;
  status: string;
  currentActorUserId: string;
  canApprove: boolean;
  canReceive: boolean;
  code: string;
  message: string;
  evaluatedAtUtc: string;
  decisionKeys: string[];
  receiptActionCoverage: string[];
  checks: ProcurementPurchaseOrderSodCheckDto[];
}

export interface ProcurementReceiptSourceLineDto {
  purchaseOrderItemId: string;
  inventoryItemId?: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  orderedQuantity: number;
  previouslyReceiptedQuantity: number;
  toleranceQuantity: number;
  maximumReceivableQuantity: number;
  remainingQuantity: number;
  requestedQuantity: number;
  allowed: boolean;
  code: string;
  message: string;
  integrityHash: string;
}

export interface ProcurementReceiptSourceReadinessDto {
  purchaseOrderId: string;
  orderNumber: string;
  purchaseOrderStatus: string;
  sourceType?: ProcurementPurchaseOrderSourceType;
  sourceId?: string;
  sourceReference: string;
  sourceIntegrityHash: string;
  tolerancePercent: number;
  sourceValid: boolean;
  canReceive: boolean;
  code: string;
  message: string;
  evaluatedAtUtc: string;
  decisionKeys: string[];
  lines: ProcurementReceiptSourceLineDto[];
  requiredActions: string[];
}

export interface PurchaseOrderSummaryDto {
  id: string;
  orderNumber: string;
  orderType: string;
  supplierId: string; // Maps to BusinessPartnerId
  supplierName: string; // Maps to BusinessPartner.PartnerName
  orderDate: string;
  requiredDate?: string;
  promisedDate?: string;
  status: string;
  totalAmount: number;
  currency: string;
  itemCount: number;
  requestedByName?: string;
  currentWorkflowStepName?: string;
  procurementSourceType?: ProcurementPurchaseOrderSourceType;
  procurementCategory?: ProcurementCategoryClass;
  procurementSourceReference?: string;
}

export interface PurchaseOrderDetailDto extends PurchaseOrderSummaryDto {
  receivedDate?: string;
  approvedByName?: string;
  approvedAt?: string;
  subTotal: number;
  taxAmount: number;
  shippingCost: number;
  miscellaneousCost: number;
  totalAdditionalCost: number;
  costAllocationMethod: 'SpreadToItemCost' | 'GLExpense';
  costApportionmentBasis: 'Value' | 'Weight' | 'Quantity';
  expenseGLAccount?: string;
  costsAllocated: boolean;
  discountAmount: number;
  paymentTerms?: string;
  shippingTerms?: string;
  terms?: string;
  notes?: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  deliveryInstructions?: string;
  supplierOrderNumber?: string; // Maps to BusinessPartnerOrderNumber
  referenceNumber?: string;
  supplierPhone?: string;
  supplierEmail?: string;
  supplierAddress?: string;
  procurementSourceId?: string;
  sourceRequisitionId?: string;
  sourceRequisitionNumber?: string;
  sourcingReleaseId?: string;
  sourcingCaseId?: string;
  awardReadinessDecisionId?: string;
  sourceIntegrityHash?: string;
  sourceValidatedAtUtc?: string;
  items: PurchaseOrderItemDto[];
  receipts: PurchaseOrderReceiptDto[];
}

export interface PurchaseOrderItemDto {
  id: string;
  purchaseOrderId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  supplierItemCode?: string; // Maps to BusinessPartnerItemCode
  itemDescription?: string;
  orderedQuantity: number;
  unitOfMeasure: string;
  itemUnitOfMeasureId?: string;
  warehouseId?: string;
  warehouseName?: string;
  receivedQuantity: number;
  remainingQuantity: number;
  unitPrice: number;
  lineTotal: number;
  allocatedAdditionalCost: number;
  allocatedCostPerUnit: number;
  landedUnitCost: number;
  expectedDeliveryDate?: string;
  notes?: string;
}

// ============================================================================
// PURCHASE ORDER LANDED COST PLAN (PLANNED LANDED COSTS AT PO STAGE)
// ============================================================================

export type LandedCostAllocationMethod =
  'ByValue' | 'ByQuantity' | 'ByWeight' | 'ByVolume' | 'Equal' | 'Manual';

export interface PurchaseOrderLandedCostPlanItemDto {
  id: string;
  costType: number; // matches backend LandedCostType enum values
  description: string;
  amount: number;
  currency: string;
  exchangeRate: number;
  amountInPlanCurrency: number;
  allocationMethod: LandedCostAllocationMethod;
  supplierId?: string;
  supplierName?: string;
  referenceNumber?: string;
  notes?: string;
}

export interface PurchaseOrderLandedCostPlanDto {
  id: string;
  purchaseOrderId: string;
  currency: string;
  status: string;
  totalPlannedCost: number;
  notes?: string;
  items: PurchaseOrderLandedCostPlanItemDto[];
}

export interface UpsertPurchaseOrderLandedCostPlanItemDto {
  costType: number;
  description: string;
  amount: number;
  currency?: string;
  exchangeRate?: number;
  allocationMethod?: LandedCostAllocationMethod;
  supplierId?: string;
  referenceNumber?: string;
  notes?: string;
}

export interface UpsertPurchaseOrderLandedCostPlanDto {
  currency?: string;
  notes?: string;
  items: UpsertPurchaseOrderLandedCostPlanItemDto[];
}

const LANDED_COST_TYPE_NAME_TO_VALUE: Record<string, number> = {
  Freight: 1,
  CustomsDuty: 2,
  Insurance: 3,
  Handling: 4,
  Brokerage: 5,
  Storage: 6,
  Other: 7,
};

const normalizeLandedCostType = (value: unknown): number => {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (trimmed in LANDED_COST_TYPE_NAME_TO_VALUE)
      return LANDED_COST_TYPE_NAME_TO_VALUE[trimmed];
    const numeric = parseInt(trimmed, 10);
    if (Number.isFinite(numeric)) return numeric;
  }
  return 7;
};

const normalizePurchaseOrderLandedCostPlanDto = (
  plan: any
): PurchaseOrderLandedCostPlanDto => {
  const items: PurchaseOrderLandedCostPlanItemDto[] = Array.isArray(plan?.items)
    ? plan.items.map((i: any) => ({
        ...i,
        costType: normalizeLandedCostType(i?.costType),
      }))
    : [];

  return {
    ...plan,
    items,
  } as PurchaseOrderLandedCostPlanDto;
};

export interface CreatePurchaseOrderDto {
  sourceType: ProcurementPurchaseOrderSourceType;
  sourceId: string;
  supplierId: string; // Maps to BusinessPartnerId
  orderType?: string;
  requiredDate?: string;
  promisedDate?: string;
  paymentTerms?: string;
  shippingTerms?: string;
  terms?: string;
  notes?: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  deliveryInstructions?: string;
  referenceNumber?: string;
  taxAmount?: number;
  shippingCost?: number;
  miscellaneousCost?: number;
  costAllocationMethod?: 'SpreadToItemCost' | 'GLExpense';
  costApportionmentBasis?: 'Value' | 'Weight' | 'Quantity';
  expenseGLAccount?: string;
  discountAmount?: number;
  requestedById: string;
  items: CreatePurchaseOrderItemDto[];
}

export interface CreatePurchaseOrderItemDto {
  inventoryItemId: string;
  supplierItemCode?: string; // Maps to BusinessPartnerItemCode
  itemDescription?: string;
  orderedQuantity: number;
  unitOfMeasure: string;
  itemUnitOfMeasureId?: string;
  warehouseId?: string;
  unitPrice: number;
  priceListLineId?: string;
  expectedDeliveryDate?: string;
  notes?: string;
}

// ============================================================================
// PURCHASE RECEIPT (GRN) INTERFACES
// ============================================================================

export interface PurchaseOrderReceiptDto {
  id: string;
  purchaseOrderId: string;
  receiptNumber: string;
  receiptDate: string;
  deliveryNote?: string;
  carrierName?: string;
  trackingNumber?: string;
  status: string;
  receivedByName?: string;
  inspectedByName?: string;
  notes?: string;
  requiresInspection: boolean;
  inspectionDate?: string;
  inspectionResult?: string;
  inspectionNotes?: string;
  purchaseOrderNumber: string;
  supplierName: string; // Maps to BusinessPartner.PartnerName
  items?: PurchaseOrderReceiptItemDto[];
}

export type ProcurementReceiptSourceEvidenceKind = 1 | 2;

export interface ProcurementReceiptSourceEvidenceDto {
  id: string;
  evidenceKind: ProcurementReceiptSourceEvidenceKind;
  referenceNumber: string;
  documentDate: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  checksumSha256: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  uploadedAtUtc: string;
  uploadedBy: string;
  isCurrent: boolean;
}

export interface ProcurementReceiptSourceEvidenceOverviewDto {
  receiptId: string;
  receiptNumber: string;
  purchaseOrderNumber: string;
  supplierName: string;
  waybillRequired: boolean;
  waybillReady: boolean;
  inspectionEvidenceLocked: boolean;
  canUpload: boolean;
  financeOwnershipNotice: string;
  evidence: ProcurementReceiptSourceEvidenceDto[];
}

export interface PurchaseOrderReceiptItemDto {
  id: string;
  receiptId: string;
  purchaseOrderItemId: string;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  unitOfMeasure?: string;
  warehouseId?: string;
  warehouseCode?: string;
  warehouseName?: string;
  locationId?: string;
  serialNumber?: string;
  lotNumber?: string;
  expirationDate?: string;
  notes?: string;
  rejectionReason?: string;
  qualityStatus?: string;
  qualityNotes?: string;
  itemCode: string;
  itemName: string;
  locationCode?: string;
}

export type ProcurementReceiptInspectionStatus =
  0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10;
export type ProcurementReceiptDisposition = 0 | 1 | 2 | 3;
export type ProcurementReceiptSupplierAcknowledgementStatus = 0 | 1 | 2 | 3;
export type ProcurementReceiptResolutionKind = 0 | 1 | 2;
export type ProcurementReceiptResolutionStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6;
export type ProcurementReceiptInspectionEvidenceKind = 0 | 1;

export interface ProcurementReceiptInspectionEvidenceRequest {
  actionKey: string;
  requirementKey: string;
  referenceKind: ProcurementReceiptInspectionEvidenceKind;
  workflowEvidenceDocumentId?: string;
  fileUploadRecordId?: string;
  evidenceReference: string;
}

export interface ProcurementReceiptInspectionLineDto {
  id: string;
  purchaseOrderReceiptItemId: string;
  purchaseOrderItemId: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  pendingQuantity: number;
  disposition: ProcurementReceiptDisposition;
  rejectionReason?: string;
  inspectionNotes?: string;
  quarantineLocationId?: string;
}

export interface ProcurementReceiptInspectionEvidenceDto {
  id: string;
  actionKey: string;
  requirementKey: string;
  referenceKind: ProcurementReceiptInspectionEvidenceKind;
  workflowEvidenceDocumentId?: string;
  fileUploadRecordId?: string;
  evidenceReference: string;
  evidenceHash: string;
}

export interface ProcurementReceiptInspectionActionDto {
  id: string;
  sequence: number;
  actionType: number;
  statusAfter: ProcurementReceiptInspectionStatus;
  resolutionKind: ProcurementReceiptResolutionKind;
  quantity: number;
  reference: string;
  comment: string;
  actorName: string;
  occurredAtUtc: string;
}

export interface ProcurementReceiptInspectionDto {
  id: string;
  purchaseOrderReceiptId: string;
  sequence: number;
  status: ProcurementReceiptInspectionStatus;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  pendingQuantity: number;
  qualityHold: boolean;
  qualityHoldReason?: string;
  rejectionNoteNumber?: string;
  supplierAcknowledgementStatus: ProcurementReceiptSupplierAcknowledgementStatus;
  resolutionKind: ProcurementReceiptResolutionKind;
  resolutionStatus: ProcurementReceiptResolutionStatus;
  stockEligibleQuantity: number;
  stockPostedQuantity: number;
  apEligibleQuantity: number;
  apBlockedQuantity: number;
  workflowInstanceId?: string;
  rowVersion: string;
  lines: ProcurementReceiptInspectionLineDto[];
  evidence: ProcurementReceiptInspectionEvidenceDto[];
  actions: ProcurementReceiptInspectionActionDto[];
}

export interface ProcurementReceiptInspectionOverviewDto {
  purchaseOrderReceiptId: string;
  receiptNumber: string;
  purchaseOrderNumber: string;
  supplierName: string;
  canEdit: boolean;
  canSubmit: boolean;
  canDecide: boolean;
  canAcknowledge: boolean;
  canResolve: boolean;
  canClose: boolean;
  decisionKeys: string[];
  evidenceRequirementKeys: string[];
  current?: ProcurementReceiptInspectionDto;
  history: ProcurementReceiptInspectionDto[];
}

export type ProcurementReceiptDocumentKind = 0 | 1 | 'Grn' | 'Mrn';
export type ProcurementReceiptDocumentStatus = 0 | 1 | 2 | 3 | 'Draft' | 'PendingSignatures' | 'Issued' | 'Cancelled';
export type ProcurementReceiptDocumentReconciliationStatus = 0 | 1 | 2 | 3 | 'Pending' | 'Reconciled' | 'Exception' | 'Cancelled';

export interface ProcurementReceiptDocumentSignatureDto {
  id: string;
  requiredRole: string;
  signedByUserId: string;
  signedByName: string;
  signedAtUtc: string;
  comment?: string;
  integrityHash: string;
}

export interface ProcurementReceiptDocumentActionDto {
  id: string;
  action: string;
  fromStatus: string;
  toStatus: string;
  actorName: string;
  occurredAtUtc: string;
  reason?: string;
  integrityHash: string;
}

export interface ProcurementReceiptDocumentDto {
  id: string;
  documentKind: ProcurementReceiptDocumentKind;
  documentNumber: string;
  templateCode: string;
  status: ProcurementReceiptDocumentStatus;
  reconciliationStatus: ProcurementReceiptDocumentReconciliationStatus;
  reconciliationMessage?: string;
  reconciledAtUtc?: string;
  preparedByName: string;
  preparedAtUtc: string;
  issuedByName?: string;
  issuedAtUtc?: string;
  cancelledByName?: string;
  cancelledAtUtc?: string;
  cancellationReason?: string;
  centralDocumentRecordId?: string;
  centralDocumentVersionId?: string;
  pdfUrl?: string;
  sourceIntegrityHash: string;
  requiredSignatures: string[];
  allowedSignatureRoles: string[];
  signatures: ProcurementReceiptDocumentSignatureDto[];
  actions: ProcurementReceiptDocumentActionDto[];
  allowedActions: string[];
  rowVersion: string;
}

export interface ProcurementReceiptDocumentOverviewDto {
  purchaseOrderReceiptId: string;
  receiptNumber: string;
  purchaseOrderNumber: string;
  supplierName: string;
  receiptStatus: string;
  inspectionStatus: string;
  configurationProfileId: string;
  configurationProfileVersion: number;
  configuredDocumentType: number;
  coexistenceRule: number;
  decisionKeys: string[];
  requiredEvidence: string[];
  availableEvidence: string[];
  checks: Array<{
    code: string;
    label: string;
    passed: boolean;
    message: string;
  }>;
  documents: ProcurementReceiptDocumentDto[];
  allowedActions: string[];
  isReconciled: boolean;
}

export interface SaveProcurementReceiptInspectionRequest {
  comment: string;
  idempotencyKey: string;
  rowVersion?: string;
  lines: Array<{
    purchaseOrderReceiptItemId: string;
    acceptedQuantity: number;
    rejectedQuantity: number;
    rejectionReason?: string;
    inspectionNotes?: string;
    quarantineLocationId?: string;
  }>;
}

export interface ProcurementReceiptResolutionRequest {
  resolutionKind: ProcurementReceiptResolutionKind;
  reference: string;
  comment: string;
  idempotencyKey: string;
  rowVersion: string;
  evidence: ProcurementReceiptInspectionEvidenceRequest[];
}

export interface ReceivePurchaseOrderDto {
  purchaseOrderId: string;
  deliveryNote?: string;
  carrierName?: string;
  trackingNumber?: string;
  receivedById: string;
  inspectedById?: string;
  notes?: string;
  requiresInspection: boolean;
  idempotencyKey?: string;
  items: ReceivePurchaseOrderItemDto[];
}

export interface ReceivePurchaseOrderItemDto {
  purchaseOrderItemId: string;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  warehouseId?: string;
  locationId?: string;
  createInventoryItemIfMissing?: boolean;
  inventoryCategoryId?: string;
  proposedItemCode?: string;
  serialNumber?: string;
  lotNumber?: string;
  expirationDate?: string;
  notes?: string;
  rejectionReason?: string;
  qualityStatus?: string;
  qualityNotes?: string;
}

// ============================================================================
// COMMON INTERFACES
// ============================================================================

export interface UpdateStatusDto {
  status: string;
  notes?: string;
}

export interface ApprovalDto {
  approved: boolean;
  comments?: string;
  rejectionReason?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ============================================================================
// PURCHASING SERVICE
// ============================================================================

export const purchasingService = {
  // ============================================================================
  // PURCHASE REQUISITIONS
  // ============================================================================

  /**
   * Get all purchase requisitions with pagination and filters
   */
  async getPurchaseRequisitions(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    priority?: string;
    startDate?: string;
    endDate?: string;
    department?: string;
    sourcePlanId?: string;
  }): Promise<PagedResult<PurchaseRequisitionSummaryDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize)
      queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.priority) queryParams.append('priority', params.priority);
    if (params?.startDate) queryParams.append('startDate', params.startDate);
    if (params?.endDate) queryParams.append('endDate', params.endDate);
    if (params?.department) queryParams.append('department', params.department);
    if (params?.sourcePlanId)
      queryParams.append('sourcePlanId', params.sourcePlanId);

    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions?${queryParams}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch purchase requisitions');
    return response.json();
  },

  /**
   * Get purchase requisition by ID
   */
  async getPurchaseRequisitionById(
    id: string
  ): Promise<PurchaseRequisitionDetailDto> {
    const response = await fetch(`${API_BASE_URL}/PurchaseRequisitions/${id}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch purchase requisition');
    return response.json();
  },

  /**
   * Create a new purchase requisition
   */
  async createPurchaseRequisition(
    data: CreatePurchaseRequisitionDto
  ): Promise<PurchaseRequisitionDetailDto> {
    const response = await fetch(`${API_BASE_URL}/PurchaseRequisitions`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));

    // Handle empty response body (201 Created with no content)
    const text = await response.text();
    if (!text) {
      throw new Error('Server returned empty response');
    }

    try {
      return JSON.parse(text);
    } catch (e) {
      throw new Error(`Invalid JSON response: ${text.substring(0, 100)}`);
    }
  },

  /**
   * Update purchase requisition status
   */
  async updatePurchaseRequisitionStatus(
    id: string,
    status: string,
    notes?: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/status`,
      {
        method: 'PATCH',
        headers: getAuthHeaders(),
        body: JSON.stringify({ status, notes }),
      }
    );

    if (!response.ok)
      throw new Error('Failed to update purchase requisition status');
  },

  /**
   * Submit purchase requisition for approval
   */
  async submitPurchaseRequisition(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
  },

  /**
   * Approve or reject purchase requisition
   */
  async approvePurchaseRequisition(
    id: string,
    approval: ApprovalDto
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(approval),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
  },

  /**
   * Get purchase requisitions by status
   */
  async getPurchaseRequisitionsByStatus(
    status: string
  ): Promise<PurchaseRequisitionSummaryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/by-status/${status}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch purchase requisitions by status');
    return response.json();
  },

  /**
   * Get purchase requisitions by priority
   */
  async getPurchaseRequisitionsByPriority(
    priority: string
  ): Promise<PurchaseRequisitionSummaryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/by-priority/${priority}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch purchase requisitions by priority');
    return response.json();
  },

  /**
   * Get purchase requisitions by department
   */
  async getPurchaseRequisitionsByDepartment(
    department: string
  ): Promise<PurchaseRequisitionSummaryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/by-department/${department}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch purchase requisitions by department');
    return response.json();
  },

  /**
   * Get pending approval purchase requisitions
   */
  async getPendingApprovalRequisitions(): Promise<
    PurchaseRequisitionSummaryDto[]
  > {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/pending-approval`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch pending approval requisitions');
    return response.json();
  },

  /**
   * Convert purchase requisition to purchase order
   */
  async convertToPurchaseOrder(id: string): Promise<CreatePurchaseOrderDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/convert-to-po`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to convert requisition to purchase order');
    return response.json();
  },

  /**
   * Get suggested suppliers for an RFQ based on a purchase requisition
   */
  async getSuggestedSuppliersForRequisition(
    id: string
  ): Promise<SuggestedSupplierDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/suggested-suppliers`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch suggested suppliers');
    return response.json();
  },

  /**
   * Create a Draft RFQ from an approved purchase requisition
   */
  async createRfqFromPurchaseRequisition(
    id: string
  ): Promise<CreateRfqFromPurchaseRequisitionResponseDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/create-rfq`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to create RFQ from purchase requisition');
    return response.json();
  },

  // ============================================================================
  // PURCHASE ORDERS
  // ============================================================================

  /**
   * Get all purchase orders with pagination and filters
   */
  async getPurchaseOrders(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    supplierId?: string; // BusinessPartnerId
    startDate?: string;
    endDate?: string;
  }): Promise<PagedResult<PurchaseOrderSummaryDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize)
      queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.supplierId) queryParams.append('supplierId', params.supplierId);
    if (params?.startDate) queryParams.append('startDate', params.startDate);
    if (params?.endDate) queryParams.append('endDate', params.endDate);

    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders?${queryParams}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch purchase orders');
    return response.json();
  },

  /**
   * Get purchase order by ID
   */
  async getPurchaseOrderById(id: string): Promise<PurchaseOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/PurchaseOrders/${id}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to fetch purchase order');
    return response.json();
  },

  /**
   * Create a new purchase order
   */
  async createPurchaseOrder(
    data: CreatePurchaseOrderDto
  ): Promise<PurchaseOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/PurchaseOrders`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create purchase order');
    }
    return response.json();
  },

  async getPurchaseOrderSourceOptions(
    purchaseRequisitionId?: string
  ): Promise<ProcurementPurchaseOrderSourceStatusDto> {
    const query = purchaseRequisitionId
      ? `?purchaseRequisitionId=${encodeURIComponent(purchaseRequisitionId)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/source-options${query}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) {
      const error = await response.text();
      throw new Error(
        error || 'Failed to load approved purchase-order sources'
      );
    }
    return response.json();
  },

  async getPurchaseOrderComplianceReadiness(
    id: string,
    action = 'Preview'
  ): Promise<ProcurementPurchaseOrderComplianceDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/${id}/compliance-readiness?action=${encodeURIComponent(action)}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) {
      throw new Error(
        await getFriendlyErrorMessage(
          response,
          'You cannot view purchase-order compliance readiness. Ask a Security administrator to grant the procurement.records.read permission for this tenant.'
        )
      );
    }
    return response.json();
  },

  async getPurchaseOrderSodReadiness(
    id: string
  ): Promise<ProcurementPurchaseOrderSodReadinessDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/${id}/sod-readiness`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) {
      throw new Error(
        await getFriendlyErrorMessage(
          response,
          'You cannot view purchase-order role-separation readiness. Ask a Security administrator to grant the procurement.records.read permission for this tenant.'
        )
      );
    }
    return response.json();
  },

  async getReceiptSourceReadiness(
    id: string,
    warehouseId?: string
  ): Promise<ProcurementReceiptSourceReadinessDto> {
    const warehouseQuery = warehouseId
      ? `?warehouseId=${encodeURIComponent(warehouseId)}`
      : '';
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/${id}/receipt-source-readiness${warehouseQuery}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  /**
   * Update an existing purchase order (only Draft status)
   */
  async updatePurchaseOrder(
    id: string,
    data: CreatePurchaseOrderDto
  ): Promise<PurchaseOrderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/PurchaseOrders/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update purchase order');
    }
    return response.json();
  },

  /**
   * Update purchase order status
   */
  async updatePurchaseOrderStatus(
    id: string,
    status: string,
    notes?: string
  ): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/${id}/status`,
      {
        method: 'PATCH',
        headers: getAuthHeaders(),
        body: JSON.stringify({ status, notes }),
      }
    );

    if (!response.ok) throw new Error('Failed to update purchase order status');
  },

  async updatePurchaseRequisition(
    id: string,
    data: UpdatePurchaseRequisitionDto
  ): Promise<PurchaseRequisitionDetailDto> {
    const response = await fetch(`${API_BASE_URL}/PurchaseRequisitions/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionLinkageOptions(): Promise<PurchaseRequisitionLinkageOptionsDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/linkage-options`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionLinkageHistory(
    id: string
  ): Promise<PurchaseRequisitionLinkageHistoryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/linkage-history`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionSubmissionReadiness(
    id: string
  ): Promise<PurchaseRequisitionSubmissionReadinessDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/submission-readiness`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionSubmissionControlHistory(
    id: string
  ): Promise<PurchaseRequisitionSubmissionControlHistoryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/submission-control-history`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionBudgetReadiness(
    id: string
  ): Promise<PurchaseRequisitionBudgetReadinessDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/budget-readiness`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionBudgetControlHistory(
    id: string
  ): Promise<PurchaseRequisitionBudgetControlHistoryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/budget-control-history`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionAuthorityReadiness(
    id: string
  ): Promise<PurchaseRequisitionAuthorityReadinessDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/authority-readiness`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionAuthorityRouteHistory(
    id: string
  ): Promise<PurchaseRequisitionAuthorityRouteHistoryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/authority-route-history`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionSourcingReadiness(
    id: string
  ): Promise<PurchaseRequisitionSourcingReadinessDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/sourcing-readiness`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getPurchaseRequisitionSourcingReleaseHistory(
    id: string
  ): Promise<PurchaseRequisitionSourcingReleaseDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/sourcing-release-history`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async releasePurchaseRequisitionForSourcing(
    id: string,
    reason: string
  ): Promise<PurchaseRequisitionSourcingReleaseDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/sourcing-release`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ reason }),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async exportPurchaseRequisitionLinkage(id: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseRequisitions/${id}/export`,
      {
        headers: getAuthHeaders(),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.blob();
  },

  /**
   * Get planned landed cost plan for a purchase order (optional)
   */
  async getPurchaseOrderLandedCostPlan(
    purchaseOrderId: string
  ): Promise<PurchaseOrderLandedCostPlanDto | null> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/purchase-orders/${purchaseOrderId}/landed-cost-plan`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      const msg = await getFriendlyErrorMessage(response);
      throw new Error(msg || 'Failed to fetch landed cost plan');
    }

    const text = await response.text();
    if (!text) return null;
    const parsed = JSON.parse(text);
    if (!parsed) return null;
    return normalizePurchaseOrderLandedCostPlanDto(parsed);
  },

  /**
   * Create/update planned landed cost plan for a purchase order
   */
  async upsertPurchaseOrderLandedCostPlan(
    purchaseOrderId: string,
    data: UpsertPurchaseOrderLandedCostPlanDto
  ): Promise<PurchaseOrderLandedCostPlanDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/purchase-orders/${purchaseOrderId}/landed-cost-plan`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(data),
      }
    );

    if (!response.ok) {
      const msg = await getFriendlyErrorMessage(response);
      throw new Error(msg || 'Failed to save landed cost plan');
    }

    const json = await response.json();
    return normalizePurchaseOrderLandedCostPlanDto(json);
  },

  /**
   * Submit purchase order for approval
   */
  async submitPurchaseOrder(id: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/${id}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) {
      throw new Error(
        await getFriendlyErrorMessage(
          response,
          'You cannot submit this purchase order for approval. Ask a Security administrator to grant the procurement.purchase-order.create permission. Only an authorized PO creator can submit it.'
        )
      );
    }
  },

  /**
   * Approve or reject purchase order
   */
  async approvePurchaseOrder(id: string, approval: ApprovalDto): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/${id}/approve`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(approval),
      }
    );

    if (!response.ok) {
      throw new Error(
        await getFriendlyErrorMessage(
          response,
          'You cannot approve or reject this purchase order. Ask a Security administrator to grant the procurement.purchase-order.approve permission. The approver must also be independent from the PO creator.'
        )
      );
    }
  },

  /**
   * Get purchase orders by business partner (supplier)
   */
  async getPurchaseOrdersBySupplier(
    supplierId: string
  ): Promise<PurchaseOrderSummaryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/by-supplier/${supplierId}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch purchase orders by supplier');
    return response.json();
  },

  /**
   * Get purchase orders by status
   */
  async getPurchaseOrdersByStatus(
    status: string
  ): Promise<PurchaseOrderSummaryDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/by-status/${status}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok)
      throw new Error('Failed to fetch purchase orders by status');
    return response.json();
  },

  /**
   * Receive purchase order (create GRN)
   */
  async receivePurchaseOrder(
    id: string,
    receiptData: ReceivePurchaseOrderDto
  ): Promise<PurchaseOrderReceiptDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrders/${id}/receive`,
      {
        method: 'POST',
        headers: {
          ...getAuthHeaders(),
          ...(receiptData.idempotencyKey
            ? { 'Idempotency-Key': receiptData.idempotencyKey }
            : {}),
        },
        body: JSON.stringify(receiptData),
      }
    );

    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  // ============================================================================
  // PURCHASE RECEIPTS (GRN)
  // ============================================================================

  /**
   * Get all purchase receipts
   */
  async getPurchaseReceipts(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    startDate?: string;
    endDate?: string;
  }): Promise<PagedResult<PurchaseOrderReceiptDto>> {
    const queryParams = new URLSearchParams();
    if (params?.page) queryParams.append('page', params.page.toString());
    if (params?.pageSize)
      queryParams.append('pageSize', params.pageSize.toString());
    if (params?.search) queryParams.append('search', params.search);
    if (params?.status) queryParams.append('status', params.status);
    if (params?.startDate) queryParams.append('startDate', params.startDate);
    if (params?.endDate) queryParams.append('endDate', params.endDate);

    // Note: This endpoint may need to be created on the backend
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts?${queryParams}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch purchase receipts');
    return response.json();
  },

  /**
   * Get purchase receipt by ID
   */
  async getPurchaseReceiptById(id: string): Promise<PurchaseOrderReceiptDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/${id}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch purchase receipt');
    return response.json();
  },

  async getReceiptInspectionControl(
    receiptId: string
  ): Promise<ProcurementReceiptInspectionOverviewDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/${receiptId}/inspection-control`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getSupplierReceiptInspectionControls(
    page = 1,
    pageSize = 20
  ): Promise<PagedResult<ProcurementReceiptInspectionOverviewDto>> {
    const query = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/inspection-control/supplier?${query}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async initializeReceiptInspection(
    receiptId: string
  ): Promise<ProcurementReceiptInspectionDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/${receiptId}/inspection-control/initialize`,
      { method: 'POST', headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async saveReceiptInspection(
    receiptId: string,
    request: SaveProcurementReceiptInspectionRequest
  ): Promise<ProcurementReceiptInspectionDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/${receiptId}/inspection-control`,
      {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async submitReceiptInspection(
    caseId: string,
    request: {
      comment: string;
      rowVersion: string;
      evidence: ProcurementReceiptInspectionEvidenceRequest[];
    }
  ): Promise<ProcurementReceiptInspectionDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/inspection-control/${caseId}/submit`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async decideReceiptInspection(
    caseId: string,
    request: { approved: boolean; comment: string; rowVersion: string }
  ): Promise<ProcurementReceiptInspectionDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/inspection-control/${caseId}/decision`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async acknowledgeReceiptInspection(
    caseId: string,
    request: {
      acknowledged: boolean;
      reference: string;
      comment: string;
      idempotencyKey: string;
      rowVersion: string;
      evidence: ProcurementReceiptInspectionEvidenceRequest[];
    }
  ): Promise<ProcurementReceiptInspectionDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/inspection-control/${caseId}/supplier-acknowledgement`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async resolveReceiptInspection(
    caseId: string,
    request: ProcurementReceiptResolutionRequest
  ): Promise<ProcurementReceiptInspectionDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/inspection-control/${caseId}/resolution`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async closeReceiptInspection(
    caseId: string,
    request: ProcurementReceiptResolutionRequest
  ): Promise<ProcurementReceiptInspectionDto> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/inspection-control/${caseId}/close`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getReceiptDocumentControl(
    receiptId: string
  ): Promise<ProcurementReceiptDocumentOverviewDto> {
    const response = await fetch(
      `${API_BASE_URL}/ProcurementReceiptDocuments/receipt/${receiptId}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async getReceiptSourceEvidence(
    receiptId: string
  ): Promise<ProcurementReceiptSourceEvidenceOverviewDto> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/purchase-order-receipts/${receiptId}/source-evidence`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async uploadReceiptSourceEvidence(
    receiptId: string,
    values: {
      evidenceKind: ProcurementReceiptSourceEvidenceKind;
      referenceNumber: string;
      documentDate: string;
      clientRequestId: string;
      file: File;
    }
  ): Promise<ProcurementReceiptSourceEvidenceDto> {
    const form = new FormData();
    form.append('evidenceKind', String(values.evidenceKind));
    form.append('referenceNumber', values.referenceNumber);
    form.append('documentDate', values.documentDate);
    form.append('clientRequestId', values.clientRequestId);
    form.append('file', values.file, values.file.name);
    const response = await fetch(
      `${API_BASE_URL}/procurement/purchase-order-receipts/${receiptId}/source-evidence`,
      { method: 'POST', headers: getMultipartAuthHeaders(), body: form }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async downloadReceiptSourceEvidence(
    receiptId: string,
    evidenceId: string
  ): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/purchase-order-receipts/${receiptId}/source-evidence/${evidenceId}/download`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.blob();
  },

  async ensureReceiptDocuments(
    receiptId: string
  ): Promise<ProcurementReceiptDocumentOverviewDto> {
    const response = await fetch(
      `${API_BASE_URL}/ProcurementReceiptDocuments/receipt/${receiptId}/ensure`,
      { method: 'POST', headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async reconcileReceiptDocuments(
    receiptId: string
  ): Promise<ProcurementReceiptDocumentOverviewDto> {
    const response = await fetch(
      `${API_BASE_URL}/ProcurementReceiptDocuments/receipt/${receiptId}/reconcile`,
      { method: 'POST', headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async signReceiptDocument(
    documentId: string,
    request: { requiredRole: string; comment?: string; rowVersion: string }
  ): Promise<ProcurementReceiptDocumentDto> {
    const response = await fetch(
      `${API_BASE_URL}/ProcurementReceiptDocuments/${documentId}/sign`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async issueReceiptDocument(
    documentId: string,
    request: { comment: string; rowVersion: string }
  ): Promise<ProcurementReceiptDocumentDto> {
    const response = await fetch(
      `${API_BASE_URL}/ProcurementReceiptDocuments/${documentId}/issue`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async cancelReceiptDocument(
    documentId: string,
    request: { reason: string; rowVersion: string }
  ): Promise<ProcurementReceiptDocumentDto> {
    const response = await fetch(
      `${API_BASE_URL}/ProcurementReceiptDocuments/${documentId}/cancel`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify(request),
      }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.json();
  },

  async downloadReceiptDocument(documentId: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/ProcurementReceiptDocuments/${documentId}/download`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await getFriendlyErrorMessage(response));
    return response.blob();
  },

  /**
   * Get receipts by purchase order ID
   */
  async getReceiptsByPurchaseOrderId(
    purchaseOrderId: string
  ): Promise<PurchaseOrderReceiptDto[]> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/by-purchase-order/${purchaseOrderId}`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to fetch receipts');
    return response.json();
  },

  /**
   * Generate a GRN PDF for a purchase receipt (opens in a new tab in the UI).
   */
  async getPurchaseReceiptGrnPdf(id: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/PurchaseOrderReceipts/${id}/grn`,
      {
        headers: getAuthHeaders(),
      }
    );

    if (!response.ok) throw new Error('Failed to generate GRN PDF');
    return response.blob();
  },
};

// Export individual functions for easier imports
export const {
  // Purchase Requisitions
  getPurchaseRequisitions,
  getPurchaseRequisitionById,
  createPurchaseRequisition,
  updatePurchaseRequisition,
  getPurchaseRequisitionLinkageOptions,
  getPurchaseRequisitionLinkageHistory,
  getPurchaseRequisitionSubmissionReadiness,
  getPurchaseRequisitionSubmissionControlHistory,
  getPurchaseRequisitionBudgetReadiness,
  getPurchaseRequisitionBudgetControlHistory,
  getPurchaseRequisitionSourcingReadiness,
  getPurchaseRequisitionSourcingReleaseHistory,
  releasePurchaseRequisitionForSourcing,
  exportPurchaseRequisitionLinkage,
  updatePurchaseRequisitionStatus,
  submitPurchaseRequisition,
  approvePurchaseRequisition,
  getPurchaseRequisitionsByStatus,
  getPurchaseRequisitionsByPriority,
  getPurchaseRequisitionsByDepartment,
  getPendingApprovalRequisitions,
  convertToPurchaseOrder,
  getSuggestedSuppliersForRequisition,
  createRfqFromPurchaseRequisition,

  // Purchase Orders
  getPurchaseOrders,
  getPurchaseOrderById,
  getPurchaseOrderSodReadiness,
  getReceiptSourceReadiness,
  createPurchaseOrder,
  updatePurchaseOrder,
  updatePurchaseOrderStatus,
  submitPurchaseOrder,
  approvePurchaseOrder,
  getPurchaseOrdersBySupplier,
  getPurchaseOrdersByStatus,
  receivePurchaseOrder,

  // Purchase Receipts
  getPurchaseReceipts,
  getPurchaseReceiptById,
  getReceiptsByPurchaseOrderId,
  getPurchaseReceiptGrnPdf,
} = purchasingService;

export default purchasingService;
