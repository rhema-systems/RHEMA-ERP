import type { ProcurementControlEvidenceReferenceKind } from './procurement-control-event';

export type FrameworkCallOffStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Issued'
  | 'Rejected'
  | 'Cancelled';

export type FrameworkBalanceMovementType =
  | 'Commitment'
  | 'Release'
  | 'Issue';

export type FrameworkCallOffAuthorityKind =
  | 'User'
  | 'Role'
  | 'OrganizationalUnit'
  | 'Permission';

export interface FrameworkCallOffSearch {
  search?: string;
  status?: FrameworkCallOffStatus;
  agreementId?: string;
  businessPartnerId?: string;
  sourceRequisitionId?: string;
  page?: number;
  pageSize?: number;
}

export interface FrameworkCallOffSummary {
  totalCount: number;
  draftCount: number;
  pendingApprovalCount: number;
  approvedCount: number;
  issuedCount: number;
  expiringAgreementCount: number;
  totalCommittedAmount: number;
  totalIssuedAmount: number;
  totalAvailableAmount: number;
  committedByCurrency: Record<string, number>;
  issuedByCurrency: Record<string, number>;
  availableByCurrency: Record<string, number>;
}

export interface FrameworkCallOffListItem {
  id: string;
  callOffNumber: string;
  status: FrameworkCallOffStatus;
  agreementId: string;
  agreementNumber: string;
  agreementVersion: number;
  purchaseOrderId: string;
  purchaseOrderNumber: string;
  purchaseOrderStatus: string;
  sourceRequisitionId: string;
  sourceRequisitionNumber: string;
  businessPartnerId: string;
  supplierCode: string;
  supplierName: string;
  currencyCode: string;
  totalAmount: number;
  agreementCeilingAmount: number;
  agreementCommittedAmount: number;
  agreementIssuedAmount: number;
  agreementAvailableAmount: number;
  requiredDateUtc: string;
  agreementEffectiveEndUtc: string;
  agreementIsEffective: boolean;
  daysToExpiry: number;
  lineCount: number;
  createdAtUtc: string;
  submittedAtUtc?: string;
  approvedAtUtc?: string;
  issuedAtUtc?: string;
  allowedActions: string[];
  rowVersion: string;
}

export interface FrameworkCallOffPage {
  page: number;
  pageSize: number;
  totalCount: number;
  items: FrameworkCallOffListItem[];
}

export interface FrameworkCallOffLine {
  id: string;
  agreementPriceLineId: string;
  purchaseRequisitionItemId: string;
  purchaseOrderItemId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  sourceDemandQuantity: number;
  sourceDemandAllocatedQuantity: number;
  sourceDemandRemainingQuantity: number;
  priceIntegrityHash: string;
  integrityHash: string;
}

export interface FrameworkBalanceMovement {
  id: string;
  movementType: FrameworkBalanceMovementType;
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  actorUserId: string;
  actorName: string;
  occurredAtUtc: string;
  correlationId: string;
  integrityHash: string;
}

export interface FrameworkCallOff extends FrameworkCallOffListItem {
  authorityId: string;
  authorityKind: FrameworkCallOffAuthorityKind;
  authorityValue: string;
  authorityThreshold?: number;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  notes?: string;
  priceListReference: string;
  priceListVersion: number;
  agreementEffectiveFromUtc: string;
  agreementIntegrityHash: string;
  awardReadinessIntegrityHash: string;
  supplierEligibilityDecisionHash: string;
  workflowDefinitionId?: string;
  workflowInstanceId?: string;
  createdByUserId: string;
  createdByName: string;
  submittedById?: string;
  submittedByName?: string;
  approvedById?: string;
  approvedByName?: string;
  issuedById?: string;
  issuedByName?: string;
  rejectedById?: string;
  rejectedByName?: string;
  rejectedAtUtc?: string;
  cancelledById?: string;
  cancelledByName?: string;
  cancelledAtUtc?: string;
  decisionComment?: string;
  integrityHash: string;
  lines: FrameworkCallOffLine[];
  balanceMovements: FrameworkBalanceMovement[];
}

export interface FrameworkCallOffAgreementOption {
  agreementId: string;
  agreementNumber: string;
  title: string;
  version: number;
  businessPartnerId: string;
  supplierCode: string;
  supplierName: string;
  currencyCode: string;
  ceilingAmount: number;
  committedAmount: number;
  availableAmount: number;
  effectiveFromUtc: string;
  effectiveEndUtc: string;
  daysToExpiry: number;
  currentActorIsAuthorized: boolean;
  currentActorMaximumCallOffAmount?: number;
  priceLines: FrameworkCallOffPriceOption[];
}

export interface FrameworkCallOffPriceOption {
  agreementPriceLineId: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  unitOfMeasure: string;
  unitPrice: number;
  minimumQuantity: number;
  maximumQuantity?: number;
  leadTimeDays: number;
}

export interface FrameworkCallOffRequisitionOption {
  requisitionId: string;
  requisitionNumber: string;
  status: string;
  requiredDate?: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  lines: FrameworkCallOffDemandOption[];
}

export interface FrameworkCallOffDemandOption {
  purchaseRequisitionItemId: string;
  inventoryItemId: string;
  itemDescription: string;
  unitOfMeasure: string;
  demandQuantity: number;
  allocatedQuantity: number;
  remainingQuantity: number;
}

export interface FrameworkCallOffWarehouseOption {
  warehouseId: string;
  code: string;
  name: string;
}

export interface FrameworkCallOffOptions {
  agreements: FrameworkCallOffAgreementOption[];
  requisitions: FrameworkCallOffRequisitionOption[];
  warehouses: FrameworkCallOffWarehouseOption[];
  purchaseOrderWorkflowReady: boolean;
  workflowReadinessMessage?: string;
}

export interface CreateFrameworkCallOffLine {
  purchaseRequisitionItemId: string;
  agreementPriceLineId: string;
  quantity: number;
}

export interface CreateFrameworkCallOff {
  agreementId: string;
  sourceRequisitionId: string;
  requiredDateUtc: string;
  deliveryWarehouseId?: string;
  deliveryAddress?: string;
  notes?: string;
  lines: CreateFrameworkCallOffLine[];
}

export interface FrameworkCallOffEvidenceReference {
  referenceKind: ProcurementControlEvidenceReferenceKind;
  referenceId?: string;
  reference?: string;
  label?: string;
  requirementKey?: string;
}

export interface FrameworkCallOffLifecycleRequest {
  rowVersion: string;
  comment: string;
  evidence: FrameworkCallOffEvidenceReference[];
}

export interface FrameworkCallOffDecisionRequest
  extends FrameworkCallOffLifecycleRequest {
  approved: boolean;
}
